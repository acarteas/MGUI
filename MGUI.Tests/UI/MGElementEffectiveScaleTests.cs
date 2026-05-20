using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Text;
using MGUI.Shared.Helpers;
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

        [Fact]
        public void UIScaleMode_DefaultInherit_PreservesScaledBehavior()
        {
            TestElement element = CreateElement<TestElement>(scale => scale.SpacingScale = 1.5f);
            SetField(element, "_Margin", new Thickness(2, 4, 6, 8));

            Assert.Equal(UIScaleMode.Inherit, element.UIScaleMode);
            Assert.Equal(UIScaleMode.Enabled, element.DerivedUIScaleMode);
            Assert.True(element.IsUIScaleEffectivelyEnabled);
            Assert.Equal(new Thickness(3, 6, 9, 12), element.EffectiveMargin);
            Assert.Equal(new Thickness(2, 4, 6, 8), element.Margin);
        }

        [Fact]
        public void UIScaleMode_DisabledParentMakesChildResolveUnscaledValues()
        {
            TestElement parent = CreateElement<TestElement>(scale => scale.SpacingScale = 2.0f);
            TestElement child = CreateElement<TestElement>(scale => scale.SpacingScale = 2.0f);
            SetField(child, "_Parent", parent);
            SetField(parent, "_UIScaleMode", UIScaleMode.Disabled);
            SetField(child, "_Margin", new Thickness(3));
            SetField(child, "_Padding", new Thickness(4));
            SetField(child, "_PreferredWidth", 10);

            Assert.Equal(UIScaleMode.Disabled, child.DerivedUIScaleMode);
            Assert.False(child.IsUIScaleEffectivelyEnabled);
            Assert.Equal(new Thickness(3), child.EffectiveMargin);
            Assert.Equal(new Thickness(4), child.EffectivePadding);
            Assert.Equal(10, child.EffectivePreferredWidth);
            Assert.Equal(new Thickness(3), child.Margin);
            Assert.Equal(new Thickness(4), child.Padding);
            Assert.Equal(10, child.PreferredWidth);
        }

        [Fact]
        public void UIScaleMode_ExplicitChildEnabledReEnablesScalingUnderDisabledParent()
        {
            TestElement parent = CreateElement<TestElement>(scale => scale.SpacingScale = 2.0f);
            TestElement child = CreateElement<TestElement>(scale => scale.SpacingScale = 2.0f);
            SetField(child, "_Parent", parent);
            SetField(parent, "_UIScaleMode", UIScaleMode.Disabled);
            SetField(child, "_UIScaleMode", UIScaleMode.Enabled);
            SetField(child, "_Margin", new Thickness(3));

            Assert.Equal(UIScaleMode.Enabled, child.DerivedUIScaleMode);
            Assert.True(child.IsUIScaleEffectivelyEnabled);
            Assert.Equal(new Thickness(6), child.EffectiveMargin);
        }

        [Fact]
        public void PerWindowUIScaleOverride_ReplacesDesktopScale()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SpacingScale = 2.0f);
            MGWindow window = CreateWindow(desktop, null);
            MGScaleSettings windowScale = new();
            windowScale.SpacingScale = 1.5f;
            SetField(window, "_UIScaleOverride", windowScale);

            TestElement element = CreateElement<TestElement>(desktop, window);
            SetField(element, "_Margin", new Thickness(10));

            Assert.Same(windowScale, window.ResolvedUIScaleSettings);
            Assert.Equal(new Thickness(15), element.EffectiveMargin);
        }

        [Fact]
        public void NestedWindows_InheritParentOverrideWhenNull()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SpacingScale = 2.0f);
            MGWindow parentWindow = CreateWindow(desktop, null);
            MGScaleSettings parentScale = new();
            parentScale.SpacingScale = 1.25f;
            SetField(parentWindow, "_UIScaleOverride", parentScale);

            MGWindow nestedWindow = CreateWindow(desktop, parentWindow);
            TestElement element = CreateElement<TestElement>(desktop, nestedWindow);
            SetField(element, "_Margin", new Thickness(8));

            Assert.Same(parentScale, nestedWindow.ResolvedUIScaleSettings);
            Assert.Equal(new Thickness(10), element.EffectiveMargin);
        }

        [Fact]
        public void NestedWindows_UseOwnOverrideWhenProvided()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SpacingScale = 2.0f);
            MGWindow parentWindow = CreateWindow(desktop, null);
            MGScaleSettings parentScale = new();
            parentScale.SpacingScale = 1.25f;
            SetField(parentWindow, "_UIScaleOverride", parentScale);

            MGWindow nestedWindow = CreateWindow(desktop, parentWindow);
            MGScaleSettings nestedScale = new();
            nestedScale.SpacingScale = 1.75f;
            SetField(nestedWindow, "_UIScaleOverride", nestedScale);

            TestElement element = CreateElement<TestElement>(desktop, nestedWindow);
            SetField(element, "_Margin", new Thickness(8));

            Assert.Same(nestedScale, nestedWindow.ResolvedUIScaleSettings);
            Assert.Equal(new Thickness(14), element.EffectiveMargin);
        }

        [Fact]
        public void UIScaleModeSetter_OnWindowInvalidatesModalWindowSubtree()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SpacingScale = 2.0f);
            MGWindow parentWindow = CreateWindow(desktop, null);
            MGWindow modalWindow = CreateWindow(desktop, parentWindow);
            SetField(parentWindow, "_ModalWindow", modalWindow);
            SetField(parentWindow, "_IsLayoutValid", true);
            SetField(modalWindow, "_IsLayoutValid", true);

            parentWindow.UIScaleMode = UIScaleMode.Disabled;

            Assert.False(parentWindow.IsLayoutValid);
            Assert.False(modalWindow.IsLayoutValid);
            Assert.Equal(UIScaleMode.Disabled, modalWindow.DerivedUIScaleMode);
        }

        [Fact]
        public void DesktopUIScaleSetter_InvalidatesModalWindows()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SpacingScale = 1.0f);
            MGWindow parentWindow = CreateWindow(desktop, null);
            MGWindow modalWindow = CreateWindow(desktop, parentWindow);
            SetField(parentWindow, "_ModalWindow", modalWindow);
            desktop.Windows.Add(parentWindow);
            SetField(parentWindow, "_IsLayoutValid", true);
            SetField(modalWindow, "_IsLayoutValid", true);

            MGScaleSettings replacementScale = new();
            replacementScale.SpacingScale = 1.5f;
            desktop.UIScale = replacementScale;

            Assert.False(parentWindow.IsLayoutValid);
            Assert.False(modalWindow.IsLayoutValid);
        }

        [Fact]
        public void DesktopSetUniformUIScale_UpdatesAllCategoriesAndInvalidatesLayouts()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(1.0f));
            MGWindow window = CreateWindow(desktop, null);
            desktop.Windows.Add(window);
            SetField(window, "_IsLayoutValid", true);

            desktop.SetUniformUIScale(1.5f);

            Assert.Equal(1.5f, desktop.UIScale.FontScale);
            Assert.Equal(1.5f, desktop.UIScale.SpacingScale);
            Assert.Equal(1.5f, desktop.UIScale.SizeScale);
            Assert.Equal(1.5f, desktop.UIScale.BorderScale);
            Assert.Equal(1.5f, desktop.UIScale.ImageScale);
            Assert.False(window.IsLayoutValid);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(0.0f)]
        [InlineData(-1.0f)]
        public void DesktopSetUniformUIScale_InvalidValuesThrow(float scale)
        {
            MGDesktop desktop = CreateDesktop(settings => settings.SetUniformScale(1.0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => desktop.SetUniformUIScale(scale));
        }

        [Fact]
        public void WindowSetUniformUIScaleOverride_CreatesOverrideAndInvalidatesSubtree()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(2.0f));
            MGWindow window = CreateWindow(desktop, null);
            SetField(window, "_IsLayoutValid", true);

            window.SetUniformUIScaleOverride(1.5f);

            Assert.NotNull(window.UIScaleOverride);
            Assert.Equal(new MGScaleSnapshot(1.5f, 1.5f, 1.5f, 1.5f, 1.5f), window.ResolvedUIScaleSnapshot);
            Assert.False(window.IsLayoutValid);
        }

        [Fact]
        public void WindowSetUniformUIScaleOverride_ReusesExistingOverride()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(2.0f));
            MGWindow window = CreateWindow(desktop, null);

            window.SetUniformUIScaleOverride(1.25f);
            MGScaleSettings originalOverride = window.UIScaleOverride;
            window.SetUniformUIScaleOverride(1.75f);

            Assert.Same(originalOverride, window.UIScaleOverride);
            Assert.Equal(new MGScaleSnapshot(1.75f, 1.75f, 1.75f, 1.75f, 1.75f), window.ResolvedUIScaleSnapshot);
        }

        [Fact]
        public void WindowSetUniformUIScaleOverride_NullClearsOverrideAndFallsBackToDesktop()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(2.0f));
            MGWindow window = CreateWindow(desktop, null);
            TestElement element = CreateElement<TestElement>(desktop, window);

            window.SetUniformUIScaleOverride(1.5f);
            window.SetUniformUIScaleOverride(null);

            Assert.Null(window.UIScaleOverride);
            Assert.Equal(new MGScaleSnapshot(2.0f, 2.0f, 2.0f, 2.0f, 2.0f), window.ResolvedUIScaleSnapshot);
            Assert.Equal(new MGScaleSnapshot(2.0f, 2.0f, 2.0f, 2.0f, 2.0f), element.EffectiveUIScaleSnapshot);
        }

        [Fact]
        public void EffectiveUIScaleSnapshot_ReflectsDesktopScale()
        {
            TestElement element = CreateElement<TestElement>(scale =>
            {
                scale.FontScale = 1.25f;
                scale.SpacingScale = 1.5f;
                scale.SizeScale = 1.75f;
                scale.BorderScale = 2.0f;
                scale.ImageScale = 2.25f;
            });

            Assert.Equal(new MGScaleSnapshot(1.25f, 1.5f, 1.75f, 2.0f, 2.25f), element.EffectiveUIScaleSnapshot);
        }

        [Fact]
        public void EffectiveUIScaleSnapshot_ReflectsWindowOverrideReplacement()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(2.0f));
            MGWindow window = CreateWindow(desktop, null);
            TestElement element = CreateElement<TestElement>(desktop, window);

            window.SetUniformUIScaleOverride(1.5f);

            Assert.Equal(new MGScaleSnapshot(1.5f, 1.5f, 1.5f, 1.5f, 1.5f), window.ResolvedUIScaleSnapshot);
            Assert.Equal(new MGScaleSnapshot(1.5f, 1.5f, 1.5f, 1.5f, 1.5f), element.EffectiveUIScaleSnapshot);
        }

        [Fact]
        public void EffectiveUIScaleSnapshot_DisabledElementReportsUnscaledValues()
        {
            TestElement element = CreateElement<TestElement>(scale => scale.SetUniformScale(2.0f));
            element.UIScaleMode = UIScaleMode.Disabled;

            Assert.Equal(new MGScaleSnapshot(1.0f, 1.0f, 1.0f, 1.0f, 1.0f), element.EffectiveUIScaleSnapshot);
        }

        [Fact]
        public void EffectiveUIScaleSnapshot_ExplicitEnabledUnderDisabledParentReportsResolvedScale()
        {
            TestElement parent = CreateElement<TestElement>(scale => scale.SetUniformScale(2.0f));
            TestElement child = CreateElement<TestElement>(parent.GetDesktop(), parent.ParentWindow);
            SetField(child, "_Parent", parent);
            parent.UIScaleMode = UIScaleMode.Disabled;
            child.UIScaleMode = UIScaleMode.Enabled;

            Assert.Equal(new MGScaleSnapshot(2.0f, 2.0f, 2.0f, 2.0f, 2.0f), child.EffectiveUIScaleSnapshot);
        }

        [Fact]
        public void ComboBoxDropdownTemplatedContent_ResolvesCurrentScaleAfterRuntimeDesktopScaleChanges()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(1.0f));
            MGWindow parentWindow = CreateWindow(desktop, null);
            MGWindow dropdownWindow = CreateWindow(desktop, parentWindow);
            MGStackPanel dropdownStackPanel = CreateStackPanel(desktop, dropdownWindow);
            MGButton dropdownItem = CreateContentButton(desktop, dropdownWindow);
            TemplatedElement<string, MGButton> templatedItem = new("Alpha", dropdownItem);
            desktop.Windows.Add(parentWindow);
            parentWindow.AddNestedWindow(dropdownWindow);
            SetField(dropdownWindow, "_Content", dropdownStackPanel);
            SetField(dropdownStackPanel, "_Parent", dropdownWindow);
            dropdownStackPanel.TryAddChild(templatedItem.Element);
            SetField(templatedItem.Element, "_Parent", dropdownStackPanel);
            SetField(dropdownItem, "_IsLayoutValid", true);

            desktop.SetUniformUIScale(1.5f);

            Assert.Same(dropdownItem, templatedItem.Element);
            Assert.Equal(new MGScaleSnapshot(1.5f, 1.5f, 1.5f, 1.5f, 1.5f), dropdownItem.EffectiveUIScaleSnapshot);
            Assert.False(dropdownItem.IsLayoutValid);
        }

        [Fact]
        public void ComboBoxDropdownTemplatedContent_ResolvesCurrentScaleAfterRuntimeWindowOverrideChanges()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(2.0f));
            MGWindow parentWindow = CreateWindow(desktop, null);
            MGWindow dropdownWindow = CreateWindow(desktop, parentWindow);
            MGStackPanel dropdownStackPanel = CreateStackPanel(desktop, dropdownWindow);
            MGButton dropdownItem = CreateContentButton(desktop, dropdownWindow);
            TemplatedElement<string, MGButton> templatedItem = new("Alpha", dropdownItem);
            parentWindow.AddNestedWindow(dropdownWindow);
            SetField(dropdownWindow, "_Content", dropdownStackPanel);
            SetField(dropdownStackPanel, "_Parent", dropdownWindow);
            dropdownStackPanel.TryAddChild(templatedItem.Element);
            SetField(templatedItem.Element, "_Parent", dropdownStackPanel);

            parentWindow.SetUniformUIScaleOverride(1.25f);
            parentWindow.SetUniformUIScaleOverride(1.75f);

            Assert.Equal(new MGScaleSnapshot(1.75f, 1.75f, 1.75f, 1.75f, 1.75f), dropdownItem.EffectiveUIScaleSnapshot);
            Assert.Equal(new MGScaleSnapshot(1.75f, 1.75f, 1.75f, 1.75f, 1.75f), dropdownWindow.ResolvedUIScaleSnapshot);
        }

        [Fact]
        public void TabHeaderTemplateContent_ResolvesCurrentScaleAfterRuntimeWindowOverrideChanges()
        {
            MGDesktop desktop = CreateDesktop(scale => scale.SetUniformScale(2.0f));
            MGWindow window = CreateWindow(desktop, null);
            MGTabControl tabControl = CreateElement<MGTabControl>(desktop, window);
            MGStackPanel headersPanel = CreateStackPanel(desktop, window);
            MGTabItem tab = CreateElement<MGTabItem>(desktop, window);
            TestElement headerContent = CreateElement<TestElement>(desktop, window);
            MGButton oldHeaderWrapper = CreateContentButton(desktop, window);
            Dictionary<MGTabItem, MGButton> actualTabHeaders = new()
            {
                [tab] = oldHeaderWrapper
            };
            SetField(tabControl, "<HeadersPanelElement>k__BackingField", headersPanel);
            SetField(tabControl, "<ActualTabHeaders>k__BackingField", actualTabHeaders);
            SetField(tabControl, "_SelectedTab", tab);
            SetField(tab, "<TabControl>k__BackingField", tabControl);
            SetField(tab, "_Header", headerContent);
            SetField(oldHeaderWrapper, "_Parent", headersPanel);
            headersPanel.TryAddChild(oldHeaderWrapper);
            SetField(tabControl, "_SelectedTabHeaderTemplate", new Func<MGTabItem, MGButton>(_ => CreateContentButton(desktop, window)));
            SetField(tabControl, "_UnselectedTabHeaderTemplate", new Func<MGTabItem, MGButton>(_ => CreateContentButton(desktop, window)));

            window.SetUniformUIScaleOverride(1.5f);
            InvokePrivate(tabControl, "UpdateHeaderWrapper", tab);
            MGButton newHeaderWrapper = actualTabHeaders[tab];

            Assert.NotSame(oldHeaderWrapper, newHeaderWrapper);
            Assert.Equal(new MGScaleSnapshot(1.5f, 1.5f, 1.5f, 1.5f, 1.5f), newHeaderWrapper.EffectiveUIScaleSnapshot);
            Assert.Equal(new MGScaleSnapshot(1.5f, 1.5f, 1.5f, 1.5f, 1.5f), headerContent.EffectiveUIScaleSnapshot);

            window.SetUniformUIScaleOverride(1.75f);

            Assert.Equal(new MGScaleSnapshot(1.75f, 1.75f, 1.75f, 1.75f, 1.75f), newHeaderWrapper.EffectiveUIScaleSnapshot);
            Assert.Equal(new MGScaleSnapshot(1.75f, 1.75f, 1.75f, 1.75f, 1.75f), headerContent.EffectiveUIScaleSnapshot);
        }

        private static T CreateElement<T>(Action<MGScaleSettings> configureScale)
            where T : MGElement
        {
            MGDesktop desktop = CreateDesktop(configureScale);
            MGWindow window = CreateWindow(desktop, null);

            return CreateElement<T>(desktop, window);
        }

        private static T CreateElement<T>(MGDesktop desktop, MGWindow window)
            where T : MGElement
        {
            T element = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            InitializeElementForScaleRefresh(element);
            SetField(element, "<ParentWindow>k__BackingField", window);
            SetField(element, "<ElementType>k__BackingField", MGElementType.Border);
            return element;
        }

        private static MGStackPanel CreateStackPanel(MGDesktop desktop, MGWindow window)
        {
            MGStackPanel stackPanel = CreateElement<MGStackPanel>(desktop, window);
            SetField(stackPanel, "<_Children>k__BackingField", new ObservableCollection<MGElement>());
            SetField(stackPanel, "_CanChangeContent", true);
            return stackPanel;
        }

        private static MGButton CreateContentButton(MGDesktop desktop, MGWindow window)
        {
            MGButton button = CreateElement<MGButton>(desktop, window);
            SetField(button, "_CanChangeContent", true);
            return button;
        }

        private static MGDesktop CreateDesktop(Action<MGScaleSettings> configureScale)
        {
            MGScaleSettings scale = new();
            configureScale(scale);

            MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
            SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
            desktop.UIScale = scale;
            return desktop;
        }

        private static MGWindow CreateWindow(MGDesktop desktop, MGWindow? parentWindow)
        {
            MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
            InitializeElementForScaleRefresh(window);
            SetField(window, "<Desktop>k__BackingField", desktop);
            SetField(window, "<ParentWindow>k__BackingField", parentWindow);
            SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
            SetField(window, "_NestedWindows", new List<MGWindow>());
            return window;
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

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, name);
            }

            method.Invoke(instance, args);
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
