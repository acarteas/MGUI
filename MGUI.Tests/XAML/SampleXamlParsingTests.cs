using MGUI.Core.UI.XAML;

namespace MGUI.Tests.XAML;

public class SampleXamlParsingTests
{
    [Fact]
    public void XAMLParser_LoadRootWindow_ParsesUIScalingSample()
    {
        string xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "MGUI.Samples", "Features", "UIScaling.xaml"));

        Window window = (Window)Portable.Xaml.XamlServices.Parse(xaml);

        Assert.Equal("UI Scaling", window.TitleText);
    }
}
