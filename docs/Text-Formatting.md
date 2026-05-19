TextBlocks support the following markdown:

| Format | Remarks | Examples |
| ------ | ------- | -------- |
| [[Bold]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#bold) | | [Bold] |
| [[Italic]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#italic) | | [Italic] |
| [[Underline={height} {offset} {brush}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#underline) | height must be a positive integer<br>offset must be an integer (can be negative)<br>positive offset moves underline downwards<br>brush must be in the format [described here](https://github.com/Videogamers0/MGUI/wiki/XAML#ifillbrush)<br><br>all parameters are optional (defaults to 1px height, 0px offset, and same brush as the `TextBlock's` current `Foreground` color) | [Underline]<br>[Underline=4]<br>[Underline=4 -2]<br>[Underline=4 -2 Red]<br>[Underline=4 -2 Red\|Orange] |
| [[Opacity={value}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#opacity) | must be a positive float value.<br>1.0=fully opaque, 0.0=fully transparent | [Opacity=0.8] |
| [[Color={color}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#color) | color must be parsable via [ColorTranslator.FromHtml(...)](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.colortranslator.fromhtml?source=recommendations&view=net-7.0) | [Color=Red]<br>[Color=#daa520]<br>[Color=#ffdaa520] |
| [[Background={brush} {padding}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#background) | brush must be in the format [described here](https://github.com/Videogamers0/MGUI/wiki/XAML#ifillbrush)<br>padding is optional (Defaults to 0) | [Background=Red]<br>[Background=#daa520]<br>[Background=#ffdaa520]<br>[Background=Navy\|Blue 1,2,1,4] |
| [[Shadow={color} {xOffset} {yOffset}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#shadow) | color must be parsable via [ColorTranslator.FromHtml(...)](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.colortranslator.fromhtml?source=recommendations&view=net-7.0)<br>offsets must be integer values (can be negative)<br>positive Y offset moves shadow downwards<br>offsets are optional (Defaults to 1,1) | [Shadow=Red]<br>[Shadow=Red -1 1]<br>[Shadow=#ffdaa520 2 2] |
| [[Image={name} {width} {height}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#image) | name must exist in `MGResources.Textures`<br>dimensions must be positive integer values<br>dimensions are optional (defaults to entire size of referenced texture region)<br>dimensions can be separated by ` `, `,`, or `x` | [Image=ArrowRightGreen]<br>[Image=ArrowRightGreen 24 16]<br>[Image=ArrowRightGreen 24,16]<br>[Image=ArrowRightGreen 24x16] |
| [[ToolTip={name}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#tooltip) | name must exist in `MGWindow.NamedToolTips` | [ToolTip=PosionDescription] |
| [[Action={name}]](https://github.com/Videogamers0/MGUI/wiki/Text-Formatting#action) | name must exist in `MGResources.Commands` | [Action=OpenHomepage] |

Formatting codes are case-insensitive. [Italic] is equivalent to [iTaLiC]

--------------

Most values are stored on a stack, so the closing tag will revert to the previous value rather than reverting to the original default value.

```xaml
<TextBlock Background="LightGray" Padding="6,3" Foreground="Red"
           Text="Default (Red), [Color=Green]Now Green, [Color=Purple]Now Purple, [/Color] Back to Green, [/Color] Back to Default (Red)" />
```
![formatting stack](https://user-images.githubusercontent.com/9426230/209581345-1a8372f6-f11e-4fbf-9b30-cf0d85aaf909.png)

Foreground="Red" was explicitly specified on the TextBlock, so that is the default value.
The first `[/Color]` closing tag closes the most recent unclosed opening tag (`[Color=Purple]`).
The second `[/Color]` closing tag closes the `[Color=Green]` tag.

---------------

Bold and Italic tags are not stored on a stack.

```xaml
<TextBlock Background="LightGray" Padding="6,3" Foreground="Red"
           Text="Default (Not Bold), [Bold]Now Bold, [Bold]Still Bold, [/Bold]Back to Default (Not Bold) even though there were 2 open tags" />
```
![formatting stack2](https://user-images.githubusercontent.com/9426230/209581573-b4bbfaa3-3394-45a3-84bd-7e1f21f9b7ac.png)

### Bold

Code: `Bold`<br>
Abbreviations: `B`<br>
Example: `[Bold]This text is bold,[/b] But this text isn't. [b]This text is bold,[/bold] But this text isn't.`<br>
Remarks: Bold has no effect if the TextBlock is using a font that doesn't have a bold variant.

![bold](https://user-images.githubusercontent.com/9426230/209582463-736b652e-6be6-4185-814b-3714094b9efc.png)

### Italic

Code: `Italic`<br>
Abbreviations: `I`<br>
Example: `[Italic]This text is italic,[/i] But this text isn't. [i]This text is italic,[/italic] But this text isn't.`<br>
Remarks: Italic has no effect if the TextBlock is using a font that doesn't have an italic variant.

![italic](https://user-images.githubusercontent.com/9426230/209582523-23a74a52-aec3-4d2a-b4dc-21d012059a8a.png)

### Underline

Code: `Underline`<br>
Abbreviations: `U`<br>
Parameters:<br>
- [Optional] The underline's Height, in pixels (positive integer value, default=1)
- [Optional] Vertical offset, in pixels (integer value, can be negative, default=0). Positive value moves the underline downwards. A value of 0 means the underline will be drawn right at the bottom of the text it's anchored to.
- [Optional] The IFillBrush to fill the underline rectangular region with. Usually a single Color such as "Green" or "#FF00FF00" but you can use multiple colors to specify a gradient fill brush ([more info here](https://github.com/Videogamers0/MGUI/wiki/XAML#ifillbrush)). 
  - If not specified, the underline is drawn with the same color as the text it's anchored to.

Example: `[Underline]This text [Color=Purple]has an underline[/Color] using the same color as the text.\n[Underline=3 0 Orange]This text has a 3px tall Orange underline.\n[Underline=3 -2 Green]Now it's [fg=Green]Green[/fg] and offsetted up by 2px.\n[/u]Now it's reverted to previous underline settings.[/u][/u]\nThis text has no underline.`<br>
Remarks: Values are stored on a stack. A closing tag reverts to the previous unclosed underline, rather than reverting back the default underline of none. Underlines do NOT consume space on the UI's layout; if the offset parameter is set to a large enough value, the underline could end up outside the bounds of the TextBlock, which may result in parts of the underline being clipped (If `MGTextBlock.ClipToBounds=true`). Underlines are affected by `Opacity` formatting codes.

![underline](https://user-images.githubusercontent.com/9426230/213096391-e18b722c-cc81-460b-b827-c464bdb6ef72.png)

### Opacity

Code: `Opacity`<br>
Abbreviations: `O`<br>
Parameters: A floating point value between 0.0 and 1.0<br>
Example: `[Opacity=0.5]This text has [Underline]50% opacity[/u],[/o] This text is opaque. [o=0.2]This text is mostly transparent[/opacity]`<br>
Remarks: Opacity affects underlines and background colors too, but does not change the opacity of inlined ToolTip content or inlined Images.

![opacity](https://user-images.githubusercontent.com/9426230/209582998-56a349c2-1eeb-497e-9f9e-088cc6be1ea1.png)

### Color

Code: `Color`<br>
Abbreviations: `FG`, `Foreground`, `C`<br>
Parameters: The HTML Color name or Hex color value<br>
Example: `Default color text (MGTextBlock.Foreground), [Color=Orange]Orange Text[/c], [Color=#00AA00]Green Text, [Color=Purple]Purple Text, [/foreground]Back to Green, [/fg] Back to Default`<br>
Remarks: Values are stored on a stack. A closing tag reverts to the previous unclosed color, rather than reverting back to the default color. Default color is determined by the `MGTextBlock.Foreground` property. If no Foreground is specified, default color is either inherited from a parent element's `MGElement.DefaultTextForeground` property, or from `MGTheme.TextBlockFallbackForeground` property. Colors are affected by `Opacity` formatting codes.

![color](https://user-images.githubusercontent.com/9426230/209583530-a386dfff-71cb-40d3-b718-ee4e67ef054e.png)

### Background

Code: `Background`<br>
Abbreviations: `BG`<br>
Parameters:<br>
- The IFillBrush to fill the background region with. Usually a single Color such as "Green" or "#FF00FF00" but you can use multiple colors to specify a gradient fill brush ([more info here](https://github.com/Videogamers0/MGUI/wiki/XAML#ifillbrush))
- [Optional] A Thickness Padding. Positive Thickness makes the Background region larger, Negative Thickness makes it smaller (Default=0)
  - Thickness should consist of comma-delimited integers. EX: "4" = 4px padding on each side. "4,2" = 4px padding on left+right, 2px on top+bottom. "4,2,1,3" = 4px left, 2px top, 1px right, 3px bottom

Example: `No background. [Background=Yellow]Yellow BG[/bg], [Background=#00AA00|GreenYellow]Green gradient BG, [Background=Purple -3,3,1,4]Purple BG with padding, [/background] Back to Green, [/bg] Back to none`<br>
Remarks: Values are stored on a stack. A closing tag reverts to the previous unclosed background color, rather than reverting back the default background of `Color.Transparent`. Backgrounds are only rendered to the extents of the associated text. If, for example, the Text is vertically-centered in a TextBlock that is taller than the Text content, or if the TextBlock has non-zero Padding, then the Background color wouldn't span the entire TextBlock's height. Backgrounds do NOT consume space on the UI's layout; if the padding parameter is set to a large enough value, the background could end up outside the bounds of the TextBlock, which may result in parts of the background being clipped (If `MGTextBlock.ClipToBounds=true`). Backgrounds are affected by `Opacity` formatting codes.

![background1](https://user-images.githubusercontent.com/9426230/210119593-c3253e14-3860-4f4d-addf-1de14de201b0.png)

### Shadow

Code: `Shadow`<br>
Abbreviations: `S`<br>
Parameters:<br>
- The HTML Color name or Hex color value
- [Optional] The number of pixels to horizontally offset the shadow by, can be negative (Default=1)
- [Optional] The number of pixels to vertically offset the shadow by, can be negative (Default=1, value is defined in Client-Space, meaning a positive value offsets downwards)

Example: `No shadow, [Shadow=Purple -1 2]Purple shadow, offsetted left by 1, down by 2, [s=#00AA00]Green shadow[/s] Back to Purple, [/s] Back to none.`<br>
Remarks: Values are stored on a stack. A closing tag reverts to the previous unclosed shadow, rather than reverting back the default value of no shadow. Shadows are rendered just before the associated text, but just after underlines, so shadows may end up on top of underlines. Shadows do NOT consume space on the UI's layout; if the offsets are set to a large enough value, the shadow could end up outside the bounds of the TextBlock, which may result in parts of the shadow being clipped (If `MGTextBlock.ClipToBounds=true`). Shadow colors are affected by `Opacity` formatting codes.

![shadow](https://user-images.githubusercontent.com/9426230/209584420-868e8843-3e07-47fa-a58e-6ff0ffd24416.png)

### Image

Code: `Image`<br>
Abbreviations: `Img`<br>
Parameters:<br>
- The name of the Image to use
  - To reference an image, you must first add it to `MGResources.Textures` via `MGResources.AddTexture(string Name, MGTextureData Data)`
- [Optional] The target dimensions of the Image, specified as 2 consecutive integers separated by either a comma, space, or `x`
  - Example: "64x32", "64 32", "64,32" (Width=64px, Height=32px)
  - If parameter is not specified, the texture is rendered using the width/height of the `MGTextureData` that is being referenced.

Example: `[Image=SkullAndCrossbones 16x16] There is a 16x16 icon left of this text, and a 24x32 stretched icon to the right [img=SkullAndCrossbones 24x32]`<br>
Remarks: The lineheight of a line containing 1 or more images is given by Max(TextHeight, ImageHeight). The content of a line is positioned based on the TextBlock's `VerticalContentAlignment` (Default=Center). If target width/height is specified, the texture is stretched/compressed to fit that space (non-uniform stretching, aspect-ratio is not preserved)

![image](https://user-images.githubusercontent.com/9426230/209585206-e2c851ca-c96b-4c2c-a9d7-b473ff1ed834.png)

```c#
//  In order to reference an image by a string key, we must add the image to the MGResources instance
//  which is typically obtained from MGDesktop.Resources or MGElement.GetResources() (both return the same instance)
MGDesktop Desktop = ...;
MGResources Resources = Desktop.Resources;
Texture2D Texture = Content.Load<Texture2D>("Foo");
//  This allows us to reference an image named "Bar", which is rendered using 64,64,32,32 SourceRect of the "Foo" texture
Resources.AddTexture("Bar", new MGTextureData(Texture, new Rectangle(64,64,32,32)));

...

MGTextBlock TextBlock = new(Window, "[Image=Bar] TextBlock with inlined image");
```

### ToolTip

Code: `ToolTip`<br>
Abbreviations: `TT`<br>
Parameters: The name of the ToolTip to use. To reference a ToolTip, you must first add it to `MGWindow.NamedToolTips` via `MGWindow.AddNamedToolTip(string, MGToolTip)`<br>
Example: `[ToolTip=Foo]This Text has a ToolTip[/tt], but this Text doesn't`<br>
Remarks: Values are stored on a stack. A closing tag reverts to the previous unclosed ToolTip, rather than reverting back the default value of no ToolTip. ToolTips can be applied to Text and/or inlined Image content. The content of a ToolTip is not affected by `Opacity` formatting codes. ToolTips are only displayed if the mouse is hovering overtop of the exact bounds of the content that is surrounded by the ToolTip tag. Suppose your TextBlock has a line of content consisting of Text that is 16px tall, and an Image that is 32px tall. This would result in a line that is Max(16,32)=32px tall. If the ToolTip were applied to the Text, and the Text was vertically centered, then it's possible that the ToolTip wouldn't be displayed while horizontally hovering the text, and vertically hovering the line that the text belongs to, because only the center 16px (of the 32px line) satisfies the HitTest logic for displaying the ToolTip in this scenario.

![tooltip](https://user-images.githubusercontent.com/9426230/209588065-27a37d91-addb-4d13-893b-de16ce07e86b.gif)
```c#
//  In order to reference a ToolTip by a string key, we must add the ToolTip to the MGWindow instance
MGDesktop Desktop = ...;
MGWindow Window = new(Desktop, 0, 0, 500, 500);
Desktop.Windows.Add(Window);

MGToolTip SampleToolTip = new(Window, Window, 0, 0);
SampleToolTip.DefaultTextForeground.NormalValue = Color.White;
SampleToolTip.BackgroundBrush.NormalValue = SolidFillBrushes.Gray;
SampleToolTip.SetContent("Sample ToolTip", null, 12);
SampleToolTip.ApplySizeToContent(SizeToContent.WidthAndHeight, 0, 0);

//  This allows us to reference a ToolTip named "Foo"
Window.AddNamedToolTip("Foo", SampleToolTip);

...

MGTextBlock TextBlock = new(Window, "TextBlock with a [ToolTip=Foo]ToolTip[/ToolTip] on a single word");
```

### Action

Actions are delegates that can be attached to specific content within a TextBlock. The delegate is invoked when the associated content is clicked.

Code: `Action`<br>
Abbreviations: `Command`<br>
Parameters: The name of the Action to use. To reference an Action, you must first add it to `MGResources.Commands` via `MGResources.AddCommand(string, Action<MGElement>)`<br>
Example: `[Action=Foo][Underline][Color=DarkBlue]Click Here[/Color][/Underline][/Action] to make this text Orange`<br>
Remarks: Values are stored on a stack. A closing tag reverts to the previous unclosed Action, rather than reverting back the default value of no action. Actions can be applied to Text and/or inlined Image content. Actions are invoked during the MouseRelease event of the left mouse button. The input does not necessarily need to be a Click, as long as the released mouse position is overtop of the actionable content.

![action](https://user-images.githubusercontent.com/9426230/209588446-594bab5f-0655-4ab1-98d6-87d2826d4a85.gif)

```c#
//  In order to reference an Action by a string key, we must add the Action delegate to the MGResources instance,
//  which is typically obtained from MGDesktop.Resources or MGElement.GetResources() (both return the same instance)
MGDesktop Desktop = ...;
MGResources Resources = Desktop.Resources;
MGWindow Window = new(Desktop, 0, 0, 500, 500);
Desktop.Windows.Add(Window);

//  This allows us to reference an Action named "Foo"
Resources.AddCommand("Foo", 
    (source) => {
        if (source is MGTextBlock TextBlock)
            TextBlock.Foreground.NormalValue = Color.Orange;
    });

...

MGTextBlock TextBlock = new(Window, "[Action=Foo][Underline][Color=DarkBlue]Click Here[/Color][/Underline][/Action] to make this text Orange");
```

### Escaping Formatting

All formatting codes are surrounded with bracket characters: `[`,`]`

To use a bracket literal, you can either disable inline formatting by setting `MGTextBlock.AllowsInlineFormatting=false`, manually escaping the open bracket by prefixing it with backslash `\`, or call `MGUI.Core.UI.Text.FTTokenizer.EscapeMarkdown(string)`

```c#
MGTextBlock TextBlock1 = new(Window, "");
TextBlock1.AllowsInlineFormatting = false;
TextBlock1.Text = "Text with open bracket literal: [";

MGTextBlock TextBlock2 = new(Window, "");
TextBlock2.Text = "Text with open bracket literal: \\[";

MGTextBlock TextBlock3 = new(Window, "");
TextBlock3.Text = MGUI.Core.UI.Text.FTTokenizer.EscapeMarkdown("Text with open bracket literal: [");
```