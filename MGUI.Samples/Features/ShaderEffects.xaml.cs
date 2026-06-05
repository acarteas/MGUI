using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
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
        private enum ShaderEffectMode
        {
            Pulse = 0,
            Hover = 1,
            Active = 2,
            FocusGlow = 3,
            Disabled = 4
        }

        private readonly Effect UiEffect;
        private float TimeSeconds;

        public string ShaderStatusText { get; private set; }
        public Color ShaderStatusColor { get; private set; }

        public ShaderEffectsSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "ShaderEffects.xaml")
        {
            UiEffect = LoadUiEffect(Content);
            ShaderStatusText = UiEffect == null
                ? "Shader unavailable: the sample uses solid-color fallback fills until the DesktopGL MGFX asset builds and loads."
                : "Loaded Content/Shaders/UiEffects.fx through the sample project's DesktopGL content pipeline.";
            ShaderStatusColor = UiEffect == null ? Color.LightCoral : Color.LightGreen;

            Desktop.Renderer.Host.PreviewUpdate += Host_PreviewUpdate;

            Window.GetElementByName<MGButton>("HoverButton").BackgroundBrush = CreateVisualBrush(
                ShaderEffectMode.Hover, new Color(25, 61, 74), new Color(71, 214, 193));
            Window.GetElementByName<MGToggleButton>("ActiveToggle").BackgroundBrush = CreateVisualBrush(
                ShaderEffectMode.Active, new Color(44, 48, 78), new Color(255, 169, 64));
            Window.GetElementByName<MGButton>("PressedButton").BackgroundBrush = CreateVisualBrush(
                ShaderEffectMode.Active, new Color(80, 52, 43), new Color(255, 92, 72));
            Window.GetElementByName<MGBorder>("PulseCard").BackgroundBrush = CreateVisualBrush(
                ShaderEffectMode.Pulse, new Color(39, 67, 52), new Color(83, 222, 139));
            Window.GetElementByName<MGBorder>("FocusCard").BackgroundBrush = CreateVisualBrush(
                ShaderEffectMode.FocusGlow, new Color(25, 35, 65), new Color(119, 191, 255), 1.0f);
            Window.GetElementByName<MGBorder>("DisabledCard").BackgroundBrush = CreateVisualBrush(
                ShaderEffectMode.Disabled, new Color(99, 104, 112), new Color(52, 62, 74));

            Window.WindowDataContext = this;
        }

        private Effect LoadUiEffect(ContentManager Content)
        {
            try
            {
                return Content.Load<Effect>(Path.Combine("Shaders", "UiEffects"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load shader sample effect: {ex}");
                return null;
            }
        }

        private void Host_PreviewUpdate(object sender, TimeSpan e)
        {
            TimeSeconds = (float)e.TotalSeconds;
        }

        private VisualStateFillBrush CreateVisualBrush(ShaderEffectMode Mode, Color ColorA, Color ColorB, float FocusAmount = 0.0f)
        {
            if (UiEffect == null)
            {
                return new VisualStateFillBrush(new MGSolidFillBrush(ColorA));
            }

            return new VisualStateFillBrush(new MGEffectFillBrush(UiEffect, (effect, drawArgs, element, bounds) =>
            {
                ConfigureEffect(effect, drawArgs, element, bounds, Mode, ColorA, ColorB, FocusAmount);
            }));
        }

        private void ConfigureEffect(Effect Effect, ElementDrawArgs DrawArgs, MGElement Element, Rectangle Bounds,
            ShaderEffectMode Mode, Color ColorA, Color ColorB, float FocusAmount)
        {
            float HoverAmount = DrawArgs.VisualState.IsHovered ? 1.0f : 0.0f;
            float PressAmount = DrawArgs.VisualState.IsPressed ? 1.0f : 0.0f;
            float ActiveAmount = FocusAmount;
            if (Element is MGToggleButton ToggleButton && ToggleButton.IsChecked)
            {
                ActiveAmount = 1.0f;
            }

            Effect.Parameters["TimeSeconds"]?.SetValue(TimeSeconds);
            Effect.Parameters["Opacity"]?.SetValue(DrawArgs.Opacity);
            Effect.Parameters["ElementSize"]?.SetValue(new Vector2(Math.Max(1, Bounds.Width), Math.Max(1, Bounds.Height)));
            Effect.Parameters["ElementPosition"]?.SetValue(new Vector2(Bounds.X, Bounds.Y));
            Effect.Parameters["HoverAmount"]?.SetValue(HoverAmount);
            Effect.Parameters["PressAmount"]?.SetValue(PressAmount);
            Effect.Parameters["FocusAmount"]?.SetValue(ActiveAmount);
            Effect.Parameters["Mode"]?.SetValue((float)Mode);
            Effect.Parameters["ColorA"]?.SetValue(ColorA.ToVector4());
            Effect.Parameters["ColorB"]?.SetValue(ColorB.ToVector4());
        }
    }
}
