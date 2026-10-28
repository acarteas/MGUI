using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using MGUI.Samples.Features;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Portable.Xaml;

namespace MGUI.Tests.XAML;

public sealed class ShaderEffectsSampleSetupTests
{
    [Fact]
    public void SuccessfulLoadPreservesShaderExamplesAndLeavesFailurePreviewUnavailable()
    {
        MGResources resources = new(new MGTheme("TestFont"));
        Effect effect = (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
        ShaderEffectsSampleSetup setup = new(resources, () => effect);
        string xaml = ReadSample();

        Assert.True(setup.HasEffect);
        Assert.Contains("loaded successfully", setup.StatusText);
        Assert.Same(effect, resources.Effects[ShaderEffectsSampleSetup.EffectName]);
        Assert.False(resources.TryGetEffect(ShaderEffectsSampleSetup.UnavailableEffectName, out _));
        Assert.Contains("Intentional load failure", setup.FailureStatusText);
        Assert.Equal(xaml, setup.PrepareXaml(xaml));
        AssertPreviewBrushes(resources, xaml, true);
    }

    [Fact]
    public void FailedLoadKeepsSampleParseableAndConvertsImageAndFrameExamplesToOrdinaryTextures()
    {
        MGResources resources = new(new MGTheme("TestFont"));
        resources.AddEffect(ShaderEffectsSampleSetup.EffectName, (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect)));
        ShaderEffectsSampleSetup setup = new(resources, () => throw new ContentLoadException("Missing UiEffects.xnb"));
        string xaml = setup.PrepareXaml(ReadSample());

        Assert.False(setup.HasEffect);
        Assert.Contains("Missing UiEffects.xnb", setup.StatusText);
        Assert.False(resources.TryGetEffect(ShaderEffectsSampleSetup.EffectName, out _));
        Assert.IsType<Window>(XamlServices.Parse(xaml));
        XElement[] textureElements = XDocument.Parse(xaml).Descendants()
            .Where(element => element.Name.LocalName is "Image" or "NineSliceFillBrush").ToArray();
        Assert.NotEmpty(textureElements);
        Assert.All(textureElements, element =>
        {
            Assert.Null(element.Attribute("EffectName"));
            Assert.Null(element.Attribute("UseStandardParameters"));
            Assert.DoesNotContain(element.Elements(), child => child.Name.LocalName == "EffectParameter");
            Assert.NotNull(element.Attribute("SourceName"));
        });
        AssertPreviewBrushes(resources, xaml, false);
    }

    [Fact]
    public void ComparisonGridsAllocateVisibleDistinctCells()
    {
        XElement[] grids = XDocument.Parse(ReadSample()).Descendants()
            .Where(element => element.Name.LocalName == "UniformGrid").ToArray();
        Assert.Equal(3, grids.Length);
        foreach (XElement declaration in grids)
        {
            UniformGrid grid = Assert.IsType<UniformGrid>(XamlServices.Parse(declaration.ToString()));
            Assert.True(grid.AutoAssignCells);
            Assert.True(grid.CellSize?.Width > 0);
            Assert.True(grid.CellSize?.Height > 0);
        }
    }

    private static void AssertPreviewBrushes(MGResources resources, string xaml, bool hasEffect)
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        typeof(MGDesktop).GetField("<Resources>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(desktop, resources);
        XElement[] declarations = XDocument.Parse(xaml).Descendants()
            .Where(element => element.Name.LocalName == "EffectFillBrush").ToArray();
        Assert.Contains(declarations, element => (string?)element.Attribute("EffectName") == ShaderEffectsSampleSetup.EffectName);
        Assert.Contains(declarations, element => (string?)element.Attribute("EffectName") == ShaderEffectsSampleSetup.UnavailableEffectName);
        foreach (XElement declaration in declarations)
        {
            EffectFillBrush brush = Assert.IsType<EffectFillBrush>(XamlServices.Parse(declaration.ToString()));
            IFillBrush runtime = brush.ToFillBrush(desktop, null);
            if (hasEffect && brush.EffectName == ShaderEffectsSampleSetup.EffectName)
            {
                Assert.IsType<MGEffectFillBrush>(runtime);
            }
            else
            {
                Assert.IsType<MGSolidFillBrush>(runtime);
            }
        }
    }

    private static string ReadSample()
    {
        using Stream stream = typeof(ShaderEffectsSampleSetupTests).Assembly.GetManifestResourceStream("MGUI.Tests.Fixtures.ShaderEffects.xaml")!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
