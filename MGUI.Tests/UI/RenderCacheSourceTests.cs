using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Runtime.CompilerServices;
using Xunit;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class RenderCacheSourceTests
{
    [Fact]
    public void OptionalContractPreservesLegacyBrushAndConservativelyRejectsIt()
    {
        var legacy = new LegacyBrush();
        legacy.Draw(default, null!, Rectangle.Empty);
        Assert.Equal(1, legacy.Draws);
        using var graph = Graph(legacy);
        Assert.False(graph.SupportsRenderCaching);
        Assert.False(graph.IsRenderVolatile);
    }

    [Fact]
    public void NestedDiamondInvalidatesExactlyOnceEvenWithoutCustomRevisionBump()
    {
        var source = new MutableSource();
        var first = new MGPaddedFillBrush(source, new Thickness(1));
        var second = new MGBorderedFillBrush(new Thickness(1), new MGUniformBorderBrush(source), source, true);
        var composite = new MGCompositedFillBrush(first, second);
        int changes = 0;
        using var graph = Graph(composite, () => changes++);
        Assert.True(graph.SupportsRenderCaching);
        source.Signal(false);
        Assert.Equal(1, changes);
        Assert.True(graph.SupportsRenderCaching);
        source.Volatile = true;
        source.Signal();
        Assert.Equal(2, changes);
        Assert.True(graph.IsRenderVolatile);
    }

    [Fact]
    public void SemanticSettersAndVisualStateModifiersNotifyButEqualValuesDoNot()
    {
        var padded = new MGPaddedFillBrush(Color.Red.AsFillBrush(), new Thickness(1));
        var setting = new VisualStateFillBrush(padded);
        int changes = 0;
        using var graph = Graph(setting, () => changes++);
        Assert.Equal(1UL, padded.VisualRevision);
        padded.Padding = new Thickness(2);
        Assert.Equal(1, changes);
        Assert.Equal(2UL, padded.VisualRevision);
        padded.Padding = new Thickness(2);
        setting.NormalValue = padded;
        Assert.Equal(1, changes);
        setting.FocusedColor = Color.Blue;
        setting.PressedModifier = 0.2f;
        setting.PressedModifierType = PressedModifierType.Brighten;
        Assert.Equal(4, changes);
    }

    [Fact]
    public void ExplicitRawListReconciliationRestoresEligibilityAndDetachesRemovedSources()
    {
        var mutable = new MutableSource { Volatile = true };
        var legacy = new LegacyBrush();
        var composite = new MGCompositedFillBrush(mutable, legacy);
        int changes = 0;
        using var graph = Graph(composite, () => changes++);
        Assert.False(graph.SupportsRenderCaching);
        Assert.True(graph.IsRenderVolatile);
        composite.Brushes.Clear();
        composite.Brushes.Add(Color.Red.AsFillBrush());
        Assert.False(graph.SupportsRenderCaching);
        graph.Reconcile();
        Assert.True(graph.SupportsRenderCaching);
        Assert.False(graph.IsRenderVolatile);
        Assert.True(composite.SupportsRenderCaching);
        Assert.False(composite.IsRenderVolatile);
        Assert.Equal(0, mutable.Subscriptions);
        mutable.Signal();
        Assert.Equal(0, changes);
        var added = new MutableSource();
        composite.Brushes.Add(added);
        graph.Reconcile();
        added.Signal();
        Assert.Equal(1, changes);
    }

    [Fact]
    public void CyclicGraphIsUnsupportedAndCanRecoverAfterExplicitInvalidation()
    {
        var a = new MGCompositedFillBrush();
        var b = new MGPaddedFillBrush(a, new Thickness(0));
        a.Brushes.Add(b);
        using var graph = Graph(a);
        Assert.False(graph.SupportsRenderCaching);
        a.Brushes.Clear();
        graph.Reconcile();
        Assert.True(graph.SupportsRenderCaching);
    }

    [Fact]
    public void IndependentCopiesHaveIndependentNestedSourcesAndRevisionState()
    {
        var original = new MGBorderedFillBrush(new Thickness(1), Color.Red.AsFillBrush().AsUniformBorderBrush(),
            new MGPaddedFillBrush(Color.Blue.AsFillBrush(), new Thickness(1)), true);
        var copy = (MGBorderedFillBrush)original.Copy();
        int originalChanges = 0;
        int copyChanges = 0;
        using var first = Graph(original, () => originalChanges++);
        using var second = Graph(copy, () => copyChanges++);
        Assert.NotSame(original.FillBrush, copy.FillBrush);
        ((MGPaddedFillBrush)copy.FillBrush).Padding = new Thickness(3);
        Assert.Equal(0, originalChanges);
        Assert.Equal(1, copyChanges);
        Assert.Equal(1UL, original.VisualRevision);
        Assert.Equal(2UL, copy.VisualRevision);
    }

    [Fact]
    public void DistinctReentrantMutationInvalidatesObservationTakenInFirstCallback()
    {
        var padded = new MGPaddedFillBrush(Color.Red.AsFillBrush(), new Thickness(1));
        int changes = 0;
        ulong observed = 0;
        MGRenderCacheSourceDependencies? graph = null;
        graph = Graph(padded, () =>
        {
            changes++;
            if (changes == 1)
            {
                observed = graph!.VisualRevision;
                ulong sourceRevision = padded.VisualRevision;
                padded.Brush = new LegacyBrush();
                Assert.Equal(2, changes);
                Assert.True(padded.VisualRevision > sourceRevision);
                Assert.False(graph.SupportsRenderCaching);
            }
        });
        using (graph)
        {
            padded.Padding = new Thickness(2);
            Assert.Equal(2, changes);
            Assert.True(graph.VisualRevision > observed);
            Assert.False(graph.SupportsRenderCaching);
        }
    }

    [Fact]
    public void UnknownEffectAndUnboundedSourcesRemainUnsupported()
    {
        using var effect = Graph(new MGEffectFillBrush(null));
        Assert.False(effect.SupportsRenderCaching);
        var padded = new MGPaddedFillBrush(Color.Red.AsFillBrush(), new Thickness(-1));
        using var graph = Graph(padded);
        Assert.False(graph.SupportsRenderCaching);
        padded.Padding = new Thickness(1);
        Assert.True(graph.SupportsRenderCaching);
        var highlight = new MGHighlightFillBrush();
        using var highlights = Graph(highlight);
        Assert.False(highlights.SupportsRenderCaching);
        highlight.IsEnabled = false;
        Assert.True(highlights.SupportsRenderCaching);
        var border = new MGHighlightBorderBrush(null, Color.White, HighlightAnimation.Pulse, null);
        using var borders = Graph(border);
        Assert.True(borders.IsRenderVolatile);
        border.IsEnabled = false;
        Assert.False(borders.IsRenderVolatile);
    }

    [Fact]
    public void UnknownSubclassCannotInheritAuditedBrushQualification()
    {
        using var graph = Graph(new UntrackedPadded());
        Assert.False(graph.SupportsRenderCaching);
    }

    [Fact]
    public void ReferencedProgressBarChangesNotifyWithoutOwningTheBar()
    {
        var bar = (MGProgressBar)RuntimeHelpers.GetUninitializedObject(typeof(MGProgressBar));
        int changes = 0;
        using var graph = Graph(new MGProgressBarGradientBrush(bar), () => changes++);
        bar.Minimum = -1;
        bar.Maximum = 10;
        bar.Value = 3;
        Assert.Equal(3, changes);
        bar.Value = 3;
        Assert.Equal(3, changes);
        graph.ReplaceSources(new object[] { Color.Red.AsFillBrush() });
        bar.Value = 5;
        Assert.Equal(3, changes);
    }

    [Fact]
    public void ResourceAndNestedThemeChangesNotifyWithoutRebindingMaterializedValues()
    {
        var resources = new MGResources(new MGTheme("example"));
        int changes = 0;
        using var graph = Graph(resources, () => changes++);
        var copied = resources.DefaultTheme.TextBlockFallbackForeground.GetValue(true);
        Color original = copied.NormalValue;
        resources.AddStaticResource("example", new object());
        Assert.Equal(1, changes);
        resources.RemoveStaticResource("example");
        Assert.Equal(2, changes);
        var theme = resources.DefaultTheme;
        theme.FontSettings.DefaultFontSize++;
        Assert.Equal(3, changes);
        var old = theme.TextBlockFallbackForeground.GetValue(false);
        old.NormalValue = Color.HotPink;
        Assert.Equal(4, changes);
        Assert.Equal(original, copied.NormalValue);
        resources.DefaultTheme = new MGTheme("replacement");
        Assert.Equal(5, changes);
        old.NormalValue = Color.Blue;
        Assert.Equal(5, changes);
        Assert.True(graph.SupportsRenderCaching);
    }

    [Fact]
    public void ObservationCatalogDoesNotHideTheSameActualPaintDependency()
    {
        var legacy = new LegacyBrush();
        var volatileSource = new MutableSource { Volatile = true };
        var theme = new MGTheme("example");
        theme.SetBackgroundBrush(MGElementType.Border, new VisualStateFillBrush(new MGCompositedFillBrush(legacy, volatileSource)));
        object owner = new();
        using var graph = new MGRenderCacheSourceDependencies(owner, static _ => { });
        graph.ReplaceSources(new object[] { theme, legacy, volatileSource });
        Assert.False(graph.SupportsRenderCaching);
        Assert.True(graph.IsRenderVolatile);
        graph.ReplaceSources(new object[] { legacy, volatileSource, theme });
        Assert.False(graph.SupportsRenderCaching);
        Assert.True(graph.IsRenderVolatile);
        GC.KeepAlive(owner);
    }

    [Fact]
    public void WarmFactsDoNotPollCustomSourceProperties()
    {
        var source = new MutableSource();
        using var graph = Graph(new MGPaddedFillBrush(source, new Thickness(0)));
        int reads = source.Reads;
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(graph.SupportsRenderCaching);
            Assert.False(graph.IsRenderVolatile);
            Assert.True(graph.ResourcesAreAlive);
            _ = graph.VisualRevision;
        }
        Assert.Equal(reads, source.Reads);
    }

    [Fact]
    public void SharedPublisherDoesNotRootOwnerAndExplicitDisposeUnsubscribes()
    {
        var source = new MutableSource();
        var (owner, graph) = CreateWeakGraph(source);
        for (int i = 0; i < 4; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(owner.IsAlive);
        Assert.False(graph.IsAlive);
        source.Signal();
        Assert.Equal(0, source.Subscriptions);
        using (var active = Graph(source))
        {
            Assert.Equal(1, source.Subscriptions);
        }
        Assert.Equal(0, source.Subscriptions);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference) CreateWeakGraph(MutableSource source)
    {
        object owner = new();
        var graph = new MGRenderCacheSourceDependencies(owner, static _ => { });
        graph.ReplaceSources(new object[] { source });
        return (new(owner), new(graph));
    }

    private static MGRenderCacheSourceDependencies Graph(object source, Action? changed = null)
    {

        // The callback target remains alive for the graph lifetime without a publisher-to-owner root.
        var graph = new MGRenderCacheSourceDependencies(source, _ => changed?.Invoke());
        graph.ReplaceSources(new[] { source });
        return graph;
    }

    private sealed class UntrackedPadded : MGPaddedFillBrush, IFillBrush
    {
        internal UntrackedPadded() : base(Color.Red.AsFillBrush(), new Thickness(0)) { }
        void IFillBrush.Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
    }
    private sealed class LegacyBrush : IFillBrush
    {
        internal int Draws;
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { Draws++; }
        public IFillBrush Copy() => new LegacyBrush();
    }
    private sealed class MutableSource : IFillBrush, IMGRenderCacheSource
    {
        internal int Subscriptions;
        internal int Reads;
        internal bool Volatile;
        public ulong VisualRevision { get; private set; } = 1;
        public bool IsRenderVolatile { get { Reads++; return Volatile; } }
        public bool SupportsRenderCaching { get { Reads++; return true; } }
        private event EventHandler? Invalidated;
        public event EventHandler VisualInvalidated
        {
            add { Subscriptions++; Invalidated += value; }
            remove { Subscriptions--; Invalidated -= value; }
        }
        internal void Signal(bool advance = true)
        {
            if (advance)
            {
                VisualRevision++;
            }
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => new MutableSource();
    }
}
