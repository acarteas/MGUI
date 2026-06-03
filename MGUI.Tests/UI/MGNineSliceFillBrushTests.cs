using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI
{
    public class MGNineSliceFillBrushTests
    {
        [Fact]
        public void EffectiveTargetMargin_ScalesUniformMarginByBorderScale()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);

            Thickness actual = MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(26));

            Assert.Equal(new Thickness(39), actual);
        }

        [Fact]
        public void EffectiveTargetMargin_ScalesAsymmetricMarginsSideBySide()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);

            Thickness actual = MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(1, 2, 3, 4));

            Assert.Equal(new Thickness(2, 3, 5, 6), actual);
        }

        [Fact]
        public void EffectiveTargetMargin_UsesUnscaledMarginWhenElementUIScaleIsDisabled()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);
            element.UIScaleMode = UIScaleMode.Disabled;

            Thickness actual = MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(26));

            Assert.Equal(new Thickness(26), actual);
        }

        [Fact]
        public void SourceMarginRegionCalculation_UsesRawTexturePixels()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);
            Thickness sourceMargin = new(7, 11, 13, 17);
            Rectangle sourceBounds = new(10, 20, 100, 80);

            MGNineSliceFillBrush.NineSliceRegions actual = MGNineSliceFillBrush.GetRegions(sourceBounds, sourceMargin);

            Assert.Equal(new Thickness(11, 17, 20, 26), MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(7, 11, 13, 17)));
            Assert.Equal(new Rectangle(10, 20, 7, 11), actual.TopLeft);
            Assert.Equal(new Rectangle(17, 20, 80, 11), actual.TopCenter);
            Assert.Equal(new Rectangle(97, 20, 13, 11), actual.TopRight);
            Assert.Equal(new Rectangle(10, 31, 7, 52), actual.MiddleLeft);
            Assert.Equal(new Rectangle(17, 31, 80, 52), actual.MiddleCenter);
            Assert.Equal(new Rectangle(97, 31, 13, 52), actual.MiddleRight);
            Assert.Equal(new Rectangle(10, 83, 7, 17), actual.BottomLeft);
            Assert.Equal(new Rectangle(17, 83, 80, 17), actual.BottomCenter);
            Assert.Equal(new Rectangle(97, 83, 13, 17), actual.BottomRight);
        }

        [Fact]
        public void DestinationRegionCalculation_UsesEffectiveTargetMargin()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 2.0f);
            Rectangle destinationBounds = new(10, 20, 200, 100);

            MGNineSliceFillBrush.NineSliceRegions actual = MGNineSliceFillBrush.GetDestinationRegions(element, destinationBounds, new Thickness(8));

            Assert.Equal(new Rectangle(26, 36, 168, 68), actual.MiddleCenter);
        }

        [Fact]
        public void DestinationRegionCalculation_UsesAsymmetricEffectiveTargetMargin()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);
            Rectangle destinationBounds = new(10, 20, 100, 80);

            MGNineSliceFillBrush.NineSliceRegions actual = MGNineSliceFillBrush.GetDestinationRegions(element, destinationBounds, new Thickness(4, 8, 12, 16));

            Assert.Equal(new Rectangle(16, 32, 76, 44), actual.MiddleCenter);
        }

        [Fact]
        public void Copy_CopiesInteriorBrush()
        {
            TestFillBrush interiorBrush = new();
            MGNineSliceFillBrush brush = new(
                new Thickness(10),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                interiorBrush);

            MGNineSliceFillBrush actual = Assert.IsType<MGNineSliceFillBrush>(brush.Copy());

            TestFillBrush copiedInteriorBrush = Assert.IsType<TestFillBrush>(actual.InteriorBrush);
            Assert.NotSame(interiorBrush, copiedInteriorBrush);
            Assert.True(interiorBrush.WasCopied);
        }

        private static TestElement CreateElement(Action<MGScaleSettings> configureScale)
        {
            MGScaleSettings scale = new();
            configureScale(scale);

            MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
            SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
            desktop.UIScale = scale;

            MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
            InitializeElementForScaleRefresh(window);
            SetField(window, "<Desktop>k__BackingField", desktop);
            SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
            SetField(window, "_NestedWindows", new List<MGWindow>());

            TestElement element = (TestElement)RuntimeHelpers.GetUninitializedObject(typeof(TestElement));
            InitializeElementForScaleRefresh(element);
            SetField(element, "<ParentWindow>k__BackingField", window);
            SetField(element, "<ElementType>k__BackingField", MGElementType.Border);
            return element;
        }

        private static void InitializeElementForScaleRefresh(MGElement element)
        {
            SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
            SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
            SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
            SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        }

        private static void SetField(object instance, string name, object? value)
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

        private sealed class TestFillBrush : IFillBrush
        {
            public bool WasCopied { get; private set; }

            public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
            {
            }

            public IFillBrush Copy()
            {
                WasCopied = true;
                return new TestFillBrush();
            }
        }
    }
}
