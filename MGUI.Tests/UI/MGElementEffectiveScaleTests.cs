using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Text;
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

        [Fact]
        public void EffectiveBorderThickness_ScalesByBorderAndPreservesPositiveMinimum()
        {
            MGBorder border = CreateElement<MGBorder>(scale => scale.BorderScale = 0.25f);
            SetField(border, "_BorderThickness", new Thickness(1, 2, 0, -1));

            Assert.Equal(new Thickness(1, 2, 0, -1), border.BorderThickness);
            Assert.Equal(new Thickness(1, 1, 0, -1), border.EffectiveBorderThickness);

            Thickness measured = border.MeasureSelfOverride(new Size(100, 100), out Thickness sharedSize);
            Assert.Equal(new Thickness(0), sharedSize);
            Assert.Equal(border.EffectiveBorderThickness, measured);
        }

        [Fact]
        public void EffectiveStackPanelSpacing_ScalesWithoutChangingAuthoredValue()
        {
            MGStackPanel stackPanel = CreateElement<MGStackPanel>(scale => scale.SpacingScale = 1.5f);
            SetField(stackPanel, "_Spacing", 3);

            Assert.Equal(3, stackPanel.Spacing);
            Assert.Equal(5, stackPanel.EffectiveSpacing);
        }

        [Fact]
        public void EffectiveGridHelpers_ScaleContainerDistancesAndOnlyPixelLengths()
        {
            MGGrid grid = CreateElement<MGGrid>(scale =>
            {
                scale.SpacingScale = 1.5f;
                scale.SizeScale = 2.0f;
            });
            SetField(grid, "_RowSpacing", 3);
            SetField(grid, "_ColumnSpacing", 4);
            SetField(grid, "_GridLineMargin", 1);

            ColumnDefinition pixelColumn = new(grid, GridLength.CreatePixelLength(10), 5, 40);
            RowDefinition weightedRow = new(grid, GridLength.CreateWeightedLength(2.0), 6, 30);

            Assert.Equal(5, grid.EffectiveRowSpacing);
            Assert.Equal(6, grid.EffectiveColumnSpacing);
            Assert.Equal(2, grid.EffectiveGridLineMargin);
            Assert.Equal(GridLength.CreatePixelLength(20), grid.EffectiveGridLength(pixelColumn.Length));
            Assert.Equal(weightedRow.Length, grid.EffectiveGridLength(weightedRow.Length));
            Assert.Equal(GridLength.Auto, grid.EffectiveGridLength(GridLength.Auto));
            Assert.Equal(10, grid.EffectiveColumnMinWidth(pixelColumn));
            Assert.Equal(80, grid.EffectiveColumnMaxWidth(pixelColumn));
            Assert.Equal(12, grid.EffectiveRowMinHeight(weightedRow));
            Assert.Equal(60, grid.EffectiveRowMaxHeight(weightedRow));
        }

        [Fact]
        public void EffectiveUniformGridHelpers_ScaleSizesAndSpacingWithoutChangingAuthoredValues()
        {
            MGUniformGrid uniformGrid = CreateElement<MGUniformGrid>(scale =>
            {
                scale.SpacingScale = 1.5f;
                scale.SizeScale = 2.0f;
            });
            SetField(uniformGrid, "_CellSize", new Size(10, 12));
            SetField(uniformGrid, "_HeaderRowHeight", 8);
            SetField(uniformGrid, "_HeaderColumnWidth", 9);
            SetField(uniformGrid, "_RowSpacing", 3);
            SetField(uniformGrid, "_ColumnSpacing", 4);
            SetField(uniformGrid, "_GridLineMargin", 1);

            Assert.Equal(new Size(10, 12), uniformGrid.CellSize);
            Assert.Equal(8, uniformGrid.HeaderRowHeight);
            Assert.Equal(9, uniformGrid.HeaderColumnWidth);

            Assert.Equal(new Size(20, 24), uniformGrid.EffectiveCellSize);
            Assert.Equal(16, uniformGrid.EffectiveHeaderRowHeight);
            Assert.Equal(18, uniformGrid.EffectiveHeaderColumnWidth);
            Assert.Equal(5, uniformGrid.EffectiveRowSpacing);
            Assert.Equal(6, uniformGrid.EffectiveColumnSpacing);
            Assert.Equal(2, uniformGrid.EffectiveGridLineMargin);
        }

        [Fact]
        public void EffectiveOverlayOffset_ScalesBySpacing()
        {
            MGOverlayPanel overlayPanel = CreateElement<MGOverlayPanel>(scale => scale.SpacingScale = 1.5f);

            Thickness authoredOffset = new(1, 2, 3, 4);

            Assert.Equal(new Thickness(2, 3, 5, 6), overlayPanel.EffectiveChildOffset(authoredOffset));
            Assert.Equal(new Thickness(1, 2, 3, 4), authoredOffset);
        }

        [Fact]
        public void EffectiveProgressButtonBorderThickness_ScalesByBorder()
        {
            MGProgressButton progressButton = CreateElement<MGProgressButton>(scale => scale.BorderScale = 0.25f);
            SetField(progressButton, "_ProgressBarBorderThickness", new Thickness(1, 2, 0, -1));

            Assert.Equal(new Thickness(1, 2, 0, -1), progressButton.ProgressBarBorderThickness);
            Assert.Equal(new Thickness(1, 1, 0, -1), progressButton.EffectiveProgressBarBorderThickness);
        }

        [Fact]
        public void EffectiveControlHelpers_ScaleWithoutChangingAuthoredValues()
        {
            MGProgressBar progressBar = CreateElement<MGProgressBar>(scale => scale.SizeScale = 1.5f);
            SetField(progressBar, "_Size", 11);
            Assert.Equal(11, progressBar.Size);
            Assert.Equal(17, progressBar.EffectiveSize);

            MGProgressButton progressButton = CreateElement<MGProgressButton>(scale =>
            {
                scale.SizeScale = 1.5f;
                scale.SpacingScale = 2.0f;
            });
            SetField(progressButton, "_ProgressBarSize", 10);
            SetField(progressButton, "_ProgressBarMargin", new Thickness(1, 2, 3, 4));
            Assert.Equal(10, progressButton.ProgressBarSize);
            Assert.Equal(new Thickness(1, 2, 3, 4), progressButton.ProgressBarMargin);
            Assert.Equal(15, progressButton.EffectiveProgressBarSize);
            Assert.Equal(new Thickness(2, 4, 6, 8), progressButton.EffectiveProgressBarMargin);
        }

        [Fact]
        public void EffectiveImageAndInlineImageHelpers_ScaleByImage()
        {
            MGImage image = CreateElement<MGImage>(scale => scale.ImageScale = 1.5f);
            SetField(image, "_ActualSource", new MGTextureData(null, null, 1f, new Size(10, 12)));
            Assert.Equal(new Size(15, 18), image.EffectiveUnstretchedSize);

            MGTextBlock textBlock = CreateElement<MGTextBlock>(scale => scale.ImageScale = 2.0f);
            Vector2 inlineSize = textBlock.MeasureImage(new MGTextRunImage("Icon", 7, 9, null, null));
            Assert.Equal(new Vector2(14, 18), inlineSize);
        }

        [Fact]
        public void MGImageIntrinsicMeasurement_UsesEffectiveImageSizeForFallbacksAndAspect()
        {
            MGImage image = CreateElement<MGImage>(scale => scale.ImageScale = 1.5f);
            SetField(image, "_ActualSource", new MGTextureData(null, null, 1f, new Size(3, 2)));

            SetField(image, "_Stretch", Stretch.Fill);
            Thickness fillMeasured = image.MeasureSelfOverride(new Size(1000000, 1000000), out Thickness fillSharedSize);
            Assert.Equal(new Thickness(0), fillSharedSize);
            Assert.Equal(new Thickness(5, 3, 0, 0), fillMeasured);

            SetField(image, "_Stretch", Stretch.Uniform);
            Thickness uniformMeasured = image.MeasureSelfOverride(new Size(1000000, 12), out Thickness uniformSharedSize);
            Assert.Equal(new Thickness(0), uniformSharedSize);
            Assert.Equal(new Thickness(20, 12, 0, 0), uniformMeasured);
        }

        [Fact]
        public void EffectiveSliderHelpers_ScaleSizesAndBordersWithoutChangingAuthoredValues()
        {
            MGSlider slider = CreateElement<MGSlider>(scale =>
            {
                scale.SizeScale = 1.5f;
                scale.BorderScale = 0.25f;
            });
            SetField(slider, "_Orientation", Orientation.Horizontal);
            SetField(slider, "_NumberLineSize", 8);
            SetField(slider, "_TickWidth", 2);
            SetField(slider, "_TickHeight", 18);
            SetField(slider, "_ThumbWidth", 12);
            SetField(slider, "_ThumbHeight", 24);
            SetField(slider, "_NumberLineBorderThickness", new Thickness(2));
            SetField(slider, "_TickBorderThickness", new Thickness(2));
            SetField(slider, "_ThumbBorderThickness", new Thickness(2));

            Assert.Equal(8, slider.NumberLineSize);
            Assert.Equal(12, slider.EffectiveNumberLineSize);
            Assert.Equal(3, slider.EffectiveActualTickWidth);
            Assert.Equal(27, slider.EffectiveActualTickHeight);
            Assert.Equal(18, slider.EffectiveActualThumbWidth);
            Assert.Equal(36, slider.EffectiveActualThumbHeight);
            Assert.Equal(new Thickness(1), slider.EffectiveNumberLineBorderThickness);
            Assert.Equal(new Thickness(1), slider.EffectiveTickBorderThickness);
            Assert.Equal(new Thickness(1), slider.EffectiveThumbBorderThickness);
        }

        [Fact]
        public void EffectiveScrollResizeSpacerAndChoiceHelpers_ScaleControlChrome()
        {
            MGScrollViewer scrollViewer = CreateElement<MGScrollViewer>(scale =>
            {
                scale.SizeScale = 1.5f;
                scale.SpacingScale = 2.0f;
            });
            Assert.Equal(24, scrollViewer.EffectiveVSBWidth);
            Assert.Equal(24, scrollViewer.EffectiveHSBHeight);
            Assert.Equal(4, scrollViewer.EffectiveScrollBarPadding);
            Assert.Equal(12, scrollViewer.EffectiveMinScrollBarThumbSize);
            Assert.Equal(60, scrollViewer.EffectiveVerticalScrollInterval);

            MGResizeGrip resizeGrip = CreateElement<MGResizeGrip>(scale =>
            {
                scale.SizeScale = 2.0f;
                scale.SpacingScale = 1.5f;
            });
            SetField(resizeGrip, "_MaxDots", 4);
            SetField(resizeGrip, "_Spacing", 3);
            SetField(resizeGrip, "_Margin", new Thickness(0, 0, 2, 0));
            Assert.Equal(5, resizeGrip.EffectiveSpacing);
            Assert.Equal(2, resizeGrip.EffectiveDotSize);
            Assert.Equal(20, resizeGrip.EffectiveSize);

            MGSpacer spacer = CreateElement<MGSpacer>(scale => scale.SizeScale = 1.5f);
            SetField(spacer, "_Width", 10);
            SetField(spacer, "_Height", 12);
            Assert.Equal(15, spacer.EffectiveWidth);
            Assert.Equal(18, spacer.EffectiveHeight);

            MGRadioButton radioButton = CreateElement<MGRadioButton>(scale => scale.BorderScale = 0.25f);
            SetField(radioButton, "_BubbleComponentBorderThickness", 2.0f);
            Assert.Equal(0.5f, radioButton.EffectiveBubbleComponentBorderThickness);
        }

        [Fact]
        public void EffectiveContainerHelpers_PreserveValuesAtScaleOne()
        {
            MGGrid grid = CreateElement<MGGrid>(_ => { });
            SetField(grid, "_RowSpacing", 3);
            SetField(grid, "_ColumnSpacing", 4);
            SetField(grid, "_GridLineMargin", 1);

            MGUniformGrid uniformGrid = CreateElement<MGUniformGrid>(_ => { });
            SetField(uniformGrid, "_CellSize", new Size(10, 12));
            SetField(uniformGrid, "_HeaderRowHeight", 8);
            SetField(uniformGrid, "_HeaderColumnWidth", 9);
            SetField(uniformGrid, "_RowSpacing", 3);
            SetField(uniformGrid, "_ColumnSpacing", 4);
            SetField(uniformGrid, "_GridLineMargin", 1);

            MGBorder border = CreateElement<MGBorder>(_ => { });
            SetField(border, "_BorderThickness", new Thickness(1, 2, 3, 4));

            Assert.Equal(3, grid.EffectiveRowSpacing);
            Assert.Equal(4, grid.EffectiveColumnSpacing);
            Assert.Equal(1, grid.EffectiveGridLineMargin);
            Assert.Equal(new Size(10, 12), uniformGrid.EffectiveCellSize);
            Assert.Equal(8, uniformGrid.EffectiveHeaderRowHeight);
            Assert.Equal(9, uniformGrid.EffectiveHeaderColumnWidth);
            Assert.Equal(3, uniformGrid.EffectiveRowSpacing);
            Assert.Equal(4, uniformGrid.EffectiveColumnSpacing);
            Assert.Equal(1, uniformGrid.EffectiveGridLineMargin);
            Assert.Equal(new Thickness(1, 2, 3, 4), border.EffectiveBorderThickness);
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
