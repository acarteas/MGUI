using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI
{
    public class MGElementEffectiveScaleTests
    {
        [Fact]
        public void EffectiveLayoutHelpers_ScaleWithoutChangingAuthoredValues()
        {
            TestElement element = CreateElement<TestElement>(scale =>
            {
                scale.SpacingScale = 1.5f;
                scale.SizeScale = 2.0f;
            });

            SetField(element, "_Margin", new Thickness(1, 2, 3, 4));
            SetField(element, "_Padding", new Thickness(5, 6, 7, 8));
            SetField(element, "_BackgroundRenderPadding", new Thickness(2));
            SetField(element, "_MinWidth", 10);
            SetField(element, "_MinHeight", 20);
            SetField(element, "_MaxWidth", 100);
            SetField(element, "_MaxHeight", 200);
            SetField(element, "_PreferredWidth", 30);
            SetField(element, "_PreferredHeight", 40);

            Assert.Equal(new Thickness(1, 2, 3, 4), element.Margin);
            Assert.Equal(new Thickness(5, 6, 7, 8), element.Padding);
            Assert.Equal(30, element.PreferredWidth);

            Assert.Equal(new Thickness(2, 3, 5, 6), element.EffectiveMargin);
            Assert.Equal(new Thickness(8, 9, 11, 12), element.EffectivePadding);
            Assert.Equal(new Thickness(3), element.EffectiveBackgroundRenderPadding);
            Assert.Equal(new Size(20, 40), element.EffectiveMinSize);
            Assert.Equal(new Size(200, 400), element.EffectiveMaxSize);
            Assert.Equal(60, element.EffectivePreferredWidth);
            Assert.Equal(80, element.EffectivePreferredHeight);
            Assert.Equal(67, element.EffectivePreferredWidthIncludingMargin);
            Assert.Equal(89, element.EffectivePreferredHeightIncludingMargin);
        }

        [Fact]
        public void EffectiveTextHelpers_ScaleByFontScale()
        {
            MGTextBlock textBlock = CreateElement<MGTextBlock>(scale => scale.FontScale = 1.5f);
            SetField(textBlock, "_FontSize", 11);
            SetField(textBlock, "_LinePadding", 2.0f);

            Assert.Equal(11, textBlock.FontSize);
            Assert.Equal(17, textBlock.EffectiveFontSize);
            Assert.Equal(3.0f, textBlock.EffectiveLinePadding);
            Assert.Equal(new Vector2(3, -6), textBlock.EffectiveTextShadowOffset(new Vector2(2, -4)));
        }

        [Fact]
        public void EffectiveFontSize_PreservesPositiveMinimum()
        {
            MGTextBlock textBlock = CreateElement<MGTextBlock>(scale => scale.FontScale = 0.1f);
            SetField(textBlock, "_FontSize", 1);

            Assert.Equal(1, textBlock.EffectiveFontSize);
        }

        private static T CreateElement<T>(Action<MGScaleSettings> configureScale)
            where T : MGElement
        {
            MGScaleSettings scale = new();
            configureScale(scale);

            MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
            SetField(desktop, "_UIScale", scale);

            MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
            SetField(window, "<Desktop>k__BackingField", desktop);

            T element = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            SetField(element, "<ParentWindow>k__BackingField", window);
            SetField(element, "<ElementType>k__BackingField", MGElementType.Border);
            return element;
        }

        private static void SetField(object instance, string name, object value)
        {
            Type? type = instance.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        private sealed class TestElement : MGElement
        {
            private TestElement()
                : base(default(MGWindow), MGElementType.Border)
            {
            }
        }
    }
}
