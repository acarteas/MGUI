using System.Reflection;
using MGUI.Core.UI.XAML;
using Portable.Xaml;

namespace MGUI.Tests.XAML;

public sealed class RenderCachingSampleTests
{
    [Fact]
    public void ActualSampleParsesAndKeepsAnimationAndDiagnosticsOutsideStaticBoundary()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MGUI.Tests.Fixtures.RenderCaching.xaml")!;
        using StreamReader reader = new(stream);
        Window window = Assert.IsType<Window>(XamlServices.Parse(reader.ReadToEnd()));
        ScrollViewer scroll = Assert.IsType<ScrollViewer>(window.Content);
        StackPanel root = Assert.IsType<StackPanel>(scroll.Content);
        OverlayPanel preview = Assert.IsType<OverlayPanel>(root.Children.Single(item => item.Name == "Preview"));
        RenderCache cache = Assert.IsType<RenderCache>(preview.Children.Single(item => item.Name == "StaticPanel"));
        Assert.False(cache.IsCacheEnabled);
        StackPanel art = Assert.IsType<StackPanel>(cache.Content);
        Assert.DoesNotContain(art.Children, item => item.Name is "Score" or "Diagnostics" or "ScoreButton");
        Assert.Contains(root.Children, item => item.Name == "Diagnostics");
        StackPanel live = Assert.IsType<StackPanel>(preview.Children.Last());
        Assert.True(live.Children.Single(item => item.Name == "Score").IsRenderVolatile);
        Assert.Equal("CacheScore", Assert.IsType<Button>(live.Children.Single(item => item.Name == "ScoreButton")).CommandName);
        CachedFillBrush background = Assert.IsType<CachedFillBrush>(preview.Children.Single(item => item.Name == "Backdrop").Background);
        Assert.Null(background.SourceMode);
        Assert.IsType<CompositedFillBrush>(background.Brush);
        CachedFillBrush custom = Assert.IsType<CachedFillBrush>(art.Children.Single(item => item.Name == "CustomSwatch").Background);
        Assert.Equal(MGUI.Core.UI.Brushes.Fill_Brushes.MGRenderCacheSourceMode.ExplicitlyStatic, custom.SourceMode);
        Assert.Contains(root.Children, item => item is RenderCache { Name: "DirtyPanel" });
    }
}
