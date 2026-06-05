using System;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        private static Effect CreateEffect()
        {
            return (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
        }
    }
}
