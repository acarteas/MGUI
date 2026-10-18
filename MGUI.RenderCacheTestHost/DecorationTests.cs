using System.Reflection;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace MGUI.RenderCacheTestHost;

internal sealed class DecorationTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;
    internal DecorationTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }
    internal static int RunAllNative()
    {
        using DecorationTests game = new();
        game.Run();
        Check(game.Completed, "Decoration checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; exact native RGBA readback and translated capture; renderer recorded by test-native.py.");
        return 0;
    }
    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 40, 40, 300, 220) { WindowStyle = WindowStyle.None, Padding = new(0) };
        MGWindow sibling = new(desktop, 400, 40, 100, 100) { WindowStyle = WindowStyle.None, Padding = new(0) };
        sibling.SetContent(new MGRectangle(sibling, 100, 100, Color.Transparent, 0, Color.CornflowerBlue));
        desktop.Windows.Add(window);
        desktop.Windows.Add(sibling);
        void Update() => fixture.Update(++Frame);
        Color[] Draw(bool capture = false)
        {
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(new Color(13, 29, 41));
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                if (capture)
                {
                    transaction.ConfigureCacheCapture(Point.Zero);
                }
                desktop.Draw(transaction);
            }
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            return pixels;
        }
        Color[] DrawElement(MGElement element, bool capture = false)
        {
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(new Color(13, 29, 41));
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                if (capture)
                {
                    transaction.ConfigureCacheCapture(Point.Zero);
                }
                element.Draw(new ElementDrawArgs(new(TimeSpan.Zero, transaction, 1), element.VisualState, Point.Zero));
            }
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            return pixels;
        }
        void SetRoot(MGElement root)
        {
            window.SetContent(root);
            for (int settle = 0; settle < 3; settle++)
            {
                Update();
                Draw();
            }
        }
        void TranslatedParity(string name)
        {
            Color[] normal = Draw();
            Rectangle bounds = window.LayoutBounds;
            Color[] expected = new Color[bounds.Width * bounds.Height];
            for (int y = 0; y < bounds.Height; y++)
            {
                Array.Copy(normal, (bounds.Top + y) * 640 + bounds.Left, expected, y * bounds.Width, bounds.Width);
            }
            using RenderTarget2D target = new(GraphicsDevice, bounds.Width, bounds.Height, false, SurfaceFormat.Color, DepthFormat.None);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(new Color(13, 29, 41));
            using (DrawTransaction transaction = new(desktop.Renderer, new(Matrix.CreateTranslation(-bounds.Left, -bounds.Top, 0)), true))
            {
                transaction.ConfigureCacheCapture(bounds.Location);
                window.Draw(new ElementDrawArgs(new(TimeSpan.Zero, transaction, 1), window.VisualState, Point.Zero));
            }
            GraphicsDevice.SetRenderTarget(null);
            Color[] actual = new Color[expected.Length];
            target.GetData(actual);
            Equal(expected, actual, name);
        }
        void ChangedThenStable(MGElement owner, Action change, bool paintOnly = true)
        {
            var before = owner.GetRenderRevisionSnapshot();
            change();
            Check(!owner.IsRenderRevisionCurrent(before), "Unobserved change on " + owner.GetType().Name);
            if (paintOnly)
            {
                Check(owner.LayoutRevision == before.Layout, "Visual mutation forced layout on " + owner.GetType().Name);
            }
            var after = owner.GetRenderRevisionSnapshot();
            change();
            Check(owner.IsRenderRevisionCurrent(after), "Equal mutation churned " + owner.GetType().Name);
        }

        MGRenderRevisionSnapshot before;
        IFillBrush replacementBrush0 = new MGSolidFillBrush(Color.Blue);
        IFillBrush replacementBrush1 = new MGSolidFillBrush(Color.Red * 0.3f);
        IFillBrush replacementBrush2 = new MGSolidFillBrush(Color.Aqua);
        IFillBrush replacementBrush3 = new MGSolidFillBrush(Color.Pink);
        IFillBrush replacementBrush4 = new MGSolidFillBrush(Color.Goldenrod);
        MGGrid grid = new(window) { RowSpacing = 6, ColumnSpacing = 6 };
        var rows = grid.AddRows(new[] { GridLength.CreateWeightedLength(1), GridLength.CreateWeightedLength(1) });
        var columns = grid.AddColumns(new[] { GridLength.CreateWeightedLength(1), GridLength.CreateWeightedLength(1) });
        grid.TryAddChild(0, 0, new MGRectangle(window, 30, 30, Color.Transparent, 0, Color.Coral));
        grid.TryAddChild(1, 1, new MGRectangle(window, 30, 30, Color.Transparent, 0, Color.Lime));
        SetRoot(grid);
        foreach (Action change in new Action[]
        {
            () => grid.CurrentSelection = new(grid, new(rows[0], columns[0]), GridSelectionMode.Cell),
            () => grid.SelectionBackground = replacementBrush0,
            () => grid.SelectionOverlay = replacementBrush1,
            () => grid.GridLineIntersectionHandling = GridLineIntersection.VerticalThenHorizontal,
            () => grid.GridLinesVisibility = GridLinesVisibility.InnerHorizontal,
            () => grid.GridLineMargin = 1,
            () => grid.HorizontalGridLineBrush = replacementBrush2,
            () => grid.VerticalGridLineBrush = replacementBrush3
        })
        {
            ChangedThenStable(grid, change);
        }
        grid.GridLinesVisibility = GridLinesVisibility.All;
        Update();
        Equal(Draw(), Draw(true), "grid builtin ending overlay");
        TranslatedParity("translated grid selection scissor");
        before = grid.GetRenderRevisionSnapshot();
        grid.GridLinesVisibility = GridLinesVisibility.None;
        Check(grid.LayoutRevision == before.Layout + 1 && grid.VisualRevision == before.Visual + 1, "Removing outer gridlines failed single layout invalidation.");
        Results["grid-visual-inputs-and-translated-overlay"] = true;
        ChangedThenStable(grid, () => rows[0].Length = GridLength.CreatePixelLength(55), false);
        ChangedThenStable(grid, () => rows[0].SetSizeConstraints(20, 100), false);
        ChangedThenStable(grid, () => columns[0].SetSizeConstraints(25, 120), false);
        ChangedThenStable(grid, () => grid.RowSpacing = 8, false);
        ChangedThenStable(grid, () => grid.ColumnSpacing = 8, false);
        var detachedLine = new ProbeBrush();
        grid.HorizontalGridLineBrush = detachedLine;
        before = grid.GetRenderRevisionSnapshot();
        detachedLine.Signal();
        Check(!grid.IsRenderRevisionCurrent(before), "Grid source mutation missed.");
        grid.HorizontalGridLineBrush = new MGSolidFillBrush(Color.White);
        before = grid.GetRenderRevisionSnapshot();
        detachedLine.Signal();
        Check(grid.IsRenderRevisionCurrent(before), "Grid old source retained.");
        Results["grid-dimensions-source-detach"] = true;

        VisualStateFillBrush uniformCells = new(new MGSolidFillBrush(Color.Purple));
        MGUniformGrid uniform = new(window, 2, 2, new(70, 60)) { RowSpacing = 6, ColumnSpacing = 6 };
        SetRoot(uniform);
        foreach (Action change in new Action[]
        {
            () => uniform.CurrentSelection = new(uniform, new(0, 0), GridSelectionMode.Cell),
            () => uniform.SelectionBackground = replacementBrush4,
            () => uniform.SelectionOverlay = replacementBrush1,
            () => uniform.GridLineIntersectionHandling = GridLineIntersection.VerticalThenHorizontal,
            () => uniform.GridLinesVisibility = GridLinesVisibility.InnerHorizontal,
            () => uniform.GridLineMargin = 1,
            () => uniform.HorizontalGridLineBrush = replacementBrush2,
            () => uniform.VerticalGridLineBrush = replacementBrush3,
            () => uniform.CellBackground = uniformCells,
            () => uniform.DrawEmptyCells = false
        })
        {
            ChangedThenStable(uniform, change);
        }
        uniform.DrawEmptyCells = true;
        Update();
        TranslatedParity("translated uniform selection and cell fill");
        ChangedThenStable(uniform, () => uniform.CellSize = new(80, 65), false);
        ChangedThenStable(uniform, () => uniform.HeaderRowHeight = 20, false);
        ChangedThenStable(uniform, () => uniform.HeaderColumnWidth = 25, false);
        ChangedThenStable(uniform, () => uniform.Rows = 3, false);
        ChangedThenStable(uniform, () => uniform.Columns = 3, false);
        Results["uniform-grid-visual-layout-and-native-pixels"] = true;
        Update();
        int cellCallbacks = 0;
        EventHandler<MGUniformGrid.RenderCellArgs> cellHandler = (_, _) => cellCallbacks++;
        EventHandler<MGUniformGrid.RenderCellArgs> twice = cellHandler + cellHandler;
        before = uniform.GetRenderRevisionSnapshot();
        uniform.OnRenderCell += twice;
        Check(uniform.ControlPublicDrawCallbackCount == 2 && !uniform.IsRenderRevisionCurrent(before), "Cell callback add count invalid.");
        Draw();
        Check(cellCallbacks > 0, "Normal cell callback stopped.");
        int normalCallbacks = cellCallbacks;
        Draw(true);
        Check(cellCallbacks == normalCallbacks, "Cell callback ran in capture.");
        uniform.OnRenderCell -= cellHandler;
        Check(uniform.ControlPublicDrawCallbackCount == 1, "Cell callback duplicate removal invalid.");
        uniform.OnRenderCell -= cellHandler;
        before = uniform.GetRenderRevisionSnapshot();
        uniform.OnRenderCell -= cellHandler;
        Check(uniform.ControlPublicDrawCallbackCount == 0 && uniform.IsRenderRevisionCurrent(before), "Absent cell callback removal churned.");
        Results["public-cell-callback-maintained-count-and-frequency"] = true;

        VisualStateFillBrush splitterForeground = new(new MGSolidFillBrush(Color.Yellow));
        MGGridSplitter splitter = new(window);
        SetRoot(splitter);
        ChangedThenStable(splitter, () => splitter.TickSize = new(30, 2));
        ChangedThenStable(splitter, () => splitter.Foreground = splitterForeground);
        ChangedThenStable(splitter, () => typeof(MGGridSplitter).GetProperty(nameof(MGGridSplitter.IsDragging))!.SetValue(splitter, true));
        before = splitter.GetRenderRevisionSnapshot();
        splitter.Foreground.FocusedColor = Color.Purple;
        Check(!splitter.IsRenderRevisionCurrent(before), "Splitter nested foreground unobserved.");
        Equal(Draw(), Draw(true), "splitter capture");
        Results["splitter-foreground-geometry-drag"] = true;

        MGOverlayPanel panel = new(window);
        MGRectangle red = new(window, 100, 80, Color.Transparent, 0, Color.Red * 0.5f);
        MGRectangle blue = new(window, 100, 80, Color.Transparent, 0, Color.Blue * 0.5f);
        panel.TryAddChild(red, new(0), 0);
        panel.TryAddChild(blue, new(10), 1);
        SetRoot(panel);
        Color[] oldOrder = Draw();
        before = panel.GetRenderRevisionSnapshot();
        panel.TryReplaceChild(red, red, new(0), 2);
        Check(panel.VisualRevision == before.Visual + 1 && panel.StructureRevision == before.Structure + 1 && panel.LayoutRevision == before.Layout,
            "Z-only replacement duplicated invalidation or forced layout.");
        before = panel.GetRenderRevisionSnapshot();
        panel.TryReplaceChild(red, red, new(15), 2);
        Check(panel.VisualRevision == before.Visual + 1 && panel.StructureRevision == before.Structure && panel.LayoutRevision == before.Layout + 1,
            "Offset-only replacement duplicated layout or changed structure.");
        before = panel.GetRenderRevisionSnapshot();
        panel.TryReplaceChild(red, red, new(15), 2);
        Check(panel.IsRenderRevisionCurrent(before), "Identical overlay metadata churned.");
        Check(ReferenceEquals(panel.RenderPaintChildren.Last(), red), "Overlay retained order stale.");
        Update();
        Check(!oldOrder.SequenceEqual(Draw()), "Overlay metadata left stale arrangement/pixels.");
        Equal(Draw(), Draw(true), "overlay translucent painter order");
        MGStackPanel stack = new(window, Orientation.Vertical);
        stack.TryAddChild(new MGSpacer(window, 10, 10));
        ChangedThenStable(stack, () => stack.Orientation = Orientation.Horizontal, false);
        ChangedThenStable(stack, () => stack.Spacing = 12, false);
        MGDockPanel dock = new(window);
        dock.TryAddChild(new MGSpacer(window, 20, 20), Dock.Left);
        ChangedThenStable(dock, () => dock.LastChildFill = false, false);
        Results["container-metadata-order-layout"] = true;

        MGUniformBorderBrush groupBorder = new(new MGSolidFillBrush(Color.Magenta));
        MGGroupBox group = new(window, new MGTextBlock(window, "header"));
        group.SetContent(new MGRectangle(window, 80, 60, Color.Transparent, 0, Color.Orange));
        SetRoot(group);
        ChangedThenStable(group, () => group.BorderBrush = groupBorder);
        before = group.GetRenderRevisionSnapshot();
        group.GetBorder().BorderBrush = new MGUniformBorderBrush(Color.Aqua.AsFillBrush());
        Check(!group.IsRenderRevisionCurrent(before), "GroupBox direct border replacement missed owner.");
        ChangedThenStable(group, () => group.GetBorder().BorderThickness = new(4), false);
        ChangedThenStable(group, () => group.HeaderHorizontalMargin = 8, false);
        ChangedThenStable(group, () => group.HeaderHorizontalPadding = 12, false);
        Equal(Draw(), Draw(true), "group header gap and manual border capture");
        ProbeBrush groupBorderSource = new();
        group.BorderBrush = new(groupBorderSource);
        before = group.GetRenderRevisionSnapshot();
        groupBorderSource.Signal();
        Check(!group.IsRenderRevisionCurrent(before), "GroupBox nested manually painted source missed.");
        Results["group-header-gap-manual-border-resource"] = true;

        VisualStateFillBrush spoilerBackground = new(new MGSolidFillBrush(Color.Purple));
        MGSpoiler spoiler = new(window);
        spoiler.SetContent(new MGRectangle(window, 80, 60, Color.Transparent, 0, Color.Orange));
        SetRoot(spoiler);
        ChangedThenStable(spoiler, () => spoiler.IsRevealed = true, false);
        ChangedThenStable(spoiler, () => spoiler.UnspoiledBackgroundBrush = spoilerBackground);
        spoiler.IsRevealed = false;
        Check(spoiler.Content.Visibility == Visibility.Hidden, "Spoiler rehide stayed visible.");
        Equal(Draw(), Draw(true), "spoiler component capture");
        MGContextualContentPresenter contextual = new(window, true, new MGSpacer(window, 20, 20), new MGSpacer(window, 30, 30));
        SetRoot(contextual);
        before = contextual.GetRenderRevisionSnapshot();
        contextual.FalseContent.Opacity = 0.4f;
        Check(contextual.IsRenderRevisionCurrent(before), "Inactive conditional child dirtied owner.");
        ChangedThenStable(contextual, () => contextual.Value = false, false);
        Results["spoiler-and-inactive-contextual-switch"] = true;

        MGMenuBar bar = new(window);
        var barItem = bar.AddItem("File");
        var nextItem = bar.AddItem("Edit");
        SetRoot(bar);
        before = bar.GetRenderRevisionSnapshot();
        bar.Items.Remove(nextItem);
        bar.Items.Insert(0, nextItem);
        Check(!bar.IsRenderRevisionCurrent(before) && ReferenceEquals(bar.ItemsPanel.Children.First(), nextItem), "MenuBar reorder stale.");
        MGContextMenu menu = new(window);
        var toggle = menu.AddCheckBox("check", false);
        var radio = menu.AddRadioButton("radio", "group");
        var menuHeader = typeof(MGWrappedContextMenuItem).GetProperty("HeaderPresenter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MGElement header = (MGElement)menuHeader.GetValue(toggle)!;
        before = header.GetRenderRevisionSnapshot();
        ChangedThenStable(toggle, () => toggle.IsChecked = true);
        Check(!header.IsRenderRevisionCurrent(before), "Menu toggle actual painter stale.");
        header = (MGElement)menuHeader.GetValue(radio)!;
        before = header.GetRenderRevisionSnapshot();
        ChangedThenStable(radio, () => radio.IsChecked = true);
        Check(!header.IsRenderRevisionCurrent(before), "Menu radio actual painter stale.");
        header.UpdateLayout(new(70, 80, 30, 30));
        radio.IsChecked = false;
        Color[] uncheckedRadio = DrawElement(header);
        radio.IsChecked = true;
        Color[] checkedRadio = DrawElement(header);
        Check(!uncheckedRadio.SequenceEqual(checkedRadio), "Radio checked painter emitted no pixel change.");
        Equal(checkedRadio, DrawElement(header, true), "bounded menu radio header capture");
        header = (MGElement)menuHeader.GetValue(toggle)!;
        header.UpdateLayout(new(70, 80, 30, 30));
        toggle.IsChecked = false;
        Color[] uncheckedToggle = DrawElement(header);
        toggle.IsChecked = true;
        Color[] checkedToggle = DrawElement(header);
        Check(!uncheckedToggle.SequenceEqual(checkedToggle), "Toggle checked painter emitted no pixel change.");
        Equal(checkedToggle, DrawElement(header, true), "bounded menu toggle header capture");
        ChangedThenStable(toggle, () => toggle.ShortcutText = "Ctrl+T", false);
        Check(!toggle.HasUnsupportedControlRenderState, "Static menu item got delegate unsupported fact.");
        toggle.ComputeIsVisible = () => true;
        Check(toggle.HasUnsupportedControlRenderState, "Opaque menu visibility delegate qualified.");
        toggle.ComputeIsVisible = null;
        Check(!toggle.HasUnsupportedControlRenderState, "Cleared visibility delegate stayed unsupported.");
        Check(!bar.RenderPaintChildren.Contains(menu) && !barItem.RenderPaintChildren.Contains(menu), "Popup was flattened into bar owner.");
        Results["menu-order-check-painter-delegate-facts"] = true;

        MGOverlayHost host = new(window) { Opacity = 0.8f };
        MGRectangle content = new(window, 100, 80, Color.Transparent, 0, Color.Coral) { Opacity = 0.5f };
        List<string> order = new();
        content.OnEndDraw += (_, _) => order.Add("before");
        host.SetContent(content);
        content.OnEndDraw += (_, _) => order.Add("after");
        ProbeBrush tint = new() { OnDraw = () => order.Add("tint") };
        host.OverlayBackground = tint;
        MGOverlay overlayA = host.AddOverlay(new MGRectangle(window, 30, 20, Color.Transparent, 0, Color.Purple));
        MGOverlay overlayB = host.AddOverlay(new MGRectangle(window, 35, 25, Color.Transparent, 0, Color.Green));
        overlayA.IsOpen = true;
        SetRoot(host);
        order.Clear();
        Draw();
        Check(order.SequenceEqual(new[] { "before", "tint", "after" }), "Overlay changed public callback order.");
        Check(Math.Abs(tint.LastOpacity - 0.4f) < 0.0001f && tint.LastState == content.VisualState, "Overlay lost child opacity/state.");
        Check(host.HasUnsupportedControlRenderState, "OverlayHost lost conservative unsupported classification.");
        content.Visibility = Visibility.Hidden;
        int calls = tint.Calls;
        Draw();
        Check(tint.Calls == calls, "Hidden content painted overlay tint.");
        content.Visibility = Visibility.Visible;
        Rectangle savedBounds = content.LayoutBounds;
        typeof(MGElement).GetProperty(nameof(MGElement.LayoutBounds))!.SetValue(content, Rectangle.Empty);
        Draw();
        Check(tint.Calls == calls, "Zero-bounds content painted overlay tint.");
        typeof(MGElement).GetProperty(nameof(MGElement.LayoutBounds))!.SetValue(content, savedBounds);
        Results["overlay-callback-order-child-opacity-visibility"] = true;
        before = host.GetRenderRevisionSnapshot();
        var unaffected = sibling.GetRenderRevisionSnapshot();
        tint.Signal();
        Check(!host.IsRenderRevisionCurrent(before) && sibling.IsRenderRevisionCurrent(unaffected), "Overlay source fanout wrong.");
        overlayB.ZIndex = 2;
        overlayB.IsOpen = true;
        Check(ReferenceEquals(host.ActiveOverlay, overlayB) && host.ActiveOverlayPresenter.RenderPaintChildren.Contains(overlayB), "Overlay active painter stale.");
        Check(host.TryRemoveOverlay(overlayB) && ReferenceEquals(host.ActiveOverlay, overlayA) && !host.OpenOverlays.Contains(overlayB), "Removed open overlay stayed active.");
        overlayA.ShowCloseButton = true;
        overlayA.IsOpen = false;
        Check(host.ActiveOverlay == null && host.ActiveOverlayPresenter.Content == null, "Overlay close retained active paint edge.");
        overlayA.IsOpen = true;
        Results["overlay-source-active-order-remove-close"] = true;

        ProbeBrush staticSource = new();
        MGCachedFillBrush staticBrush = new(staticSource);
        sibling.Content.BackgroundBrush = new(staticBrush);
        for (int settle = 0; settle < 4; settle++)
        {
            Update();
            desktop.PrepareRenderCaches();
            Draw();
        }
        Check(desktop.RenderCaches.EntryCount > 0, "Independent sibling failed to prepare cache.");
        long hits = desktop.RenderCaches.Statistics.Hits;
        long builds = desktop.RenderCaches.Statistics.Builds;
        for (int frame = 0; frame < 3; frame++)
        {
            overlayA.Content.Opacity = 0.7f - frame * 0.1f;
            tint.Signal();
            Update();
            desktop.PrepareRenderCaches();
            Draw();
        }
        Check(desktop.RenderCaches.Statistics.Hits >= hits + 3 && desktop.RenderCaches.Statistics.Builds == builds, "Live overlay rebuilt independent static sibling.");
        Results["live-overlay-independent-cached-sibling"] = true;

        MGToolTip tooltip = new(window, content, 100, 60);
        var toolContent = new MGSpacer(tooltip, 20, 20);
        tooltip.SetContent(toolContent);
        before = toolContent.GetRenderRevisionSnapshot();
        ChangedThenStable(tooltip, () => tooltip.DrawOffset = new(12, 15));
        Check(!toolContent.IsRenderRevisionCurrent(before), "Tooltip offset failed descendant context invalidation.");
        before = content.GetRenderRevisionSnapshot();
        content.ToolTip = tooltip;
        tint.Signal();
        var popup = new MGContextMenu(window);
        content.ContextMenu = popup;
        Check(!content.RenderPaintChildren.Contains(tooltip) && !content.RenderPaintChildren.Contains(popup), "Desktop popup joined owner paint tree.");
        MGWindow nested = new(window, 70, 60, 80, 80) { WindowStyle = WindowStyle.None, BackgroundBrush = new(Color.Orange.AsFillBrush()) };
        window.AddNestedWindow(nested);
        window.ModalWindow = new(window, 90, 80, 60, 60) { WindowStyle = WindowStyle.None, BackgroundBrush = new(Color.Lime.AsFillBrush()) };
        Check(!window.RenderPaintChildren.Contains(nested) && !window.RenderPaintChildren.Contains(window.ModalWindow), "Window layers were flattened.");
        Update();
        Check(Draw()[100 * 640 + 100] == Color.Lime, "Modal window did not retain top painter priority.");
        window.ModalWindow = null;
        Update();
        Check(Draw()[100 * 640 + 100] == Color.Orange, "Nested window did not remain live after modal removal.");
        window.RemoveNestedWindow(nested);
        Results["tooltip-popup-nested-modal-live-ownership"] = true;

        VisualStateColorBrush externalForeground = new(Color.Red);
        MGResizeGrip external = new(window, content);
        Check(external.HasUnsupportedControlRenderState, "External grip qualified as bounded content.");
        ChangedThenStable(external, () => external.Foreground = externalForeground);
        VisualStateColorBrush componentForeground = new(Color.Gold);
        MGResizeGrip component = (MGResizeGrip)window.ResizeGripComponent.BaseElement;
        Check(!component.HasUnsupportedControlRenderState, "Component grip got external-host fact.");
        ChangedThenStable(component, () => component.Foreground = componentForeground);
        before = component.GetRenderRevisionSnapshot();
        component.Foreground.FocusedColor = Color.Blue;
        Check(!component.IsRenderRevisionCurrent(before), "Grip nested foreground missed.");
        MGRectangle shape = new(window, 20, 25, Color.Black, 1, Color.White);
        ChangedThenStable(shape, () => shape.Width = 30, false);
        ChangedThenStable(shape, () => shape.Height = 35, false);
        ChangedThenStable(shape, () => shape.Stroke = Color.Gold);
        ChangedThenStable(shape, () => shape.StrokeThickness = 3);
        MGSeparator separator = new(window, Orientation.Horizontal, 1);
        ChangedThenStable(separator, () => separator.Orientation = Orientation.Vertical, false);
        ChangedThenStable(separator, () => separator.Size = 4, false);
        MGSpacer spacer = new(window, 10, 10);
        ChangedThenStable(spacer, () => spacer.Width = 20, false);
        ChangedThenStable(spacer, () => spacer.Height = 30, false);
        MGInputConsumer consumer = new(window);
        consumer.SetContent(new MGSpacer(window, 10, 10));
        before = consumer.GetRenderRevisionSnapshot();
        consumer.HandlesMousePresses = !consumer.HandlesMousePresses;
        Check(consumer.IsRenderRevisionCurrent(before), "Input-only consumer option dirtied painting.");
        Results["grip-external-classification-and-input-consumer"] = true;
        desktop.DisposeRenderCaches();
        Completed = true;
    }
    protected override void Update(GameTime gameTime) { Exit(); }
    protected override void Draw(GameTime gameTime) { }
    private static void Equal(Color[] expected, Color[] actual, string name)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            Check(expected[index] == actual[index], name + " pixel " + index + ": " + expected[index] + " != " + actual[index]);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private sealed class ProbeBrush : IFillBrush, IMGRenderCacheSource
    {
        internal int Calls;
        internal float LastOpacity;
        internal VisualState LastState;
        internal Action? OnDraw;
        public ulong VisualRevision { get; private set; } = 1;
        public bool IsRenderVolatile => false;
        public bool SupportsRenderCaching => true;
        public event EventHandler? VisualInvalidated;
        internal void Signal()
        {
            VisualRevision++;
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds)
        {
            Calls++;
            LastOpacity = args.Opacity;
            LastState = args.VisualState;
            OnDraw?.Invoke();
            args.DT.FillRectangle(args.Offset.ToVector2(), bounds, Color.Black * 0.3f * args.Opacity);
        }
        public IFillBrush Copy() => new ProbeBrush();
    }
}
