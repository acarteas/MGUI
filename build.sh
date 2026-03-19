#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

projects=(
  "MGUI.Shared/MGUI.Shared.csproj"
  "MGUI.Core/MGUI.Core.csproj"
  "MGUI.FontStashSharp/MGUI.FontStashSharp.csproj"
  "MGUI.Tests/MGUI.Tests.csproj"
  "MGUI.Samples/MGUI.Samples.csproj"
)

for project in "${projects[@]}"; do
  echo "==> Building $project"
  dotnet build "$project" "$@"
done
