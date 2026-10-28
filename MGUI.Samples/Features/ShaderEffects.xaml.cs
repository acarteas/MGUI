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
        private const string OrnamentalFrameTextureName = "ShaderOrnamentalFrame";

        public string ShaderStatusText { get; private set; }
        public Color ShaderStatusColor { get; private set; }
        public string FailureStatusText { get; private set; }
        public string ShaderPreviewTitle { get; private set; }

        public ShaderEffectsSamples(ContentManager Content, MGDesktop Desktop)
            : this(Content, Desktop, new ShaderEffectsSampleSetup(Desktop.Resources,
                () => Content.Load<Effect>(Path.Combine("Shaders", "UiEffects"))))
        {
        }

        private ShaderEffectsSamples(ContentManager Content, MGDesktop Desktop, ShaderEffectsSampleSetup Setup)
            : base(Content, Desktop, $"{nameof(Features)}", "ShaderEffects.xaml",
                () => RegisterTextureResources(Content, Desktop.Resources), Setup.PrepareXaml)
        {
            ShaderStatusText = Setup.StatusText;
            ShaderStatusColor = Setup.HasEffect ? Color.LightGreen : Color.LightCoral;
            FailureStatusText = Setup.FailureStatusText;
            ShaderPreviewTitle = Setup.HasEffect ? "Loaded effect: action states" : "Effect unavailable: fallback action states";
            Window.WindowDataContext = this;
        }

        private static void RegisterTextureResources(ContentManager Content, MGResources Resources)
        {
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
