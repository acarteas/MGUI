using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGElementRenderEligibilityTests
{
    [Fact]
    public void BuiltinAdmissionUsesExactRuntimeTypesAndKnownGenericDefinitions()
    {
        Type[] excluded = [typeof(MGWindow), typeof(MGToolTip), typeof(MGContextMenu), typeof(MGOverlayHost),
            typeof(MGXAMLDesigner), typeof(MGTreeViewItem)];
        Type[] concrete = typeof(MGElement).Assembly.GetTypes()
            .Where(type => typeof(MGElement).IsAssignableFrom(type) && !type.IsAbstract).ToArray();
        Assert.Equal(56, concrete.Length);
        foreach (Type type in concrete)
        {
            Assert.Equal(!excluded.Contains(type), MGElement.IsBuiltInRenderCacheType(type));
        }
        Assert.True(MGElement.IsBuiltInRenderCacheType(typeof(MGListBox<int>)));
        Assert.True(MGElement.IsBuiltInRenderCacheType(typeof(MGComboBox<string>)));
        Assert.True(MGElement.IsBuiltInRenderCacheType(typeof(MGListView<object>)));
        Assert.False(MGElement.IsBuiltInRenderCacheType(typeof(CustomButton)));
        Assert.False(MGElement.IsBuiltInRenderCacheType(typeof(UnknownElement)));
        Assert.False(MGElement.IsBuiltInRenderCacheType(typeof(MGElement)));
    }

    [Fact]
    public void CustomParticipationAndIntrinsicVolatilityAreMaintainedAndRecover()
    {
        Probe root = Create();
        Probe child = Create();
        root.Children.Add(child);
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        ulong layout = root.LayoutRevision;
        ulong visual = root.VisualRevision;
        child.Participates = false;
        child.InvalidateVisual();
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnsupportedCount);
        Assert.True(root.VisualRevision > visual);
        child.Participates = true;
        child.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        child.IsRenderVolatile = true;
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().VolatileCount);
        child.IntrinsicVolatility = true;
        child.InvalidateVisual();
        child.IsRenderVolatile = false;
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().VolatileCount);
        child.IntrinsicVolatility = false;
        child.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        Assert.Equal(layout, root.LayoutRevision);
    }

    [Fact]
    public void ContentAndComponentAliasesAreDistinctAndRemovalReparentingResetRecover()
    {
        Probe first = Create();
        Probe second = Create();
        Probe child = Create();
        child.IsRenderVolatile = true;
        first.Children.Add(child);
        first.Attach(child);
        first.InvalidateVisual();
        first.UpdateLayout(Rectangle.Empty);
        child.UpdateLayout(Rectangle.Empty);
        Assert.Equal(1, first.GetRenderEligibilitySnapshot().VolatileCount);
        Assert.True(second.GetRenderEligibilitySnapshot().IsEligible);
        first.Children.Clear();
        first.InvalidateVisual();
        Assert.Equal(1, first.GetRenderEligibilitySnapshot().VolatileCount);
        first.ClearComponents();
        first.InvalidateVisual();
        second.Children.Add(child);
        second.InvalidateVisual();
        Assert.True(first.GetRenderEligibilitySnapshot().IsEligible);
        Assert.Equal(1, second.GetRenderEligibilitySnapshot().VolatileCount);
        child.IsRenderVolatile = false;
        Assert.True(second.GetRenderEligibilitySnapshot().IsEligible);
        child.Participates = false;
        child.InvalidateVisual();
        Assert.Equal(1, second.GetRenderEligibilitySnapshot().UnsupportedCount);
        second.Children.Clear();
        second.InvalidateVisual();
        Assert.True(second.GetRenderEligibilitySnapshot().IsEligible);
        child.IsRenderVolatile = true;
        Assert.True(first.GetRenderEligibilitySnapshot().IsEligible);
        Assert.True(second.GetRenderEligibilitySnapshot().IsEligible);
    }

    [Fact]
    public void PublicHooksCountMulticastDuplicatesAndAbsentRemovalWithoutIdleDelegateInspection()
    {
        Probe root = Create();
        Probe child = Create();
        root.Children.Add(child);
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        EventHandler<MGElement.MGElementDrawEventArgs> first = (_, _) => { };
        EventHandler<MGElement.MGElementDrawEventArgs> second = (_, _) => { };
        EventHandler<MGElement.MGElementDrawEventArgs> missing = (_, _) => { };
        child.OnBeginDraw += first + second;
        child.OnBeginDraw += first;
        child.OnEndingDraw += second;
        child.OnEndDraw += first;
        Assert.Equal(5, child.PublicDrawCallbackCount);
        Assert.Equal(5, root.GetRenderEligibilitySnapshot().PublicDrawCallbackCount);
        ulong before = root.VisualRevision;
        child.OnBeginDraw -= missing;
        Assert.Equal(before, root.VisualRevision);
        child.OnBeginDraw -= first + second;
        Assert.Equal(3, root.GetRenderEligibilitySnapshot().PublicDrawCallbackCount);
        child.OnBeginDraw -= first;
        child.OnEndingDraw -= second;
        child.OnEndDraw -= first;
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
    }

    [Fact]
    public void ObservableAndExplicitRawSourceChangesReconcileSupportVolatilityAndUnboundedFacts()
    {
        Probe root = Create();
        Probe child = Create();
        MutableBrush source = new();
        MGCompositedFillBrush composite = new(source);
        child.BackgroundBrush = new(composite);
        root.Children.Add(child);
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        source.Volatile = true;
        source.Signal();
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().VolatileCount);
        source.Volatile = false;
        source.Supported = false;
        source.Signal();
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnsupportedCount);
        source.Supported = true;
        source.Signal();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        composite.Brushes.Add(new LegacyBrush());
        root.InvalidateVisual();
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnsupportedCount);
        composite.Brushes.Clear();
        composite.Brushes.Add(new MGPaddedFillBrush(Color.Red.AsFillBrush(), new Thickness(-3)));
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().UnboundedPaintCount > 0);
        composite.Brushes.Clear();
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
    }

    [Fact]
    public void TransformClipAndControlFactsUpdateAtSemanticMutationAndRestore()
    {
        Probe root = Create();
        Probe child = Create();
        root.Children.Add(child);
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        child.RenderScale = new(0.9f, 1.1f);
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        typeof(MGElement).GetProperty(nameof(MGElement.VisualState))!.SetValue(child,
            new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.Hovered));
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnsupportedTransformCount);
        child.RenderScale = new(1, 1);
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        child.ClipToBounds = false;
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnboundedPaintCount);
        child.ClipToBounds = true;
        child.OpaqueState = true;
        child.ControlHooks = 3;
        child.InvalidateVisual();
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnsupportedCount);
        Assert.Equal(3, root.GetRenderEligibilitySnapshot().PublicDrawCallbackCount);
        child.OpaqueState = false;
        child.ControlHooks = 0;
        child.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
    }

    [Fact]
    public void DeferredLayoutBlocksImmediatelyAndIndependentChildArrangeDoesNotLeaveOwnerPending()
    {
        Probe root = Create();
        Probe child = Create();
        root.Children.Add(child);
        root.InvalidateVisual();
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        using (child.BeginInitializing())
        {
            child.PreferredWidth = 20;
            Assert.True(child.IsLayoutValid);
            Assert.Equal(1, root.GetRenderEligibilitySnapshot().UnresolvedLayoutCount);
        }
        child.UpdateLayout(Rectangle.Empty);
        Assert.True(root.IsLayoutValid);
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        SetField(child, "_LayoutBounds", new Rectangle(3, 4, 20, 20));
        child.UpdateLayout(Rectangle.Empty);
        Assert.True(root.IsLayoutValid);
        Assert.True(child.IsLayoutValid);
        Assert.Equal(0, root.GetRenderEligibilitySnapshot().UnresolvedLayoutCount);
        Assert.Equal(Rectangle.Empty, child.GetRenderEligibilitySnapshot().PaintBounds);
    }

    [Fact]
    public void EligibilityIsLazyAndThousandNodeIdleReadsDoNotDiscoverPollOrAllocate()
    {
        Probe[] nodes = Enumerable.Range(0, 1000).Select(_ => Create()).ToArray();
        for (int index = 1; index < nodes.Length; index++)
        {
            nodes[(index - 1) / 4].Children.Add(nodes[index]);
        }
        Probe root = nodes[0];
        root.InvalidateVisual();
        Assert.All(nodes, node => Assert.False(node.IsRenderSourceSubtreeTrackingEnabled));
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        foreach (Probe node in nodes)
        {
            node.Discoveries = 0;
            node.SourceDiscoveries = 0;
            node.SupportReads = 0;
        }
        for (int iteration = 0; iteration < 100; iteration++)
        {
            Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.All(nodes, node =>
        {
            Assert.Equal(0, node.Discoveries);
            Assert.Equal(0, node.SourceDiscoveries);
            Assert.Equal(0, node.SupportReads);
        });
        nodes[999].IsRenderVolatile = true;
        Assert.Equal(1, root.GetRenderEligibilitySnapshot().VolatileCount);
        nodes[999].IsRenderVolatile = false;
        Assert.True(root.GetRenderEligibilitySnapshot().IsEligible);
    }

    private static Probe Create()
    {
        Probe element = (Probe)RuntimeHelpers.GetUninitializedObject(typeof(Probe));
        element.Children = new();
        element.Participates = true;
        SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(element.SignalLayout));
        SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
        SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
        SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        SetField(element, "_ClipToBounds", true);
        SetField(element, "_IsLayoutValid", true);
        SetField(element, "_VisualState", new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None));
        return element;
    }

    private static void SetField(object instance, string name, object value)
        => typeof(MGElement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private sealed class CustomButton : MGButton
    {
        public CustomButton(MGWindow window) : base(window) { }
    }

    private sealed class UnknownElement : MGElement
    {
        public UnknownElement(MGWindow window) : base(window, MGElementType.Border) { }
    }

    private sealed class Probe : MGElement
    {
        public List<MGElement> Children = new();
        public bool Participates;
        public bool IntrinsicVolatility;
        public bool OpaqueState;
        public int ControlHooks;
        public int Discoveries;
        public int SourceDiscoveries;
        public int SupportReads;
        private Probe() : base(default(MGWindow), MGElementType.Border) { }
        protected override bool SupportsRenderCaching { get { SupportReads++; return Participates; } }
        protected internal override bool IsBuiltInRenderVolatile => IntrinsicVolatility;
        internal override bool HasUnsupportedControlRenderState => OpaqueState;
        internal override int ControlPublicDrawCallbackCount => ControlHooks;
        public override IEnumerable<MGElement> GetChildren() { Discoveries++; return Children; }
        protected override IEnumerable<object> EnumerateControlRenderSources() { SourceDiscoveries++; return Array.Empty<object>(); }
        public void SignalLayout() => LayoutChanged(this, false);
        public void ClearComponents() => Components.Clear();
        public void Attach(MGElement child)
            => AddComponent(new MGComponent<MGElement>(child, ComponentUpdatePriority.BeforeContents,
                ComponentDrawPriority.BeforeContents, false, false, false, false, false, false, false, (bounds, _) => bounds));
    }

    private sealed class LegacyBrush : IFillBrush
    {
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => new LegacyBrush();
    }

    private sealed class MutableBrush : IFillBrush, IMGRenderCacheSource
    {
        public bool Volatile;
        public bool Supported = true;
        public ulong VisualRevision { get; private set; } = 1;
        public bool SupportsRenderCaching => Supported;
        public bool IsRenderVolatile => Volatile;
        public event EventHandler? VisualInvalidated;
        public void Signal() { VisualRevision++; VisualInvalidated?.Invoke(this, EventArgs.Empty); }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => new MutableBrush();
    }
}
