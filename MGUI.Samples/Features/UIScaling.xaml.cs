using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class UIScalingSamples : SampleBase
    {
        private readonly MGTextBlock ScaleStatus;
        private MGWindow OverrideWindow;

        public UIScalingSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", $"UIScaling.xaml")
        {
            ScaleStatus = Window.GetElementByName<MGTextBlock>(nameof(ScaleStatus));
            AddScaleCommand("UIScale_1_0", 1.0f);
            AddScaleCommand("UIScale_1_25", 1.25f);
            AddScaleCommand("UIScale_1_5", 1.5f);
            AddScaleCommand("UIScale_2_0", 2.0f);
            Window.GetResources().AddCommand("OpenScaleOverrideWindow", _ => OpenScaleOverrideWindow());

            VisibilityChanged += (sender, isVisible) =>
            {
                if (!isVisible)
                {
                    SetScale(1.0f);
                    CloseScaleOverrideWindow();
                }
            };
        }

        private void AddScaleCommand(string Name, float Scale)
        {
            Window.GetResources().AddCommand(Name, x => SetScale(Scale));
        }

        private void SetScale(float Scale)
        {
            Desktop.UIScale.SetUniformScale(Scale);
            ScaleStatus.SetText($"Desktop.UIScale = {Scale:0.##}");
        }

        private void OpenScaleOverrideWindow()
        {
            if (OverrideWindow == null)
            {
                const string Xaml = @"
                    <Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                            Left=""1220"" Top=""80"" Width=""360"" Height=""220"" TitleText=""Override 1.5x"" Padding=""10"" UIScaleOverride=""1.5"">
                        <StackPanel Orientation=""Vertical"" Spacing=""8"">
                            <TextBlock IsBold=""True"" Text=""Window UIScaleOverride = 1.5"" />
                            <TextBlock Text=""This window replaces the desktop scale instead of multiplying it."" />
                            <Button Content=""Scaled by window override"" />
                            <StackPanel UIScaleMode=""Disabled"" Padding=""6"" Spacing=""4"" Background=""rgb(38,42,48)"">
                                <TextBlock Text=""Opt-out island inside the override."" />
                                <CheckBox IsChecked=""True"" Content=""Unscaled checkbox"" />
                            </StackPanel>
                        </StackPanel>
                    </Window>";

                OverrideWindow = XAMLParser.LoadRootWindow(Desktop, Xaml, false, true);
                OverrideWindow.WindowClosed += (_, _) => OverrideWindow = null;
            }

            if (!Desktop.Windows.Contains(OverrideWindow))
            {
                Desktop.Windows.Add(OverrideWindow);
            }

            Desktop.BringToFront(OverrideWindow);
        }

        private void CloseScaleOverrideWindow()
        {
            if (OverrideWindow != null)
            {
                Desktop.Windows.Remove(OverrideWindow);
                OverrideWindow = null;
            }
        }
    }
}
