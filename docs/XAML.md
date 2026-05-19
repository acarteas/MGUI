# Getting Started

All MGUI-related controls can be created and manipulated through c# code, but XAML may be preferable as it's often more concise. XAML strings can be parsed into `MGElement` instances via [MGUI.Core.UI.XAML.XAMLParser](https://github.com/Videogamers0/MGUI/blob/master/MGUI.Core/UI/XAML/XAMLParser.cs)'s `Load<T>(...)` and `LoadRootWindow(...)` static methods

Recommended usage:
- Add a .xaml file to your project, with `Build Action` = `Embedded Resource`
- In your c# code:
  - Read the embedded resource into a string
  - Call `MGUI.Core.UI.XAML.XAMLParser.LoadRootWindow(...)` or `MGUI.Core.UI.XAML.XAMLParser.Load<T>(...)`

```c#
public static string ReadEmbeddedResourceAsString(Assembly CurrentAssembly, string ResourceName)
{
    using (Stream ResourceStream = CurrentAssembly.GetManifestResourceStream(ResourceName))
    using (StreamReader Reader = new StreamReader(ResourceStream))
    return Reader.ReadToEnd();
}

...

//  Parse the XAML into an MGWindow instance
MGDesktop Desktop = ...
Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
string ResourceName = $"{nameof(MyProject)}.{nameof(XAMLResources)}.Window1.xaml"; // Fill this in with your resource name
string XAMLString = ReadEmbeddedResourceAsString(CurrentAssembly, ResourceName);
MGWindow Window = XAMLParser.LoadRootWindow(Desktop, XAMLString, false);
```

Sample XAML:
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" 
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Left="100" Top="100" Width="200" Height="100"
        BorderBrush="Crimson" BorderThickness="2" Background="White" TextForeground="Black" 
        Padding="10" TitleText="XAML Test" IsUserResizable="False">
    <TextBlock Text="Hello World" Padding="10" Background="LightGray" TextAlignment="Center" />
</Window>
```

Result:

![MGUI_XAML1](https://user-images.githubusercontent.com/9426230/202819782-f35c7c54-0717-4833-8532-ec7807917f59.png)

Assuming you set the default XML namespace to `xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"`, Visual Studio's XAML editor can provide autocomplete suggestions.

![MGUI_XAML_Autocomplete1](https://user-images.githubusercontent.com/9426230/202871837-67650db2-4a7f-4730-91a0-2dfba32ce787.png)

A few common properties have optional abbreviated property names:

| Property | Abbreviation |
| -------- | --------------- |
| HorizontalAlignment | HA |
| HorizontalContentAlignment | HCA |
| VerticalAlignment | VA |
| VerticalContentAlignment | VCA |
| Background | BG |
| BorderBrush | BB |
| BorderThickness | BT |

# Referencing XAML Elements in c#

Everything that can be done in XAML can also be done in c#. But not everything that can be done in c# can be done in XAML. Oftentimes you will need to apply a `Name` to an element defined in XAML.
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" 
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Left="100" Top="100" Width="200" Height="100"
        IsTitleBarVisible="False" IsUserResizable="False" Padding="0" BorderThickness="0">
    <Button Name="Button_Sample1" Content="Click me" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" />
</Window>
```
Then you can retrieve the element in your c# code using `MGWindow.GetElementByName<T>(string)` instance method:
```c#
MGWindow Window1 = XAMLParser.LoadRootWindow(Desktop, XAMLString, false);
MGButton Button_Sample1 = Window1.GetElementByName<MGButton>("Button_Sample1");
Button_Sample1.AddCommandHandler((button, e) =>
{
    //  This delegate is invoked when the button is clicked
    button.SetContent("Clicked");
});
```

# Generic Types

Some `MGElement` types have a generic type parameter, such as `MGComboBox<TItemType`, `MGListBox<TItemType>`, and `MGListView<TItemType>`. To specify a generic type in XAML, use the [x:Type Markup Extension](https://learn.microsoft.com/en-us/dotnet/desktop/xaml-services/xtype-markup-extension). For example, suppose you have a simple `Person` object:
```c#
public readonly record struct Person(int Id, string FirstName, string LastName, bool IsMale);
```
And you want to use this type for an `MGListView<TItemType>`'s `ItemsSource`.
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" 
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Left="100" Top="100" Width="300" Height="300"
        TitleText="ListView Sample">

    <!-- Define the namespace where the Person object exists, then set ItemType using the x:Type markup extension -->
    <ListView Name="ListView_Sample1" xmlns:local="clr-namespace:MGUI.Samples;assembly=MGUI.Samples" ItemType="{x:Type local:Person}">

        <ListViewColumn Width="50">
            <ListViewColumn.Header>
                <TextBlock IsBold="True" TextAlignment="Center" Text="Id" />
            </ListViewColumn.Header>
        </ListViewColumn>

        <ListViewColumn Width="*">
            <ListViewColumn.Header>
                <TextBlock IsBold="True" TextAlignment="Center" Text="First Name" />
            </ListViewColumn.Header>
        </ListViewColumn>

        <ListViewColumn Width="*">
            <ListViewColumn.Header>
                <TextBlock IsBold="True" TextAlignment="Center" Text="Last Name" />
            </ListViewColumn.Header>
        </ListViewColumn>
    </ListView>
</Window>
```
Then in your c# code:
```c#
MGWindow Window1 = XAMLParser.LoadRootWindow(Desktop, XAMLString, false);

//  Get the ListView
MGListView<Person> ListView_Sample1 = Window1.GetElementByName<MGListView<Person>>("ListView_Sample1");

//  Set the ItemTemplate of each column
ListView_Sample1.Columns[0].ItemTemplate = (person) => new MGTextBlock(Window1, person.Id.ToString()) { HorizontalAlignment = HorizontalAlignment.Center };
ListView_Sample1.Columns[1].ItemTemplate = (person) => new MGTextBlock(Window1, person.FirstName, person.IsMale ? Color.Blue : Color.Pink);
ListView_Sample1.Columns[2].ItemTemplate = (person) => new MGTextBlock(Window1, person.LastName, person.IsMale ? Color.Blue : Color.Pink);

//  Set the row data
List<Person> People = new()
{
    new(1, "John", "Smith", true),
    new(2, "James", "Johnson", true),
    new(3, "Emily", "Doe", false),
    new(4, "Chris", "Brown", true),
    new(5, "Melissa", "Wilson", false),
    new(6, "Richard", "Anderson", true),
    new(7, "Taylor", "Moore", false),
    new(8, "Tom", "Lee", true),
    new(9, "Joe", "White", true),
    new(10, "Alice", "Wright", false)
};
ListView_Sample1.SetItemsSource(People);
```

Result:

![MGUI_XAML_ListView1](https://user-images.githubusercontent.com/9426230/202834741-81d48833-f34d-4528-996a-7f80e661d5b1.png)

# Styles

Styles allow you to conveniently set properties of all child elements to a particular value.
```xaml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Left="50" Top="50" Width="200" Height="150" IsUserResizable="False" TitleText="Styles Sample">
    
    <StackPanel Orientation="Vertical" Spacing="5">
        <StackPanel.Styles>
            <!-- This is an implicit style. Since it doesn't have a Name, it will affect ALL child TextBlocks 
            (unless the TextBlock explicitly opts out of styling by setting IsStyleable="false") -->
            <Style TargetType="TextBlock">
                <Setter Property="HorizontalAlignment" Value="Right" />
            </Style>
        </StackPanel.Styles>

        <!-- This textblock ignores all styles because IsStyleable="false" -->
        <!-- Its HorizontalAlignment remains at the default value of "Stretch" -->
        <TextBlock Background="RGB(40,40,40)" Text="One" IsStyleable="False" />
        
        <!-- This textblock inherits HorizontalAlignment="Right" from the implicit style defined above -->
        <TextBlock Background="RGB(40,40,40)" Text="Two" />

        <StackPanel Orientation="Vertical">
            <!-- These styles have an explicit name. They will only affect TextBlocks where StyleNames contains the name -->
            <StackPanel.Styles>
                <Style TargetType="TextBlock" Name="Explicit1">
                    <Setter Property="HorizontalAlignment" Value="Center" />
                    <Setter Property="Foreground" Value="Red" />
                </Style>
                <Style TargetType="TextBlock" Name="Explicit2">
                    <Setter Property="HorizontalAlignment" Value="Left" />
                </Style>
            </StackPanel.Styles>

            <!-- This TextBlock is affected by the implicit style and the "Explicit1" style, in that order. -->
            <!-- So HorizontalAlignment="Right" is applied first, then HorizontalAlignment="Center" and Foreground="Red" are applied -->
            <TextBlock Background="RGB(40,40,40)" Text="Three" StyleNames="Explicit1" />
            
            <!-- This TextBlock uses a comma delimiter to specify multiple explicit style names that are applied to it. -->
            <!-- The "Explicit1" style sets Foreground="Red", but that value is overridden with Foreground="Orange" -->
            <TextBlock Background="RGB(40,40,40)" Text="Four" StyleNames="Explicit1,Explicit2" Foreground="Orange" />
            
            <!-- This TextBlock didn't opt in to either Explicit style, so it's only affected by the outer implicit style, setting HorizontalAlignment="Right" -->
            <TextBlock Background="RGB(40,40,40)" Text="Five" Foreground="Blue" />
        </StackPanel>
    </StackPanel>
</Window>
```
Result:

![MGUI_XAML_Styles1](https://user-images.githubusercontent.com/9426230/202835448-f81e6e05-9f9b-42e9-b87a-1afbe6bef545.png)

- Styles must have a `TargetType` (`Button`, `TextBlock`, `Image`, `CheckBox` etc)
- They may also have a `Name`
  - If a `Name` is not specified, the style is **implicit**. It will be applied to all child elements of the given `TargetType`
  - If a `Name` is specified, the style is **explicit**. It will only be applied to child elements of the given `TargetType` that also opt in to receiving the style via the `StyleNames` property
  - Elements can opt in to multiple explicit styles by specifying several style names, separated by a comma. The styles are applied in the order their names are listed.

# Type Converters

### Color

To specify a Color in XAML, use one of these formats:

| Format | Example | Result |
| ------ | ------- | ------ |
| Name | LightCoral | ![LightCoral](https://placehold.co/15x15/f03c15/f03c15.png) |
| Hex | #F08080 | ![LightCoral](https://placehold.co/15x15/f03c15/f03c15.png) |
| RGB | RGB(240, 128, 128) | ![LightCoral](https://placehold.co/15x15/f03c15/f03c15.png) |
| RGBA | RGBA(240, 128, 128, 255) | ![LightCoral](https://placehold.co/15x15/f03c15/f03c15.png) |

You can also apply an opacity scalar, such as `Red * 0.8` (Spaces are not required. `Red*0.8` is also valid)

Example:
```xaml
<TextBlock Background="rgb(240, 128, 128)" Foreground="Cyan * 0.5" />
```
(Values are case-insensitive except the HTML Color Name)

### IFillBrush

Many elements have `IFillBrush` properties. 

If you specify a single color, it will be converted to an `MGSolidFillBrush`.
```xaml
<Button Background="Blue" Padding="10" BorderBrush="Red" BorderThickness="2">
    <TextBlock IsBold="true" FontSize="14" TextAlignment="Center" Text="Hello\nWorld" />
</Button>
```
![MGUI_XAML_SolidFillBrush1](https://user-images.githubusercontent.com/9426230/202835966-69daa975-2085-442c-a406-632f4fa5730e.png)

If you specify 2 colors, delimited by a pipe `|` character, it will be converted to an `MGDiagonalGradientBrush`.
```xaml
<Button Background="Blue|Yellow" Padding="10" BorderBrush="Red" BorderThickness="2">
    <TextBlock IsBold="true" FontSize="14" TextAlignment="Center" Text="Hello\nWorld" />
</Button>
```
![MGUI_XAML_DiagonalGradientBrush1](https://user-images.githubusercontent.com/9426230/202835996-60dff189-6e7d-43ae-8908-1a5a0cd92702.png)

If you specify 4 colors, delimited by a pipe `|` character, it will be converted to an `MGGradientFillBrush`. (Topleft, Topright, Bottomright, Bottomleft)
```xaml
<Button Background="Blue|Yellow|Green|Orange" Padding="10" BorderBrush="Red" BorderThickness="2">
    <TextBlock IsBold="true" FontSize="14" TextAlignment="Center" Text="Hello\nWorld" />
</Button>
```
![MGUI_XAML_GradientBrush1](https://user-images.githubusercontent.com/9426230/202836054-12fba6f0-da51-4a63-84f1-ce53600a335f.png)

### IBorderBrush

Many elements have a `BorderBrush` property of type `IBorderBrush`.

If you specify a single `IFillBrush`, it will be converted to an `MGUniformBorderBrush` where each edge is comprised of the same `IFillBrush`.
```xaml
<Button BorderBrush="rgb(240,50,150)" BorderThickness="10" Padding="10">
    <TextBlock IsBold="true" FontSize="14" TextAlignment="Center" Text="Hello\nWorld" />
</Button>
```
![MGUI_XAML_UniformBorderBrush1](https://user-images.githubusercontent.com/9426230/202836234-af4ce25b-77c7-4366-a554-b7d910a19517.png)

If you specify 4 `IFillBrush`es, separated by a hyphen `-` character, it will be converted to an `MGDockedBorderBrush` (Left, Top, Right, Bottom).
```xaml
<Button BorderBrush="LightGreen-DarkGreen-ForestGreen-GreenYellow" BorderThickness="10" Padding="10">
    <TextBlock IsBold="true" FontSize="14" TextAlignment="Center" Text="Hello\nWorld" />
</Button>
```
![MGUI_XAML_DockedBorderBrush1](https://user-images.githubusercontent.com/9426230/202836335-850f8ee5-ea91-4605-ac53-468aee9db1c4.png)

Note: If all 4 `IFillBrushes` are `MGSolidColorBrush`, then the corners of the border will be split diagonally, as seen in the above image.

You don't need to use solid color brushes. If you'd like, you can specify gradient fill brushes. Check out this abomination!
```xaml
<Button BorderBrush="Purple|Cyan-DarkGreen|White|RoyalBlue|rgb(0,128,240)-ForestGreen-GreenYellow|Gray" BorderThickness="10" Padding="10">
    <TextBlock IsBold="true" FontSize="14" TextAlignment="Center" Text="Hello\nWorld" />
</Button>
```
![MGUI_XAML_DockedBorderBrush2](https://user-images.githubusercontent.com/9426230/202836490-151bd95d-a6f3-4186-9453-aad79922dcbf.png)

Note: Since not all 4 `IFillBrushes` are `MGSolidColorBrush`, the corners aren't split diagonally. Instead, the top and bottom brushes occupy the corners.
