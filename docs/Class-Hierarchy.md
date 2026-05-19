### Class Hierarchies

- MGElement (_abstract_)
  - MGContentHost (_abstract_)
    - MGSingleContentHost (_abstract_)
    - MGMultiContentHost (_abstract_)

All controls inherit from either `MGElement`, `MGSingleContentHost`, or `MGMultiContentHost`.

- Types that inherit from `MGElement`
  - ChatBox
  - GridSplitter
  - Image
  - ListBox
  - ProgressBar
  - RatingControl
  - Rectangle
  - ResizeGrip
  - Separator
  - Slider
  - Spacer
  - Stopwatch
  - TextBlock
  - TextBox
  - Timer
  - XAMLDesigner

- Types that inherit from `MGSingleContentHost`
  - Border
  - Button
  - CheckBox
  - ComboBox
  - ContentPresenter
  - ContextMenu
  - ContextMenuItem
  - Expander
  - GroupBox
  - HeaderedContentPresenter
  - InputConsumer
  - ListView
  - PasswordBox
  - RadioButton
  - ScrollViewer
  - Spoiler
  - TabControl
  - TabItem
  - ToggleButton
  - ToolTip
  - Window

- Types that inherit from `MGMultiContentHost`
  - DockPanel
  - Grid
  - OverlayPanel
  - StackPanel
  - UniformGrid

- Types that inherit from `MGWindow`
  - ContextMenu
  - ToolTip

----------------------

Some of the inheritances are inconsistent. Why does `ListBox` extend `MGElement` while `ListView` extends `MGSingleContentHost`? The general rules of thumb are: 

- If the control defines the most primitive renderable content, it extends `MGElement`.
- If the control allows the user to specify exactly 1 piece of child content, it extends `MGSingleContentHost`
- If the control is used as a wrapper to arrange multiple immediate children, it extends `MGMultiContentHost`

Some types are glorified wrappers around several hierarchical levels of children, and don't exactly fit into 1 of the 3 groups above. 

For example, a `ListView` always has nested content in roughly this visual tree:
```xaml
<ListView>
    <DockPanel>
        <!-- This Grid represents the ListView's column headers -->
        <Grid Dock="Top" />
        <ScrollViewer>
            <!-- This Grid represents the ListView's row content -->
            <Grid />
        </ScrollViewer>
    </DockPanel>
</ListView>
```
The `ListView` micromanages all child content for you. For example, if you call `ListView.AddColumn(...)`, it will create/populate a column in both of the nested Grids, using the same column dimensions. If you call `ListView.SetItemsSource(...)`, it will create/populate a row in the second Grid for each item in the ItemsSource. `ListView` extends `MGSingleContentHost` as it has exactly 1 immediate child, even though the `ListView` is micro-managing several children in the hierarchical tree.

`ListBox` always has nested content in roughly this visual tree:
```xaml
<ListBox>
    <DockPanel>
        <!-- This ContentPresenter is a placeholder for the ListBox's Header content -->
        <ContentPresenter Dock="Top" />
        <ScrollViewer>
            <!-- This StackPanel represents the row content -->
            <StackPanel Orientation="Vertical" />
        </ScrollViewer>
    </DockPanel>
</ListBox>
```
Like the `ListView`, `ListBox` micro-manages the child content for you. If you set `ListBox.SetItemsSource(...)`, it will create/populate exactly 1 child in the `StackPanel` for each item in the ItemsSource. `ListBox` could have extended `MGSingleContentHost`, but instead it uses a _Component_ to display its child content.

---------------------

### Components

Components represent nested content within an element that the user isn't intended to modify. 

For example, a `CheckBox` always has a `Button` on the left edge that can be clicked to check or uncheck the `CheckBox`. That `Button` is a _Component_ of the `CheckBox`. It's always a `Button`, whereas the actual content of the `CheckBox` can be whatever you want (usually a `TextBlock`), even null/empty. You can still re-style the `Button` Component of the `CheckBox` and change its properties, but you cannot (and should not!) delete it as it's integral to the `CheckBoxes` functionality. 
As another example, consider a `Button`. `Buttons` always have a `Border` around them. Rather than defining Border-related logic in every bordered control (or making most controls a subclass of a `Border` class), bordered controls simply have a `Border` component.

Components are arranged within their host (parent) similarly to how a `DockPanel` arranges its children, so you can think of each `MGElement` as having its own built-in simplified `DockPanel` for handling its Components.