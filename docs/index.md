# UI Elements

This index documents the XAML-facing UI element types defined under `MGUI.Core/UI`, along with the shared base abstractions they build on and the runtime-only `ChatBoxMessage` element.

Each page lists the direct properties introduced by that type. Inherited members are documented on the linked base-type pages.

## Shared Base API

- All concrete elements inherit the core `Element` API: sizing, alignment, margins, padding, backgrounds, tooltips, context menus, selection/enabled state, styling, and attached layout properties.
- `SingleContentHost` adds `Content` for controls with a single primary child.
- `MultiContentHost` adds `Children` for controls with a child collection.
- `ContextMenuItem` and `WrappedContextMenuItem` define the shared shape of context-menu entries.

## Foundations

- [Element](./ui/element.md)
- [SingleContentHost](./ui/single-content-host.md)
- [MultiContentHost](./ui/multi-content-host.md)
- [ContextMenuItem](./ui/context-menu-item.md)
- [WrappedContextMenuItem](./ui/wrapped-context-menu-item.md)

## Presenters and Layout

- [ContentPresenter](./ui/content-presenter.md)
- [HeaderedContentPresenter](./ui/headered-content-presenter.md)
- [ContextualContentPresenter](./ui/contextual-content-presenter.md)
- [Border](./ui/border.md)
- [Grid](./ui/grid.md)
- [UniformGrid](./ui/uniform-grid.md)
- [DockPanel](./ui/dock-panel.md)
- [StackPanel](./ui/stack-panel.md)
- [OverlayPanel](./ui/overlay-panel.md)
- [ScrollViewer](./ui/scroll-viewer.md)
- [GridSplitter](./ui/grid-splitter.md)
- [Separator](./ui/separator.md)
- [Spacer](./ui/spacer.md)

## Windows and Composition

- [Window](./ui/window.md)
- [ToolTip](./ui/tool-tip.md)
- [OverlayHost](./ui/overlay-host.md)
- [Overlay](./ui/overlay.md)
- [GroupBox](./ui/group-box.md)
- [Expander](./ui/expander.md)
- [TabControl](./ui/tab-control.md)
- [TabItem](./ui/tab-item.md)
- [ResizeGrip](./ui/resize-grip.md)
- [XAMLDesigner](./ui/xaml-designer.md)

## Input and Commands

- [Button](./ui/button.md)
- [ToggleButton](./ui/toggle-button.md)
- [ProgressButton](./ui/progress-button.md)
- [CheckBox](./ui/check-box.md)
- [RadioButton](./ui/radio-button.md)
- [ComboBox](./ui/combo-box.md)
- [Slider](./ui/slider.md)
- [TextBox](./ui/text-box.md)
- [PasswordBox](./ui/password-box.md)
- [InputConsumer](./ui/input-consumer.md)
- [ContextMenu](./ui/context-menu.md)
- [ContextMenuButton](./ui/context-menu-button.md)
- [ContextMenuToggle](./ui/context-menu-toggle.md)
- [ContextMenuSeparator](./ui/context-menu-separator.md)
- [ContextMenuRadioButton](./ui/context-menu-radio-button.md)
- [MenuBar](./ui/menu-bar.md)
- [MenuBarItem](./ui/menu-bar-item.md)

## Data and Display

- [TextBlock](./ui/text-block.md)
- [Image](./ui/image.md)
- [Rectangle](./ui/rectangle.md)
- [ProgressBar](./ui/progress-bar.md)
- [Timer](./ui/timer.md)
- [Stopwatch](./ui/stopwatch.md)
- [RatingControl](./ui/rating-control.md)
- [GridColorPicker](./ui/grid-color-picker.md)
- [ListBox](./ui/list-box.md)
- [ListView](./ui/list-view.md)
- [TreeView](./ui/tree-view.md)
- [TreeViewItem](./ui/tree-view-item.md)
- [ChatBox](./ui/chat-box.md)
- [Spoiler](./ui/spoiler.md)
- [ChatBoxMessage](./ui/chat-box-message.md)
