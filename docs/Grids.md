`Grids` are a layout container that arrange their children according to the row+column (cell) they're placed in. You must configure at least one `RowDefinition` and at least one `ColumnDefinition`. These definitions define sizing constraints that are used to arrange the cells' contents. Then you must specify the `Row` and `Column` zero-based index that each child resides in.

# Definitions

`Grids` use a list of `RowDefinitions` and `ColumnDefinitions` to arrange its layout.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="White">
        <Grid.RowDefinitions>
            <RowDefinition Length="45px" />
            <RowDefinition Length="75px" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Length="100px" />
            <ColumnDefinition Length="80px" />
        </Grid.ColumnDefinitions>

        <Border Row="0" Column="0" Background="Red" Content="Content of Cell=(0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of Cell=(0,1)" />
        <Border Row="1" Column="0" Background="Green" Content="Content of Cell=(1,0)" />
        <Border Row="1" Column="1" Background="MediumPurple" Content="Content of Cell=(1,1)" />
    </Grid>
</Window>
```
![grid1](https://user-images.githubusercontent.com/9426230/212405045-8f242c47-2247-4e4e-935e-2f08946e4c8f.png)

Each `RowDefinition` or `ColumnDefinition` must have a `Length` value. `Lengths` can be defined using one of these 3 formats:

| Length Type | Examples | Description |
| ----------- | -------- | ----------- |
| `Auto` | `Length="Auto"` | _Auto_-sized definitions will compute the minimally-required size needed to display all the contents within that row or column.<br><br>For a `RowDefinition`, the `Grid` measures all children within that row, and sets the Row's Height to the maximum Height of those children. For a `ColumnDefinition`, the `Grid` measures all children within that column, and sets the Column's Width to the maximum Width of those children. |
| `Pixel` | `Length="100px"`<br>`Length="45"` | _Pixel_-sized definitions are explicitly sized by a given number of pixels. Pixels must be a positive integer value, optionally suffixed with `px` |
| `Weighted` | `Length="*"`<br>`Length="0.4*"` | _Weighted_-sized definitions consume a percentage of the grid's available space.<br><br>If you have multiple weighted definitions, they will each receive a proportional percentage of the remaining space.<br>**Example**: Row1's `Length` is `0.8*`, Row2's `Length` is `1.3*`. Row1 will receive 0.8/(0.8+1.3)=38.1% of the space, Row2 will receive 1.3/(0.8+1.3)=61.9%<br><br>`Weighted` definitions are allocated space _after_ `Auto` and `Pixel` sized definitions. |

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="White" Height="300" Width="450">
        <Grid.RowDefinitions>
            <RowDefinition Length="Auto" />
            <RowDefinition Length="100px" />
            <RowDefinition Length="0.65*" />
            <RowDefinition Length="0.35*" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Length="Auto" />
            <ColumnDefinition Length="80" />
            <ColumnDefinition Length="*" />
        </Grid.ColumnDefinitions>

        <Border Row="0" Column="0" Background="Red" Content="(0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="(0,1)" />
        <Border Row="0" Column="2" Background="Green" Content="(0,2)" />
        
        <Border Row="1" Column="0" Background="MediumPurple" Content="(1,0)" />
        <Border Row="1" Column="1" Background="Purple" Content="(1,1)" />
        <Border Row="1" Column="2" Background="LightGray" Content="(1,2)" />
        
        <Border Row="2" Column="0" Background="Magenta" Content="(2,0)" />
        <Border Row="2" Column="1" Background="Cyan" Content="(2,1)" />
        <Border Row="2" Column="2" Background="Navy" Content="(2,2)" />
        
        <Border Row="3" Column="0" Background="Gold" Content="(3,0)" />
        <Border Row="3" Column="1" Background="Crimson" Content="(3,1)" />
        <Border Row="3" Column="2" Background="Coral" Content="(3,2)" />
    </Grid>
</Window>
```
![grid2](https://user-images.githubusercontent.com/9426230/212409905-fec1cde1-0ac2-4b91-bebf-7392a4b8fe4f.png)

## Size Constraints

You may specify minimum/maximum constraints on each `RowDefinition` or `ColumnDefinition`.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="White" Height="300" Width="200">
        <Grid.RowDefinitions>
            <RowDefinition Length="*" MaxHeight="70" />
            <RowDefinition Length="*" />
            <RowDefinition Length="*" MinHeight="190" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Length="*" />
        </Grid.ColumnDefinitions>

        <Border Row="0" Column="0" Background="Red" Content="(0,0)" />
        <Border Row="1" Column="0" Background="Orange" Content="(0,1)" />
        <Border Row="2" Column="0" Background="Magenta" Content="(0,2)" />
    </Grid>
</Window>
```
![grid3](https://user-images.githubusercontent.com/9426230/212412761-15ba3c47-987c-4caf-b434-dffc134a1190.png)

The `Grid` is explicitly set to `Height="300"`. It contains 3 `RowDefinitions`, each requesting 1/3 of the height. Normally, each of these rows would receive 1/3*300=100px of height. But because there are `MinHeight` and `MaxHeight` constraints, the calculation changes as follows:

- (There are currently **300px** of remaining unallocated Height, and the sum of the weights is currently **3.0**)
- `RowDefinitions` with a `MaxHeight` are processed first (because if the Row's Height is truncated, it frees up more Height for the next Row to consume) 
  - This row is processed: `<RowDefinition Length="*" MaxHeight="70" />`
  - It receives `Math.Clamp(1.0 / 3.0 * 300, 0, 70)` = 70px.
- (There are now 300-70=**230px** of remaining unallocated Height, and the sum of the remaining weights is now **2.0**)
- `RowDefinitions` with a `MinHeight` are processed next (because if the Row's Height is increased to it's `MinHeight`, it consumes more Height than usual, leaving less leftover Height for the next `Weighted` Rows to consume) 
  - This row is processed: `<RowDefinition Length="*" MinHeight="190" />`
  - It receives `Math.Clamp(1.0 / 2.0 * 230, 190, int.MaxValue)` = 190px.
- (There are now 230-190=**40px** of remaining unallocated Height, and the sum of the remaining weights is now **1.0**)
- `RowDefinitions` without a `MinHeight` nor a `MaxHeight` are processed last
  - This row is processed: `<RowDefinition Length="*" />`
  - It receives `Math.Clamp(1.0 / 1.0 * 40, 0, int.MaxValue)` = 40px.

## Alternative Syntax

For convenience, you can also specify several `RowDefinitions` or `ColumnDefinitions` using a single string value that is parsed to a list of values, by utilizing the `RowLengths` and `ColumnLengths` string properties instead of the `RowDefinitions` and `ColumnDefinitions` List properties.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="White" Height="300" Width="200" RowLengths="*[,70],*,*[190,]" ColumnLengths="*">
        <Border Row="0" Column="0" Background="Red" Content="(0,0)" />
        <Border Row="1" Column="0" Background="Orange" Content="(0,1)" />
        <Border Row="2" Column="0" Background="Magenta" Content="(0,2)" />
    </Grid>
</Window>
```

Values are delimited by a comma `,`, and can optionally contain Minimum/Maximum size constraints inside Brackets `[` `]`<br>
- `RowLengths="*[,70],*,*[190,]"` parses to 3 `RowDefinitions`:
  - `*[,70]`
    - `Length="*"`, `MinHeight=null`, `MaxHeight="70"`
  - `*`
    - `Length="*"`, `MinHeight=null`, `MaxHeight=null`
  - `*[190,]`
    - `Length="*"`, `MinHeight="190"`, `MaxHeight=null`

Complex example: `ColumnLengths="Auto[20,50],1.2*[,200],16px,60,*,1.5*[80,300]"`

## RowSpan / ColumnSpan

Use the `RowSpan` or `ColumnSpan` properties to allow a child to span multiple cells.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="LightGray" RowLengths="70,60" ColumnLengths="140,140">
        <Border Row="0" Column="0" Background="Red" Content="Content of (0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of (0,1)" />
        <Border Row="1" Column="0" Background="Magenta" Content="Content of (1,0)" />
        <Border Row="1" Column="1" Background="MediumPurple" Content="Content of (1,1)" />
        
        <Border Row="0" Column="0" ColumnSpan="2" Background="LightBlue" Opacity="0.8" Content="Stretches (0,0) to (0,1), centered" HorizontalAlignment="Center" VerticalAlignment="Center" />
        <Border Row="1" Column="0" ColumnSpan="2" Background="Green" Opacity="0.8" Content="Stretches (1,0) to (1,1), aligned bottom" VerticalAlignment="Bottom" Margin="0,0,0,5" />
        <Border Row="0" Column="0" RowSpan="2" ColumnSpan="2" Background="Blue" Opacity="0.8" Content="Stretches (0,0) to (1,1), centered" 
                HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Grid>
</Window>
```
![grid5](https://user-images.githubusercontent.com/9426230/212438000-459efd7d-3cc6-4c9a-b971-824a0e251e7e.png)

Warning - If a child spans multiple cells, the `Grid` measurement logic still treats it as if it only exists in a single cell. This may cause unexpected results if your `RowDefinitions` or `ColumnDefinitions` are using `Length="Auto"`.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="LightGray" RowLengths="50" ColumnLengths="Auto,Auto">
        <Border Row="0" Column="0" Background="Red" Content="Content of (0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of (0,1)" />
        <Border Row="0" Column="0" ColumnSpan="2" Background="MediumPurple" Content="Stretches from (0,0) to (0,1)" VerticalAlignment="Bottom" Margin="5" />
    </Grid>
</Window>
```
![grid6](https://user-images.githubusercontent.com/9426230/212438580-edb9ec0e-7d5f-4964-a22b-eaa5abef2157.png)

As a workaround, consider setting `GridAffectsMeasure="false"` on the child that spans multiple cells:

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="LightGray" RowLengths="50" ColumnLengths="Auto,Auto">
        <Border Row="0" Column="0" Background="Red" Content="Content of (0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of (0,1)" />
        <Border Row="0" Column="0" ColumnSpan="2" GridAffectsMeasure="False" Background="MediumPurple" Content="Stretches from (0,0) to (0,1)" VerticalAlignment="Bottom" Margin="5" />
    </Grid>
</Window>
```
![grid7](https://user-images.githubusercontent.com/9426230/212438759-b28ed435-f2eb-4368-ae8b-85fbf8189a53.png)

# Rendering Order

You can add multiple children to the same cell. The children are drawn in the order they were added, so children added last appear on top.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="LightGray" Padding="5" RowLengths="Auto" ColumnLengths="Auto">
        <Border Row="0" Column="0" Background="Red" Content="First Child of (0,0)" Margin="0,0,12,12" />
        <Border Row="0" Column="0" Background="LightBlue * 0.85" Content="Second Child of (0,0)" Margin="12,12,0,0" />
    </Grid>
</Window>
```
![grid4](https://user-images.githubusercontent.com/9426230/212436575-edc9f9f5-363e-42c0-8637-e828541ebaf1.png)

# Cell Spacing

Use the `RowSpacing` and `ColumnSpacing` properties to apply padding between each row or column.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="LightGray" Padding="2" RowLengths="45,75,60" ColumnLengths="100,80" RowSpacing="8" ColumnSpacing="20">
        <Border Row="0" Column="0" Background="Red" Content="Content of Cell=(0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of Cell=(0,1)" />
        <Border Row="1" Column="0" Background="Green" Content="Content of Cell=(1,0)" />
        <Border Row="1" Column="1" Background="MediumPurple" Content="Content of Cell=(1,1)" />
        <Border Row="2" Column="0" Background="Magenta" Content="Content of Cell=(2,0)" />
        <Border Row="2" Column="1" Background="Coral" Content="Content of Cell=(2,1)" />
    </Grid>
</Window>
```
![grid8](https://user-images.githubusercontent.com/9426230/212439266-bba63f19-42a8-4682-a52f-98e332cd7f9a.png)

# GridLines

Use the `GridLinesVisibility`, `GridLineIntersectionHandling`, `GridLineMargin`, `HorizontalGridLineBrush`, and `VerticalGridLineBrush` properties to manage the `Grid's` gridlines.

| Type | Property | Possible Values | Description |
| ---- | -------- | --------------- | ----------- |
| `GridLinesVisibility` | `GridLinesVisibility` | `None`, `InnerHorizontal`, `TopEdge`, `BottomEdge`, `InnerVertical`, `LeftEdge`, `RightEdge`, `All`, `AllHorizontal`, `AllVertical` | Determines which grid lines, if any, will be visible. This is a `Flags` enum, so you may use bitwise operators to combine them, such as `TopEdge \| BottomEdge \| LeftEdge` if you only wanted gridlines on certain locations. |
| `GridLineIntersection` | `GridLineIntersectionHandling` | `HorizontalThenVertical`, `VerticalThenHorizontal` | Determines the order in which grid lines are drawn, which affects how the intersection points of the gridlines will appear. |
| `int` | `GridLineMargin` | | Can be used to reserve a defined amount of empty space around the gridlines. You can also think of this as a margin around each cell in the `Grid` |
| `IFillBrush` | `HorizontalGridLineBrush` | | The brush used to draw horizontal gridlines. |
| `IFillBrush` | `VerticalGridLineBrush` | | The brush used to draw vertical gridlines. |

Example:

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="Cyan" Padding="2" RowLengths="45,75,60" ColumnLengths="100,80" RowSpacing="12" ColumnSpacing="12"
          GridLinesVisibility="All" GridLineMargin="3" GridLineIntersectionHandling="HorizontalThenVertical" HorizontalGridLineBrush="Purple" VerticalGridLineBrush="MediumPurple">
        <Border Row="0" Column="0" Background="Red" Content="Content of Cell=(0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of Cell=(0,1)" />
        <Border Row="1" Column="0" Background="Green" Content="Content of Cell=(1,0)" />
        <Border Row="1" Column="1" Background="MediumPurple" Content="Content of Cell=(1,1)" />
        <Border Row="2" Column="0" Background="Magenta" Content="Content of Cell=(2,0)" />
        <Border Row="2" Column="1" Background="Coral" Content="Content of Cell=(2,1)" />
    </Grid>
</Window>
```
![grid9](https://user-images.githubusercontent.com/9426230/212441185-154d09e2-1e43-41c0-8952-62ea163a8bd5.png)

The horizontal gridlines are 4px tall because `RowSpacing-GridLineMargin*2=4`.The vertical gridlines are 4px wide because `ColumnSpacing-GridLineMargin*2=4`. So the spacing properties define how much empty space is between consecutive rows or columns, and then the `GridLineMargin` is reserved on each edge of that space. All leftover space is used to fill in the gridline.

(Note: The `GridLineMargin` is not reserved along the outer edges of the `Grid`. If you want empty space along the outer edge, set `Grid.Padding` to a non-zero value instead.)

# Selection

Use the `SelectionMode`, `SelectionBackground`, `SelectionOverlay`, and `CanDeselectByClickingSelectedCell` properties to manage `Grid's` selection capabilities. Use `Grid.CurrentSelection.GetCells()` method to get the cells that are currently selected (`CurrentSelection` may be null).

| Type | Property | Values | Description |
| ---- | -------- | ------ | ----------- |
| `GridSelectionMode` | `SelectionMode` | `None`, `Row`, `Column`, `Cell` | Determines what type of selection the user may make by clicking within the `Grid`. |
| `IFillBrush` | `SelectionBackground` | | A brush that is drawn _underneath_ selected cell(s). The content of the cell is drawn after this brush. |
| `IFillBrush` | `SelectionOverlay` | | A brush that is drawn _overtop of_ selected cell(s). The content of the cell is drawn before this brush. This brush should typically have some transparency. |
| bool | `CanDeselectByClickingSelectedCell` | | If true, user may click on a selected cell to set the `Grid's` `CurrentSelection` back to null. |
| `GridSelection` | `CurrentSelection` | | Retrieves the current selection of the `Grid`, if any (can be null) |

Example: (`SelectionMode="Cell"`)

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Background="Cyan" Padding="2" RowLengths="45,75" ColumnLengths="100,80" RowSpacing="12" ColumnSpacing="12" SelectionMode="Cell" SelectionOverlay="Yellow * 0.5">
        <Border Row="0" Column="0" Background="Red" Content="Content of Cell=(0,0)" />
        <Border Row="0" Column="1" Background="Orange" Content="Content of Cell=(0,1)" />
        <Border Row="1" Column="0" Background="Green" Content="Content of Cell=(1,0)" />
        <Border Row="1" Column="1" Background="MediumPurple" Content="Content of Cell=(1,1)" />
    </Grid>
</Window>
```
![grid10](https://user-images.githubusercontent.com/9426230/212442628-e65d7e1b-1190-480e-94c6-2a4012b08f44.gif)

Try setting `SelectionMode` to `Row`:

![grid11](https://user-images.githubusercontent.com/9426230/212442763-095bf90f-2ca4-49b7-b675-2fb55df441a8.gif)

# GridSplitters

`GridSplitters` can be placed within a `Grid`, allowing the user to click and drag them to dynamically resize the `Grid's` rows or columns.

```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        MinHeight="0" SizeToContent="WidthAndHeight" WindowStyle="None">
    <Grid Width="300" Height="250" Background="LightGray" RowLengths="150[80,],10,*[80,]" ColumnLengths="*[80,],12,180[80,]">
        <Border Row="0" Column="0" Background="Red" Content="Content of Cell=(0,0)" />
        <Border Row="2" Column="0" Background="Magenta" Content="Content of Cell=(2,0)" />
        <Border Row="0" Column="2" Background="Orange" Content="Content of Cell=(0,2)" />
        <Border Row="2" Column="2" Background="MediumPurple" Content="Content of Cell=(2,2)" />

        <GridSplitter Row="1" Column="0" ColumnSpan="3" />
        <GridSplitter Row="0" Column="1" RowSpan="3" />
    </Grid>
</Window>
```
![grid12](https://user-images.githubusercontent.com/9426230/212445720-acd0337b-37ff-475b-a6df-ea0ffd588780.gif)


It's common to have vertical `GridSplitters` span multiple rows via `RowSpan`, or horizontal `GridSplitters` span multiple columns via `ColumnSpan`). Also notice that the resizing still respected the `MinHeight` and `MinWidth` values applied to the rows and columns in the above example.