using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGCachedFillBrushTests
{
    [Fact]
    public void DefaultsValidationEqualityAndIndependentCopy()
    {
        Assert.Throws<ArgumentNullException>(() => new MGCachedFillBrush(null!));
        var source = new Probe();
        var brush = new MGCachedFillBrush(source);
        Assert.True(brush.IsCacheEnabled);
        Assert.Equal(MGRenderCacheSourceMode.Tracked, brush.SourceMode);
        Assert.Equal(1UL, brush.VisualRevision);
        Assert.Throws<ArgumentNullException>(() => brush.Brush = null!);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.SourceMode = (MGRenderCacheSourceMode)12);
        int invalidations = 0;
        brush.VisualInvalidated += (_, _) => invalidations++;
        brush.Brush = source;
        brush.IsCacheEnabled = true;
        brush.SourceMode = MGRenderCacheSourceMode.Tracked;
        Assert.Equal(0, invalidations);
        brush.IsCacheEnabled = false;
        brush.SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic;
        var copy = Assert.IsType<MGCachedFillBrush>(brush.Copy());
        Assert.NotSame(brush.Brush, copy.Brush);
        Assert.False(copy.IsCacheEnabled);
        Assert.Equal(brush.SourceMode, copy.SourceMode);
        ulong copied = copy.VisualRevision;
        brush.InvalidateVisual();
        source.Signal();
        Assert.Equal(copied, copy.VisualRevision);
        Assert.Equal(4, invalidations);
    }

    [Fact]
    public void EqualValueSourceAssignmentIsANoop()
    {
        var brush = new MGCachedFillBrush(new MGSolidFillBrush(Color.Red));
        ulong revision = brush.VisualRevision;
        brush.Brush = new MGSolidFillBrush(Color.Red);
        Assert.Equal(revision, brush.VisualRevision);
    }

    [Fact]
    public void DirectFallbackUsesUntouchedArgumentsAndEmptyBoundsDoNothing()
    {
        var source = new Probe();
        var brush = new MGCachedFillBrush(source);
        var args = new ElementDrawArgs(default, new(PrimaryVisualState.Disabled, SecondaryVisualState.Pressed), new(31, -19));
        var bounds = new Rectangle(-7, 15, 80, 43);
        brush.Draw(args, null!, bounds);
        Assert.Equal(args, source.Args);
        Assert.Equal(bounds, source.Bounds);
        Assert.Null(source.Owner);
        brush.Draw(args, null!, new Rectangle(1, 2, 0, 10));
        brush.Draw(args, null!, new Rectangle(1, 2, 10, -1));
        Assert.Equal(1, source.Draws);
    }

    [Fact]
    public void SourceReplacementDetachesAndRawListInvalidationReconciles()
    {
        var old = new Probe();
        var composite = new MGCompositedFillBrush(old);
        var brush = new MGCachedFillBrush(composite);
        int invalidations = 0;
        brush.VisualInvalidated += (_, _) => invalidations++;
        Assert.True(brush.SupportsRenderCaching);
        old.Signal();
        Assert.Equal(1, invalidations);
        composite.Brushes.Clear();
        composite.Brushes.Add(new Unknown());
        brush.InvalidateVisual();
        Assert.False(brush.SupportsRenderCaching);
        old.Signal();
        Assert.Equal(2, invalidations);
        var next = new Probe();
        brush.Brush = next;
        Assert.True(brush.SupportsRenderCaching);
        next.Signal(false);
        Assert.Equal(4, invalidations);
    }

    [Fact]
    public void StaticAssertionOverridesOnlySupportAndDoesNotBlessSharedSibling()
    {
        var unknown = new Unknown();
        var source = new Probe { Volatile = true };
        var composite = new MGCompositedFillBrush(unknown, source);
        var brush = new MGCachedFillBrush(composite) { SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic };
        using var asserted = Graph(brush);
        Assert.True(asserted.SupportsRenderCaching);
        Assert.True(asserted.IsRenderVolatile);
        source.Volatile = false;
        source.Signal();
        Assert.False(asserted.IsRenderVolatile);
        using var sibling = Graph(new MGCompositedFillBrush(brush, unknown));
        Assert.False(sibling.SupportsRenderCaching);
        composite.Brushes.Add(new MGPaddedFillBrush(new Unknown(), new Thickness(-3)));
        brush.InvalidateVisual();
        Assert.False(brush.SupportsRenderCaching);
        Assert.True(asserted.HasUnboundedPaint);
        Assert.False(asserted.SupportsRenderCaching);
        composite.Brushes.RemoveAt(composite.Brushes.Count - 1);
        brush.InvalidateVisual();
        Assert.True(asserted.SupportsRenderCaching);
        brush.SourceMode = MGRenderCacheSourceMode.Tracked;
        Assert.False(asserted.SupportsRenderCaching);
    }

    [Fact]
    public void CyclicAssertionIsRejectedWithoutRecursiveNotification()
    {
        var brush = new MGCachedFillBrush(new Unknown()) { SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic };
        brush.Brush = brush;
        Assert.False(brush.SupportsRenderCaching);
        brush.InvalidateVisual();
        Assert.False(brush.SupportsRenderCaching);
        brush.Brush = new Unknown();
        Assert.True(brush.SupportsRenderCaching);
    }

    [Fact]
    public void FirstRevisionObservationActivatesSourceInvalidation()
    {
        var source = new Probe();
        var brush = new MGCachedFillBrush(source);
        ulong before = brush.VisualRevision;
        source.Signal();
        Assert.Equal(before + 1, brush.VisualRevision);
        int reads = source.Reads;
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(before + 1, brush.VisualRevision);
        }
        Assert.Equal(reads, source.Reads);
    }

    [Fact]
    public void WarmWrapperFactsDoNotRediscoverSourceAndSharedSourceDoesNotRootWrapper()
    {
        var source = new Probe();
        var brush = new MGCachedFillBrush(source);
        Assert.True(brush.SupportsRenderCaching);
        int reads = source.Reads;
        for (int i = 0; i < 100; i++)
        {
            Assert.True(brush.SupportsRenderCaching);
            Assert.False(brush.IsRenderVolatile);
        }
        Assert.Equal(reads, source.Reads);
        var weak = CreateWeakWrapper(source);
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(weak.IsAlive);
        source.Signal();
        GC.KeepAlive(brush);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeakWrapper(Probe source)
    {
        var brush = new MGCachedFillBrush(source);
        Assert.True(brush.SupportsRenderCaching);
        return new WeakReference(brush);
    }

    private static MGRenderCacheSourceDependencies Graph(object source)
    {
        var graph = new MGRenderCacheSourceDependencies(source, static _ => { });
        graph.ReplaceSources(new[] { source });
        return graph;
    }
    private sealed class Unknown : IFillBrush
    {
        public void Draw(ElementDrawArgs args, MGElement owner, Rectangle bounds) { }
        public IFillBrush Copy() => new Unknown();
    }
    private sealed class Probe : IFillBrush, IMGRenderCacheSource
    {
        internal ElementDrawArgs Args;
        internal MGElement? Owner;
        internal Rectangle Bounds;
        internal int Draws;
        internal bool Volatile;
        internal int Reads;
        public ulong VisualRevision { get; private set; } = 1;
        public bool SupportsRenderCaching { get { Reads++; return true; } }
        public bool IsRenderVolatile { get { Reads++; return Volatile; } }
        public event EventHandler? VisualInvalidated;
        internal void Signal(bool advance = true)
        {
            if (advance)
            {
                VisualRevision++;
            }
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Draw(ElementDrawArgs args, MGElement owner, Rectangle bounds)
        {
            Args = args;
            Owner = owner;
            Bounds = bounds;
            Draws++;
        }
        public IFillBrush Copy() => new Probe();
    }
}
