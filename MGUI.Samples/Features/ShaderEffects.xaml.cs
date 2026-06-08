using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.IO;

namespace MGUI.Samples.Features
{
    public class ShaderEffectsSamples : SampleBase
    {
        private const string UiEffectName = "SampleUiEffect";

        public string ShaderStatusText { get; private set; }
        public Color ShaderStatusColor { get; private set; }

        public ShaderEffectsSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "ShaderEffects.xaml", () => RegisterUiEffect(Content, Desktop.Resources))
        {
            bool HasEffect = Resources.TryGetEffect(UiEffectName, out _);
            ShaderStatusText = !HasEffect
                ? "Shader unavailable: the sample uses solid-color fallback fills until the DesktopGL MGFX asset builds and loads."
                : "Loaded Content/Shaders/UiEffects.fx through the sample project's DesktopGL content pipeline.";
            ShaderStatusColor = HasEffect ? Color.LightGreen : Color.LightCoral;

            Window.WindowDataContext = this;
        }

        private static void RegisterUiEffect(ContentManager Content, MGResources Resources)
        {
            try
            {
                Effect Effect = Content.Load<Effect>(Path.Combine("Shaders", "UiEffects"));
                Resources.AddOrReplaceEffect(UiEffectName, Effect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load shader sample effect: {ex}");
            }

            try
            {
                Texture2D Icon = Content.Load<Texture2D>(Path.Combine("Icons", "Item"));
                if (!Resources.TryGetTexture("ShaderActionIcon", out _))
                {
                    Resources.AddTexture("ShaderActionIcon", new MGTextureData(Icon));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load shader sample icon: {ex}");
            }
        }
    }
}
