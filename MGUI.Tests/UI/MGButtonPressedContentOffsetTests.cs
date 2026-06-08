using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.UI;

public class MGButtonPressedContentOffsetTests
{
    [Theory]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.None, 10, 20)]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Hovered, 10, 20)]
    [InlineData(PrimaryVisualState.Selected, SecondaryVisualState.None, 10, 20)]
    [InlineData(PrimaryVisualState.Disabled, SecondaryVisualState.Pressed, 10, 20)]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Pressed, 12, 24)]
    public void CalculateContentDrawOffset_OnlyMovesEnabledPressedContent(
        PrimaryVisualState primary, SecondaryVisualState secondary, int expectedX, int expectedY)
    {
        MGScaleSettings scale = new() { SpacingScale = 2.0f };

        Point result = MGButton.CalculateContentDrawOffset(
            new Point(10, 20), new VisualState(primary, secondary), new Point(1, 2), scale);

        Assert.Equal(new Point(expectedX, expectedY), result);
    }

    [Fact]
    public void CalculateContentDrawOffset_DefaultZeroPreservesGeometry()
    {
        Point drawOffset = new(12, 34);

        Point result = MGButton.CalculateContentDrawOffset(
            drawOffset, new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.Pressed), Point.Zero, new MGScaleSettings());

        Assert.Equal(drawOffset, result);
    }
}
