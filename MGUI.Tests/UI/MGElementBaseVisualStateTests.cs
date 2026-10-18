using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGElementBaseVisualStateTests
{
    public static IEnumerable<object[]> VisualSetters()
    {
        yield return new object[] { "enabled", (Action<Probe>)(e => e.IsEnabled = false) };
        yield return new object[] { "selected", (Action<Probe>)(e => e.IsSelected = true) };
        yield return new object[] { "opacity", (Action<Probe>)(e => e.Opacity = 0.5f) };
        yield return new object[] { "hidden", (Action<Probe>)(e => e.Visibility = Visibility.Hidden) };
        yield return new object[] { "clip", (Action<Probe>)(e => e.ClipToBounds = false) };
        yield return new object[] { "padding", (Action<Probe>)(e => e.BackgroundRenderPadding = new Thickness(-2)) };
        yield return new object[] { "scale", (Action<Probe>)(e => e.RenderScale = new ConditionalScaleTransform(0.9f, 1.1f)) };
        yield return new object[] { "spoof press", (Action<Probe>)(e => e.SpoofPress(true)) };
        yield return new object[] { "spoof hover", (Action<Probe>)(e => e.SpoofHover(true)) };
    }

    [Theory]
    [MemberData(nameof(VisualSetters))]
    public void SemanticSettersAdvanceOnlyPaintAndEqualityIsANoop(string name, Action<Probe> change)
    {
        Probe element = new(CreateWindow());
        SetField(element, "_IsLayoutValid", true);
        MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();

        change(element);

        Assert.Equal(before.Visual + 1, element.VisualRevision);
        Assert.Equal(before.Layout, element.LayoutRevision);
        Assert.Equal(before.Structure, element.StructureRevision);
        Assert.True(element.IsLayoutValid, name);
        MGRenderRevisionSnapshot changed = element.GetRenderRevisionSnapshot();
        change(element);
        Assert.True(element.IsRenderRevisionCurrent(changed), name);
    }

    [Fact]
    public void BrushAndForegroundReplacementAndStateSlotsInvalidateTheirOwners()
    {
        Probe element = new(CreateWindow());
        Probe child = new(element.ParentWindow);
        element.Add(child);
        VisualStateFillBrush background = new(new MGSolidFillBrush(Color.Red));
        element.BackgroundBrush = background;
        ulong before = element.VisualRevision;
        background.SelectedValue = new MGSolidFillBrush(Color.Blue);
        Assert.Equal(before + 1, element.VisualRevision);
        before = element.VisualRevision;
        background.FocusedColor = Color.Yellow;
        Assert.Equal(before + 1, element.VisualRevision);
        before = element.VisualRevision;
        background.PressedModifier = 0.2f;
        Assert.Equal(before + 1, element.VisualRevision);

        VisualStateSetting<Color?> foreground = new(Color.White);
        element.DefaultTextForeground = foreground;
        before = element.VisualRevision;
        ulong childBefore = child.VisualRevision;
        foreground.NormalValue = Color.Green;
        Assert.Equal(before + 1, element.VisualRevision);
        Assert.Equal(childBefore + 1, child.VisualRevision);
        Assert.Equal(Color.Green, child.DerivedDefaultTextForeground);

        element.BackgroundBrush = new(new MGSolidFillBrush(Color.Pink));
        MGRenderRevisionSnapshot detached = element.GetRenderRevisionSnapshot();
        background.NormalValue = new MGSolidFillBrush(Color.Black);
        Assert.True(element.IsRenderRevisionCurrent(detached));
        IFillBrush overlay = new MGSolidFillBrush(Color.Orange);
        element.OverlayBrush = overlay;
        detached = element.GetRenderRevisionSnapshot();
        element.OverlayBrush = overlay;
        Assert.True(element.IsRenderRevisionCurrent(detached));
    }

    [Fact]
    public void SharedSourceGenerationInvalidatesItsOwningAncestorOnlyOnce()
    {
        Probe parent = new(CreateWindow());
        Probe child = new(parent.ParentWindow);
        parent.Add(child);
        VisualStateFillBrush shared = new(new MGSolidFillBrush(Color.Red));
        parent.BackgroundBrush = shared;
        child.BackgroundBrush = shared;
        MGRenderRevisionSnapshot parentBefore = parent.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot childBefore = child.GetRenderRevisionSnapshot();

        shared.NormalValue = new MGSolidFillBrush(Color.Blue);

        Assert.Equal(parentBefore.Visual + 1, parent.VisualRevision);
        Assert.Equal(childBefore.Visual + 1, child.VisualRevision);
    }

    [Fact]
    public void BorderBrushReplacementAndNestedSourceMutationReachTheComponentOwner()
    {
        MGWindow window = CreateWindow();
        Probe owner = new(window);
        MGPaddedFillBrush mutable = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        MGCompositedFillBrush fills = new(mutable);
        MGBorder border = new(window, new Thickness(1), fills);
        owner.AddComponentForTest(border);
        MGRenderRevisionSnapshot before = owner.GetRenderRevisionSnapshot();
        border.BorderBrush = new MGUI.Core.UI.Brushes.Border_Brushes.MGUniformBorderBrush(Color.Blue);
        Assert.Equal(before.Visual + 1, owner.VisualRevision);
        before = owner.GetRenderRevisionSnapshot();
        border.BorderBrush = new MGUI.Core.UI.Brushes.Border_Brushes.MGUniformBorderBrush(fills);
        before = owner.GetRenderRevisionSnapshot();
        mutable.Padding = new Thickness(2);
        Assert.Equal(before.Visual + 1, owner.VisualRevision);
        before = owner.GetRenderRevisionSnapshot();
        fills.Brushes.Add(new MGSolidFillBrush(Color.Yellow));
        border.InvalidateVisual();
        Assert.Equal(before.Visual + 1, owner.VisualRevision);
        MGRenderRevisionSnapshot borderBefore = border.GetRenderRevisionSnapshot();
        border.BorderBrush = border.BorderBrush;
        Assert.True(border.IsRenderRevisionCurrent(borderBefore));
    }

    [Theory]
    [InlineData(false, false, PrimaryVisualState.Disabled)]
    [InlineData(true, true, PrimaryVisualState.Selected)]
    [InlineData(true, false, PrimaryVisualState.Normal)]
    public void UpdateComputesInheritedPrimaryStateAndThenRemainsIdle(bool enabled, bool selected, PrimaryVisualState expected)
    {
        Probe parent = new(CreateWindow());
        Probe child = new(parent.ParentWindow);
        parent.Add(child);
        ElementUpdateArgs args = new(default, enabled, selected, false, Point.Zero, Rectangle.Empty);
        parent.Update(args);
        Assert.Equal(expected, parent.VisualState.Primary);
        Assert.Equal(expected, child.VisualState.Primary);
        MGRenderRevisionSnapshot parentBefore = parent.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot childBefore = child.GetRenderRevisionSnapshot();

        parent.Update(args);

        Assert.True(parent.IsRenderRevisionCurrent(parentBefore));
        Assert.True(child.IsRenderRevisionCurrent(childBefore));
    }

    [Fact]
    public void PointerUpdatesComputeHoverPressAndReleaseWithoutIdleChurn()
    {
        MGWindow window = CreateWindow();
        Probe element = new(window);
        SetProperty(element, nameof(MGElement.LayoutBounds), new Rectangle(0, 0, 30, 30));
        SetField(window, "_HoveredElement", element);
        SetField(window, "_PressedElement", element);
        ElementUpdateArgs args = new(default, true, false, true, Point.Zero, new Rectangle(0, 0, 100, 100));
        InputTracker input = window.Desktop.InputTracker;
        input.Update(new(TimeSpan.Zero, TimeSpan.Zero, new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), default));
        element.Update(args);
        Assert.Equal(SecondaryVisualState.Hovered, element.VisualState.Secondary);
        MGRenderRevisionSnapshot hovered = element.GetRenderRevisionSnapshot();
        element.Update(args);
        Assert.True(element.IsRenderRevisionCurrent(hovered));
        input.Update(new(TimeSpan.Zero, TimeSpan.Zero, new MouseState(10, 10, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), default));
        element.Update(args);
        Assert.Equal(SecondaryVisualState.Pressed, element.VisualState.Secondary);
        Assert.Equal(hovered.Visual + 1, element.VisualRevision);
        input.Update(new(TimeSpan.Zero, TimeSpan.Zero, new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), default));
        element.Update(args);
        Assert.Equal(SecondaryVisualState.None, element.VisualState.Secondary);
    }

    [Fact]
    public void InheritedContextUsesOneStampForAliasesAndDoesNotDiscoverOnIdle()
    {
        MGWindow window = CreateWindow();
        Probe parent = new(window);
        Probe child = new(window);
        Probe grandchild = new(window);
        Probe unrelated = new(window);
        parent.Add(child);
        child.Add(grandchild);
        MGRenderRevisionSnapshot ownerBefore = parent.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot childBefore = child.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot grandchildBefore = grandchild.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot unrelatedBefore = unrelated.GetRenderRevisionSnapshot();
        parent.Discoveries = child.Discoveries = grandchild.Discoveries = 0;

        parent.Opacity = 0.5f;

        Assert.Equal(ownerBefore.Visual + 1, parent.VisualRevision);
        Assert.Equal(childBefore.Visual + 1, child.VisualRevision);
        Assert.Equal(grandchildBefore.Visual + 1, grandchild.VisualRevision);
        Assert.True(unrelated.IsRenderRevisionCurrent(unrelatedBefore));
        Assert.Equal(0, parent.Discoveries + child.Discoveries + grandchild.Discoveries);
        for (int iteration = 0; iteration < 10; ++iteration)
        {
            parent.Opacity = 0.5f;
            parent.NotifyBookkeeping();
            parent.Draw(new(new DrawBaseArgs(default, null!, 1), default, Point.Zero));
        }
        Assert.Equal(ownerBefore.Visual + 1, parent.VisualRevision);
        Assert.Equal(0, parent.Discoveries + child.Discoveries + grandchild.Discoveries);
    }

    [Fact]
    public void WindowPositionScaleAndChildMovementRejectOldObservations()
    {
        MGWindow window = CreateWindow();
        Probe child = new(window);
        window.SetContent(child);
        MGRenderRevisionSnapshot before = child.GetRenderRevisionSnapshot();
        window.Left = 10;
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        before = child.GetRenderRevisionSnapshot();
        window.Left = 10;
        Assert.True(child.IsRenderRevisionCurrent(before));
        window.Top = 20;
        Assert.False(child.IsRenderRevisionCurrent(before));
        before = child.GetRenderRevisionSnapshot();
        window.Scale = 1.5f;
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        before = child.GetRenderRevisionSnapshot();
        window.Scale = 1.5f;
        Assert.True(child.IsRenderRevisionCurrent(before));

        MGRenderRevisionSnapshot ownerBefore = window.GetRenderRevisionSnapshot();
        child.Move(new Point(3, 0));
        Assert.False(window.IsRenderRevisionCurrent(ownerBefore));
    }

    [Fact]
    public void CollapsedVisibilityRetainsLayoutInvalidationAndOriginUpdatesAreObserved()
    {
        Probe element = new(CreateWindow());
        Probe child = new(element.ParentWindow);
        element.Add(child);
        MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot childBefore = child.GetRenderRevisionSnapshot();
        element.Visibility = Visibility.Collapsed;
        Assert.Equal(before.Visual + 1, element.VisualRevision);
        Assert.Equal(before.Layout + 1, element.LayoutRevision);
        Assert.Equal(childBefore.Visual + 1, child.VisualRevision);
        before = element.GetRenderRevisionSnapshot();
        childBefore = child.GetRenderRevisionSnapshot();
        element.Visibility = Visibility.Visible;
        Assert.Equal(before.Visual + 1, element.VisualRevision);
        Assert.Equal(before.Layout + 1, element.LayoutRevision);
        Assert.Equal(childBefore.Visual + 1, child.VisualRevision);
        before = element.GetRenderRevisionSnapshot();
        element.Update(new(default, true, false, false, new Point(5, 0), Rectangle.Empty));
        Assert.False(element.IsRenderRevisionCurrent(before));
        before = element.GetRenderRevisionSnapshot();
        element.Update(new(default, true, false, false, new Point(5, 0), Rectangle.Empty));
        Assert.True(element.IsRenderRevisionCurrent(before));
    }

    [Fact]
    public void DescendantsObserveRenderScaleAndInheritedStateChangesWhileInitializing()
    {
        Probe parent = new(CreateWindow());
        Probe child = new(parent.ParentWindow);
        parent.Add(child);
        MGRenderRevisionSnapshot before = child.GetRenderRevisionSnapshot();
        parent.RenderScale = new ConditionalScaleTransform(0.9f, 1.1f);
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        before = child.GetRenderRevisionSnapshot();
        parent.IsSelected = true;
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        Assert.True(child.DerivedIsSelected);
        before = child.GetRenderRevisionSnapshot();
        parent.IsEnabled = false;
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        Assert.False(child.DerivedIsEnabled);
        before = child.GetRenderRevisionSnapshot();
        using (parent.BeginInitializing())
        {
            parent.UIScaleMode = UIScaleMode.Disabled;
            Assert.Equal(before.Visual + 1, child.VisualRevision);
        }
    }

    [Fact]
    public void CustomWindowThemeSourceInvalidatesDescendantsAndDetachesReplacedTheme()
    {
        MGWindow window = CreateWindow();
        Probe child = new(window);
        window.SetContent(child);
        MGTheme theme = new("TestFont");
        window.Theme = theme;
        MGRenderRevisionSnapshot before = child.GetRenderRevisionSnapshot();
        theme.FontSettings.DefaultFontSize += 1;
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        window.Theme = new MGTheme("OtherFont");
        before = child.GetRenderRevisionSnapshot();
        theme.FontSettings.DefaultFontSize += 1;
        Assert.True(child.IsRenderRevisionCurrent(before));
    }

    [Fact]
    public void TextEngineReplacementInvalidatesInheritedPaintAndExistingLayoutPaths()
    {
        Probe parent = new(CreateWindow());
        Probe child = new(parent.ParentWindow);
        parent.Add(child);
        MGUI.Shared.Text.Engines.ITextEngine engine = new MGUI.Shared.Text.Engines.SpriteFontTextEngine(new MGUI.Shared.Text.FontManager());
        MGRenderRevisionSnapshot before = child.GetRenderRevisionSnapshot();
        parent.TextEngineOverride = engine;
        Assert.False(child.IsRenderRevisionCurrent(before));
        Assert.True(child.LayoutRevision > before.Layout);
        Assert.Same(engine, child.GetTextEngine());
        before = child.GetRenderRevisionSnapshot();
        parent.TextEngineOverride = engine;
        Assert.True(child.IsRenderRevisionCurrent(before));
    }

    [Fact]
    public void DesktopResourceAndFontNotificationsReachWindowsWithoutVisualOnlyLayoutWork()
    {
        MGWindow window = CreateWindow();
        MGDesktop desktop = window.Desktop;
        desktop.Windows.Add(window);
        Probe child = new(window);
        window.SetContent(child);
        typeof(MGDesktop).GetMethod("InitializeRenderResourceNotifications", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(desktop, null);
        MGRenderRevisionSnapshot before = child.GetRenderRevisionSnapshot();
        desktop.Resources.AddTexture("placeholder", default);
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        Assert.Equal(before.Layout, child.LayoutRevision);
        before = child.GetRenderRevisionSnapshot();
        desktop.FontManager.DefaultFontFamily = "ChangedFont";
        Assert.Equal(before.Visual + 1, child.VisualRevision);
        before = child.GetRenderRevisionSnapshot();
        desktop.TextEngine = desktop.TextEngine;
        Assert.True(child.IsRenderRevisionCurrent(before));
        desktop.UIScale = new MGScaleSettings();
        before = child.GetRenderRevisionSnapshot();
        desktop.UIScale.SizeScale = 1.5f;
        Assert.False(child.IsRenderRevisionCurrent(before));
        Assert.True(child.LayoutRevision > before.Layout);
    }

    [Theory]
    [InlineData("foreground")]
    [InlineData("scale")]
    [InlineData("opacity")]
    public void ReactivatedPaintSubtreeRejectsObservationFromBeforeInheritedContextChanged(string context)
    {
        MGWindow window = CreateWindow();
        Probe previouslyActive = new(window);
        Probe descendant = new(window);
        previouslyActive.Add(descendant);
        MGContextualContentPresenter host = new(window, true, previouslyActive, new Probe(window));
        host.Value = false;
        MGRenderRevisionSnapshot previous = previouslyActive.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot descendantPrevious = descendant.GetRenderRevisionSnapshot();
        switch (context)
        {
            case "foreground":
                host.DefaultTextForeground = new VisualStateSetting<Color?>(Color.Red);
                break;
            case "scale":
                host.RenderScale = new ConditionalScaleTransform(0.8f, 1.2f);
                break;
            case "opacity":
                host.Opacity = 0.5f;
                break;
        }
        Assert.True(previouslyActive.IsRenderRevisionCurrent(previous));
        Assert.True(descendant.IsRenderRevisionCurrent(descendantPrevious));

        host.Value = true;

        Assert.False(previouslyActive.IsRenderRevisionCurrent(previous));
        Assert.False(descendant.IsRenderRevisionCurrent(descendantPrevious));
        MGRenderRevisionSnapshot active = previouslyActive.GetRenderRevisionSnapshot();
        host.Value = true;
        Assert.True(previouslyActive.IsRenderRevisionCurrent(active));
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("font")]
    [InlineData("scale")]
    public void GlobalContextChangesInvalidateActiveSubmenuAndOrdinarySiblingRoots(string context)
    {
        MGWindow window = CreateWindow();
        MGDesktop desktop = window.Desktop;
        MGWindow sibling = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeWindow(sibling, desktop, MGElementType.Window);
        desktop.Windows.Add(window);
        desktop.Windows.Add(sibling);
        MGContextMenu menu = (MGContextMenu)RuntimeHelpers.GetUninitializedObject(typeof(MGContextMenu));
        MGContextMenu submenu = (MGContextMenu)RuntimeHelpers.GetUninitializedObject(typeof(MGContextMenu));
        InitializeWindow(menu, desktop, MGElementType.ContextMenu);
        InitializeWindow(submenu, desktop, MGElementType.ContextMenu);
        SetField(desktop, "_ActiveContextMenu", menu);
        SetField(menu, "_ActiveContextMenu", submenu);
        Probe ordinaryContent = new(window);
        Probe siblingContent = new(sibling);
        Probe menuContent = new(menu);
        Probe submenuContent = new(submenu);
        window.SetContent(ordinaryContent);
        sibling.SetContent(siblingContent);
        menu.SetContent(menuContent);
        submenu.SetContent(submenuContent);
        typeof(MGDesktop).GetMethod("InitializeRenderResourceNotifications", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(desktop, null);
        desktop.ReconcileRenderRootAttachments();
        Probe[] contents = { ordinaryContent, siblingContent, menuContent, submenuContent };
        MGRenderRevisionSnapshot[] before = contents.Select(child => child.GetRenderRevisionSnapshot()).ToArray();

        switch (context)
        {
            case "resource":
                desktop.Resources.AddTexture("placeholder", default);
                break;
            case "font":
                desktop.FontManager.DefaultFontFamily = "ChangedFont";
                break;
            case "scale":
                desktop.UIScale = new MGScaleSettings { SizeScale = 1.5f };
                break;
        }

        for (int index = 0; index < contents.Length; ++index)
        {
            Assert.False(contents[index].IsRenderRevisionCurrent(before[index]));
        }
        Assert.Equal(before[3].Visual + 1, submenuContent.VisualRevision);
    }

    [Fact]
    public void UnobservedAnimatedBrushHasNoCacheSubscriptionsOrPerTickCacheAllocations()
    {
        MGWindow window = CreateWindow();
        MGUI.Core.UI.Brushes.Border_Brushes.MGHighlightBorderBrush highlight = new(null, Color.White, MGUI.Core.UI.Brushes.Border_Brushes.HighlightAnimation.Pulse);
        MGBorder border = new(window, new Thickness(1), highlight);
        MGUI.Core.UI.Brushes.Border_Brushes.IBorderBrush animated = highlight;
        UpdateBaseArgs args = new(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60), default, default);
        for (int warmup = 0; warmup < 100; ++warmup)
        {
            animated.Update(args);
        }
        Assert.False(border.IsRenderSourceSubtreeTrackingEnabled);
        Assert.Null(typeof(MGUI.Core.UI.Brushes.Border_Brushes.MGHighlightBorderBrush).GetField("_RenderCacheSource", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(highlight));
        Assert.Null(typeof(MGResources).GetField("_RenderCacheSource", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window.Desktop.Resources));
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 1000; ++frame)
        {
            animated.Update(args);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - beforeAllocation);

        MGRenderRevisionSnapshot observed = border.GetRenderRevisionSnapshot();
        animated.Update(args);
        Assert.True(border.IsRenderSourceSubtreeTrackingEnabled);
        Assert.False(border.IsRenderRevisionCurrent(observed));
    }

    [Fact]
    public void ObservingAChildTracksInheritedSourcesWithoutActivatingUnrelatedSiblingGraphs()
    {
        MGWindow window = CreateWindow();
        Probe parent = new(window);
        Probe observed = new(window);
        Probe sibling = new(window);
        CountingSourceBrush siblingSource = new();
        sibling.OverlayBrush = siblingSource;
        parent.Add(observed);
        parent.Add(sibling);
        VisualStateSetting<Color?> foreground = new(Color.Red);
        parent.DefaultTextForeground = foreground;
        MGRenderRevisionSnapshot before = observed.GetRenderRevisionSnapshot();
        Assert.False(parent.IsRenderSourceSubtreeTrackingEnabled);
        Assert.False(sibling.IsRenderSourceSubtreeTrackingEnabled);
        Assert.Equal(0, siblingSource.Reads);

        foreground.NormalValue = Color.Blue;

        Assert.False(observed.IsRenderRevisionCurrent(before));
        Assert.Equal(0, siblingSource.Reads);
    }

    [Fact]
    public void ObservationTracksNewContributingChildrenAndRepeatedReadsDoNotRediscoverSources()
    {
        Probe owner = new(CreateWindow());
        owner.GetRenderRevisionSnapshot();
        CountingSourceBrush source = new();
        Probe child = new(owner.ParentWindow) { OverlayBrush = source };
        Assert.Equal(0, source.Reads);
        owner.Add(child);
        Assert.True(child.IsRenderSourceSubtreeTrackingEnabled);
        Assert.True(source.Reads > 0);
        int reads = source.Reads;
        MGRenderRevisionSnapshot before = owner.GetRenderRevisionSnapshot();
        for (int iteration = 0; iteration < 100; ++iteration)
        {
            Assert.True(owner.IsRenderRevisionCurrent(before));
        }
        Assert.Equal(reads, source.Reads);

        source.Change();

        Assert.Equal(before.Visual + 1, owner.VisualRevision);
    }

    private static MGWindow CreateWindow()
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
        SetField(renderer, "<Host>k__BackingField", new FixedRenderHost());
        SetField(renderer, "<Input>k__BackingField", new InputTracker());
        MGUI.Shared.Text.FontManager fonts = new();
        SetField(renderer, "<FontManager>k__BackingField", fonts);
        SetField(renderer, "_textEngine", new MGUI.Shared.Text.Engines.SpriteFontTextEngine(fonts));
        SetField(desktop, "<Renderer>k__BackingField", renderer);
        SetField(desktop, "_UIScale", new MGScaleSettings());
        SetField(desktop, "<Resources>k__BackingField", new MGResources(new MGTheme("TestFont")));
        SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
        MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeWindow(window, desktop, MGElementType.Window);
        return window;
    }

    private static void InitializeWindow(MGWindow window, MGDesktop desktop, MGElementType elementType)
    {
        SetField(window, "<Desktop>k__BackingField", desktop);
        SetField(window, "<ElementType>k__BackingField", elementType);
        SetField(window, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
        SetField(window, "<Components>k__BackingField", new List<MGComponentBase>());
        SetField(window, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
        SetField(window, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        SetField(window, "_NestedWindows", new List<MGWindow>());
        SetField(window, "_CanChangeContent", true);
        SetField(window, "_Scale", 1.0f);
        SetField(window, "_UnscaledScreenSpaceToScaledScreenSpace", Matrix.Identity);
        SetField(window, "_ScaledScreenSpaceToUnscaledScreenSpace", Matrix.Identity);
    }

    private static void SetProperty(object element, string name, object value)
        => typeof(MGElement).GetProperty(name)!.SetValue(element, value);

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

    public sealed class Probe : MGElement
    {
        private readonly List<MGElement> Children = new();
        public int Discoveries;
        public Probe(MGWindow window) : base(window, MGElementType.Border) { }
        public void Add(Probe child)
        {
            Children.Add(child);
            child.SetParent(this);
            ReconcileRenderPaintChildren();
        }
        public void AddComponentForTest(MGElement child)
            => AddComponent(new MGComponent<MGElement>(child, ComponentUpdatePriority.BeforeContents, ComponentDrawPriority.BeforeSelf, false, false, false, false, false, false, false, (bounds, _) => bounds));
        public override IEnumerable<MGElement> GetChildren()
        {
            ++Discoveries;
            return Children;
        }
        public void SpoofPress(bool value) => SpoofIsPressedWhileDrawingBackground = value;
        public void SpoofHover(bool value) => SpoofIsHoveredWhileDrawingBackground = value;
        public void NotifyBookkeeping() => NPC(nameof(RecentDrawWasClipped));
        public void Move(Point offset) => TranslateAllBounds(offset);
    }

    private sealed class CountingSourceBrush : IFillBrush, IMGRenderCacheSource
    {
        private ulong Revision = 1;
        public int Reads;
        public ulong VisualRevision { get { ++Reads; return Revision; } }
        public bool IsRenderVolatile { get { ++Reads; return false; } }
        public bool SupportsRenderCaching { get { ++Reads; return true; } }
        public event EventHandler? VisualInvalidated;
        public void Change()
        {
            ++Revision;
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => this;
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
