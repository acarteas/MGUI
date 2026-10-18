using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGInteractiveVisualStateTests
{
    private static void ChangedThenStable(MGElement element, Action change, bool paintOnly = true)
    {
        SetField(element, "_IsLayoutValid", true);
        MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
        change();
        Assert.True(element.VisualRevision > before.Visual);
        if (paintOnly)
        {
            Assert.Equal(before.Layout, element.LayoutRevision);
            Assert.True(element.IsLayoutValid);
        }
        MGRenderRevisionSnapshot changed = element.GetRenderRevisionSnapshot();
        change();
        Assert.True(element.IsRenderRevisionCurrent(changed));
    }

    [Fact]
    public void ButtonAndToggleStatePreserveCommandsAndNoopAssignments()
    {
        MGWindow window = CreateWindow();
        MGButton button = new(window);
        ChangedThenStable(button, () => button.PressedContentOffset = new(2, 3));
        MGRenderRevisionSnapshot before = button.GetRenderRevisionSnapshot();
        button.CommandName = "new command";
        button.IsRepeatButton = true;
        button.RepeatInterval = TimeSpan.FromMilliseconds(50);
        button.UpdateRepeatState(DateTime.Now, false);
        Assert.True(button.IsRenderRevisionCurrent(before));
        MGToggleButton toggle = new(window);
        ChangedThenStable(toggle, () => toggle.IsChecked = true);
        IFillBrush selected = new MGSolidFillBrush(Color.Pink);
        ChangedThenStable(toggle, () => toggle.CheckedBackgroundBrush = selected);
        ChangedThenStable(toggle, () => toggle.CheckedTextForeground = Color.Yellow);
    }

    [Fact]
    public void CheckboxMarksInvalidateTheActualChildPaintStageAndParentOnce()
    {
        MGCheckBox check = new(CreateWindow());
        foreach (Action change in new Action[]
        {
            () => check.CheckMarkColor = Color.Pink,
            () => check.IsCheckMarkShadowed = true,
            () => check.CheckMarkShadowColor = Color.Yellow,
            () => check.CheckMarkShadowOffset = new(3, 4),
            () => check.IsChecked = true,
            () => { check.IsThreeState = true; check.IsChecked = null; }
        })
        {
            MGRenderRevisionSnapshot child = check.ButtonElement.GetRenderRevisionSnapshot();
            ulong parent = check.VisualRevision;
            ChangedThenStable(check, change);
            Assert.True(check.ButtonElement.VisualRevision > child.Visual);
            Assert.Equal(parent + 1, check.VisualRevision);
        }
        Assert.Null(typeof(MGElement).GetField("OnEndingDraw", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(check.ButtonElement));
    }

    [Fact]
    public void RadioGroupInvalidatesOldAndNewSelectionAndSemanticHoverOnly()
    {
        MGWindow window = CreateWindow();
        MGRadioButtonGroup group = new(window, "radio");
        MGRadioButton first = new(window, group);
        MGRadioButton second = new(window, group);
        MGRenderRevisionSnapshot old = first.GetRenderRevisionSnapshot();
        ChangedThenStable(second, () => group.CheckedItem = second);
        Assert.True(first.VisualRevision > old.Visual);
        ChangedThenStable(second, () => second.BubbleComponentBorderColor = Color.Pink);
        ChangedThenStable(second, () => second.BubbleComponentBorderThickness = 3);
        ChangedThenStable(second, () => second.BubbleCheckedColor = Color.Blue);
        ChangedThenStable(second, () => second.SetBubbleVisualState(SecondaryVisualState.Hovered));
        ChangedThenStable(second, () => second.SetBubbleVisualState(SecondaryVisualState.Pressed));
        ulong before = second.VisualRevision;
        second.BubbleComponentBackground.FocusedColor = Color.Yellow;
        Assert.True(second.VisualRevision > before);
    }

    [Fact]
    public void SliderVisualInputsAndNestedSourcesAreTrackedWithoutValueLayout()
    {
        MGSlider slider = new(CreateWindow(), 0, 100, 25);
        foreach (Action change in new Action[]
        {
            () => slider.Value = 50,
            () => slider.Minimum = 10,
            () => slider.Maximum = 120,
            () => slider.NumberLineBorderThickness = new(3),
            () => slider.TickFrequency = 10,
            () => slider.TickBorderThickness = new(2),
            () => slider.ThumbBorderThickness = new(3)
        })
        {
            ChangedThenStable(slider, change);
        }
        MGPaddedFillBrush fill = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        slider.ThumbFillBrush = fill;
        ulong before = slider.VisualRevision;
        fill.Padding = new(2);
        Assert.True(slider.VisualRevision > before);
        before = slider.VisualRevision;
        slider.FocusBrush.FocusedColor = Color.Pink;
        Assert.True(slider.VisualRevision > before);
        ChangedThenStable(slider, () => slider.Orientation = Orientation.Vertical, false);
        ChangedThenStable(slider, () => slider.ThumbWidth = 21, false);
        ChangedThenStable(slider, () => slider.ThumbHeight = 22, false);
        ChangedThenStable(slider, () => slider.NumberLineSize = 12, false);
        ChangedThenStable(slider, () => slider.DrawTicks = true, false);
        ChangedThenStable(slider, () => slider.TickWidth = 4, false);
        ChangedThenStable(slider, () => slider.TickHeight = 14, false);
    }

    [Fact]
    public void RatingCommittedPreviewAndAllShapeDecorationsAreSemantic()
    {
        MGRatingControl rating = new(CreateWindow());
        foreach (Action change in new Action[]
        {
            () => rating.ItemShape = RatingItemShape.Circle,
            () => rating.SetValue(2),
            () => typeof(MGRatingControl).GetProperty(nameof(rating.PreviewValue))!.SetValue(rating, 2.5f),
            () => rating.UnfilledShapeStrokeThickness = 3,
            () => rating.UnfilledShapeStrokeColor = Color.Pink,
            () => rating.UnfilledShapeFillColor = Color.Cyan,
            () => rating.FilledShapeStrokeThickness = 3,
            () => rating.FilledShapeStrokeColor = Color.Pink,
            () => rating.FilledShapeFillColor = Color.Cyan,
            () => rating.PreviewShapeStrokeThickness = 3,
            () => rating.PreviewShapeStrokeColor = Color.Pink,
            () => rating.PreviewShapeFillColor = Color.Cyan
        })
        {
            ChangedThenStable(rating, change);
        }
        ChangedThenStable(rating, () => rating.ItemSize = 24, false);
        ChangedThenStable(rating, () => rating.Spacing = 8, false);
    }

    [Fact]
    public void ProgressValuesAndReferencedGradientAreTracked()
    {
        MGWindow window = CreateWindow();
        MGProgressBar bar = new(window);
        foreach (Action change in new Action[]
        {
            () => bar.Value = 50,
            () => bar.Minimum = 10,
            () => bar.Maximum = 120,
            () => bar.IsReversed = true
        })
        {
            ChangedThenStable(bar, change);
        }
        MGProgressButton button = new(window, new(1), MGUniformBorderBrush.Black);
        button.ProgressBarForeground = new MGProgressBarGradientBrush(bar);
        ulong before = button.VisualRevision;
        bar.Value = 60;
        Assert.True(button.VisualRevision > before);
        foreach (Action change in new Action[]
        {
            () => button.Value = 50,
            () => button.Minimum = 10,
            () => button.Maximum = 120,
            () => button.IsReversed = true,
            () => button.ProgressBarBorderThickness = new(3)
        })
        {
            ChangedThenStable(button, change);
        }
        before = bar.VisualRevision;
        bar.CompletedBrush.NormalValue = new MGSolidFillBrush(Color.Pink);
        Assert.True(bar.VisualRevision > before);
    }

    [Fact]
    public void RunningBuiltinsCannotBeMadeStaticByClearingUserFlag()
    {
        MGWindow window = CreateWindow();
        MGTimer timer = new(window, TimeSpan.FromSeconds(30));
        MGStopwatch stopwatch = new(window);
        MGProgressButton button = new(window, new(1), MGUniformBorderBrush.Black);
        Assert.False(timer.IsBuiltInRenderVolatile);
        Assert.False(stopwatch.IsBuiltInRenderVolatile);
        Assert.False(button.IsBuiltInRenderVolatile);
        ChangedThenStable(timer, () => timer.IsPaused = false);
        ChangedThenStable(stopwatch, () => stopwatch.IsRunning = true);
        button.Duration = TimeSpan.FromSeconds(2);
        ChangedThenStable(button, () => button.IsPaused = false);
        foreach (MGElement element in new MGElement[] { timer, stopwatch, button })
        {
            element.IsRenderVolatile = false;
            Assert.True(element.IsBuiltInRenderVolatile);
        }
        ChangedThenStable(timer, () => timer.IsPaused = true);
        ChangedThenStable(stopwatch, () => stopwatch.IsRunning = false);
        ChangedThenStable(button, () => button.IsPaused = true);
        Assert.False(timer.IsBuiltInRenderVolatile);
        Assert.False(stopwatch.IsBuiltInRenderVolatile);
        Assert.False(button.IsBuiltInRenderVolatile);
        ChangedThenStable(timer, () => timer.RemainingDuration = TimeSpan.FromSeconds(20));
        ChangedThenStable(stopwatch, () => stopwatch.Elapsed = TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ColorPickerSelectionHoverResourcesAndRawSameCountPaintRefresh()
    {
        MGGridColorPicker picker = new(CreateWindow(), 2, Color.Red, Color.Blue);
        picker.ShowSelectedColorLabel = true;
        ChangedThenStable(picker, () => picker.SelectedColor = Color.Red, false);
        ChangedThenStable(picker, () => picker.SelectedColor = Color.Blue);
        ChangedThenStable(picker, () => picker.SelectedColorBorderThickness = new(4));
        ChangedThenStable(picker, () => picker.UnselectedColorBorderThickness = new(2));
        ChangedThenStable(picker, () => typeof(MGGridColorPicker).GetProperty(nameof(picker.HoveredColorIndex))!.SetValue(picker, 1));
        MGPaddedFillBrush overlay = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        picker.HoveredColorOverlay = overlay;
        ulong before = picker.VisualRevision;
        overlay.Padding = new(2);
        Assert.True(picker.VisualRevision > before);
        List<Color> colors = (List<Color>)picker.Colors;
        colors[1] = Color.Yellow;
        MGRenderRevisionSnapshot raw = picker.GetRenderRevisionSnapshot();
        picker.InvalidateVisual();
        Assert.Equal(raw.Layout, picker.LayoutRevision);
        Assert.Equal(Color.Yellow, Assert.IsType<MGSolidFillBrush>(picker.SelectedColorValue.Fill).Color);
        ((List<int>)picker.SelectedColorIndexes)[0] = 0;
        raw = picker.GetRenderRevisionSnapshot();
        picker.InvalidateVisual();
        Assert.Equal(raw.Layout, picker.LayoutRevision);
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(picker.SelectedColorValue.Fill).Color);
        picker.AllowMultiSelect = true;
        ChangedThenStable(picker, () => picker.SelectedColorIndexes = new[] { 0, 1 });
        ChangedThenStable(picker, () => picker.Columns = 1, false);
        ChangedThenStable(picker, () => picker.ColorSize = new(24, 25), false);
        ChangedThenStable(picker, () => picker.RowSpacing = 9, false);
        ChangedThenStable(picker, () => picker.ColumnSpacing = 9, false);
    }

    [Fact]
    public void PickerPreviewRectangleTracksItsOwnDecorationsAndFillSources()
    {
        MGGridColorPicker picker = new(CreateWindow(), 2, Color.Red, Color.Blue);
        MGRectangle preview = picker.SelectedColorValue;
        ChangedThenStable(preview, () => preview.Stroke = Color.Yellow);
        ChangedThenStable(preview, () => preview.StrokeThickness = 3);
        MGPaddedFillBrush fill = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        ChangedThenStable(preview, () => preview.Fill = fill);
        ulong before = preview.VisualRevision;
        fill.Padding = new(2);
        Assert.True(preview.VisualRevision > before);
    }

    [Fact]
    public void ExplicitAncestorInvalidationRefreshesObservedDescendantRawSourceGraphs()
    {
        MGWindow window = CreateWindow();
        MGButton ancestor = new(window);
        MGRectangle child = new(window, 16, 16, Color.Black, 1, Color.Red);
        MGCompositedFillBrush composite = new(new MGSolidFillBrush(Color.Blue));
        child.Fill = composite;
        ancestor.SetContent(child);
        MGRenderRevisionSnapshot before = ancestor.GetRenderRevisionSnapshot();
        Assert.True(child.BaseRenderSourceDependencies.SupportsRenderCaching);
        MGHighlightFillBrush animated = new();
        composite.Brushes.Add(animated);
        ancestor.InvalidateVisual();
        Assert.False(child.BaseRenderSourceDependencies.SupportsRenderCaching);
        Assert.True(ancestor.VisualRevision > before.Visual);
        before = ancestor.GetRenderRevisionSnapshot();
        composite.Brushes.Remove(animated);
        ancestor.InvalidateVisual();
        Assert.True(child.BaseRenderSourceDependencies.SupportsRenderCaching);
        Assert.True(ancestor.VisualRevision > before.Visual);
        Assert.Equal(before.Layout, ancestor.LayoutRevision);
    }

    [Fact]
    public void UnobservedInteractiveControlsAndExplicitPaintRefreshStayLazy()
    {
        MGWindow window = CreateWindow();
        SourceProbe source = new(window);
        source.ThumbFillBrush = new MGPaddedFillBrush(new MGSolidFillBrush(Color.Red), new Thickness(0));
        source.InvalidateVisual();
        source.Value = 50;
        Assert.Equal(0, source.Discoveries);
        _ = source.VisualRevision;
        Assert.Equal(1, source.Discoveries);
        for (int i = 0; i < 100; i++)
        {
            _ = source.GetRenderRevisionSnapshot();
            source.Value = 50;
        }
        Assert.Equal(1, source.Discoveries);
    }

    private sealed class SourceProbe : MGSlider
    {
        internal int Discoveries;
        internal SourceProbe(MGWindow window) : base(window, 0, 100, 20) { }
        protected override IEnumerable<object> EnumerateControlRenderSources()
        {
            Discoveries++;
            return base.EnumerateControlRenderSources();
        }
    }

    [Fact]
    public void RadioMouseFeedbackTransitionsAreObservedDuringNormalUpdates()
    {
        MGWindow window = CreateWindow();
        MGRadioButton radio = new(window, "mouse-radio");
        SetProperty(radio.ButtonComponent.BaseElement, nameof(MGElement.LayoutBounds), new Rectangle(5, 5, 16, 16));
        InputTracker input = window.Desktop.InputTracker;
        ElementUpdateArgs args = new(default, true, false, true, Point.Zero, new Rectangle(0, 0, 50, 50));
        input.Update(new(TimeSpan.Zero, TimeSpan.Zero, new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), default));
        MGRenderRevisionSnapshot before = radio.GetRenderRevisionSnapshot();
        radio.UpdateSelf(args);
        Assert.True(radio.VisualRevision > before.Visual);
        before = radio.GetRenderRevisionSnapshot();
        radio.UpdateSelf(args);
        Assert.True(radio.IsRenderRevisionCurrent(before));
        input.Update(new(TimeSpan.Zero, TimeSpan.Zero, new MouseState(10, 10, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), default));
        radio.UpdateSelf(args);
        Assert.True(radio.VisualRevision > before.Visual);
    }

    [Fact]
    public void TimeUpdatesStillChangeValuesWhilePausedTicksAndZeroScaleStayStable()
    {
        MGWindow window = CreateWindow();
        MGTimer timer = new(window, TimeSpan.FromSeconds(30));
        MGStopwatch stopwatch = new(window);
        MGProgressButton button = new(window, new(1), MGUniformBorderBrush.Black) { Duration = TimeSpan.FromSeconds(10) };
        ElementUpdateArgs tick = new(new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.25), default, default), true, false, true, Point.Zero, Rectangle.Empty);
        foreach (MGElement element in new MGElement[] { timer, stopwatch, button })
        {
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
            element.UpdateSelf(tick);
            Assert.True(element.IsRenderRevisionCurrent(before));
        }
        timer.IsPaused = false;
        stopwatch.IsRunning = true;
        button.IsPaused = false;
        timer.UpdateSelf(tick);
        stopwatch.UpdateSelf(tick);
        button.UpdateSelf(tick);
        Assert.Equal(TimeSpan.FromSeconds(29.75), timer.RemainingDuration);
        Assert.Equal(TimeSpan.FromSeconds(0.25), stopwatch.Elapsed);
        Assert.Equal(2.5f, button.Value);
        timer.TimeScale = 0;
        stopwatch.TimeScale = 0;
        foreach (MGElement element in new MGElement[] { timer, stopwatch })
        {
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
            element.UpdateSelf(tick);
            Assert.True(element.IsRenderRevisionCurrent(before));
            Assert.True(element.IsBuiltInRenderVolatile);
        }
    }

    [Fact]
    public void EveryInteractiveBrushSlotTracksMutationAndDetachesReplacedSource()
    {
        MGWindow window = CreateWindow();
        foreach ((MGElement element, string property) in new (MGElement, string)[]
        {
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.NumberLineFillBrush)),
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.TickFillBrush)),
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.ThumbFillBrush)),
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.Foreground)),
            (new MGProgressButton(window, new(1), MGUniformBorderBrush.Black), nameof(MGProgressButton.ProgressBarBackground)),
            (new MGProgressButton(window, new(1), MGUniformBorderBrush.Black), nameof(MGProgressButton.ProgressBarForeground)),
            (new MGGridColorPicker(window, 2, Color.Red, Color.Blue), nameof(MGGridColorPicker.HoveredColorOverlay)),
            (new MGGridColorPicker(window, 2, Color.Red, Color.Blue), nameof(MGGridColorPicker.SelectedColorOverlay))
        })
        {
            PropertyInfo slot = element.GetType().GetProperty(property)!;
            MGPaddedFillBrush brush = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
            ChangedThenStable(element, () => slot.SetValue(element, brush));
            ulong before = element.VisualRevision;
            brush.Padding = new(2);
            Assert.True(element.VisualRevision > before, property);
            slot.SetValue(element, new MGSolidFillBrush(Color.Blue));
            MGRenderRevisionSnapshot detached = element.GetRenderRevisionSnapshot();
            brush.Padding = new(3);
            Assert.True(element.IsRenderRevisionCurrent(detached), property);
        }
    }

    [Fact]
    public void ExplicitFormattingEscapeRefreshesCapturedConverterStateWithoutLayout()
    {
        MGWindow window = CreateWindow();
        string formatted = "first";
        MGTimer timer = new(window, TimeSpan.FromSeconds(10)) { RemainingDurationToString = _ => formatted };
        MGStopwatch stopwatch = new(window) { ElapsedToString = _ => formatted };
        MGButton owner = new(window);
        owner.SetContent(timer);
        _ = owner.VisualRevision;
        MGRenderRevisionSnapshot timerBefore = timer.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot stopwatchBefore = stopwatch.GetRenderRevisionSnapshot();
        formatted = "other";
        owner.InvalidateVisual();
        stopwatch.InvalidateVisual();
        Assert.Contains("other", timer.ValueComponent.BaseElement is MGTextBlock timerText ? timerText.Text : "");
        Assert.Contains("other", stopwatch.ValueComponent.BaseElement is MGTextBlock stopwatchText ? stopwatchText.Text : "");
        Assert.Equal(timerBefore.Layout, timer.LayoutRevision);
        Assert.Equal(stopwatchBefore.Layout, stopwatch.LayoutRevision);
    }

    [Fact]
    public void BorderAndStateBrushSlotsTrackAllInteractiveLocalSources()
    {
        MGWindow window = CreateWindow();
        foreach ((MGElement element, string property) in new (MGElement, string)[]
        {
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.NumberLineBorderBrush)),
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.TickBorderBrush)),
            (new MGSlider(window, 0, 100, 20), nameof(MGSlider.ThumbBorderBrush)),
            (new MGProgressButton(window, new(1), MGUniformBorderBrush.Black), nameof(MGProgressButton.ProgressBarBorderBrush)),
            (new MGGridColorPicker(window, 2, Color.Red, Color.Blue), nameof(MGGridColorPicker.SelectedColorBorderBrush)),
            (new MGGridColorPicker(window, 2, Color.Red, Color.Blue), nameof(MGGridColorPicker.UnselectedColorBorderBrush))
        })
        {
            PropertyInfo slot = element.GetType().GetProperty(property)!;
            MGPaddedFillBrush fill = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
            IBorderBrush border = new MGUniformBorderBrush(fill);
            ChangedThenStable(element, () => slot.SetValue(element, border));
            ulong before = element.VisualRevision;
            fill.Padding = new(2);
            Assert.True(element.VisualRevision > before, property);
        }
        MGProgressBar progress = new(window);
        VisualStateFillBrush completed = new(new MGSolidFillBrush(Color.Red));
        VisualStateFillBrush incomplete = new(new MGSolidFillBrush(Color.Blue));
        ChangedThenStable(progress, () => progress.CompletedBrush = completed);
        ChangedThenStable(progress, () => progress.IncompleteBrush = incomplete);
        ulong previous = progress.VisualRevision;
        incomplete.SelectedValue = new MGSolidFillBrush(Color.Yellow);
        Assert.True(progress.VisualRevision > previous);
        MGSlider slider = new(window, 0, 100, 20);
        VisualStateFillBrush focus = new(new MGSolidFillBrush(Color.Red));
        ChangedThenStable(slider, () => slider.FocusBrush = focus);
        MGRadioButton radio = new(window, "source-radio");
        VisualStateColorBrush bubble = new(Color.Red);
        ChangedThenStable(radio, () => radio.BubbleComponentBackground = bubble);
    }

    [Fact]
    public void ExplicitAncestorInvalidationAdvancesStaticDescendantAndAncestorOnce()
    {
        MGWindow window = CreateWindow();
        MGButton ancestor = new(window);
        MGRectangle child = new(window, 16, 16, Color.Black, 1, Color.Red);
        MGCompositedFillBrush composite = new(new MGSolidFillBrush(Color.Blue));
        child.BackgroundBrush = new(new MGCachedFillBrush(composite));
        ancestor.SetContent(child);
        MGRenderRevisionSnapshot before = ancestor.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot childBefore = child.GetRenderRevisionSnapshot();
        composite.Brushes[0] = new MGSolidFillBrush(Color.Yellow);
        ancestor.InvalidateVisual();
        Assert.Equal(before.Visual + 1, ancestor.VisualRevision);
        Assert.Equal(childBefore.Visual + 1, child.VisualRevision);
        Assert.Equal(before.Layout, ancestor.LayoutRevision);
        Assert.Equal(childBefore.Layout, child.LayoutRevision);
    }

    [Fact]
    public void ExplicitSubtreeEpochOverflowForwardsOneNormalizedMutationStamp()
    {
        MGWindow window = CreateWindow();
        MGButton ancestor = new(window);
        MGRectangle child = new(window, 16, 16, Color.Black, 1, Color.Red);
        ancestor.SetContent(child);
        _ = ancestor.GetRenderRevisionSnapshot();
        _ = child.GetRenderRevisionSnapshot();
        SetField(ancestor, "CurrentVisualRevision", ulong.MaxValue);
        ancestor.InvalidateVisual();
        Assert.Equal(2UL, ancestor.VisualRevision);
        Assert.Equal(2UL, child.VisualRevision);
        Assert.Same(ancestor.GetRenderRevisionSnapshot().Epoch, child.GetRenderRevisionSnapshot().Epoch);
    }

    [Fact]
    public void LateModalAssignmentImmediatelyInvalidatesObservedWindowPaintContext()
    {
        MGWindow window = CreateWindow();
        MGRadioButton radio = new(window, "late-modal");
        window.SetContent(radio);
        radio.SetBubbleVisualState(SecondaryVisualState.Hovered);
        MGWindow modal = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeWindow(modal, window.Desktop, MGElementType.Window);
        MGRenderRevisionSnapshot before = radio.GetRenderRevisionSnapshot();
        MGRenderRevisionSnapshot windowBefore = window.GetRenderRevisionSnapshot();
        window.ModalWindow = modal;
        Assert.Equal(before.Visual + 1, radio.VisualRevision);
        Assert.Equal(windowBefore.Visual + 1, window.VisualRevision);
        Assert.Equal(before.Layout, radio.LayoutRevision);
        MGRenderRevisionSnapshot changed = radio.GetRenderRevisionSnapshot();
        window.ModalWindow = modal;
        Assert.True(radio.IsRenderRevisionCurrent(changed));
        window.ModalWindow = null;
        Assert.Equal(changed.Visual + 1, radio.VisualRevision);
        Assert.Equal(changed.Layout, radio.LayoutRevision);
    }

    [Fact]
    public void RadioRetainsPointerFeedbackWhileModalIsActiveForLateRemoval()
    {
        MGWindow window = CreateWindow();
        MGRadioButton radio = new(window, "modal-feedback");
        window.SetContent(radio);
        SetProperty(radio.ButtonComponent.BaseElement, nameof(MGElement.LayoutBounds), new Rectangle(5, 5, 16, 16));
        MGWindow modal = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeWindow(modal, window.Desktop, MGElementType.Window);
        window.ModalWindow = modal;
        InputTracker input = window.Desktop.InputTracker;
        input.Update(new(TimeSpan.Zero, TimeSpan.Zero, new MouseState(10, 10, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), default));
        radio.UpdateSelf(new(default, true, false, true, Point.Zero, new Rectangle(0, 0, 50, 50)));
        Assert.Equal(SecondaryVisualState.Hovered, typeof(MGRadioButton).GetField("BubbleVisualState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(radio));
        MGRenderRevisionSnapshot before = radio.GetRenderRevisionSnapshot();
        window.ModalWindow = null;
        Assert.Equal(before.Visual + 1, radio.VisualRevision);
    }

    private static MGWindow CreateWindow()
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
        SetField(renderer, "<Host>k__BackingField", new FixedRenderHost());
        SetField(renderer, "<Input>k__BackingField", new InputTracker());
        MGUI.Shared.Text.FontManager fonts = new();
        SetField(renderer, "<FontManager>k__BackingField", fonts);
        byte[] fontBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoSans-Regular.ttf"));
        global::FontStashSharp.FontSystem fontSystem = new();
        fontSystem.AddFont(fontBytes);
        MGUI.FontStashSharp.FontStashSharpTextEngine engine = new();
        engine.AddFontSystem("TestFont", MGUI.Shared.Text.CustomFontStyles.Normal, fontSystem, fontBytes);
        SetField(renderer, "_textEngine", engine);
        SetField(desktop, "<Renderer>k__BackingField", renderer);
        SetField(desktop, "_UIScale", new MGScaleSettings());
        SetField(desktop, "<Resources>k__BackingField", new MGResources(new MGTheme("TestFont")));
        SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
        MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeWindow(window, desktop, MGElementType.Window);
        return window;
    }

    private static void InitializeWindow(MGWindow window, MGDesktop desktop, MGElementType elementType)
    {
        SetField(window, "<Desktop>k__BackingField", desktop);
        SetField(window, "<ElementType>k__BackingField", elementType);
        SetField(window, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
        SetField(window, "<Components>k__BackingField", new List<MGComponentBase>());
        SetField(window, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
        SetField(window, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        SetField(window, "_NestedWindows", new List<MGWindow>());
        SetField(window, "<RadioButtonGroups>k__BackingField", new Dictionary<string, MGRadioButtonGroup>());
        SetField(window, "_CanChangeContent", true);
        SetField(window, "_Scale", 1.0f);
        SetField(window, "_UnscaledScreenSpaceToScaledScreenSpace", Matrix.Identity);
        SetField(window, "_ScaledScreenSpaceToUnscaledScreenSpace", Matrix.Identity);
    }

    private static void SetProperty(object element, string name, object value)
        => typeof(MGElement).GetProperty(name)!.SetValue(element, value);

    private static void SetField(object instance, string name, object? value)
    {
        for (Type? type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }
        }
        throw new MissingFieldException(instance.GetType().FullName, name);
    }

    private sealed class FixedRenderHost : IRenderHost
    {
        public GraphicsDevice GraphicsDevice => null!;
        public event EventHandler<TimeSpan> PreviewUpdate { add { } remove { } }
        public event EventHandler<EventArgs> EndUpdate { add { } remove { } }
        public Rectangle GetBounds() => new(0, 0, 800, 600);
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;
        public object GetService(Type serviceType) => null!;
    }
}
