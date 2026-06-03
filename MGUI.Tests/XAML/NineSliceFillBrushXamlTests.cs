using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Portable.Xaml;

namespace MGUI.Tests.XAML;

public class NineSliceFillBrushXamlTests
{
    [Fact]
    public void NineSliceFillBrush_ParsesInteriorBrushAttribute()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="PanelFrame"
                                SourceMargin="64"
                                TargetMargin="32"
                                InteriorBrush="Red" />
            """;

        NineSliceFillBrush brush = (NineSliceFillBrush)XamlServices.Parse(xaml);

        SolidFillBrush interiorBrush = Assert.IsType<SolidFillBrush>(brush.InteriorBrush);
        Assert.Equal(255, interiorBrush.Color.R);
        Assert.Equal(0, interiorBrush.Color.G);
        Assert.Equal(0, interiorBrush.Color.B);
        Assert.Equal(255, interiorBrush.Color.A);
    }

    [Fact]
    public void NineSliceFillBrush_ParsesInteriorBrushPropertyElement()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="PanelFrame"
                                SourceMargin="64"
                                TargetMargin="32">
                <NineSliceFillBrush.InteriorBrush>
                    <TextureFillBrush SourceName="PanelInterior" Stretch="Fill" />
                </NineSliceFillBrush.InteriorBrush>
            </NineSliceFillBrush>
            """;

        NineSliceFillBrush brush = (NineSliceFillBrush)XamlServices.Parse(xaml);

        TextureFillBrush interiorBrush = Assert.IsType<TextureFillBrush>(brush.InteriorBrush);
        Assert.Equal("PanelInterior", interiorBrush.SourceName);
        Assert.Equal(Stretch.Fill, interiorBrush.Stretch);
    }
}
