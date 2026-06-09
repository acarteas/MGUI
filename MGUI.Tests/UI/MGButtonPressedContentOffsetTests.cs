using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.UI;

public class MGButtonPressedContentOffsetTests
{
    [Theory]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.None, 10, 20)]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Hovered, 10, 20)]
    [InlineData(PrimaryVisualState.Selected, SecondaryVisualState.None, 10, 20)]
    [InlineData(PrimaryVisualState.Disabled, SecondaryVisualState.Pressed, 10, 20)]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Pressed, 12, 24)]
    public void DrawContents_OnlyMovesEnabledPressedContent(
        PrimaryVisualState primary, SecondaryVisualState secondary, int expectedX, int expectedY)
    {
        TestButton button = CreateButton(2.0f);
        RecordingElement content = CreateRecordingElement();
        SetContent(button, content);
        button.PressedContentOffset = new Point(1, 2);
        ElementDrawArgs drawArgs = CreateDrawArgs(primary, secondary, new Point(10, 20));

        button.InvokeDrawContents(drawArgs);

        Assert.Equal(new Point(10, 20), drawArgs.Offset);
        Assert.Equal(new Point(expectedX, expectedY), content.ReceivedDrawArgs!.Value.Offset);
        Assert.Equal(drawArgs.VisualState, content.ReceivedDrawArgs.Value.VisualState);
    }

    [Fact]
    public void DrawContents_DefaultZeroPreservesContentDrawArgs()
    {
        TestButton button = CreateButton(2.0f);
        RecordingElement content = CreateRecordingElement();
        SetContent(button, content);
        ElementDrawArgs drawArgs = CreateDrawArgs(PrimaryVisualState.Normal, SecondaryVisualState.Pressed, new Point(12, 34));

        button.InvokeDrawContents(drawArgs);

        Assert.Equal(drawArgs, content.ReceivedDrawArgs);
    }

    [Fact]
    public void DrawContents_NestedContentInheritsOneSpacingScaledTranslation()
    {
        TestButton button = CreateButton(2.0f);
        RecordingElement nested = CreateRecordingElement();
        RecordingElement content = CreateRecordingElement(nested);
        SetContent(button, content);
        button.PressedContentOffset = new Point(2, 3);
        ElementDrawArgs drawArgs = CreateDrawArgs(PrimaryVisualState.Selected, SecondaryVisualState.Pressed, new Point(10, 20));

        button.InvokeDrawContents(drawArgs);

        Point expectedOffset = new(14, 26);
        Assert.Equal(expectedOffset, content.ReceivedDrawArgs!.Value.Offset);
        Assert.Equal(expectedOffset, nested.ReceivedDrawArgs!.Value.Offset);
        Assert.Equal(1, content.DrawCount);
        Assert.Equal(1, nested.DrawCount);
    }

    [Fact]
    public void Draw_PressedContentOffsetTranslatesOnlyContentAndLeavesChromeAndClipTargetUnshifted()
    {
        DrawSnapshot baseline = DrawButton(Point.Zero);
        DrawSnapshot offset = DrawButton(new Point(3, 4));

        AssertDrawArgsEquivalent(baseline.BackgroundArgs, offset.BackgroundArgs);
        Assert.Equal(baseline.BackgroundBounds, offset.BackgroundBounds);
        AssertDrawArgsEquivalent(baseline.ComponentArgs, offset.ComponentArgs);
        AssertDrawArgsEquivalent(baseline.OverlayArgs, offset.OverlayArgs);
        Assert.Equal(baseline.OverlayBounds, offset.OverlayBounds);
        Assert.Equal(baseline.ClipTargetBounds, offset.ClipTargetBounds);
        Assert.Equal(new Point(11, 13), baseline.ContentArgs.Offset);
        Assert.Equal(new Point(17, 21), offset.ContentArgs.Offset);
    }

    [Fact]
    public void MeasurementLayoutAndHitTesting_AreIndependentOfPressedContentOffset()
    {
        TestButton baseline = CreateMeasuredButton(Point.Zero);
        TestButton offset = CreateMeasuredButton(new Point(30, 0));
        Thickness baselineSize = MeasureAndArrange(baseline);
        Thickness offsetSize = MeasureAndArrange(offset);
        RecordingElement baselineContent = Assert.IsType<RecordingElement>(baseline.Content);
        RecordingElement offsetContent = Assert.IsType<RecordingElement>(offset.Content);
        Point inside = baseline.LayoutBounds.Center;
        Point shiftedContentOnly = new(baseline.LayoutBounds.Right + 10, baseline.LayoutBounds.Center.Y);
        Point outsideBoth = new(baseline.LayoutBounds.Right + 100, baseline.LayoutBounds.Bottom + 100);

        Assert.Equal(baselineSize, offsetSize);
        Assert.Equal(baseline.LayoutBounds, offset.LayoutBounds);
        Assert.Equal(baseline.ActualLayoutBounds, offset.ActualLayoutBounds);
        Assert.Equal(baselineContent.LayoutBounds, offsetContent.LayoutBounds);
        Assert.Equal(baselineContent.Margin, offsetContent.Margin);
        Assert.Equal(baselineContent.RenderScale, offsetContent.RenderScale);
        Assert.True(IsInside(offset, inside));
        Assert.False(IsInside(offset, shiftedContentOnly));
        Assert.False(IsInside(offset, outsideBoth));
        Assert.Equal(IsInside(baseline, inside), IsInside(offset, inside));
        Assert.Equal(IsInside(baseline, shiftedContentOnly), IsInside(offset, shiftedContentOnly));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 20)]
    public void RouteLeftClickCommands_PreservesCommandPrecedenceAndHandledState(int offsetX, int offsetY)
    {
        MGResources resources = new(new MGTheme("TestFont"));
        int explicitCount = 0;
        int namedCount = 0;
        resources.AddCommand("Named", _ => { namedCount++; });
        TestButton button = CreateButton(1.0f, resources);
        button.PressedContentOffset = new Point(offsetX, offsetY);
        button.CommandName = "Named";
        button.Command = _ =>
        {
            explicitCount++;
            return false;
        };
        BaseMouseReleasedEventArgs released = CreateReleasedArgs(new Point(20, 20));

        bool invoked = button.RouteLeftClickCommands(released);

        Assert.True(invoked);
        Assert.Equal(1, explicitCount);
        Assert.Equal(1, namedCount);
        Assert.True(released.IsHandled);
        Assert.Same(button, released.HandledBy);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 20)]
    public void RouteLeftClickCommands_HandledExplicitCommandSuppressesNamedCommand(int offsetX, int offsetY)
    {
        MGResources resources = new(new MGTheme("TestFont"));
        int explicitCount = 0;
        int namedCount = 0;
        resources.AddCommand("Named", _ => { namedCount++; });
        TestButton button = CreateButton(1.0f, resources);
        button.PressedContentOffset = new Point(offsetX, offsetY);
        button.CommandName = "Named";
        button.Command = _ =>
        {
            explicitCount++;
            return true;
        };
        BaseMouseReleasedEventArgs released = CreateReleasedArgs(new Point(20, 20));

        bool invoked = button.RouteLeftClickCommands(released);

        Assert.True(invoked);
        Assert.Equal(1, explicitCount);
        Assert.Equal(0, namedCount);
        Assert.True(released.IsHandled);
        Assert.Same(button, released.HandledBy);
    }

    [Fact]
    public void UpdateRepeatState_OffsetDoesNotAffectInitialDelayCadenceOrReset()
    {
        RepeatResult baseline = ExerciseRepeatButton(Point.Zero);
        RepeatResult offset = ExerciseRepeatButton(new Point(30, 20));

        Assert.Equal(baseline.InvocationCounts, offset.InvocationCounts);
        Assert.Equal(baseline.FirstRepeatElapsed, offset.FirstRepeatElapsed);
        Assert.Equal(baseline.SecondRepeatElapsed, offset.SecondRepeatElapsed);
        Assert.Equal(baseline.PressedAtAfterReset, offset.PressedAtAfterReset);
        Assert.Equal(baseline.RepeatedAtAfterReset, offset.RepeatedAtAfterReset);
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2 }, baseline.InvocationCounts);
        Assert.Equal(TimeSpan.FromMilliseconds(500), baseline.FirstRepeatElapsed);
        Assert.Equal(TimeSpan.FromMilliseconds(100), baseline.SecondRepeatElapsed);
        Assert.Null(baseline.PressedAtAfterReset);
        Assert.Null(baseline.RepeatedAtAfterReset);
    }

    private static DrawSnapshot DrawButton(Point pressedContentOffset)
    {
        TestButton button = CreateButton(2.0f);
        RecordingFillBrush background = new();
        RecordingFillBrush overlay = new();
        RecordingElement component = CreateRecordingElement();
        RecordingElement content = CreateRecordingElement();
        button.BackgroundBrush = new VisualStateFillBrush(background);
        button.OverlayBrush = overlay;
        button.AddRecordingComponent(component);
        SetContent(button, content);
        button.PressedContentOffset = pressedContentOffset;
        SetField(button, "_VisualState", new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.Pressed));
        SetField(button, "_LayoutBounds", new Rectangle(20, 30, 100, 40));
        SetField(button, "_ClipToBounds", false);
        ElementDrawArgs drawArgs = CreateDrawArgs(PrimaryVisualState.Normal, SecondaryVisualState.Pressed, new Point(11, 13));
        Rectangle clipTargetBounds = button.GetDrawTargetBounds(drawArgs);

        button.Draw(drawArgs);

        return new(
            background.ReceivedDrawArgs!.Value,
            background.ReceivedBounds!.Value,
            component.ReceivedDrawArgs!.Value,
            content.ReceivedDrawArgs!.Value,
            overlay.ReceivedDrawArgs!.Value,
            overlay.ReceivedBounds!.Value,
            clipTargetBounds);
    }

    private static TestButton CreateMeasuredButton(Point pressedContentOffset)
    {
        TestButton button = CreateButton(1.0f);
        RecordingElement content = CreateRecordingElement();
        content.DesiredSize = new Size(40, 20);
        content.Margin = new Thickness(2, 3, 4, 5);
        content.RenderScale = new ConditionalScaleTransform(0.9f, 1.1f);
        SetContent(button, content);
        button.Padding = new Thickness(4);
        button.PressedContentOffset = pressedContentOffset;
        return button;
    }

    private static Thickness MeasureAndArrange(TestButton button)
    {
        button.UpdateMeasurement(new Size(100, 60), out _, out Thickness fullSize, out _, out _);
        button.UpdateLayout(new Rectangle(10, 20, 100, 60));
        SetField(button, "_ActualLayoutBounds", button.LayoutBounds);
        return fullSize;
    }

    private static bool IsInside(MGButton button, Point point)
        => ((IMouseViewport)button).IsInside(point.ToVector2());

    private static void AssertDrawArgsEquivalent(ElementDrawArgs expected, ElementDrawArgs actual)
    {
        Assert.Equal(expected.TS, actual.TS);
        Assert.Equal(expected.Opacity, actual.Opacity);
        Assert.Equal(expected.VisualState, actual.VisualState);
        Assert.Equal(expected.Offset, actual.Offset);
        Assert.Equal(expected.DT.CurrentSettings, actual.DT.CurrentSettings);
    }

    private static RepeatResult ExerciseRepeatButton(Point pressedContentOffset)
    {
        TestButton button = CreateButton(1.0f);
        int invocationCount = 0;
        button.PressedContentOffset = pressedContentOffset;
        button.IsRepeatButton = true;
        button.InitialRepeatInterval = TimeSpan.FromMilliseconds(500);
        button.RepeatInterval = TimeSpan.FromMilliseconds(100);
        button.Command = _ =>
        {
            invocationCount++;
            return true;
        };
        button.HandlePressedInside(CreatePressedArgs(new Point(20, 20)));
        DateTime start = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
        List<int> counts = new();

        button.UpdateRepeatState(start, true);
        counts.Add(invocationCount);
        button.UpdateRepeatState(start.AddMilliseconds(499), true);
        counts.Add(invocationCount);
        button.UpdateRepeatState(start.AddMilliseconds(500), true);
        counts.Add(invocationCount);
        DateTime firstRepeat = button.LastRepeatedAt!.Value;
        button.UpdateRepeatState(start.AddMilliseconds(599), true);
        counts.Add(invocationCount);
        button.UpdateRepeatState(start.AddMilliseconds(600), true);
        counts.Add(invocationCount);
        DateTime secondRepeat = button.LastRepeatedAt!.Value;
        button.UpdateRepeatState(start.AddMilliseconds(601), false);
        counts.Add(invocationCount);

        return new(
            counts.ToArray(),
            firstRepeat - start,
            secondRepeat - firstRepeat,
            button.RepeatPressedAt,
            button.LastRepeatedAt);
    }

    private static ElementDrawArgs CreateDrawArgs(PrimaryVisualState primary, SecondaryVisualState secondary, Point offset)
    {
        DrawTransaction transaction = (DrawTransaction)RuntimeHelpers.GetUninitializedObject(typeof(DrawTransaction));
        SetField(transaction, "<CurrentSettings>k__BackingField", new DrawSettings(Matrix.Identity, RasterizerType.Solid));
        return new ElementDrawArgs(new DrawBaseArgs(TimeSpan.Zero, transaction, 1.0f), new VisualState(primary, secondary), offset);
    }

    private static BaseMousePressedEventArgs CreatePressedArgs(Point position)
        => new(new InputTracker().Mouse, MouseButton.Left, position);

    private static BaseMouseReleasedEventArgs CreateReleasedArgs(Point position)
    {
        BaseMousePressedEventArgs pressed = CreatePressedArgs(position);
        return new BaseMouseReleasedEventArgs(pressed.Tracker, pressed, MouseButton.Left, position);
    }

    private static TestButton CreateButton(float spacingScale, MGResources? resources = null)
    {
        FixedRenderHost host = new(new Rectangle(0, 0, 500, 500));
        MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
        SetField(renderer, "<Host>k__BackingField", host);

        MGScaleSettings scale = new() { SpacingScale = spacingScale };
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        SetField(desktop, "<Renderer>k__BackingField", renderer);
        SetField(desktop, "_UIScale", scale);
        SetField(desktop, "<Resources>k__BackingField", resources ?? new MGResources(new MGTheme("TestFont")));

        MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        InitializeElement(window);
        SetField(window, "<Desktop>k__BackingField", desktop);
        SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
        SetField(window, "_UnscaledScreenSpaceToScaledScreenSpace", Matrix.Identity);
        SetField(window, "_ScaledScreenSpaceToUnscaledScreenSpace", Matrix.Identity);

        TestButton button = (TestButton)RuntimeHelpers.GetUninitializedObject(typeof(TestButton));
        InitializeElement(button);
        SetField(button, "<ParentWindow>k__BackingField", window);
        SetField(button, "<ElementType>k__BackingField", MGElementType.Button);
        return button;
    }

    private static RecordingElement CreateRecordingElement(RecordingElement? child = null)
    {
        RecordingElement element = (RecordingElement)RuntimeHelpers.GetUninitializedObject(typeof(RecordingElement));
        InitializeElement(element);
        element.Child = child;
        return element;
    }

    private static void SetContent(TestButton button, RecordingElement content)
    {
        SetField(button, "_Content", content);
        SetField(content, "<ParentWindow>k__BackingField", button.ParentWindow);
        content.SetParent(button);
    }

    private static void InitializeElement(MGElement element)
    {
        SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
        SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
        SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
        SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        SetField(element, "_Visibility", Visibility.Visible);
        SetField(element, "_IsEnabled", true);
        SetField(element, "_IsHitTestVisible", true);
        SetField(element, "_Opacity", 1.0f);
        SetField(element, "_HorizontalAlignment", HorizontalAlignment.Stretch);
        SetField(element, "_VerticalAlignment", VerticalAlignment.Stretch);
        SetField(element, "_HorizontalContentAlignment", HorizontalAlignment.Stretch);
        SetField(element, "_VerticalContentAlignment", VerticalAlignment.Stretch);
        SetField(element, "_BackgroundBrush", new VisualStateFillBrush(null));
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

    private readonly record struct DrawSnapshot(
        ElementDrawArgs BackgroundArgs,
        Rectangle BackgroundBounds,
        ElementDrawArgs ComponentArgs,
        ElementDrawArgs ContentArgs,
        ElementDrawArgs OverlayArgs,
        Rectangle OverlayBounds,
        Rectangle ClipTargetBounds);

    private readonly record struct RepeatResult(
        int[] InvocationCounts,
        TimeSpan FirstRepeatElapsed,
        TimeSpan SecondRepeatElapsed,
        DateTime? PressedAtAfterReset,
        DateTime? RepeatedAtAfterReset);

    private sealed class TestButton : MGButton
    {
        private TestButton()
            : base(default!)
        {
        }

        public void InvokeDrawContents(ElementDrawArgs drawArgs)
        {
            DrawContents(drawArgs);
        }

        public void AddRecordingComponent(RecordingElement element)
        {
            AddComponent(new MGComponent<RecordingElement>(
                element, false, false, false, false, false, false, false, (bounds, _) => bounds));
        }
    }

    private sealed class RecordingElement : MGElement
    {
        public ElementDrawArgs? ReceivedDrawArgs { get; private set; }
        public int DrawCount { get; private set; }
        public RecordingElement? Child { get; set; }
        public Size DesiredSize { get; set; }

        private RecordingElement()
            : base(default!, MGElementType.Border)
        {
        }

        public override void Draw(ElementDrawArgs drawArgs)
        {
            ReceivedDrawArgs = drawArgs;
            DrawCount++;
            Child?.Draw(drawArgs);
        }

        public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
        {
            sharedSize = new Thickness(0);
            return new Thickness(DesiredSize.Width, DesiredSize.Height, 0, 0);
        }
    }

    private sealed class RecordingFillBrush : IFillBrush
    {
        public ElementDrawArgs? ReceivedDrawArgs { get; private set; }
        public Rectangle? ReceivedBounds { get; private set; }

        public void Draw(ElementDrawArgs drawArgs, MGElement element, Rectangle bounds)
        {
            ReceivedDrawArgs = drawArgs;
            ReceivedBounds = bounds;
        }

        public IFillBrush Copy() => this;
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
