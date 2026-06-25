using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace MGUI.EffectTestHost;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        try
        {
            using EffectTestGame Game = new();
            Game.RunOneFrame();
            Console.WriteLine("RESULT:" + JsonSerializer.Serialize(Game.Results));
            return Game.Results.Values.All(x => x) ? 0 : 1;
        }
        catch (Exception Ex)
        {
            Console.WriteLine("ERROR:" + Ex);
            return 2;
        }
    }

    private sealed class EffectTestGame : Game
    {
        private readonly GraphicsDeviceManager Graphics;
        public Dictionary<string, bool> Results { get; } = new();

        public EffectTestGame()
        {
            Graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
        }

        protected override void Initialize()
        {
            Graphics.ApplyChanges();
            base.Initialize();

            using Effect Source = Content.Load<Effect>("EffectParameters");
            RunStandardAndCallback(Source);
            RunCustomTypesAndMissing(Source);
            RunIncompatible(Source);
            RunSharedEffect(Source);
            RunReplacement(Source);
            RunCopyIndependence(Source);
            RunReusableBinding(Source);
            RunNineSliceBinding(Source);
            Exit();
        }

        private void RunStandardAndCallback(Effect Source)
        {
            using Effect Effect = Source.Clone();
            bool CallbackObservedStandard = false;
            MGEffectFillBrush Brush = new(Effect, (ConfiguredEffect, _, _, _) =>
            {
                CallbackObservedStandard = Near(ConfiguredEffect.Parameters["Opacity"].GetValueSingle(), 0.35f);
                ConfiguredEffect.Parameters["Opacity"].SetValue(0.8f);
            })
            {
                UseStandardParameters = true
            };
            MGStandardEffectParameterValues Values = CreateValues(
                new VisualState(PrimaryVisualState.Selected, SecondaryVisualState.Pressed), 0.35f, 1234.5f);
            Brush.ApplyEffectConfiguration(Values, default, null, new Rectangle(10, 20, 30, 40));

            Results["standard-values"] =
                Effect.Parameters["MatrixTransform"].GetValueMatrix() == Values.MatrixTransform &&
                Effect.Parameters["ElementPosition"].GetValueVector2() == Values.ElementPosition &&
                Effect.Parameters["ElementSize"].GetValueVector2() == Values.ElementSize &&
                Near(Effect.Parameters["TimeSeconds"].GetValueSingle(), 1234.5f) &&
                Near(Effect.Parameters["HoverAmount"].GetValueSingle(), 0) &&
                Near(Effect.Parameters["PressAmount"].GetValueSingle(), 1) &&
                Near(Effect.Parameters["SelectedAmount"].GetValueSingle(), 1) &&
                Near(Effect.Parameters["DisabledAmount"].GetValueSingle(), 0);
            Results["callback-last"] = CallbackObservedStandard && Near(Effect.Parameters["Opacity"].GetValueSingle(), 0.8f);

            bool CallbackObservedCustomOverride = false;
            MGEffectFillBrush OrderedBrush = new(Effect, (ConfiguredEffect, _, _, _) =>
            {
                CallbackObservedCustomOverride = Near(ConfiguredEffect.Parameters["Opacity"].GetValueSingle(), 0.55f);
                ConfiguredEffect.Parameters["Opacity"].SetValue(0.9f);
            })
            {
                UseStandardParameters = true,
                Parameters = new[] { new MGEffectParameterValue("Opacity", MGEffectParameterType.Float, 0.55f) }
            };
            OrderedBrush.ApplyEffectConfiguration(Values, default, null, Rectangle.Empty);
            Results["standard-custom-callback-order"] = CallbackObservedCustomOverride &&
                Near(Effect.Parameters["Opacity"].GetValueSingle(), 0.9f);

            Effect.Parameters["Opacity"].SetValue(0.6f);
            new MGEffectFillBrush(Effect).ApplyEffectConfiguration(CreateValues(default, 0.2f, 1), default, null, Rectangle.Empty);
            Results["opt-in-default-off"] = Near(Effect.Parameters["Opacity"].GetValueSingle(), 0.6f);
        }

        private void RunCustomTypesAndMissing(Effect Source)
        {
            using Effect Effect = Source.Clone();
            MGEffectFillBrush Brush = new(Effect)
            {
                Parameters = new[]
                {
                    new MGEffectParameterValue("CustomFloat", MGEffectParameterType.Float, 1.25f),
                    new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 7),
                    new MGEffectParameterValue("CustomBool", MGEffectParameterType.Bool, true),
                    new MGEffectParameterValue("CustomVector2", MGEffectParameterType.Vector2, new Vector2(2, 3)),
                    new MGEffectParameterValue("CustomVector3", MGEffectParameterType.Vector3, new Vector3(4, 5, 6)),
                    new MGEffectParameterValue("CustomVector4", MGEffectParameterType.Vector4, new Vector4(7, 8, 9, 10)),
                    new MGEffectParameterValue("CustomColor", MGEffectParameterType.Color, Color.Goldenrod.ToVector4()),
                    new MGEffectParameterValue("NotDeclaredByShader", MGEffectParameterType.Float, 99.0f)
                }
            };

            bool DidNotThrow = Try(() => Brush.ApplyEffectConfiguration(null, default, null, Rectangle.Empty));
            Results["custom-types"] = DidNotThrow &&
                Near(Effect.Parameters["CustomFloat"].GetValueSingle(), 1.25f) &&
                Effect.Parameters["CustomInt"].GetValueInt32() == 7 &&
                Effect.Parameters["CustomBool"].GetValueBoolean() &&
                Effect.Parameters["CustomVector2"].GetValueVector2() == new Vector2(2, 3) &&
                Effect.Parameters["CustomVector3"].GetValueVector3() == new Vector3(4, 5, 6) &&
                Effect.Parameters["CustomVector4"].GetValueVector4() == new Vector4(7, 8, 9, 10) &&
                Effect.Parameters["CustomColor"].GetValueVector4() == Color.Goldenrod.ToVector4();
            Results["missing-parameter"] = DidNotThrow;
        }

        private void RunIncompatible(Effect Source)
        {
            using Effect Effect = Source.Clone();
            MGEffectFillBrush Brush = new(Effect)
            {
                Parameters = new[] { new MGEffectParameterValue("IncompatibleScalar", MGEffectParameterType.Vector4, Vector4.One) }
            };

            try
            {
                Brush.ApplyEffectConfiguration(null, default, null, Rectangle.Empty);
                Results["incompatible-diagnostic"] = false;
            }
            catch (InvalidOperationException Ex)
            {
                Results["incompatible-diagnostic"] = Ex.Message.Contains("IncompatibleScalar") &&
                    Ex.Message.Contains("Vector4") && Ex.Message.Contains("Scalar") && Ex.Message.Contains("Single");
            }
        }

        private void RunSharedEffect(Effect Source)
        {
            using Effect Effect = Source.Clone();
            MGEffectFillBrush BrushA = CreateRoleBrush(Effect, 1, Color.CornflowerBlue);
            MGEffectFillBrush BrushB = CreateRoleBrush(Effect, 2, Color.OrangeRed);
            MGStandardEffectParameterValues ValuesA = CreateValues(new VisualState(PrimaryVisualState.Selected, SecondaryVisualState.None), 1, 1);
            MGStandardEffectParameterValues ValuesB = CreateValues(new VisualState(PrimaryVisualState.Disabled, SecondaryVisualState.None), 0.5f, 2);

            BrushA.ApplyEffectConfiguration(ValuesA, default, null, Rectangle.Empty);
            bool A1 = IsRole(Effect, 1, Color.CornflowerBlue, 1, 0);
            BrushB.ApplyEffectConfiguration(ValuesB, default, null, Rectangle.Empty);
            bool B = IsRole(Effect, 2, Color.OrangeRed, 0, 1);
            BrushA.ApplyEffectConfiguration(ValuesA, default, null, Rectangle.Empty);
            Results["shared-aba"] = A1 && B && IsRole(Effect, 1, Color.CornflowerBlue, 1, 0);
        }

        private void RunReplacement(Effect Source)
        {
            using Effect First = Source.Clone();
            using Effect Replacement = Source.Clone();
            MGEffectFillBrush Brush = CreateRoleBrush(First, 3, Color.Green);
            MGStandardEffectParameterValues Values = CreateValues(default, 0.7f, 8);
            Brush.ApplyEffectConfiguration(Values, default, null, Rectangle.Empty);
            Brush.Effect = Replacement;
            Brush.Parameters = new[] { new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 9) };
            Brush.ApplyEffectConfiguration(Values, default, null, Rectangle.Empty);

            Results["cache-invalidation"] = First.Parameters["CustomInt"].GetValueInt32() == 3 &&
                Replacement.Parameters["CustomInt"].GetValueInt32() == 9 && Near(Replacement.Parameters["Opacity"].GetValueSingle(), 0.7f);
        }

        private void RunCopyIndependence(Effect Source)
        {
            using Effect First = Source.Clone();
            using Effect Replacement = Source.Clone();
            MGEffectFillBrush Original = CreateRoleBrush(First, 4, Color.Red);
            MGEffectFillBrush Copy = (MGEffectFillBrush)Original.Copy();
            Original.Parameters = new[] { new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 5) };
            Copy.Effect = Replacement;
            Original.ApplyEffectConfiguration(CreateValues(default, 1, 1), default, null, Rectangle.Empty);
            Copy.ApplyEffectConfiguration(CreateValues(default, 1, 1), default, null, Rectangle.Empty);

            Results["copy-independent"] = First.Parameters["CustomInt"].GetValueInt32() == 5 &&
                Replacement.Parameters["CustomInt"].GetValueInt32() == 4 && !ReferenceEquals(Original.Parameters, Copy.Parameters);
        }

        private void RunReusableBinding(Effect Source)
        {
            using Effect Effect = Source.Clone();
            Rectangle Bounds = new(10, 20, 30, 40);
            bool CallbackObservedConstants = false;
            MGEffectBinding Binding = new(Effect, (ConfiguredEffect, _, _, ConfiguredBounds) =>
            {
                CallbackObservedConstants =
                    ConfiguredBounds == Bounds &&
                    Near(ConfiguredEffect.Parameters["Opacity"].GetValueSingle(), 0.65f) &&
                    ConfiguredEffect.Parameters["CustomInt"].GetValueInt32() == 12;
                ConfiguredEffect.Parameters["Opacity"].SetValue(0.95f);
            })
            {
                UseStandardParameters = true,
                Parameters = new[]
                {
                    new MGEffectParameterValue("Opacity", MGEffectParameterType.Float, 0.65f),
                    new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 12)
                }
            };

            Binding.Apply(CreateValues(default, 0.25f, 4), default, null, Bounds);

            Results["reusable-binding"] =
                CallbackObservedConstants &&
                Near(Effect.Parameters["Opacity"].GetValueSingle(), 0.95f);
        }

        private void RunNineSliceBinding(Effect Source)
        {
            using Effect Effect = Source.Clone();
            Rectangle Bounds = new(12, 23, 34, 45);
            int CallbackCount = 0;
            MGNineSliceFillBrush Brush = new(
                Effect,
                new Thickness(1),
                default, default, default,
                default, default, default,
                default, default, default,
                null,
                (ConfiguredEffect, _, _, ConfiguredBounds) =>
                {
                    CallbackCount++;
                    if (ConfiguredBounds == Bounds &&
                        Near(ConfiguredEffect.Parameters["Opacity"].GetValueSingle(), 0.65f) &&
                        ConfiguredEffect.Parameters["CustomInt"].GetValueInt32() == 21)
                    {
                        ConfiguredEffect.Parameters["Opacity"].SetValue(0.9f);
                    }
                })
            {
                UseStandardParameters = true,
                Parameters = new[]
                {
                    new MGEffectParameterValue("Opacity", MGEffectParameterType.Float, 0.65f),
                    new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 21)
                }
            };

            Brush.ApplyEffectConfiguration(CreateValues(default, 0.25f, 5), default, null, Bounds);
            Results["nine-slice-configuration"] =
                CallbackCount == 1 &&
                Near(Effect.Parameters["Opacity"].GetValueSingle(), 0.9f) &&
                Effect.Parameters["CustomInt"].GetValueInt32() == 21;

            MGNineSliceFillBrush Other = (MGNineSliceFillBrush)Brush.Copy();
            Other.Parameters = new[] { new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 22) };
            Other.ConfigureEffect = null;
            Other.ApplyEffectConfiguration(CreateValues(default, 0.4f, 6), default, null, Bounds);
            bool OtherApplied = Effect.Parameters["CustomInt"].GetValueInt32() == 22;
            Brush.ApplyEffectConfiguration(CreateValues(default, 0.25f, 5), default, null, Bounds);
            Results["nine-slice-shared-reuse"] =
                OtherApplied &&
                Effect.Parameters["CustomInt"].GetValueInt32() == 21 &&
                CallbackCount == 2;

            Vector2 ElementPosition = Effect.Parameters["ElementPosition"].GetValueVector2();
            Vector2 ElementSize = Effect.Parameters["ElementSize"].GetValueVector2();
            MGElementTextureCoordinateMapping FirstMapping = new(
                new Vector2(1.5f, 2.5f),
                new Vector2(-0.25f, 0.125f));
            MGElementTextureCoordinateMapping SecondMapping = new(
                new Vector2(3.5f, 4.5f),
                new Vector2(-0.75f, 0.625f));
            Brush.ApplyElementTextureCoordinateMapping(FirstMapping);
            bool FirstMappingApplied =
                Effect.Parameters["ElementTextureCoordinateScale"].GetValueVector2() == FirstMapping.Scale &&
                Effect.Parameters["ElementTextureCoordinateOffset"].GetValueVector2() == FirstMapping.Offset;
            Brush.ApplyElementTextureCoordinateMapping(SecondMapping);
            Results["nine-slice-coordinate-mapping"] =
                FirstMappingApplied &&
                Effect.Parameters["ElementTextureCoordinateScale"].GetValueVector2() == SecondMapping.Scale &&
                Effect.Parameters["ElementTextureCoordinateOffset"].GetValueVector2() == SecondMapping.Offset &&
                Effect.Parameters["ElementPosition"].GetValueVector2() == ElementPosition &&
                Effect.Parameters["ElementSize"].GetValueVector2() == ElementSize;
        }

        private static MGEffectFillBrush CreateRoleBrush(Effect Effect, int Role, Color Accent)
            => new(Effect)
            {
                UseStandardParameters = true,
                Parameters = new[]
                {
                    new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, Role),
                    new MGEffectParameterValue("CustomColor", MGEffectParameterType.Color, Accent.ToVector4())
                }
            };

        private static bool IsRole(Effect Effect, int Role, Color Accent, float Selected, float Disabled)
            => Effect.Parameters["CustomInt"].GetValueInt32() == Role &&
               Effect.Parameters["CustomColor"].GetValueVector4() == Accent.ToVector4() &&
               Near(Effect.Parameters["SelectedAmount"].GetValueSingle(), Selected) &&
               Near(Effect.Parameters["DisabledAmount"].GetValueSingle(), Disabled);

        private static MGStandardEffectParameterValues CreateValues(VisualState State, float Opacity, float Time)
            => MGEffectFillBrush.CalculateStandardParameters(TimeSpan.FromSeconds(Time), Matrix.CreateTranslation(2, 3, 0),
                new Viewport(0, 0, 640, 480), false, State, new Point(5, 6), Opacity, new Rectangle(10, 20, 30, 40));

        private static bool Try(Action Action)
        {
            try { Action(); return true; }
            catch { return false; }
        }

        private static bool Near(float A, float B) => Math.Abs(A - B) < 0.0001f;
    }
}
