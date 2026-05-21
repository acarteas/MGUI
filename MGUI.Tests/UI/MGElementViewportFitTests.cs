using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using XamlSpacer = MGUI.Core.UI.XAML.Spacer;
using XamlThickness = MGUI.Core.UI.XAML.Thickness;
#if UseWPF
using System.Xaml;
#else
using Portable.Xaml;
#endif
using Xunit;

namespace MGUI.Tests.UI
{
    public class MGElementViewportFitTests
    {
        [Fact]
        public void XamlParsing_SetsViewportFitAndViewportMargin()
        {
            string xaml = """
                <mgui:Spacer xmlns:mgui="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                             ViewportFit="WidthAndHeight"
                             ViewportMargin="48" />
                """;

            XamlSpacer spacer = (XamlSpacer)XamlServices.Parse(xaml);

            Assert.Equal(ViewportFitMode.WidthAndHeight, spacer.ViewportFit);
            Assert.Equal(new XamlThickness(48), spacer.ViewportMargin);
        }

        [Fact]
        public void None_PreservesAuthoredMaxSizeBehavior()
        {
            TestElement element = CreateElement(new Size(300, 250), new Rectangle(0, 0, 120, 100), 1.0f);
            element.MaxWidth = 180;
            element.MaxHeight = 160;

            Thickness measured = Measure(element, new Size(500, 500));

            Assert.Equal(180, measured.Width);
            Assert.Equal(160, measured.Height);
        }

        [Theory]
        [InlineData(ViewportFitMode.Width, 160, 300)]
        [InlineData(ViewportFitMode.Height, 400, 120)]
        [InlineData(ViewportFitMode.WidthAndHeight, 160, 120)]
        public void ViewportFit_AppliesSelectedAxes(ViewportFitMode fit, int expectedWidth, int expectedHeight)
        {
            TestElement element = CreateElement(new Size(400, 300), new Rectangle(0, 0, 200, 180), 1.0f);
            element.ViewportFit = fit;
            element.ViewportMargin = new Thickness(20, 30, 20, 30);

            Thickness measured = Measure(element, new Size(500, 500));

            Assert.Equal(expectedWidth, measured.Width);
            Assert.Equal(expectedHeight, measured.Height);
        }

        [Fact]
        public void ViewportFit_CombinesWithAuthoredMaxSize()
        {
            TestElement element = CreateElement(new Size(400, 300), new Rectangle(0, 0, 240, 220), 1.0f);
            element.ViewportFit = ViewportFitMode.WidthAndHeight;
            element.MaxWidth = 180;
            element.MaxHeight = 260;

            Thickness measured = Measure(element, new Size(500, 500));

            Assert.Equal(180, measured.Width);
            Assert.Equal(220, measured.Height);
        }

        [Fact]
        public void ExplicitSize_ShrinksWhenViewportCapIsSmaller()
        {
            TestElement element = CreateElement(new Size(100, 100), new Rectangle(0, 0, 220, 180), 1.0f);
            element.ViewportFit = ViewportFitMode.WidthAndHeight;
            element.PreferredWidth = 400;
            element.PreferredHeight = 300;
            element.ViewportMargin = new Thickness(10);

            Thickness measured = Measure(element, new Size(500, 500));

            Assert.Equal(200, measured.Width);
            Assert.Equal(160, measured.Height);
        }

        [Fact]
        public void MinSize_RemainsLowerBound()
        {
            TestElement element = CreateElement(new Size(50, 50), new Rectangle(0, 0, 120, 100), 1.0f);
            element.ViewportFit = ViewportFitMode.WidthAndHeight;
            element.MinWidth = 180;
            element.MinHeight = 140;

            Thickness measured = Measure(element, new Size(500, 500));

            Assert.Equal(180, measured.Width);
            Assert.Equal(140, measured.Height);
        }

        [Fact]
        public void UiScale_UsesEffectiveMeasuredSizeAgainstViewportCap()
        {
            TestElement element = CreateElement(new Size(300, 260), new Rectangle(0, 0, 500, 400), 2.0f);
            element.ViewportFit = ViewportFitMode.WidthAndHeight;
            element.ViewportMargin = new Thickness(50);

            Thickness measured = Measure(element, new Size(1000, 1000));

            Assert.Equal(400, measured.Width);
            Assert.Equal(300, measured.Height);
        }

        [Fact]
        public void StretchAlignedElement_ArrangesToViewportCapWithoutAuthoredMaxSize()
        {
            TestElement element = CreateElement(new Size(400, 300), new Rectangle(0, 0, 220, 180), 1.0f);
            element.ViewportFit = ViewportFitMode.WidthAndHeight;
            element.ViewportMargin = new Thickness(10);

            element.UpdateLayout(new Rectangle(0, 0, 500, 500));

            Assert.Equal(new Rectangle(150, 170, 200, 160), element.RenderBounds);
            Assert.Equal(element.RenderBounds, element.LayoutBounds);
        }

        [Fact]
        public void ChangingViewportFitSettings_InvalidatesLayout()
        {
            TestElement element = CreateElement(new Size(100, 100), new Rectangle(0, 0, 300, 300), 1.0f);
            SetField(element, "_IsLayoutValid", true);

            element.ViewportFit = ViewportFitMode.Width;

            Assert.False(element.IsLayoutValid);

            SetField(element, "_IsLayoutValid", true);

            element.ViewportMargin = new Thickness(24);

            Assert.False(element.IsLayoutValid);
        }

        private static Thickness Measure(TestElement element, Size availableSize)
        {
            element.UpdateMeasurement(availableSize, out _, out Thickness fullSize, out _, out _);
            return fullSize;
        }

        private static TestElement CreateElement(Size desiredSize, Rectangle viewport, float sizeScale)
        {
            MGDesktop desktop = CreateDesktop(viewport, sizeScale);
            MGWindow window = CreateWindow(desktop);
            TestElement element = (TestElement)RuntimeHelpers.GetUninitializedObject(typeof(TestElement));
            InitializeElement(element);
            SetField(element, "<ParentWindow>k__BackingField", window);
            SetField(element, "<ElementType>k__BackingField", MGElementType.Border);
            element.DesiredSize = desiredSize;
            return element;
        }

        private static MGDesktop CreateDesktop(Rectangle viewport, float sizeScale)
        {
            FixedRenderHost host = new(viewport);
            MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
            SetField(renderer, "<Host>k__BackingField", host);

            MGScaleSettings scale = new();
            scale.SizeScale = sizeScale;

            MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
            SetField(desktop, "<Renderer>k__BackingField", renderer);
            SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
            SetField(desktop, "_UIScale", scale);
            return desktop;
        }

        private static MGWindow CreateWindow(MGDesktop desktop)
        {
            MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
            InitializeElement(window);
            SetField(window, "<Desktop>k__BackingField", desktop);
            SetField(window, "<ParentWindow>k__BackingField", null);
            SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
            SetField(window, "_Scale", 1.0f);
            SetField(window, "_NestedWindows", new List<MGWindow>());
            return window;
        }

        private static void InitializeElement(MGElement element)
        {
            SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
            SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
            SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
            SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
            SetField(element, "_Visibility", Visibility.Visible);
            SetField(element, "_HorizontalAlignment", HorizontalAlignment.Stretch);
            SetField(element, "_VerticalAlignment", VerticalAlignment.Stretch);
            SetField(element, "_HorizontalContentAlignment", HorizontalAlignment.Stretch);
            SetField(element, "_VerticalContentAlignment", VerticalAlignment.Stretch);
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
            public Size DesiredSize { get; set; }

            private TestElement()
                : base(default!, MGElementType.Border)
            {
            }

            public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
            {
                SharedSize = new Thickness(0);
                Size effectiveSize = EffectiveScaleSettings.ScaleSize(DesiredSize, MGScaleCategory.Size);
                return new Thickness(effectiveSize.Width, effectiveSize.Height, 0, 0);
            }
        }

        private sealed class FixedRenderHost : IRenderHost
        {
            private readonly Rectangle _bounds;

            public FixedRenderHost(Rectangle bounds)
            {
                _bounds = bounds;
            }

            public GraphicsDevice GraphicsDevice => null!;
            public event EventHandler<TimeSpan> PreviewUpdate { add { } remove { } }
            public event EventHandler<EventArgs> EndUpdate { add { } remove { } }

            public Rectangle GetBounds() => _bounds;
            public MouseState GetMouseState() => default;
            public KeyboardState GetKeyboardState() => default;
            public object GetService(Type serviceType) => null!;
        }
    }
}
