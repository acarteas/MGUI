#!/usr/bin/env python3
"""Check SDK output cleanup across real and symlink project paths."""

import argparse
import json
from pathlib import Path
import subprocess
import tempfile
from xml.sax.saxutils import escape


def check_restore_graph(consumer_project, run_root, name):
    graph_path = run_root / f"{name}.restoregraph.json"
    log_path = run_root / f"{name}.restoregraph.log"
    command = ["dotnet", "msbuild", str(consumer_project), "-m:1",
               "-t:GenerateRestoreGraphFile", f"-p:RestoreGraphOutputPath={graph_path}",
               "-p:EnableMGCBItems=false", "-p:AutoRestoreMGCBTool=false"]
    with log_path.open("w", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=run_root, stdout=log, stderr=subprocess.STDOUT,
                                timeout=120, check=False)
    if result.returncode:
        raise AssertionError(f"{name} graph generation failed; see {log_path}")
    graph = json.loads(graph_path.read_text(encoding="utf-8"))
    shared_nodes = {}
    for project_path in graph["projects"]:
        physical_path = Path(project_path).resolve()
        if physical_path.name == "MGUI.Shared.csproj":
            shared_nodes.setdefault(physical_path, []).append(project_path)
    if len(shared_nodes) != 1 or len(next(iter(shared_nodes.values()), [])) != 1:
        raise AssertionError(f"Expected one MGUI.Shared restore identity, got {shared_nodes}; see {graph_path}")
    print(f"PASS {name} restore graph has one MGUI.Shared identity", flush=True)


def check_build_path_identity(props, artifact_root, consumer_project=None):
    artifact_root.mkdir(parents=True, exist_ok=True)
    run_root = Path(tempfile.mkdtemp(prefix="build-path-identity-", dir=artifact_root)).resolve()
    project_root = run_root / "real"
    project_root.mkdir()
    alias_root = run_root / "alias"
    alias_root.symlink_to(project_root, target_is_directory=True)
    project = project_root / "PathIdentity.csproj"
    project.write_text("""<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="%s" />
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <Target Name="WriteTrackedArtifacts" AfterTargets="CopyFilesToOutputDirectory">
    <PropertyGroup>
      <CurrentArtifact>$(MSBuildProjectDirectory)/$(IntermediateOutputPath)current.txt</CurrentArtifact>
      <ObsoleteArtifact>$(MSBuildProjectDirectory)/$(IntermediateOutputPath)obsolete.txt</ObsoleteArtifact>
    </PropertyGroup>
    <WriteLinesToFile File="$(CurrentArtifact)" Lines="current" Overwrite="true" />
    <WriteLinesToFile File="$(ObsoleteArtifact)" Lines="obsolete" Overwrite="true" Condition="'$(WriteObsolete)' == 'true'" />
    <ItemGroup>
      <FileWrites Include="$(CurrentArtifact)" />
      <FileWrites Include="$(ObsoleteArtifact)" Condition="'$(WriteObsolete)' == 'true'" />
    </ItemGroup>
  </Target>
</Project>
""" % escape(str(props), {'"': '&quot;'}), encoding="utf-8")
    (project_root / "Class1.cs").write_text("public sealed class Class1 { }\n", encoding="utf-8")
    expected = [
        "obj/Release/net8.0/ref/PathIdentity.dll",
        "bin/Release/net8.0/PathIdentity.dll",
        "bin/Release/net8.0/PathIdentity.deps.json",
        "obj/Release/net8.0/current.txt",
    ]
    steps = [
        ("real-build", project, ["-p:WriteObsolete=true"]),
        ("alias-rebuild", alias_root / project.name, ["-t:Rebuild"]),
        ("real-build-again", project, []),
    ]
    print(f"Artifacts: {run_root}", flush=True)
    if consumer_project is not None:
        check_restore_graph(consumer_project, run_root, "before-builds")
    for index, (name, project_path, arguments) in enumerate(steps):
        command = ["dotnet", "build", str(project_path), "-c", "Release", "-m:1",
                   "-p:EnableMGCBItems=false", "-p:AutoRestoreMGCBTool=false", *arguments]
        log_path = run_root / f"{name}.log"
        with log_path.open("w", encoding="utf-8") as log:
            result = subprocess.run(command, cwd=run_root, stdout=log, stderr=subprocess.STDOUT,
                                    timeout=120, check=False)
        if result.returncode:
            raise AssertionError(f"{name} failed; see {log_path}")
        for relative_path in expected:
            if not (project_root / relative_path).is_file():
                raise AssertionError(f"{name} removed current output {relative_path}; see {log_path}")
        obsolete = project_root / "obj/Release/net8.0/obsolete.txt"
        if obsolete.exists() != (index == 0):
            raise AssertionError(f"{name} did not maintain obsolete-output cleanup; see {log_path}")
        print(f"PASS {name}", flush=True)
    if consumer_project is not None:
        check_restore_graph(consumer_project, run_root, "after-builds")
    return run_root


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--props", type=Path,
                        default=Path(__file__).resolve().parents[1] / "MGUI.MonoGame.props",
                        help="Props under test (accepts a saved baseline for A/B checks)")
    parser.add_argument("--artifact-root", type=Path, default=Path(tempfile.gettempdir()),
                        help="Parent for preserved disposable projects and build logs")
    parser.add_argument("--consumer-project", type=Path,
                        help="MGUI consumer whose restore graph must contain one MGUI.Shared identity")
    arguments = parser.parse_args()
    props = arguments.props.resolve(strict=True)
    consumer = arguments.consumer_project.absolute() if arguments.consumer_project else None
    check_build_path_identity(props, arguments.artifact_root.resolve(), consumer)


if __name__ == "__main__":
    main()
