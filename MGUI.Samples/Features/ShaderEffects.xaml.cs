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
        private const string OrnamentalFrameTextureName = "ShaderOrnamentalFrame";

        public string ShaderStatusText { get; private set; }
        public Color ShaderStatusColor { get; private set; }

        public ShaderEffectsSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "ShaderEffects.xaml", () => RegisterShaderResources(Content, Desktop.Resources))
        {
            bool HasEffect = Resources.TryGetEffect(UiEffectName, out _);
            ShaderStatusText = HasEffect
                ? "Loaded and registered the sample project's caller-owned DesktopGL effect and ornamental frame texture."
                : "Shader resources were not registered.";
            ShaderStatusColor = HasEffect ? Color.LightGreen : Color.LightCoral;

            Window.WindowDataContext = this;
        }

        private static void RegisterShaderResources(ContentManager Content, MGResources Resources)
        {
            Effect Effect = Content.Load<Effect>(Path.Combine("Shaders", "UiEffects"));
            Resources.AddOrReplaceEffect(UiEffectName, Effect);

            Texture2D OrnamentalFrame = Content.Load<Texture2D>(Path.Combine("Brush Textures", "9SliceTexture-1"));
            if (!Resources.TryGetTexture(OrnamentalFrameTextureName, out _))
            {
                Resources.AddTexture(OrnamentalFrameTextureName, new MGTextureData(OrnamentalFrame));
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
