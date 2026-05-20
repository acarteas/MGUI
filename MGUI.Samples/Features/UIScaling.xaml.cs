using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class UIScalingSamples : SampleBase
    {
        private readonly MGTextBlock ScaleStatus;

        public UIScalingSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", $"UIScaling.xaml")
        {
            ScaleStatus = Window.GetElementByName<MGTextBlock>(nameof(ScaleStatus));
            AddScaleCommand("UIScale_1_0", 1.0f);
            AddScaleCommand("UIScale_1_25", 1.25f);
            AddScaleCommand("UIScale_1_5", 1.5f);
            AddScaleCommand("UIScale_2_0", 2.0f);

            VisibilityChanged += (sender, isVisible) =>
            {
                if (!isVisible)
                {
                    SetScale(1.0f);
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
    }
}
