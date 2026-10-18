using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
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

[Collection("Render revision clock")]
public sealed class MGElementRenderOwnershipTests
{
    [Fact]
    public void DescendantChangesSummarizeOnlyCurrentPaintAncestors()
    {
        MGWindow window = CreateWindow();
        TestMulti root = new(window);
        TestMulti branch = new(window);
        TestMulti sibling = new(window);
        ProbeElement leaf = new(window);
        branch.Items.Add(leaf);
        root.Items.Add(branch);
        root.Items.Add(sibling);
        MGRenderRevisionSnapshot rootBefore = root.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot branchBefore = branch.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot siblingBefore = sibling.GetRenderRevisionSnapshot();

        leaf.InvalidateVisual();

        Assert.Equal(rootBefore.Visual + 1, root.VisualRevision);
        Assert.Equal(branchBefore.Visual + 1, branch.VisualRevision);
        Assert.Equal(rootBefore.Layout, root.LayoutRevision);
        Assert.Equal(rootBefore.Structure, root.StructureRevision);
        Assert.True(sibling.IsRenderRevisionCurrent(siblingBefore));
    }

    [Fact]
    public void ComponentAndManagedAliasesDoNotDuplicatePaintPropagation()
    {
        MGWindow window = CreateWindow();
        TestMulti root = new(window);
        TestMulti component = new(window);
        ProbeElement leaf = new(window);
        component.Items.Add(leaf);
        root.AttachComponent(component);
        component.ManagedParent = root;
        leaf.ManagedParent = root;
        MGRenderRevisionSnapshot before = root.GetRenderRevisionSnapshot();
        int notifications = 0;
        root.RenderInvalidated += (_, _, _) => ++notifications;

        leaf.InvalidateVisual();

        Assert.Equal(before.Visual + 1, root.VisualRevision);
        Assert.Equal(1, notifications);
        Assert.Single(root.RenderPaintChildren);
        Assert.Same(component, root.RenderPaintChildren[0]);
    }

    [Fact]
    public void ListViewManagedGridReachesOwnerOnlyThroughItsActualContentPath()
    {
        MGWindow window = CreateWindow();
        MGListView<int> owner = new(window);
        Assert.Same(owner, owner.DataGrid.ManagedParent);
        Assert.DoesNotContain(owner.DataGrid, owner.RenderPaintChildren);
        Assert.Contains(owner.DockPanelElement, owner.RenderPaintChildren);
        MGRenderRevisionSnapshot before = owner.GetRenderRevisionSnapshot();
        int notifications = 0;
        owner.RenderInvalidated += (_, _, _) => ++notifications;

        owner.DataGrid.InvalidateVisual();

        Assert.Equal(before.Visual + 1, owner.VisualRevision);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void SharedRealPaintPathsCoalesceAtTheirCommonAncestor()
    {
        ProbeElement root = CreateUninitializedProbe();
        ProbeElement left = CreateUninitializedProbe();
        ProbeElement right = CreateUninitializedProbe();
        ProbeElement leaf = CreateUninitializedProbe();
        root.Children.AddRange(new[] { left, right });
        left.Children.Add(leaf);
        right.Children.Add(leaf);
        root.InvalidateVisual();
        ulong before = root.VisualRevision;

        leaf.InvalidateVisual();

        Assert.Equal(before + 1, root.VisualRevision);
    }

    [Fact]
    public void ResetRetainsRemovedMembershipAndUnsubscribesNestedContentObservers()
    {
        MGWindow window = CreateWindow();
        TestMulti host = new(window);
        TestMulti child = new(window);
        ProbeElement nested = new(window);
        child.Items.Add(nested);
        host.Items.Add(child);
        int removed = 0;
        int nestedAdded = 0;
        host.OnContentRemoved += (_, _) => ++removed;
        host.OnNestedContentAdded += (_, _) => ++nestedAdded;
        MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();

        host.Items.Clear();
        MGRenderRevisionSnapshot detached = host.GetRenderRevisionSnapshot();
        child.InvalidateVisual();
        child.Items.Add(new ProbeElement(window));

        Assert.Null(child.Parent);
        Assert.Empty(host.RenderPaintChildren);
        Assert.Equal(1, removed);
        Assert.Equal(0, nestedAdded);
        Assert.True(detached.Structure > before.Structure);
        Assert.True(host.IsRenderRevisionCurrent(detached));
        Assert.Equal(0, SubscriberCount(child, "OnContentAdded"));
    }

    [Fact]
    public void ReplaceMoveAndReparentUpdateBothOwnersAndDetachOldPath()
    {
        MGWindow window = CreateWindow();
        TestMulti oldOwner = new(window);
        TestMulti newOwner = new(window);
        ProbeElement first = new(window);
        ProbeElement second = new(window);
        oldOwner.Items.Add(first);
        oldOwner.Items.Add(second);
        MGRenderRevisionSnapshot order = oldOwner.GetRenderRevisionSnapshot();

        oldOwner.Items.Move(0, 1);
        Assert.True(oldOwner.StructureRevision > order.Structure);
        Assert.Same(first, oldOwner.RenderPaintChildren[1]);
        oldOwner.Items[0] = new ProbeElement(window);
        Assert.Null(second.Parent);
        Assert.DoesNotContain(second, oldOwner.RenderPaintChildren);
        MGRenderRevisionSnapshot oldBefore = oldOwner.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot newBefore = newOwner.GetRenderRevisionSnapshot();
        oldOwner.Items.Remove(first);
        newOwner.Items.Add(first);
        Assert.True(oldOwner.StructureRevision > oldBefore.Structure);
        Assert.True(newOwner.StructureRevision > newBefore.Structure);
        MGRenderRevisionSnapshot oldDetached = oldOwner.GetRenderRevisionSnapshot();
        ulong newVisual = newOwner.VisualRevision;

        first.InvalidateVisual();

        Assert.True(oldOwner.IsRenderRevisionCurrent(oldDetached));
        Assert.Equal(newVisual + 1, newOwner.VisualRevision);
        Assert.Same(newOwner, first.Parent);
    }

    [Fact]
    public void RepeatedReparentDoesNotAccumulateLegacyNestedSubscriptions()
    {
        MGWindow window = CreateWindow();
        TestMulti left = new(window);
        TestMulti right = new(window);
        TestMulti child = new(window);
        for (int iteration = 0; iteration < 12; ++iteration)
        {
            left.Items.Add(child);
            Assert.Equal(1, SubscriberCount(child, "OnContentAdded"));
            left.Items.Clear();
            right.Items.Add(child);
            Assert.Equal(1, SubscriberCount(child, "OnContentAdded"));
            right.Items.Clear();
            Assert.Equal(0, SubscriberCount(child, "OnContentAdded"));
        }
    }

    [Fact]
    public void SingleAndSuppressedContentReplacementDetachFormerPaintOwner()
    {
        MGWindow window = CreateWindow();
        foreach (bool suppressed in new[] { false, true })
        {
            MGContentPresenter host = new(window, suppressed);
            ProbeElement oldContent = new(window);
            ProbeElement newContent = new(window);
            host.SetContent(oldContent);
            MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();
            host.SetContent(newContent);
            MGRenderRevisionSnapshot replaced = host.GetRenderRevisionSnapshot();
            oldContent.InvalidateVisual();
            Assert.True(host.IsRenderRevisionCurrent(replaced));
            Assert.True(replaced.Structure > before.Structure);
            newContent.InvalidateVisual();
            Assert.False(host.IsRenderRevisionCurrent(replaced));
        }
    }

    [Fact]
    public void ContextualActivationTracksOnlyActiveContentIncludingLayoutChanges()
    {
        MGWindow window = CreateWindow();
        ProbeElement active = new(window);
        ProbeElement inactive = new(window);
        MGContextualContentPresenter host = new(window, true, active, inactive);
        MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();

        inactive.InvalidateVisual();
        inactive.PreferredWidth = 40;
        Assert.True(host.IsRenderRevisionCurrent(before));
        host.Value = false;
        Assert.True(host.StructureRevision > before.Structure);
        MGRenderRevisionSnapshot switched = host.GetRenderRevisionSnapshot();
        active.InvalidateVisual();
        active.PreferredHeight = 20;
        Assert.True(host.IsRenderRevisionCurrent(switched));
        inactive.InvalidateVisual();
        Assert.False(host.IsRenderRevisionCurrent(switched));
    }

    [Fact]
    public void TabSwitchTracksNewContentAndDetachesInactiveTab()
    {
        MGWindow window = CreateWindow();
        MGTabControl host = new(window);
        ProbeElement first = new(window);
        ProbeElement second = new(window);
        MGTabItem firstTab = host.AddTab(new ProbeElement(window), first);
        MGTabItem secondTab = host.AddTab(new ProbeElement(window), second);
        MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();
        second.InvalidateVisual();
        second.PreferredWidth = 20;
        Assert.True(host.IsRenderRevisionCurrent(before));

        Assert.True(host.TrySelectTab(secondTab));
        Assert.DoesNotContain(firstTab, host.RenderPaintChildren);
        Assert.Contains(secondTab, host.RenderPaintChildren);
        MGRenderRevisionSnapshot switched = host.GetRenderRevisionSnapshot();
        first.InvalidateVisual();
        Assert.True(host.IsRenderRevisionCurrent(switched));
        second.InvalidateVisual();
        Assert.False(host.IsRenderRevisionCurrent(switched));
    }

    [Fact]
    public void GroupBoxHeaderIsAContributingPaintChild()
    {
        MGWindow window = CreateWindow();
        MGGroupBox host = new(window, new ProbeElement(window));
        Assert.Contains(host.OuterHeaderPresenter, host.RenderPaintChildren);
        ulong before = host.VisualRevision;

        host.HeaderPresenter.Content.InvalidateVisual();

        Assert.Equal(before + 1, host.VisualRevision);
    }

    [Fact]
    public void ExplicitlyPaintedWindowRemainsVisibleToEligibilityClassification()
    {
        ProbeElement host = CreateUninitializedProbe();
        MGWindow childWindow = CreateWindow();
        host.Children.Add(childWindow);
        host.InvalidateVisual();
        Assert.Contains(childWindow, host.RenderPaintChildren);
    }

    [Fact]
    public void PopupLogicalParentDoesNotCreateFlattenedPaintEdge()
    {
        MGWindow window = CreateWindow();
        TestMulti host = new(window);
        MGToolTip popup = (MGToolTip)RuntimeHelpers.GetUninitializedObject(typeof(MGToolTip));
        InitializeElement(popup, window);
        SetField(popup, "_NestedWindows", new List<MGWindow>());
        SetField(popup, "<ElementType>k__BackingField", MGElementType.ToolTip);
        popup.SetParent(host);
        MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();

        popup.InvalidateVisual();

        Assert.True(host.IsRenderRevisionCurrent(before));
        Assert.Empty(host.RenderPaintChildren);
    }

    [Fact]
    public void AdditionalComponentPaintOperationInvalidatesEvenWhenTheDistinctChildrenAreUnchanged()
    {
        MGWindow window = CreateWindow();
        TestMulti host = new(window);
        ProbeElement child = new(window);
        host.AttachComponent(child, ComponentDrawPriority.BeforeBackground);
        MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();
        IReadOnlyList<MGElement> membership = host.RenderPaintChildren;

        host.AttachComponent(child, ComponentDrawPriority.AfterContents);

        Assert.Same(membership, host.RenderPaintChildren);
        Assert.Equal(before.Structure + 1, host.StructureRevision);
        ulong visual = host.VisualRevision;
        child.InvalidateVisual();
        Assert.Equal(visual + 1, host.VisualRevision);
    }

    [Fact]
    public void ExplicitInvalidationReconcilesRawComponentRemovalAndDrawOrder()
    {
        MGWindow window = CreateWindow();
        TestMulti host = new(window);
        ProbeElement before = new(window);
        ProbeElement after = new(window);
        host.AttachComponent(before, ComponentDrawPriority.BeforeBackground);
        host.AttachComponent(after, ComponentDrawPriority.AfterContents);
        MGRenderRevisionSnapshot original = host.GetRenderRevisionSnapshot();
        host.RawComponents.RemoveAt(0);
        host.InvalidateVisual();
        MGRenderRevisionSnapshot removed = host.GetRenderRevisionSnapshot();
        before.InvalidateVisual();
        Assert.True(host.IsRenderRevisionCurrent(removed));
        Assert.True(removed.Structure > original.Structure);
        host.AttachComponent(before, ComponentDrawPriority.AfterContents);
        host.RawComponents.Reverse();
        host.InvalidateVisual();
        Assert.Equal(new MGElement[] { before, after }, host.RenderPaintChildren);
    }

    [Fact]
    public void OverlayMetadataIsReconciledInActualPaintOrderAndChangesStructure()
    {
        MGWindow window = CreateWindow();
        MGOverlayPanel host = new(window);
        ProbeElement high = new(window);
        ProbeElement low = new(window);
        Assert.True(host.TryAddChild(high, default, 10));
        Assert.True(host.TryAddChild(low, default, 0));
        Assert.Equal(new MGElement[] { low, high }, host.RenderPaintChildren);
        MGRenderRevisionSnapshot before = host.GetRenderRevisionSnapshot();

        Assert.True(host.TryReplaceChild(low, low, new Thickness(3), 20));

        Assert.True(host.StructureRevision > before.Structure);
        Assert.Equal(new MGElement[] { high, low }, host.RenderPaintChildren);
        Assert.True(host.TryRemoveChild(high));
        MGRenderRevisionSnapshot detached = host.GetRenderRevisionSnapshot();
        high.InvalidateVisual();
        Assert.True(host.IsRenderRevisionCurrent(detached));
    }

    [Fact]
    public void GridAndUniformGridPlacementMovesInvalidateStructureAndDetachRemovedContent()
    {
        MGWindow window = CreateWindow();
        MGGrid grid = new(window);
        grid.AddRow(GridLength.CreatePixelLength(20));
        grid.AddColumn(GridLength.CreatePixelLength(20));
        grid.AddColumn(GridLength.CreatePixelLength(20));
        ProbeElement child = new(window);
        Assert.True(grid.TryAddChild(0, 0, child));
        MGRenderRevisionSnapshot placed = grid.GetRenderRevisionSnapshot();
        Assert.True(grid.TryRemoveChild(child));
        Assert.True(grid.TryAddChild(0, 1, child));
        Assert.True(grid.StructureRevision > placed.Structure);
        Assert.True(grid.TryGetCell(child, out GridCell cell));
        Assert.Same(grid.Columns[1], cell.Column);
        Assert.True(grid.TryRemoveChild(child));
        MGRenderRevisionSnapshot detached = grid.GetRenderRevisionSnapshot();
        child.InvalidateVisual();
        Assert.True(grid.IsRenderRevisionCurrent(detached));

        MGUniformGrid uniform = new(window, 1, 2, new Size(20, 20));
        Assert.True(uniform.TryAddChild(0, 0, child));
        placed = uniform.GetRenderRevisionSnapshot();
        Assert.True(uniform.TryRemoveChild(child));
        Assert.True(uniform.TryAddChild(0, 1, child));
        Assert.True(uniform.StructureRevision > placed.Structure);
        Assert.True(uniform.TryRemoveChild(child));
        detached = uniform.GetRenderRevisionSnapshot();
        child.InvalidateVisual();
        Assert.True(uniform.IsRenderRevisionCurrent(detached));
    }

    [Fact]
    public void DeferredDescendantLayoutInvalidatesPaintAncestorsSynchronouslyOnce()
    {
        MGWindow window = CreateWindow();
        TestMulti owner = new(window);
        TestMulti child = new(window);
        owner.Items.Add(child);
        MGRenderRevisionSnapshot before = owner.GetRenderRevisionSnapshot();
        using (child.BeginInitializing())
        {
            child.PreferredWidth = 50;
            Assert.Equal(before.Visual + 1, owner.VisualRevision);
            Assert.Equal(before.Layout + 1, owner.LayoutRevision);
            Assert.Equal(before.Structure, owner.StructureRevision);
        }
    }

    [Fact]
    public void ComponentInsertionAndOrderHookUseTheRealFourPaintStages()
    {
        MGWindow window = CreateWindow();
        TestMulti owner = new(window);
        ProbeElement content = new(window);
        ProbeElement background = new(window);
        ProbeElement self = new(window);
        ProbeElement beforeContent = new(window);
        ProbeElement afterContent = new(window);
        owner.Items.Add(content);
        owner.AttachComponent(afterContent, ComponentDrawPriority.AfterContents);
        owner.AttachComponent(background, ComponentDrawPriority.BeforeBackground);
        owner.AttachComponent(beforeContent, ComponentDrawPriority.BeforeContents);
        owner.AttachComponent(self, ComponentDrawPriority.BeforeSelf);
        Assert.Equal(new MGElement[] { background, self, beforeContent, content, afterContent }, owner.RenderPaintChildren);
    }

    [Fact]
    public void RootSubtreeRemovalDetachesAllWindowsAndRemovedRegistryEntriesAreCollectible()
    {
        MGWindow root = CreateWindow();
        MGWindow nested = CreateWindow(root.Desktop);
        root.Desktop.Windows.Add(root);
        root.AddNestedWindow(nested);
        MGRenderRootAttachment rootAttachment = root.RenderRootAttachment;
        MGRenderRootAttachment nestedAttachment = nested.RenderRootAttachment;
        root.Desktop.Windows.Clear();
        root.Desktop.ReconcileRenderRootAttachments();
        Assert.False(rootAttachment.IsAttached);
        Assert.False(nestedAttachment.IsAttached);
        MGDesktop desktop = root.Desktop;
        WeakReference removed = AttachAndRemoveWindow(desktop);
        ForceCollection();
        Assert.False(removed.IsAlive);
        GC.KeepAlive(desktop);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndRemoveWindow(MGDesktop desktop)
    {
        MGWindow window = CreateWindow(desktop);
        desktop.Windows.Add(window);
        desktop.ReconcileRenderRootAttachments();
        desktop.Windows.Remove(window);
        desktop.ReconcileRenderRootAttachments();
        return new WeakReference(window);
    }

    [Fact]
    public void MembershipHookProvidesRetainedImmutableOldAndNewSnapshots()
    {
        ProbeElement host = CreateUninitializedProbe();
        ProbeElement first = CreateUninitializedProbe();
        ProbeElement second = CreateUninitializedProbe();
        host.Children.Add(first);
        host.InvalidateVisual();
        IReadOnlyList<MGElement>? oldChildren = null;
        IReadOnlyList<MGElement>? newChildren = null;
        host.RenderPaintChildrenChanged += (_, oldValue, newValue, _) =>
        {
            oldChildren = oldValue;
            newChildren = newValue;
        };
        host.Children.Clear();
        host.Children.Add(second);
        host.InvalidateVisual();
        Assert.Equal(new[] { first }, oldChildren);
        Assert.Equal(new[] { second }, newChildren);
        IReadOnlyList<MGElement> retainedOld = oldChildren!;
        IReadOnlyList<MGElement> retainedNew = newChildren!;
        host.Children.Clear();
        host.InvalidateVisual();
        Assert.Empty(host.RenderPaintChildren);
        Assert.Equal(new[] { first }, retainedOld);
        Assert.Equal(new[] { second }, retainedNew);
    }

    [Fact]
    public void DetachedChildDoesNotKeepFormerPaintOwnersAlive()
    {
        ProbeElement retainedChild = CreateUninitializedProbe();
        WeakReference formerOwner = CreateAndDetachOwner(retainedChild);
        ForceCollection();
        Assert.False(formerOwner.IsAlive);
        retainedChild.InvalidateVisual();
        GC.KeepAlive(retainedChild);
    }

    [Fact]
    public void IdleRevisionReadsAndDrawDoNotDiscoverMaintainedSubtree()
    {
        ProbeElement root = CreateUninitializedProbe();
        ProbeElement child = CreateUninitializedProbe();
        root.Children.Add(child);
        root.InvalidateVisual();
        root.DiscoveryCount = 0;
        child.DiscoveryCount = 0;
        MGRenderRevisionSnapshot before = root.GetRenderRevisionSnapshot();
        for (int iteration = 0; iteration < 20; ++iteration)
        {
            Assert.True(root.IsRenderRevisionCurrent(before));
            root.Draw(new(new DrawBaseArgs(default, null!, 1), default, Point.Zero));
        }
        Assert.Equal(0, root.DiscoveryCount);
        Assert.Equal(0, child.DiscoveryCount);
    }

    [Fact]
    public void RawWindowListRemovalReinsertionAndClearRetireAttachmentIdentities()
    {
        MGWindow first = CreateWindow();
        MGDesktop desktop = first.Desktop;
        MGWindow second = CreateWindow(desktop);
        desktop.Windows.Add(first);
        desktop.Windows.Add(second);
        desktop.ReconcileRenderRootAttachments();
        MGRenderRootAttachment initial = first.RenderRootAttachment;
        MGRenderRootAttachment unrelated = second.RenderRootAttachment;
        int detached = 0;
        desktop.RenderRootDetached += (_, _) => ++detached;
        desktop.Windows.Remove(first);
        Assert.True(initial.IsAttached);

        desktop.ReconcileRenderRootAttachments();

        Assert.False(initial.IsAttached);
        Assert.Null(first.RenderRootAttachment);
        Assert.Same(unrelated, second.RenderRootAttachment);
        desktop.Windows.Add(first);
        desktop.ReconcileRenderRootAttachments();
        Assert.NotSame(initial, first.RenderRootAttachment);
        Assert.True(first.RenderRootAttachment.IsAttached);
        MGRenderRootAttachment reinserted = first.RenderRootAttachment;
        desktop.Windows.Clear();
        desktop.ReconcileRenderRootAttachments();
        Assert.False(reinserted.IsAttached);
        Assert.False(unrelated.IsAttached);
        Assert.Equal(3, detached);
    }

    [Fact]
    public void NestedWindowAttachmentChangesDoNotFlattenIntoParentRevisions()
    {
        MGWindow first = CreateWindow();
        MGDesktop desktop = first.Desktop;
        MGWindow second = CreateWindow(desktop);
        MGWindow nested = CreateWindow(desktop);
        desktop.Windows.Add(first);
        desktop.Windows.Add(second);
        desktop.ReconcileRenderRootAttachments();
        first.AddNestedWindow(nested);
        MGRenderRootAttachment old = nested.RenderRootAttachment;
        MGRenderRevisionSnapshot parentBefore = first.GetRenderRevisionSnapshot();
        nested.InvalidateVisual();
        Assert.True(first.IsRenderRevisionCurrent(parentBefore));
        Assert.True(first.RemoveNestedWindow(nested));
        Assert.False(old.IsAttached);
        second.AddNestedWindow(nested);
        Assert.NotSame(old, nested.RenderRootAttachment);
        MGRenderRootAttachment attachment = nested.RenderRootAttachment;
        second.ModalWindow = nested;
        second.RemoveNestedWindow(nested);
        Assert.Same(attachment, nested.RenderRootAttachment);
        second.ModalWindow = null;
        Assert.False(attachment.IsAttached);
    }

    [Fact]
    public void RootReconciliationDoesNotDiscoverElementsOrAllocateWhenUnchanged()
    {
        MGWindow window = CreateWindow();
        ProbeElement content = CreateUninitializedProbe();
        SetField(window, "_Content", content);
        window.Desktop.Windows.Add(window);
        window.AddNestedWindow(CreateWindow(window.Desktop));
        window.Desktop.ReconcileRenderRootAttachments();
        for (int warmup = 0; warmup < 100; ++warmup)
        {
            window.Desktop.ReconcileRenderRootAttachments();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 100; ++iteration)
        {
            window.Desktop.ReconcileRenderRootAttachments();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, content.DiscoveryCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDetachOwner(ProbeElement child)
    {
        ProbeElement owner = CreateUninitializedProbe();
        owner.Children.Add(child);
        owner.InvalidateVisual();
        owner.Children.Clear();
        owner.InvalidateVisual();
        return new WeakReference(owner);
    }

    private static void ForceCollection()
    {
        for (int iteration = 0; iteration < 3; ++iteration)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static int SubscriberCount(MGContentHost host, string eventName)
        => ((Delegate?)typeof(MGContentHost).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host))?.GetInvocationList().Length ?? 0;

    private static MGWindow CreateWindow(MGDesktop? existingDesktop = null)
    {
        MGDesktop desktop = existingDesktop ?? (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        if (existingDesktop == null)
        {
            MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
            SetField(renderer, "<Host>k__BackingField", new FixedRenderHost());
            SetField(renderer, "<Input>k__BackingField", new InputTracker());
            SetField(desktop, "<Renderer>k__BackingField", renderer);
            SetField(desktop, "_UIScale", new MGScaleSettings());
            SetField(desktop, "<Resources>k__BackingField", new MGResources(new MGTheme("TestFont")));
            SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
        }
        MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeElement(window, null);
        SetField(window, "<Desktop>k__BackingField", desktop);
        SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
        SetField(window, "_NestedWindows", new List<MGWindow>());
        SetField(window, "_UnscaledScreenSpaceToScaledScreenSpace", Matrix.Identity);
        SetField(window, "_ScaledScreenSpaceToUnscaledScreenSpace", Matrix.Identity);
        return window;
    }

    private static ProbeElement CreateUninitializedProbe()
    {
        ProbeElement element = (ProbeElement)RuntimeHelpers.GetUninitializedObject(typeof(ProbeElement));
        InitializeElement(element, null);
        element.Children = new();
        return element;
    }

    private static void InitializeElement(MGElement element, MGWindow? window)
    {
        SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
        SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
        SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
        SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        SetField(element, "<ParentWindow>k__BackingField", window);
        SetField(element, "_CanChangeContent", true, false);
    }

    private static void SetField(object instance, string name, object? value, bool required = true)
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
        if (required)
        {
            throw new MissingFieldException(instance.GetType().FullName, name);
        }
    }

    private sealed class TestMulti : MGMultiContentHost
    {
        public TestMulti(MGWindow window) : base(window, MGElementType.StackPanel) { }
        public ObservableCollection<MGElement> Items => _Children;
        public List<MGComponentBase> RawComponents => Components;
        public void AttachComponent(MGElement child, ComponentDrawPriority priority = ComponentDrawPriority.BeforeContents)
            => AddComponent(new MGComponent<MGElement>(child, ComponentUpdatePriority.BeforeContents, priority, false, false, false, false, false, false, false, (bounds, _) => bounds));
        protected override Thickness UpdateContentMeasurement(Size availableSize) => new(0);
        protected override void UpdateContentLayout(Rectangle bounds) { }
    }

    private sealed class ProbeElement : MGElement
    {
        public List<MGElement> Children = new();
        public int DiscoveryCount;
        public ProbeElement(MGWindow window) : base(window, MGElementType.Border) { }
        public override IEnumerable<MGElement> GetChildren()
        {
            ++DiscoveryCount;
            return Children;
        }
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
