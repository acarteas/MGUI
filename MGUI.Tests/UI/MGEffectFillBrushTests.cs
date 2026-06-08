using System;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MGUI.Tests.UI
{
    public class MGEffectFillBrushTests
    {
        [Fact]
        public void Constructor_StoresEffect()
        {
            Effect effect = CreateEffect();

            MGEffectFillBrush brush = new(effect);

            Assert.Same(effect, brush.Effect);
            Assert.Null(brush.ConfigureEffect);
            Assert.False(brush.UseStandardParameters);
        }

        [Fact]
        public void Constructor_StoresConfigureCallback()
        {
            Effect effect = CreateEffect();
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> configureEffect = (_, _, _, _) => { };

            MGEffectFillBrush brush = new(effect, configureEffect);

            Assert.Same(effect, brush.Effect);
            Assert.Same(configureEffect, brush.ConfigureEffect);
        }

        [Fact]
        public void Constructor_AllowsNullEffect()
        {
            MGEffectFillBrush brush = new(null);

            Assert.Null(brush.Effect);
        }

        [Fact]
        public void Copy_PreservesEffectAndConfigureCallbackReferences()
        {
            Effect effect = CreateEffect();
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> configureEffect = (_, _, _, _) => { };
            MGEffectFillBrush brush = new(effect, configureEffect);

            MGEffectFillBrush copy = Assert.IsType<MGEffectFillBrush>(brush.Copy());

            Assert.NotSame(brush, copy);
            Assert.Same(effect, copy.Effect);
            Assert.Same(configureEffect, copy.ConfigureEffect);
        }

        [Fact]
        public void Copy_PreservesAutomaticAndCustomParameterConfiguration()
        {
            Effect effect = CreateEffect();
            IReadOnlyList<MGEffectParameterValue> parameters = new[]
            {
                new MGEffectParameterValue("Role", MGEffectParameterType.Int, 2)
            };
            MGEffectFillBrush brush = new(effect)
            {
                UseStandardParameters = true,
                Parameters = parameters
            };

            MGEffectFillBrush copy = Assert.IsType<MGEffectFillBrush>(brush.Copy());

            Assert.True(copy.UseStandardParameters);
            Assert.NotSame(parameters, copy.Parameters);
            Assert.NotSame(brush.Parameters, copy.Parameters);
            Assert.Equal(parameters, copy.Parameters);
        }

        [Fact]
        public void CalculateStandardParameters_AppliesSpriteEffectHalfPixelConventionWhenEnabled()
        {
            Viewport viewport = new(0, 0, 800, 600);

            MGStandardEffectParameterValues values = MGEffectFillBrush.CalculateStandardParameters(
                TimeSpan.Zero, Matrix.Identity, viewport, true, default, Point.Zero, 1, new Rectangle(0, 0, 1, 1));

            Matrix expected = Matrix.CreateOrthographicOffCenter(0, 800, 600, 0, 0, -1);
            expected.M41 += -0.5f * expected.M11;
            expected.M42 += -0.5f * expected.M22;
            Assert.Equal(expected, values.MatrixTransform);
        }

        [Theory]
        [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.None, 0, 0, 0, 0)]
        [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Hovered, 1, 0, 0, 0)]
        [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Pressed, 0, 1, 0, 0)]
        [InlineData(PrimaryVisualState.Selected, SecondaryVisualState.None, 0, 0, 1, 0)]
        [InlineData(PrimaryVisualState.Disabled, SecondaryVisualState.None, 0, 0, 0, 1)]
        public void CalculateStandardParameters_MapsVisualStates(
            PrimaryVisualState primary,
            SecondaryVisualState secondary,
            float expectedHover,
            float expectedPress,
            float expectedSelected,
            float expectedDisabled)
        {
            MGStandardEffectParameterValues values = MGEffectFillBrush.CalculateStandardParameters(
                TimeSpan.Zero,
                Matrix.Identity,
                new Viewport(0, 0, 800, 600),
                false,
                new VisualState(primary, secondary),
                Point.Zero,
                1.0f,
                new Rectangle(0, 0, 1, 1));

            Assert.Equal(expectedHover, values.HoverAmount);
            Assert.Equal(expectedPress, values.PressAmount);
            Assert.Equal(expectedSelected, values.SelectedAmount);
            Assert.Equal(expectedDisabled, values.DisabledAmount);
        }

        [Fact]
        public void CalculateStandardParameters_UsesDrawTransformBoundsOpacityAndTime()
        {
            Matrix transform = Matrix.CreateTranslation(3, 4, 0);
            Viewport viewport = new(0, 0, 800, 600);

            MGStandardEffectParameterValues values = MGEffectFillBrush.CalculateStandardParameters(
                TimeSpan.FromSeconds(12.5),
                transform,
                viewport,
                false,
                new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None),
                new Point(7, 9),
                0.4f,
                new Rectangle(11, 13, 0, 25));

            Matrix projection = Matrix.CreateOrthographicOffCenter(0, 800, 600, 0, 0, -1);
            Assert.Equal(transform * projection, values.MatrixTransform);
            Assert.Equal(new Vector2(18, 22), values.ElementPosition);
            Assert.Equal(new Vector2(1, 25), values.ElementSize);
            Assert.Equal(0.4f, values.Opacity);
            Assert.Equal(12.5f, values.TimeSeconds);
        }

        private static Effect CreateEffect()
        {
            return (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
        }
    }
}
