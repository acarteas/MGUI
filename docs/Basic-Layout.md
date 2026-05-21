This is a tutorial to get you started with arranging your UI. If you're already familiar with [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/?view=netdesktop-6.0), you probably already know most of what this tutorial covers, since MGUI is heavily inspired by WPF and has many similar properties and functionality.

-------------------

# Getting Started

UI's with MGUI are defined hierarchically. The `MGWindow` is the outermost node of the visual tree. Most controls have a `Content` property, allowing you to specify exactly 1 child node, and that child node could then have its own `Content`, resulting in a tree structure (visual tree).

<sub>XAML:</sub>
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Width="220" Height="100" Background="White">
    <CheckBox IsChecked="true">
        <TextBlock FontSize="9" Foreground="Black" Text="The CheckBox is the Window's Content, and this TextBlock is the CheckBox's Content" />
    </CheckBox>
</Window>
```

<sub>c#:</sub>
```c#
MGWindow Window1 = new(Desktop, 0, 0, 220, 100);
Window1.BackgroundBrush.NormalValue = SolidFillBrushes.White;
MGCheckBox CheckBox = new(Window1, true);
MGTextBlock Text = new(Window1, "The CheckBox is the Window's Content, and this TextBlock is the CheckBox's Content", Color.Black, 9);
CheckBox.SetContent(Text);
Window1.SetContent(CheckBox);
```

![window1](https://user-images.githubusercontent.com/9426230/209874328-fe94fc6f-d65e-42de-98bc-feb92151ac6a.png)

If you just want to use the window as a blank slate for your UI content, then set `MGWindow.WindowStyle=WindowStyle.None`. This will hide several of the window's graphics, such as hiding its title bar, setting its `Padding` and `BorderThickness` to `0`, setting its `Background` to `Transparent` etc.

<sub>XAML:</sub>
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Width="220" SizeToContent="Height" WindowStyle="None">
    <CheckBox IsChecked="True">
        <TextBlock FontSize="9" Foreground="Black" Text="The CheckBox is the Window's Content, and this TextBlock is the CheckBox's Content" />
    </CheckBox>
</Window>
```

<sub>c#:</sub>
```c#
MGWindow Window1 = new(Desktop, 0, 0, 220, 100);
MGCheckBox CheckBox = new(Window1, true);
MGTextBlock Text = new(Window1, "The CheckBox is the Window's Content, and this TextBlock is the CheckBox's Content", Color.Black, 9);
CheckBox.SetContent(Text);
Window1.SetContent(CheckBox);
Window1.WindowStyle = WindowStyle.None;
Window1.ApplySizeToContent(SizeToContent.Height, 0, 0);
```
![window2](https://user-images.githubusercontent.com/9426230/209882901-2eee6eb1-951b-439c-838a-9d7b2115d742.png)

(The background of the Window is Transparent. In this example it's just using Color.CornflowerBlue as that's what the screen was cleared with before drawing the UI)

# Common Controls

A control (also commonly referred to as an 'element') generally just refers to any class that represents a visible object in your user interface.

The most commonly-used controls are:

| Control | Example | Purpose |
| ------- | ------- | ------- |
| `Border` | ![controls2](https://user-images.githubusercontent.com/9426230/210031830-6c10a071-8c6c-47ff-956b-7c78919670c0.png) | Acts as an outline for arbitrary content |
| `Button` | ![controls3](https://user-images.githubusercontent.com/9426230/210031971-69d914b8-df29-4a71-8e0b-0819b197b1cd.png) | Rectangular clickable shape that invokes some `Action` when clicked |
| `CheckBox` | ![controls4](https://user-images.githubusercontent.com/9426230/210032428-a884af4c-24ee-4ffd-b237-d30b6c2237e2.gif) | A 2-state button that cycles through checked and unchecked states when clicked<br>Unlike `ToggleButton`, the `Content` of a `CheckBox` is placed outside of the checkable button |
| `ComboBox` | ![controls5](https://user-images.githubusercontent.com/9426230/210032922-7db95e6e-9b34-417f-89e2-f154697399de.gif) | Sometimes called a 'Dropdown', 'Dropdown Box' Dropdown List' etc,<br>allows user to choose 1 value from a predefined list of values.<br>Value choices are displayed in a floating window that is contextually visible. |
| `Image` | ![controls6](https://user-images.githubusercontent.com/9426230/210033036-e4790da3-b971-4a9c-8ac5-3e60b0da2fcd.png) | Draws a `Texture2D` |
| `RadioButton` | ![controls6](https://user-images.githubusercontent.com/9426230/210033365-4afe45ef-5840-4306-b868-d8624516c019.gif) | A 2-state button (like a `CheckBox`) that allows mutual exclusion.<br>Several `RadioButtons` are added to a `RadioButtonGroup` so that only 1 `RadioButton` may be checked at a time |
| `ScrollViewer` | ![controls7](https://user-images.githubusercontent.com/9426230/210033870-c2fbc2b1-a3f4-4c3e-86f0-0bb434e7a4dd.gif) | Enables vertical and/or horizontal scrollbars around content that might require more space than is available |
| `Slider` | ![controls8](https://user-images.githubusercontent.com/9426230/210034127-3481f00a-856b-4316-89ae-ee757b164756.gif) | Draggable number-line to allow choosing a numeric value |
| `TabControl` | ![controls9](https://user-images.githubusercontent.com/9426230/210043508-e6e849e6-b1f9-472b-b545-a0703b2b1028.gif) | Hosts 0 to many `TabItems` |
| `TabItem` | | A single tab within a `TabControl` |
| `TextBlock` | ![controls10](https://user-images.githubusercontent.com/9426230/210043511-6b206a18-2e41-4b0b-8cb4-37a961e8bee6.png) | Renders Text content<br>[Supported markdown can be found here](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting) |
| `TextBox` | ![controls11](https://user-images.githubusercontent.com/9426230/210043976-f79c4211-bb36-4219-a02f-e57f324921b0.gif) | Allows user to input a text value |
| `ToggleButton` | ![controls12](https://user-images.githubusercontent.com/9426230/210044626-b24962cf-2d7d-479d-aff0-e1bbbec6d4b9.gif) | A 2-state button that cycles through checked and unchecked states when clicked<br>Unlike `CheckBox`, the `Content` of a `ToggleButton` is placed directly inside the checkable button |
| `ToolTip` | ![controls13](https://user-images.githubusercontent.com/9426230/210046818-e78a0158-87f7-437a-8690-79e79d368f67.gif) | Content that is attached to a parent, and is contextually visible when the parent is hovered by the mouse<br>`ToolTips` typically follow the mouse cursor (I.E. the top-left corner of the `ToolTip` is positioned where the mouse cursor is)
| `Window` | ![controls1](https://user-images.githubusercontent.com/9426230/210030693-c6039f52-bb25-47db-899e-6e8b03a9a31f.png) | The outermost control that other content is placed upon, like a canvas to paint with your UI |

# Box Model

MGUI controls _mostly_ adhere to the [Box Model](https://www.w3schools.com/css/css_boxmodel.asp) (except not all elements have a `Border`)

![box model1](https://user-images.githubusercontent.com/9426230/209903043-5e7d22e5-8858-4ed0-8bbb-bae3eb4d0244.png)

- `Margin` is empty space reserved outside of the bounds an element draws itself to
- `Padding` is empty space reserved inside the bounds an element draws itself to, but outside the bounds of the element's `Content`

For layout purposes (I.E. measuring how much space an element requires), an element's bounds is Margin+Border+Padding+Content. For rendering purposes (I.E. actually drawing the element), an element's bounds is Border+Padding+Content. The `Background` of an element spans Padding+Content.

Most elements, but not all, have a `Border` built-in to them. If you wish to have a `Border` around an element that doesn't have a built-in `Border`, just wrap it inside of a `Border`:

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Border BorderBrush="Turquoise" BorderThickness="2" Background="Gray">
        <CheckBox Content="CheckBoxes don't have a built-in Border" />
    </Border>
</Window>
```
![border1](https://user-images.githubusercontent.com/9426230/209903371-1b26a676-5f17-4bc5-9c3e-988626e8e4e6.png)

`Margin`, `Padding`, and `BorderThickness` are all of type: `Thickness`.<br>Thicknesses are commonly defined in XAML as a comma delimited string: "{_Left_}, {_Top_}, {_Right_}, {_Bottom_}", or "{_Left+Right_}, {_Top+Bottom_}", or "{_All_}"

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Border BorderBrush="Green" BorderThickness="4, 7, 2, 10" Background="White" Width="50" Height="50" />
</Window>
```
![border2](https://user-images.githubusercontent.com/9426230/209904055-b9a86781-6a35-4f72-baa6-cd54d0d928ed.png) Left is 4px, Top is 7px, Right is 2px, Bottom is 10px

# Size and Alignment

All elements expose these common properties for sizing:

| Type | Property | Description |
| ---- | -------- | ----------- |
| int? | `MinWidth` / `MinHeight` | Min Width/Height in pixels, does not include `Margin`. 0 if null |
| int? | `MaxWidth` / `MaxHeight` | Max Width/Height in pixels, does not include `Margin`. `int.MaxValue` if null |
| int? | `PreferredWidth` / `PreferredHeight` | Desired Width/Height in pixels, does not include `Margin`.<br>If null, element is dynamically sized to be just big enough to draw itself and its `Content`<br>This value is clamped to the range [`MinWidth`, `MaxWidth`] or [`MinHeight`, `MaxHeight`] when possible |
| int | `ActualWidth` / `ActualHeight` | Readonly. The actual Width/Height in pixels, does not include `Margin`.<br>This value isn't updated immediately when changing size-related properties.<br>It's updated the next time the layout of the element is recalculated, during an Update tick |

Elements can also opt into viewport-based caps with `ViewportFit`. This is intended for dialogs, overlays, popups, and other bounded UI that should remain usable on small windows or at high UI scale.

| Type | Property | Description |
| ---- | -------- | ----------- |
| `ViewportFitMode` | `ViewportFit` | Adds a measurement cap based on the current desktop valid screen bounds. Supported values are `None`, `Width`, `Height`, and `WidthAndHeight`. The cap is combined with authored `MaxWidth` / `MaxHeight`; it does not mutate authored size properties. |
| `Thickness` | `ViewportMargin` | A viewport inset in screen pixels. For example, `ViewportMargin="48"` reserves 48px on each side before the viewport cap is calculated. This is not authored UI spacing and is not scaled like `Margin` or `Padding`. |

All elements expose these common properties for alignment:

| Type | Property |
| ---- | -------- |
| HorizontalAlignment | `HorizontalAlignment` |
| HorizontalAlignment | `HorizontalContentAlignment` |
| VerticalAlignment | `VerticalAlignment` |
| VerticalAlignment | `VerticalContentAlignment` |

All alignment properties default to `Stretch`, meaning they will attempt to give as much space as possible to their child, and attempt to take up as much space as their parent offers.

## Understanding Alignments

Content alignments (`HorizontalContentAlignment` / `VerticalContentAlignment`) determine what space an element offers to its children.<br>Regular alignments (`HorizontalAlignment` / `VerticalAlignment`) determine what space an element consumes from what its parent offers.<br>Alignments are processed top-down, starting from the root-level `Window` element.

To understand how space is allocated, let's walk through a simple example. Suppose you have the following `Window`:
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Width="200" Height="100" 
        Background="Cyan" Padding="6" IsUserResizable="False" IsTitleBarVisible="False"
        BorderBrush="Gray" BorderThickness="2">
    <Border BorderBrush="Red" BorderThickness="2" Background="Green" Padding="12">
        <TextBlock Background="Orange" Text="Hello World" />
    </Border>
</Window>
```
![alignment1](https://user-images.githubusercontent.com/9426230/209905765-7419c6fb-8552-4b7d-91f1-50f5cd6f6702.png)

The `Window` is explicitly sized with `Width="200"`, `Height="100"`.<br>The `Window` reserves 6px of `Padding` on each side, and 2px of `BorderThickness` on each side, leaving 184x84 leftover space.<br>Since the `Window's` `VerticalContentAlignment` and `HorizontalContentAlignment` both default to `Stretch`, the `Window` attempts to give all this remaining space to its `Content` (The `Border`).

The `Border` has `VerticalAlignment` and `HorizontalAlignment` set to the default of `Stretch`, so the `Border` decides to consume all 184x84 space that its parent offers.<br>The `Border` reserves 12px `Padding` on each side, and 2px of `BorderThickness` on each side, leaving 156x56 leftover space.<br>Since the `Border's` `VerticalContentAlignment` and `HorizontalContentAlignment` both default to `Stretch`, the `Border` attempts to give all this remaining space to its `Content` (The `TextBlock`).

The `TextBlock` has `VerticalAlignment` and `HorizontalAlignment` set to the default of `Stretch`, so the `TextBlock` decides to consume all 156x56 space that its parent offers.

------------------

Suppose we set the `Border's` `VerticalAlignment=Center`:
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Width="200" Height="100" 
        Background="Cyan" Padding="6" IsUserResizable="False" IsTitleBarVisible="False"
        BorderBrush="Gray" BorderThickness="2">
    <Border VerticalAlignment="Center" BorderBrush="Red" BorderThickness="2" Background="Green" Padding="12">
        <TextBlock Background="Orange" Text="Hello World" />
    </Border>
</Window>
```
![alignment2](https://user-images.githubusercontent.com/9426230/209906586-2a84203c-324b-4f6e-bed8-67ca5315eab2.png)

Now the `Border` is still offered 184x84 by its parent (the `Window`), but decides to only consume 47px of Height because that's the minimum Height it needs to draw itself and its `Content`. Those 47px are taken from the center of the Rectangular bounding box it was offered.

-------------

Now try setting the `TextBlock's` `HorizontalAlignment=Right`:
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Left="200" Top="200" Width="200" Height="100" 
        Background="Cyan" Padding="6" IsUserResizable="False" IsTitleBarVisible="False"
        BorderBrush="Gray" BorderThickness="2">
    <Border VerticalAlignment="Center" BorderBrush="Red" BorderThickness="2" Background="Green" Padding="12">
        <TextBlock HorizontalAlignment="Right" Background="Orange" Text="Hello World" />
    </Border>
</Window>
```
![alignment3](https://user-images.githubusercontent.com/9426230/209906866-0aaec0d3-43f6-4329-8e2f-ac10396b3811.png)

What if we also set the `Border's` `HorizontalContentAlignment=Left`?:
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Left="200" Top="200" Width="200" Height="100" 
        Background="Cyan" Padding="6" IsUserResizable="False" IsTitleBarVisible="False"
        BorderBrush="Gray" BorderThickness="2">
    <Border VerticalAlignment="Center" HorizontalContentAlignment="Left" BorderBrush="Red" BorderThickness="2" Background="Green" Padding="12">
        <TextBlock HorizontalAlignment="Right" Background="Orange" Text="Hello World" />
    </Border>
</Window>
```
![alignment4](https://user-images.githubusercontent.com/9426230/209906978-f6c66ea4-f821-4ec0-93f8-8f241ab98360.png)

The `HorizontalContentAlignment` of the parent (`Border`) took precedence over the `HorizontalAlignment` of the child (`TextBlock`), so the child ends up aligned Left. In other words, the Horizontal positioning of the innermost child (`TextBlock`) is dependent on these properties, in this order:

1. `Window's` `HorizontalContentAlignment`
2. `Border's` `HorizontalAlignment`
3. `Border's` `HorizontalContentAlignment`
4. `TextBlock's` `HorizontalAlignment`

Because alignments are processed in top-down order.

# Containers

What if you wanted to put 2 `CheckBoxes` inside a `Window`? A `Window` can only have 1 element as its `Content`, so you'd need to wrap the `CheckBoxes` inside a container that supports multiple children.

Each container defines its own rules for how it arranges its children. 

## StackPanel

`StackPanels` arrange their children in order, either from Left to Right (`Orientation=Horizontal`) or Top to Bottom (`Orientation=Vertical`).

<sub>XAML:</sub>
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <StackPanel Orientation="Vertical" Background="MediumPurple">
        <CheckBox IsChecked="False">
            <TextBlock FontSize="9" Foreground="Black" Text="This CheckBox is unchecked" />
        </CheckBox>
        <CheckBox IsChecked="True">
            <TextBlock FontSize="9" Foreground="Black" Text="This CheckBox is checked" />
        </CheckBox>
    </StackPanel>
</Window>
```

![window3](https://user-images.githubusercontent.com/9426230/209884893-3903e27b-853b-45a6-9026-c55fa06ac73f.png)

`StackPanels` only allocate as much space as is requested to the children. They make no guarantee that the children will fill all of the `StackPanel's` available space.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <StackPanel Orientation="Horizontal" Background="Red" Width="300" Height="100">
        <Border Background="Green" Content="This element requests 150px" Width="150" />
        <Border Background="Orange" Content="This element requests 100px" Width="100" />
    </StackPanel>
</Window>
```
![window5](https://user-images.githubusercontent.com/9426230/209892002-c203e123-bff8-4c56-af35-d12fc9fe80c3.png)

<sub>StackPanel Width=300<br>
StackPanel Remaining Width to allocate=300<br>
1st child requests 150, receives 150<br>
StackPanel Remaining Width to allocate=300-150=150<br>
2nd child requests 100, receives 100<br>
StackPanel Remaining Width to allocate=150-100=50, no children receive this Width</sub>

If there isn't enough space for all the children, space is allocated first-come first-serve until it runs out.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <StackPanel Orientation="Horizontal" Background="Red" Width="220" Height="100">
        <Border Background="Green" Content="This element requests 150px" Width="150" />
        <Border Background="Orange" Content="This element requests 100px" Width="100" />
    </StackPanel>
</Window>
```
![window4](https://user-images.githubusercontent.com/9426230/209891855-025f2cc4-36d5-42ac-bd79-03b638907aa4.png)

<sub>StackPanel Width=220<br>
StackPanel Remaining Width to allocate=220<br>
1st child requests 150, receives 150<br>
StackPanel Remaining Width to allocate=220-150=70<br>
2nd child requests 100, receives 70</sub>

If you want a uniform padding between children, set `StackPanel.Spacing` to a non-zero value.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <StackPanel Orientation="Horizontal" Background="Red" Height="50" Spacing="20">
        <Border Background="Green" Content="1" Width="40" />
        <Border Background="Purple" Content="2" Width="40" />
        <Border Background="Brown" Content="3" Width="40" />
        <Border Background="Magenta" Content="4" Width="40" />
    </StackPanel>
</Window>
```
![window6](https://user-images.githubusercontent.com/9426230/209892313-6a3d6f66-0ac5-4db8-a49d-331dbc9b33d3.png)

## DockPanel

`DockPanels` arrange their children by 'docking' them to an edge (Left, Top, Right, or Bottom). Use the `Dock` property to specify which edge the child should be anchored to. The last child is given all the remaining space, effectively ignoring its `Dock` value.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <DockPanel Background="Red" Width="250" Height="120">
        <Border Dock="Left" Background="Green" Content="Docked Left" />
        <Border Dock="Top" Background="Purple" Content="Docked Top" />
        <Border Dock="Bottom" Background="Orange" Content="Docked Bottom but is last child, spans all remaining space" />
    </DockPanel>
</Window>
```
![dockpanel1](https://user-images.githubusercontent.com/9426230/209892656-3e111d86-3786-4c96-bda8-4bb253951cc3.png)

If you don't want the last child to fill all remaining space, then set `LastChildFill=false`:
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <DockPanel Background="Red" Width="250" Height="120" LastChildFill="false">
        <Border Dock="Left" Background="Green" Content="Docked Left" />
        <Border Dock="Top" Background="Purple" Content="Docked Top" />
        <Border Dock="Bottom" Background="Orange" Content="Docked Bottom" />
    </DockPanel>
</Window>
```
![dockpanel4](https://user-images.githubusercontent.com/9426230/209893562-21af6870-0780-4464-a574-5285b54d908c.png)

You can dock multiple children to the same edge:

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <DockPanel Background="Red" Width="250" Height="120">
        <Border Dock="Left" Background="Green" Content="Docked Left" />
        <Border Dock="Top" Background="Purple" Content="Docked Top #1" />
        <Border Dock="Top" Background="Magenta" Content="Docked Top #2" />
        <Border Dock="Bottom" Background="Orange" Content="Last child" />
    </DockPanel>
</Window>
```
![dockpanel2](https://user-images.githubusercontent.com/9426230/209892819-1af6bd34-df53-4e88-8f6a-516b66f55dfa.png)

The space is allocated inwards. The first child to be docked to the edge will be closer to the outermost edge of the entire `DockPanel`. Space is also allocated in first-come first-serve order. Try docking multiple children to the same edge, but don't add those children to the `DockPanel` consecutively:

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <DockPanel Background="Red" Width="250" Height="120">
        <Border Dock="Top" Background="Purple" Content="Docked Top #1" />
        <Border Dock="Left" Background="Green" Content="Docked Left" />
        <Border Dock="Top" Background="Magenta" Content="Docked Top #2" />
        <Border Dock="Bottom" Background="Orange" Content="Last child" />
    </DockPanel>
</Window>
```
![dockpanel3](https://user-images.githubusercontent.com/9426230/209893026-afc37366-218d-4201-9a84-ccfbe5263e05.png)

<sub>The Purple Border is docked to the top first, receiving the remaining unallocated width of the `DockPanel`, and just as much height as it needed.<br>
Then the Green Border is docked to the left, receiving the remaining unallocated height of the `DockPanel`, and just as much width as it needed.<br>
Then the Magenta Border is docked to the top, receiving the remaining unallocated width of the `DockPanel`, and just as much height as it needed.<br>
Then the Orange Border fills all remaining unallocated width and height of the `DockPanel`, because it is the last child.</sub>

## OverlayPanel

`OverlayPanels` arrange their children on top of each other. Children are drawn in ascending order of their `ZIndex` values, or in the order they were added to the `OverlayPanel` if no `ZIndex` is specified.

(Tip: You can easily specify Alpha transparency in XAML by multiplying a color by a decimal value, such as `Background="Purple * 0.7"`)

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <OverlayPanel Background="White" Width="200" Height="60">
        <Border Background="Orange" VerticalAlignment="Bottom" Content="First child" />
        <Border Background="Purple * 0.7" VerticalAlignment="Stretch" Content="Second child" />
    </OverlayPanel>
</Window>
```
![overlay1](https://user-images.githubusercontent.com/9426230/209893992-ab2dd480-fb27-46cc-93de-ff7d5d65a490.png)

If we swapped the order of the children, we'd get:

![overlay2](https://user-images.githubusercontent.com/9426230/209894104-0440c4e7-b8d3-479d-b980-00e5bad0ca9a.png)

You can also specify an `Offset` to apply to the children (Default value = `"0, 0, 0, 0"` (Left, Top, Right, Bottom))

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <OverlayPanel Background="Red" Width="200" Height="50">
        <Border Background="Purple" VerticalAlignment="Stretch" Content="First child" Offset="15, 3, 6, 25" />
        <Border Background="Orange" VerticalAlignment="Bottom" Content="Second child" Offset="5, 0, 12, 10" />
    </OverlayPanel>
</Window>
```
![overlay3](https://user-images.githubusercontent.com/9426230/209894320-e686b8bc-9132-40cf-8f02-a77d6f71bf03.png)

If you want more control over the ordering of the children, you can specify a `ZIndex` value on each child.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <OverlayPanel Background="Red" Width="200" Height="50">
        <Border Background="Purple" Content="First child" Offset="15, 3, 62, 25" ZIndex="0.01" />
        <Border Background="Orange" Content="Second child" Offset="5, 0, 50, 10" ZIndex="10" VerticalAlignment="Bottom" />
        <Border Background="Green" Content="Third child" Offset="120, 10, 4, 18" ZIndex="-10" />
    </OverlayPanel>
</Window>
```

![overlay4](https://user-images.githubusercontent.com/9426230/210911988-33365981-e370-43e3-ac6e-e1ad3ebc20b2.png)

Even though the Green `Border` is added last, it appears underneath the other children because it has the lowest `ZIndex` value. (Children without a `ZIndex` are rendered first. If multiple children have the same `ZIndex`, ordering is based on the order the child was added to the panel)

## Grid

`Grids` arrange their children according to the row+column (cell) they're placed in.<br>
[More details available here](https://github.com/Videogamers0/MGUI/wiki/Grids)



TODO: Other containers such as HeaderedContentPresenter and UniformGrid.<br>
More documentation coming soon... probably...
