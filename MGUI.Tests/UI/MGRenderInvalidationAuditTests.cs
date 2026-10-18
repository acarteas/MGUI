using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGRenderInvalidationAuditTests
{

    // Classification is an audit inventory, not the runtime eligibility allowlist owned by T18.
    private static readonly Type[] AuditedTypes =
    [
        typeof(MGElement), typeof(MGContentHost), typeof(MGSingleContentHost), typeof(MGMultiContentHost),
        typeof(MGContextMenuItem), typeof(MGWrappedContextMenuItem),
        typeof(MGBorder), typeof(MGButton), typeof(MGChatBox), typeof(MGChatBoxMessage), typeof(MGCheckBox),
        typeof(MGComboBox<>), typeof(MGContentPresenter), typeof(MGContextMenu), typeof(MGContextMenuButton),
        typeof(MGContextMenuRadioButton), typeof(MGContextMenuSeparator), typeof(MGContextMenuToggle),
        typeof(MGContextualContentPresenter), typeof(MGDockPanel), typeof(MGExpander), typeof(MGGrid),
        typeof(MGGridColorPicker), typeof(MGGridSplitter), typeof(MGGroupBox), typeof(MGHeaderedContentPresenter),
        typeof(MGImage), typeof(MGInputConsumer), typeof(MGListBox<>), typeof(MGListView<>), typeof(MGMenuBar),
        typeof(MGMenuBarItem), typeof(MGOverlay), typeof(MGOverlayHost), typeof(MGOverlayPanel), typeof(MGPasswordBox),
        typeof(MGProgressBar), typeof(MGProgressButton), typeof(MGRadioButton), typeof(MGRatingControl),
        typeof(MGRectangle), typeof(MGResizeGrip), typeof(MGScrollViewer), typeof(MGSeparator), typeof(MGSlider),
        typeof(MGSpacer), typeof(MGSpoiler), typeof(MGStackPanel), typeof(MGStopwatch), typeof(MGTabControl),
        typeof(MGTabItem), typeof(MGTextBlock), typeof(MGTextBox), typeof(MGTimer), typeof(MGToggleButton),
        typeof(MGToolTip), typeof(MGTreeView), typeof(MGTreeViewItem), typeof(MGUniformGrid), typeof(MGWindow),
        typeof(MGXAMLDesigner)
    ];

    [Fact]
    public void EveryRuntimeElementTypeAndDeclaredPaintMethodIsInTheReviewedInventory()
    {
        Type[] actual = typeof(MGElement).Assembly.GetTypes().Where(IsElementType).OrderBy(t => t.FullName).ToArray();
        Assert.Equal(AuditedTypes.OrderBy(t => t.FullName), actual);
        string[] actualPainters = actual.SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(m => m.IsVirtual &&
            m.Name is "Draw" or "DrawBackground" or "DrawSelf" or "DrawContents")
            .Select(m => $"{t.Name}.{m.Name}")).OrderBy(n => n).ToArray();
        string[] expectedPainters =
        [
            "MGBorder.DrawSelf", "MGButton.DrawContents", "MGCheckBox.DrawSelf", "MGContentHost.DrawContents",
            "MGContentPresenter.DrawSelf", "MGElement.Draw", "MGElement.DrawBackground", "MGElement.DrawContents",
            "MGElement.DrawSelf", "MGGrid.DrawSelf", "MGGridColorPicker.DrawSelf", "MGGridSplitter.DrawSelf",
            "MGGroupBox.DrawBackground", "MGGroupBox.DrawSelf", "MGImage.DrawSelf", "MGListBox`1.DrawSelf",
            "MGListView`1.DrawBackground", "MGOverlayHost.DrawBackground", "MGProgressBar.DrawSelf",
            "MGProgressButton.DrawSelf", "MGRadioButton.DrawSelf", "MGRatingControl.DrawSelf", "MGRectangle.DrawSelf",
            "MGResizeGrip.DrawSelf", "MGScrollViewer.DrawContents", "MGScrollViewer.DrawSelf", "MGSlider.DrawSelf",
            "MGSpacer.DrawBackground", "MGSpacer.DrawContents", "MGTabControl.DrawBackground", "MGTabControl.DrawSelf",
            "MGTabItem.DrawSelf", "MGTextBlock.DrawSelf", "MGTextBox.DrawSelf", "MGTreeViewItem.Draw",
            "MGUniformGrid.DrawSelf", "MGWindow.Draw"
        ];
        Assert.Equal(expectedPainters.OrderBy(n => n), actualPainters);
    }

    private static bool IsElementType(Type type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            if (current == typeof(MGElement))
            {
                return true;
            }
        }
        return false;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void ThousandWarmedRevisionChecksHaveZeroDiscoveryObserverChurnAndAllocation(int nodeCount)
    {
        MGWindow window = CreateWindow();
        AuditProbe[] nodes = Enumerable.Range(0, nodeCount).Select(_ => new AuditProbe(window)).ToArray();
        for (int index = 1; index < nodes.Length; ++index)
        {
            nodes[(index - 1) / 4].PaintChildren.Add(nodes[index]);
        }
        AuditProbe root = nodes[0];
        root.InvalidateVisual();
        MGRenderRevisionSnapshot captured = root.GetRenderRevisionSnapshot();
        MGRenderCacheSourceDependencies dependencies = root.BaseRenderSourceDependencies;
        ulong sourceRevision = dependencies.VisualRevision;
        Assert.All(nodes, node => Assert.True(node.Source.ActiveSubscriptions > 0));
        CheckIdle(root, captured, dependencies, sourceRevision, 100);
        foreach (AuditProbe node in nodes)
        {
            node.ResetCounters();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool current = CheckIdle(root, captured, dependencies, sourceRevision, 1000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current);
        Assert.Equal(0, allocated);
        Assert.All(nodes, node =>
        {
            Assert.Equal(0, node.ChildDiscoveries);
            Assert.Equal(0, node.SourceDiscoveries);
            Assert.Equal(0, node.Source.Reads);
            Assert.Equal(0, node.Source.SubscriptionChanges);
        });
        ulong revisionBeforeBurst = root.VisualRevision;
        for (int index = 0; index < 100; ++index)
        {
            nodes[nodes.Length - 1 - index % Math.Min(nodes.Length, 7)].Source.Change();
        }
        Assert.Equal(revisionBeforeBurst + 100, root.VisualRevision);
        Assert.False(root.IsRenderRevisionCurrent(captured));
        captured = root.GetRenderRevisionSnapshot();
        Assert.True(CheckIdle(root, captured, dependencies, dependencies.VisualRevision, 1000));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckIdle(MGElement root, MGRenderRevisionSnapshot captured,
        MGRenderCacheSourceDependencies dependencies, ulong sourceRevision, int frames)
    {
        bool current = true;
        for (int frame = 0; frame < frames; ++frame)
        {
            current &= root.IsRenderRevisionCurrent(captured);
            current &= root.VisualRevision == captured.Visual && root.LayoutRevision == captured.Layout &&
                root.StructureRevision == captured.Structure && root.GetRenderRevisionSnapshot() == captured;
            current &= dependencies.VisualRevision == sourceRevision && dependencies.SupportsRenderCaching &&
                !dependencies.IsRenderVolatile && dependencies.ResourcesAreAlive && !dependencies.HasUnboundedPaint;
        }
        return current;
    }

    [Fact]
    public void MixedHudSemanticMutationsInvalidateOnlyTheirRenderedOwnershipPaths()
    {
        MGWindow window = CreateWindow();
        MGStackPanel root = new(window, Orientation.Horizontal);
        MGStackPanel status = new(window, Orientation.Vertical);
        MGStackPanel inventory = new(window, Orientation.Vertical);
        MGTextBlock score = new(window, "score 10");
        MGButton toggle = new(window);
        MGToggleButton checkedButton = new(window);
        MGTextBlock caption = new(window, "ready");
        toggle.SetContent(caption);
        MGImage icon = new(window, "missing-test-icon");
        MGListBox<string> list = new(window);
        ObservableCollection<string> items = new() { "first", "other" };
        list.SetItemsSource(items);
        AuditProbe custom = new(window);
        custom.OnEndingDraw += (_, _) => { };
        status.TryAddChild(score);
        status.TryAddChild(toggle);
        status.TryAddChild(checkedButton);
        status.TryAddChild(icon);
        inventory.TryAddChild(list);
        inventory.TryAddChild(custom);
        root.TryAddChild(status);
        root.TryAddChild(inventory);
        window.SetContent(root);
        _ = root.VisualRevision;
        MGElement[] observed = Reachable(root);
        Assert.Contains(status, root.RenderPaintChildren);
        Assert.Contains(inventory, root.RenderPaintChildren);
        Assert.Contains(score, status.RenderPaintChildren);
        Assert.Contains(toggle, status.RenderPaintChildren);
        Assert.Contains(icon, status.RenderPaintChildren);
        Assert.Contains(list, inventory.RenderPaintChildren);
        Assert.Contains(caption, toggle.RenderPaintChildren);
        Assert.Contains(list.ScrollViewer, observed);
        Assert.Contains(toggle.BorderComponent.Element, toggle.RenderPaintChildren);
        AssertPaintPaths(observed, () => score.SetText("score 11", true), score);
        AssertPaintPaths(observed, () => icon.TextureColor = Color.Red, icon);
        AssertPaintPaths(observed, () => toggle.PressedContentOffset = new(1, 2), toggle);
        AssertPaintPaths(observed, () => checkedButton.IsChecked = true, Reachable(checkedButton));
        MGPaddedFillBrush borderSource = new(new MGSolidFillBrush(Color.Blue), new Thickness(0));
        toggle.BorderComponent.Element.BorderBrush = new MGUI.Core.UI.Brushes.Border_Brushes.MGUniformBorderBrush(borderSource);
        AssertPaintPaths(observed, () => borderSource.Padding = new(1), toggle.BorderComponent.BaseElement);
        SetField(list.ScrollViewer, "_MaxVerticalOffset", 100f);
        AssertPaintPaths(observed, () => list.ScrollViewer.VerticalOffset = 17, list.ScrollViewer);
        AssertPaintPaths(observed, custom.InvalidateVisual, custom);
        MGPaddedFillBrush sharedSource = new(new MGSolidFillBrush(Color.Yellow), new Thickness(0));
        score.OverlayBrush = sharedSource;
        icon.OverlayBrush = sharedSource;
        AssertPaintPaths(observed, () => sharedSource.Padding = new(2), score, icon);
        AssertPaintPaths(observed, () => status.IsEnabled = false, Reachable(status));
        MGRenderRevisionSnapshot stableStatus = status.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot listBefore = list.GetRenderRevisionSnapshot();
        items.Add("added");
        Assert.False(list.IsRenderRevisionCurrent(listBefore));
        Assert.True(status.IsRenderRevisionCurrent(stableStatus));
        MGRenderRevisionSnapshot noOp = root.GetRenderRevisionSnapshot();
        score.SetText("score 11", true);
        icon.TextureColor = Color.Red;
        toggle.PressedContentOffset = new(1, 2);
        list.ScrollViewer.VerticalOffset = 17;
        toggle.CommandName = "nonvisual";
        custom.Name = "metadata";
        Assert.True(root.IsRenderRevisionCurrent(noOp));
    }

    [Fact]
    public void InheritedContextAndRawSourceRefreshRespectInactiveAndSeparateSiblingOwnership()
    {
        MGWindow window = CreateWindow();
        MGStackPanel root = new(window, Orientation.Horizontal);
        MGStackPanel branch = new(window, Orientation.Vertical);
        MGTextBlock active = new(window, "active");
        MGTextBlock inactive = new(window, "inactive");
        MGContextualContentPresenter context = new(window, true, active, inactive);
        MGTextBlock sibling = new(window, "sibling");
        branch.TryAddChild(context);
        root.TryAddChild(branch);
        root.TryAddChild(sibling);
        window.SetContent(root);
        _ = root.VisualRevision;
        MGRenderRevisionSnapshot inactiveBefore = inactive.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot siblingBefore = sibling.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot activeBefore = active.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot branchBefore = branch.GetRenderRevisionSnapshot();
        branch.DefaultTextForeground.NormalValue = Color.Red;
        Assert.False(active.IsRenderRevisionCurrent(activeBefore));
        Assert.False(branch.IsRenderRevisionCurrent(branchBefore));
        Assert.True(inactive.IsRenderRevisionCurrent(inactiveBefore));
        Assert.True(sibling.IsRenderRevisionCurrent(siblingBefore));
        MGRenderRevisionSnapshot rootBefore = root.GetRenderRevisionSnapshot();
        inactive.SetText("changed inactive", true);
        Assert.True(root.IsRenderRevisionCurrent(rootBefore));
        context.Value = false;
        Assert.False(root.IsRenderRevisionCurrent(rootBefore));
        Assert.Contains(inactive, context.RenderPaintChildren);
        Assert.DoesNotContain(active, context.RenderPaintChildren);
        MGPaddedFillBrush oldSource = new(new MGSolidFillBrush(Color.Blue), new Thickness(0));
        MGPaddedFillBrush newSource = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        MGCompositedFillBrush composite = new(oldSource);
        inactive.OverlayBrush = composite;
        _ = root.VisualRevision;
        rootBefore = root.GetRenderRevisionSnapshot();
        composite.Brushes[0] = newSource;
        Assert.True(root.IsRenderRevisionCurrent(rootBefore));
        context.InvalidateVisual();
        Assert.False(root.IsRenderRevisionCurrent(rootBefore));
        Assert.Equal(rootBefore.Layout, root.LayoutRevision);
        AssertPaintPaths(Reachable(root), () => newSource.Padding = new(2), inactive);
        rootBefore = root.GetRenderRevisionSnapshot();
        oldSource.Padding = new(3);
        Assert.True(root.IsRenderRevisionCurrent(rootBefore));
    }

    [Fact]
    public void RepeatedAttachDetachAndReparentBurstsDoNotLeaveOldPaintOrSourcePaths()
    {
        MGWindow window = CreateWindow();
        MGStackPanel root = new(window, Orientation.Horizontal);
        MGStackPanel left = new(window, Orientation.Vertical);
        MGStackPanel right = new(window, Orientation.Vertical);
        MGRectangle child = new(window, 12, 12, Color.Black, 1, Color.Blue);
        MGPaddedFillBrush brush = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        child.Fill = brush;
        root.TryAddChild(left);
        root.TryAddChild(right);
        left.TryAddChild(child);
        _ = root.VisualRevision;
        MGStackPanel owner = left;
        for (int iteration = 0; iteration < 100; ++iteration)
        {
            MGStackPanel next = ReferenceEquals(owner, left) ? right : left;
            MGRenderRevisionSnapshot rootBefore = root.GetRenderRevisionSnapshot();
            MGRenderRevisionSnapshot oldBefore = owner.GetRenderRevisionSnapshot();
            Assert.True(owner.TryRemoveChild(child));
            Assert.False(owner.IsRenderRevisionCurrent(oldBefore));
            MGRenderRevisionSnapshot detachedRoot = root.GetRenderRevisionSnapshot();
            MGRenderRevisionSnapshot detachedOwner = owner.GetRenderRevisionSnapshot();
            brush.Padding = new(iteration * 2 + 1);
            Assert.True(root.IsRenderRevisionCurrent(detachedRoot));
            Assert.True(owner.IsRenderRevisionCurrent(detachedOwner));
            Assert.True(next.TryAddChild(child));
            Assert.False(root.IsRenderRevisionCurrent(rootBefore));
            AssertPaintPaths(Reachable(root), () => brush.Padding = new(iteration * 2 + 2), child);
            Assert.DoesNotContain(child, owner.RenderPaintChildren);
            Assert.Single(next.RenderPaintChildren.Where(element => ReferenceEquals(element, child)));
            Assert.True(owner.IsRenderRevisionCurrent(detachedOwner));
            owner = next;
        }
    }

    private static void AssertPaintPaths(MGElement[] observed, Action mutation, params MGElement[] changedPainters)
    {
        Assert.All(changedPainters, painter => Assert.Contains(painter, observed));
        Dictionary<MGElement, MGRenderRevisionSnapshot> before = observed.ToDictionary(e => e, e => e.GetRenderRevisionSnapshot());
        HashSet<MGElement> affected = new(changedPainters);
        bool added;
        do
        {
            added = false;
            foreach (MGElement element in observed)
            {
                if (element.RenderPaintChildren.Any(affected.Contains))
                {
                    added |= affected.Add(element);
                }
            }
        } while (added);
        mutation();
        foreach (MGElement element in observed)
        {
            MGRenderRevisionSnapshot prior = before[element];
            Assert.Equal(prior.Layout, element.LayoutRevision);
            Assert.Equal(prior.Structure, element.StructureRevision);
            Assert.Equal(prior.Visual + (affected.Contains(element) ? 1UL : 0UL), element.VisualRevision);
        }
    }

    private static MGElement[] Reachable(MGElement root)
    {
        HashSet<MGElement> seen = new();
        void Visit(MGElement element)
        {
            if (seen.Add(element))
            {
                foreach (MGElement child in element.RenderPaintChildren)
                {
                    Visit(child);
                }
            }
        }
        Visit(root);
        return seen.ToArray();
    }

    private sealed class AuditProbe : MGElement
    {
        internal readonly List<MGElement> PaintChildren = new();
        internal readonly CountingSource Source = new();
        internal int ChildDiscoveries;
        internal int SourceDiscoveries;
        internal AuditProbe(MGWindow window) : base(window, MGElementType.Rectangle) { }
        public override IEnumerable<MGElement> GetChildren()
        {
            ++ChildDiscoveries;
            return PaintChildren;
        }
        protected override IEnumerable<object> EnumerateControlRenderSources()
        {
            ++SourceDiscoveries;
            yield return Source;
        }
        internal void ResetCounters()
        {
            ChildDiscoveries = SourceDiscoveries = 0;
            Source.Reads = Source.SubscriptionChanges = 0;
        }
    }

    private sealed class CountingSource : IFillBrush, IMGRenderCacheSource
    {
        internal int Reads;
        internal int SubscriptionChanges;
        internal int ActiveSubscriptions;
        private ulong Revision = 1;
        public ulong VisualRevision { get { ++Reads; return Revision; } }
        public bool IsRenderVolatile { get { ++Reads; return false; } }
        public bool SupportsRenderCaching { get { ++Reads; return true; } }
        private event EventHandler? Invalidated;
        public event EventHandler VisualInvalidated
        {
            add { Invalidated += value; ++SubscriptionChanges; ++ActiveSubscriptions; }
            remove { Invalidated -= value; ++SubscriptionChanges; --ActiveSubscriptions; }
        }
        internal void Change()
        {
            ++Revision;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => new CountingSource();
    }

    private static MGWindow CreateWindow()
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
        SetField(renderer, "<Host>k__BackingField", new FixedRenderHost());
        SetField(renderer, "<Input>k__BackingField", new InputTracker());
        SetField(renderer, "<FontManager>k__BackingField", new MGUI.Shared.Text.FontManager());
        byte[] fontBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoSans-Regular.ttf"));
        global::FontStashSharp.FontSystem fontSystem = new();
        fontSystem.AddFont(fontBytes);
        MGUI.FontStashSharp.FontStashSharpTextEngine engine = new();
        engine.AddFontSystem("TestFont", MGUI.Shared.Text.CustomFontStyles.Normal, fontSystem, fontBytes);
        SetField(renderer, "_textEngine", engine);
        SetField(desktop, "<Renderer>k__BackingField", renderer);
        SetField(desktop, "_UIScale", new MGScaleSettings());
        SetField(desktop, "<Resources>k__BackingField", new MGResources(new MGTheme("TestFont")));
        SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
        MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        SetField(window, "<Desktop>k__BackingField", desktop);
        SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
        SetField(window, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
        SetField(window, "<Components>k__BackingField", new List<MGComponentBase>());
        SetField(window, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
        SetField(window, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        SetField(window, "_NestedWindows", new List<MGWindow>());
        SetField(window, "<RadioButtonGroups>k__BackingField", new Dictionary<string, MGRadioButtonGroup>());
        SetField(window, "_CanChangeContent", true);
        SetField(window, "_Scale", 1f);
        SetField(window, "_UnscaledScreenSpaceToScaledScreenSpace", Matrix.Identity);
        SetField(window, "_ScaledScreenSpaceToUnscaledScreenSpace", Matrix.Identity);
        return window;
    }

    private static void SetField(object instance, string name, object? value)
    {
        for (Type? type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }
        }
        throw new MissingFieldException(instance.GetType().FullName, name);
    }

    private sealed class FixedRenderHost : IRenderHost
    {
        public GraphicsDevice GraphicsDevice => null!;
        public event EventHandler<TimeSpan> PreviewUpdate { add { } remove { } }
        public event EventHandler<EventArgs> EndUpdate { add { } remove { } }
        public Rectangle GetBounds() => new(0, 0, 800, 600);
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;
        public object GetService(Type serviceType) => null!;
    }
}
