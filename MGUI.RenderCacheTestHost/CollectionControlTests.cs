using System.ComponentModel;
using MGUI.Core.UI.Data_Binding;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class CollectionControlTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;
    internal CollectionControlTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }
    internal static int RunAllNative()
    {
        using CollectionControlTests game = new();
        game.Run();
        Check(game.Completed, "Collection checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Debug; exact native RGBA normal/capture parity; renderer recorded by test-native.py.");
        return 0;
    }
    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 40, 40, 300, 220) { WindowStyle = WindowStyle.None, Padding = new(0) };
        MGWindow unrelated = new(desktop, 400, 40, 100, 100) { WindowStyle = WindowStyle.None };
        unrelated.SetContent(new MGBorder(unrelated));
        desktop.Windows.Add(window);
        desktop.Windows.Add(unrelated);
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
        void SetRoot(MGElement root)
        {
            window.SetContent(root);
            for (int settle = 0; settle < 3; settle++)
            {
                Update();
                Draw();
            }
        }
        MGListBox<string> list = new(window);
        ObservableCollection<string> items = new() { "one", "two", "three" };
        list.SetItemsSource(items);
        SetRoot(list);
        list.SelectItem("one", true);
        var row = list.ListBoxItems[0];
        var oldRow = row.ContentPresenter.GetRenderRevisionSnapshot();
        var owner = window.GetRenderRevisionSnapshot();
        var other = unrelated.GetRenderRevisionSnapshot();
        list.SelectItem("two", true);
        Check(!row.ContentPresenter.IsRenderRevisionCurrent(oldRow) && !window.IsRenderRevisionCurrent(owner), "List selection did not dirty old row/paint owner.");
        Check(unrelated.IsRenderRevisionCurrent(other), "List selection dirtied unrelated window.");
        Check(list.ListBoxItems[1].ContentPresenter.IsSelected && !row.ContentPresenter.IsSelected, "List selection styling incorrect.");
        owner = list.GetRenderRevisionSnapshot();
        list.SelectItem("two", true);
        Check(list.IsRenderRevisionCurrent(owner), "Equal selection churned revisions.");
        Results["list-selection-ownership"] = true;
        var retained = list.ListBoxItems[1];
        items.Move(1, 0);
        Check(ReferenceEquals(retained, list.ListBoxItems[0]) && ReferenceEquals(retained.ContentPresenter, list.ItemsPanel.Children[0]), "List move lost wrapper or paint order.");
        Check(retained.ContentPresenter.IsSelected && !list.IsRenderRevisionCurrent(owner), "List move lost selection or revision.");
        owner = list.GetRenderRevisionSnapshot();
        var removed = list.ListBoxItems[1].Content;
        items[1] = "replacement";
        Check(!list.IsRenderRevisionCurrent(owner) && !ReferenceEquals(removed, list.ListBoxItems[1].Content), "List replacement did not regenerate content.");
        owner = list.GetRenderRevisionSnapshot();
        list.ItemTemplate = value => new MGTextBlock(window, "custom " + value);
        Check(!list.IsRenderRevisionCurrent(owner) && ((MGTextBlock)list.ListBoxItems[0].Content).Text == "custom two", "List template replacement remained stale.");
        List<IFillBrush> alternating = new() { new MGSolidFillBrush(Color.Red) };
        list.AlternatingRowBackgrounds = alternating.AsReadOnly();
        alternating[0] = new MGSolidFillBrush(Color.Blue);
        list.InvalidateVisual();
        Check(ReferenceEquals(alternating[0], list.ListBoxItems[0].ContentPresenter.BackgroundBrush.NormalValue), "Explicit raw alternating list invalidation did not refresh rows.");
        items.Clear();
        Check(list.ItemsPanel.Children.Count == 0 && list.SelectedItems.Count == 0, "List reset retained painted rows or selection.");
        Results["list-generation-move-reset-raw-invalidate"] = true;

        MGListView<string> view = new(window);
        var column = view.AddColumn(new ListViewColumnWidth(120), new MGTextBlock(window, "Header"), value => new MGTextBlock(window, value));
        ObservableCollection<string> data = new() { "A", "B", "C" };
        view.SetItemsSource(data);
        SetRoot(view);
        var first = view.RowItems[0];
        var second = view.RowItems[1];
        var cell = second.GetRowContents()[column];
        owner = view.GetRenderRevisionSnapshot();
        data.Move(1, 0);
        Check(ReferenceEquals(second, view.RowItems[0]) && ReferenceEquals(second.DataRow, view.DataGrid.Rows[0]) && ReferenceEquals(cell, second.GetRowContents()[column]), "ListView move changed row/cell identity or grid order.");
        Check(!view.IsRenderRevisionCurrent(owner), "ListView move did not invalidate.");
        data[1] = "D";
        Check(view.RowItems[1].Data == "D" && view.RowItems[1].DataRow == view.DataGrid.Rows[1], "Nonzero ListView replacement used the wrong row.");
        Check(((MGTextBlock)view.RowItems[0].GetRowContents()[column]).Text == "B", "ListView replacement overwrote another row.");
        owner = view.GetRenderRevisionSnapshot();
        column.Header = new MGTextBlock(window, "new header");
        column.CellTemplate = value => new MGTextBlock(window, "new " + value);
        column.Width.WidthPixels = 160;
        Check(!view.IsRenderRevisionCurrent(owner) && view.DataGrid.Columns[0].Length.Pixels == 160, "ListView header/template/width change unobserved.");
        owner = view.GetRenderRevisionSnapshot();
        data.Clear();
        Check(view.RowItems.Count == 0 && view.DataGrid.Rows.Count == 0 && !view.IsRenderRevisionCurrent(owner), "ListView reset retained grid rows.");
        data.Add("after reset");
        Check(view.DataGrid.Rows.Count == 1 && view.RowItems[0].DataRow == view.DataGrid.Rows[0], "ListView reset/add left phantom rows.");
        Results["list-view-move-replacement-column-generation"] = true;

        MGTreeView tree = new(window);
        MGTreeViewItem root = new(window) { Header = "root" };
        MGTreeViewItem child = new(window) { Header = "child" };
        root.AddItem(child);
        tree.AddItem(root);
        SetRoot(tree);
        owner = tree.GetRenderRevisionSnapshot();
        root.IsExpanded = true;
        Check(!tree.IsRenderRevisionCurrent(owner), "Tree expansion unobserved.");
        owner = root.GetRenderRevisionSnapshot();
        root.IsExpanded = true;
        Check(root.IsRenderRevisionCurrent(owner), "Equal expansion churned revision.");
        tree.SelectedItem = root;
        oldRow = root.GetRenderRevisionSnapshot();
        owner = child.GetRenderRevisionSnapshot();
        tree.SelectedItem = child;
        Check(!root.IsRenderRevisionCurrent(oldRow) && !child.IsRenderRevisionCurrent(owner) && child.IsSelected && !root.IsSelected, "Programmatic tree selection failed old/new styling.");
        owner = tree.GetRenderRevisionSnapshot();
        tree.SelectionBackgroundBrush = new(new MGSolidFillBrush(Color.Magenta));
        tree.SelectionForeground = Color.Cyan;
        tree.IndentSize = 30;
        Check(!tree.IsRenderRevisionCurrent(owner), "Tree selected decoration/indentation unobserved.");
        owner = tree.GetRenderRevisionSnapshot();
        Check(root.Items.Contains(child), "Tree child missing before removal.");
        Check(ReferenceEquals(root.OwnerTreeView, tree), "Tree root owner missing before removal.");
        root.RemoveItem(child);
        Check(tree.SelectedItem == null, "Removing selected tree child retained selection.");
        Check(child.OwnerTreeView == null, "Removing selected tree child retained owner.");
        Check(!tree.IsRenderRevisionCurrent(owner), "Removing selected tree child did not invalidate owner.");
        int detachedExpansionEvents = 0;
        tree.ItemExpanded += (_, _) => detachedExpansionEvents++;
        child.Expand();
        Check(detachedExpansionEvents == 0, "Detached tree child kept owner expansion subscription.");
        ObservableCollection<string> nodes = new() { "X", "Y" };
        tree.ItemsSource = nodes;
        owner = tree.GetRenderRevisionSnapshot();
        MGTreeViewItem keptNode = tree.Items[1];
        nodes.Move(1, 0);
        Check(ReferenceEquals(keptNode, tree.Items[0]), "Tree move regenerated an unchanged node.");
        nodes.Insert(1, "inserted");
        Check(ReferenceEquals(keptNode, tree.Items[0]) && tree.Items[1].Header?.ToString() == "inserted", "Tree insert regenerated existing nodes or changed order.");
        nodes.RemoveAt(1);
        nodes[1] = "Z";
        Check(tree.Items[0].Header?.ToString() == "Y" && tree.Items[1].Header?.ToString() == "Z" && !tree.IsRenderRevisionCurrent(owner), "Tree observable replace/move stayed stale.");
        Results["tree-selection-expand-generated-source"] = true;

        MGTabControl tabs = new(window);
        MGTabItem tabA = tabs.AddTab("A", new MGBorder(window));
        MGTabItem tabB = tabs.AddTab("B", new MGBorder(window));
        tabs.TrySelectTab(tabA);
        SetRoot(tabs);
        owner = tabs.GetRenderRevisionSnapshot();
        tabB.Content.Opacity = 0.5f;
        Check(tabs.IsRenderRevisionCurrent(owner), "Inactive tab paint dirtied active owner.");
        var inactive = tabB.Content.GetRenderRevisionSnapshot();
        tabs.TrySelectTab(tabB);
        Check(!tabs.IsRenderRevisionCurrent(owner) && !tabB.Content.IsRenderRevisionCurrent(inactive), "Tab activation retained stale owner/context.");
        Check(tabs.RenderPaintChildren.Contains(tabB) && !tabs.RenderPaintChildren.Contains(tabA), "Tab paint membership incorrect.");
        Results["tab-active-membership-and-reactivation"] = true;

        MGContextualContentPresenter presenter = new(window, true, new MGBorder(window), new MGBorder(window));
        SetRoot(presenter);
        owner = presenter.GetRenderRevisionSnapshot();
        presenter.FalseContent.Opacity = 0.6f;
        Check(presenter.IsRenderRevisionCurrent(owner), "Inactive contextual content dirtied active owner.");
        presenter.Value = false;
        Check(!presenter.IsRenderRevisionCurrent(owner) && presenter.RenderPaintChildren.Contains(presenter.FalseContent), "Contextual switch was stale.");
        Results["contextual-switch"] = true;

        MGComboBox<string> combo = new(window);
        ObservableCollection<string> choices = new() { "one", "two", "three" };
        combo.SetItemsSource(choices);
        combo.SelectedIndex = 1;
        SetRoot(combo);
        owner = combo.GetRenderRevisionSnapshot();
        choices.Move(1, 0);
        Check(combo.SelectedItem == "two" && combo.SelectedIndex == 0, "Combo move lost selected wrapper.");
        choices[0] = "new choice";
        Check(combo.SelectedIndex == -1 && combo.Content == null && !combo.IsRenderRevisionCurrent(owner), "Combo removed selection retained stale presentation.");
        combo.SelectedIndex = 0;
        combo.DropdownItemTemplate = value =>
        {
            MGButton Button = combo.CreateDefaultDropdownButton();
            Button.SetContent("option " + value);
            return Button;
        };
        Check(combo.SelectedItem == "new choice" && combo.SelectedIndex == 0, "Combo dropdown template regeneration lost selected data.");
        owner = combo.GetRenderRevisionSnapshot();
        combo.SelectedItemTemplate = value => new MGTextBlock(window, "selected " + value);
        Check(!combo.IsRenderRevisionCurrent(owner) && ((MGTextBlock)combo.Content!).Text == "selected new choice", "Combo selected template remained stale.");
        owner = combo.GetRenderRevisionSnapshot();
        combo.IsDropdownOpen = true;
        Check(!combo.RenderPaintChildren.Contains(combo.Dropdown), "Separate combo popup was flattened into owner.");
        combo.IsDropdownOpen = false;
        combo.DropdownArrowColor = Color.Red;
        Color[] arrowRed = Draw();
        Equal(arrowRed, Draw(true), "combo arrow capture");
        combo.DropdownArrowColor = Color.Lime;
        Check(!arrowRed.SequenceEqual(Draw()), "Combo arrow color did not change pixels.");
        Equal(Draw(), Draw(true), "changed combo arrow capture");
        Results["combo-generation-popup-and-arrow-capture"] = true;

        MGExpander expander = new(window);
        expander.Header = new MGTextBlock(window, "details");
        expander.SetContent(new MGBorder(window));
        SetRoot(expander);
        owner = expander.GetRenderRevisionSnapshot();
        var arrow = expander.ExpanderToggleButton.GetRenderRevisionSnapshot();
        expander.ExpanderDropdownArrowColor = Color.Red;
        expander.ExpanderDropdownArrowSize += 2;
        Check(!expander.IsRenderRevisionCurrent(owner) && !expander.ExpanderToggleButton.IsRenderRevisionCurrent(arrow), "Expander parent fields did not dirty arrow painter.");
        Equal(Draw(), Draw(true), "expander arrow capture");
        expander.IsExpanded = false;
        Check(expander.Content.Visibility == Visibility.Collapsed, "Expander collapse failed.");
        expander.CollapsedVisibility = Visibility.Hidden;
        Check(expander.Content.Visibility == Visibility.Hidden, "Expander visibility setting did not apply to active state.");
        expander.IsExpanded = true;
        Equal(Draw(), Draw(true), "expanded arrow capture");
        Results["expander-state-and-arrow-capture"] = true;

        MGScrollViewer scroll = new(window, ScrollBarVisibility.Visible, ScrollBarVisibility.Visible);
        scroll.SetContent(new MGRectangle(window, 600, 500, Color.Black, 1, Color.Goldenrod));
        SetRoot(scroll);
        owner = scroll.GetRenderRevisionSnapshot();
        other = unrelated.GetRenderRevisionSnapshot();
        Color[] top = Draw();
        scroll.VerticalOffset = 40;
        scroll.HorizontalOffset = 25;
        Check(!scroll.IsRenderRevisionCurrent(owner) && scroll.LayoutRevision == owner.Layout, "Scroll offsets failed paint-only invalidation.");
        Check(unrelated.IsRenderRevisionCurrent(other), "Scroll dirtied unrelated window.");
        Check(!top.SequenceEqual(Draw()), "Scroll did not change viewport/scrollbar pixels.");
        Equal(Draw(), Draw(true), "scrolled viewport capture");
        owner = scroll.GetRenderRevisionSnapshot();
        scroll.VerticalOffset = 40;
        Check(scroll.IsRenderRevisionCurrent(owner), "Equal scroll offset churned revision.");
        foreach (string property in new[] { "IsHoveringVSB", "IsDraggingVSB", "IsHoveringHSB", "IsDraggingHSB" })
        {
            owner = scroll.GetRenderRevisionSnapshot();
            typeof(MGScrollViewer).GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scroll, true);
            Check(!scroll.IsRenderRevisionCurrent(owner) && scroll.LayoutRevision == owner.Layout, property + " failed paint-only invalidation.");
        }
        var oldBrush = scroll.ScrollBarInnerBrush;
        owner = scroll.GetRenderRevisionSnapshot();
        oldBrush.FocusedColor = Color.Purple;
        Check(!scroll.IsRenderRevisionCurrent(owner), "Scrollbar resource mutation unobserved.");
        scroll.ScrollBarInnerBrush = new(new MGSolidFillBrush(Color.Coral));
        owner = scroll.GetRenderRevisionSnapshot();
        oldBrush.FocusedColor = Color.Red;
        Check(scroll.IsRenderRevisionCurrent(owner), "Replaced scrollbar source remained attached.");
        Results["scroll-offset-viewport-state-resource-ownership"] = true;

        MGScrollViewer unobserved = new(window);
        CountingBrush source = new();
        unobserved.ScrollBarOuterBrush = new(source);
        unobserved.ScrollBarOuterBrush.FocusedColor = Color.Red;
        Check(source.Reads == 0, "Unobserved control performed cache source work.");
        _ = unobserved.VisualRevision;
        Check(source.Reads > 0, "Observed control did not register its local sources.");
        int reads = source.Reads;
        for (int index = 0; index < 10; index++)
        {
            _ = unobserved.VisualRevision;
        }
        Check(source.Reads == reads, "Idle revision reads rediscovered control sources.");
        unobserved.ScrollBarOuterBrush = new(new UnknownBrush());
        Check(!unobserved.BaseRenderSourceDependencies.SupportsRenderCaching, "Unknown scrollbar renderer qualified as tracked source.");
        MGListBox<string> customTemplateList = new(window);
        customTemplateList.ItemTemplate = value => new UnknownRenderer(window);
        customTemplateList.SetItemsSource(new[] { "custom" });
        Check(customTemplateList.ListBoxItems[0].ContentPresenter.RenderPaintChildren.Single() is UnknownRenderer,
            "Generated custom renderer was absent from maintained paint membership for eligibility classification.");
        Results["lazy-control-source-observation"] = true;

        MGChatBox chat = new(window);
        SetRoot(chat);
        chat.Messages.Add(new("Alice", new DateTime(2026, 1, 1, 12, 34, 56), "message"));
        owner = chat.GetRenderRevisionSnapshot();
        chat.TimestampFormat = "HH:mm";
        MGChatBoxMessage message = (MGChatBoxMessage)chat.MessagesContainer.ListBoxItems[0].Content;
        Check(message.TimestampTextBlock.Text == "12:34" && !chat.IsRenderRevisionCurrent(owner), "Chat timestamp reformat remained stale.");
        chat.Messages.Add(new("Bob", DateTime.Now, "second"));
        chat.MaxMessages = 1;
        Update();
        Check(chat.Messages.Count == 1 && chat.MessagesContainer.ListBoxItems.Count == 1, "Chat pruning retained generated messages.");
        owner = chat.GetRenderRevisionSnapshot();
        chat.Messages.Clear();
        Check(chat.MessagesContainer.ItemsPanel.Children.Count == 0 && !chat.IsRenderRevisionCurrent(owner), "Chat reset retained painted messages.");
        Results["chat-generated-format-prune-reset"] = true;
        VerifyGeneratedBindingCleanup(window);
        Results["generated-binding-and-handler-cleanup"] = true;
        desktop.DisposeRenderCaches();
        Completed = true;
    }
    protected override void Update(GameTime gameTime) { Exit(); }
    protected override void Draw(GameTime gameTime) { }
    private static void Equal(Color[] expected, Color[] actual, string name)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            Check(expected[index] == actual[index], name + " pixel " + index);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void VerifyGeneratedBindingCleanup(MGWindow window)
    {
        BindingSource source = new();
        List<DataBinding> bindings = new();
        DataBinding Bind(MGElement target)
        {
            DataBinding binding = DataBindingManager.AddBinding(new(nameof(MGElement.Opacity), nameof(BindingSource.Value),
                SourceResolver: new FixedSourceResolver(source), DataContextResolver: DataContextResolver.Self), target);
            bindings.Add(binding);
            return binding;
        }
        try
        {
            MGListBox<string> list = new(window) { ItemTemplate = _ => new MGBorder(window) };
            ObservableCollection<string> items = new() { "first" };
            list.SetItemsSource(items);
            MGListBoxItem<string> removed = list.ListBoxItems[0];
            MGElement oldContent = removed.Content;
            DataBinding containerBinding = Bind(removed.ContentPresenter);
            DataBinding contentBinding = Bind(oldContent);
            Check(source.SubscriberCount == 2, "List cleanup test did not attach actual source subscribers.");
            items.Clear();
            Check(containerBinding.IsDisposed && contentBinding.IsDisposed && source.SubscriberCount == 0, "List reset retained presenter/content binding subscribers.");
            Check(EventSubscriberCount(list, "ItemTemplateChanged") == 0 && EventSubscriberCount(list, "ItemContainerStyleChanged") == 0,
                "List reset retained removed wrapper template/style handlers.");
            list.ItemTemplate = _ => new MGTextBlock(window, "changed");
            Check(ReferenceEquals(removed.Content, oldContent), "Removed wrapper still regenerated on template change.");
            items.Add("replacement");
            items.Add("second");
            MGListBoxItem<string> replaced = list.ListBoxItems[0];
            DataBinding replacementBinding = Bind(replaced.ContentPresenter);
            items.Move(0, 1);
            Check(!replacementBinding.IsDisposed && source.SubscriberCount == 1 && ReferenceEquals(replaced, list.ListBoxItems[1]),
                "List move detached the retained wrapper binding.");
            items.RemoveAt(0);
            items[0] = "new";
            Check(replacementBinding.IsDisposed && source.SubscriberCount == 0 && EventSubscriberCount(list, "ItemTemplateChanged") == 1,
                "List replace retained old binding/handler or detached surviving wrapper.");
            MGListBoxItem<string> rebound = list.ListBoxItems[0];
            DataBinding reboundBinding = Bind(rebound.Content);
            list.SetItemsSource(new[] { "another source" });
            Check(reboundBinding.IsDisposed && source.SubscriberCount == 0 && EventSubscriberCount(list, "ItemTemplateChanged") == 1,
                "List source replacement retained old binding/handlers.");
            list.SetItemsSource(Array.Empty<string>());

            MGComboBox<string> combo = new(window);
            ObservableCollection<string> options = new() { "unpainted" };
            combo.SetItemsSource(options);
            var generated = (ObservableCollection<TemplatedElement<string, MGButton>>)typeof(MGComboBox<string>)
                .GetProperty("TemplatedItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;
            MGButton oldButton = generated[0].Element;
            DataBinding buttonBinding = Bind(oldButton);
            DataBinding buttonContentBinding = Bind(oldButton.Content!);
            Check(combo.DropdownStackPanel.Children.Count == 0 && source.SubscriberCount == 2,
                "Combo cleanup test did not bind unpainted generated dropdown content.");
            options.Clear();
            Check(buttonBinding.IsDisposed && buttonContentBinding.IsDisposed && source.SubscriberCount == 0,
                "Combo reset retained unpainted template binding subscribers.");

            MGListView<string> view = new(window);
            var column = view.AddColumn(new ListViewColumnWidth(80), new MGBorder(window), _ => new MGBorder(window));
            ObservableCollection<string> rows = new() { "one", "two" };
            view.SetItemsSource(rows);
            DataBinding rowBinding = Bind(view.RowItems[1].GetRowContents()[column]);
            rows[1] = "replacement";
            Check(rowBinding.IsDisposed && source.SubscriberCount == 0, "ListView replacement retained old row binding subscriber.");
            DataBinding resetBinding = Bind(view.RowItems[0].GetRowContents()[column]);
            rows.Clear();
            Check(resetBinding.IsDisposed && source.SubscriberCount == 0, "ListView reset retained row binding subscribers.");
            rows.Add("before source replacement");
            DataBinding viewSourceBinding = Bind(view.RowItems[0].GetRowContents()[column]);
            view.SetItemsSource(new[] { "after source replacement" });
            Check(viewSourceBinding.IsDisposed && source.SubscriberCount == 0, "ListView source replacement retained row binding subscribers.");
        }
        finally
        {
            foreach (DataBinding binding in bindings)
            {
                DataBindingManager.RemoveBinding(binding);
            }
        }
    }
    private static int EventSubscriberCount(object owner, string name)
        => ((Delegate?)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner))?.GetInvocationList().Length ?? 0;
    private sealed class BindingSource : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? Handlers;
        public float Value => 0.8f;
        public int SubscriberCount => Handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { Handlers += value; }
            remove { Handlers -= value; }
        }
    }
    private sealed class FixedSourceResolver : ISourceObjectResolver
    {
        private readonly object Source;
        internal FixedSourceResolver(object source) { Source = source; }
        public object ResolveSourceObject(object target) => Source;
    }
    private sealed class UnknownRenderer : MGBorder
    {
        internal UnknownRenderer(MGWindow window) : base(window) { }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds) { }
    }
    private sealed class UnknownBrush : IFillBrush
    {
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => this;
    }
    private sealed class CountingBrush : IFillBrush, IMGRenderCacheSource
    {
        internal int Reads;
        public ulong VisualRevision { get { Reads++; return 1; } }
        public bool IsRenderVolatile { get { Reads++; return false; } }
        public bool SupportsRenderCaching { get { Reads++; return true; } }
        public event EventHandler? VisualInvalidated { add { } remove { } }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => this;
    }
}
