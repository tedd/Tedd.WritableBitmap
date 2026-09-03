using Microsoft.Maui;
using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class BitmapLayoutTests
{
    [Theory]
    [InlineData(Aspect.Fill, 0, 0, 200, 200, false)]
    [InlineData(Aspect.AspectFit, 0, 50, 200, 150, true)]
    [InlineData(Aspect.AspectFill, -100, 0, 300, 200, false)]
    [InlineData(Aspect.Center, 50, 75, 150, 125, true)]
    public void CalculatesDestinationAndClearRequirement(
        Aspect aspect,
        float left,
        float top,
        float right,
        float bottom,
        bool requiresClear)
    {
        var actual = BitmapLayout.CalculateDestination(100, 50, 200, 200, aspect);

        Assert.Equal(new SKRect(left, top, right, bottom), actual);
        Assert.Equal(requiresClear, BitmapLayout.RequiresClear(actual, 200, 200));
    }

    [Theory]
    [InlineData(Aspect.AspectFit, 50, 0, 150, 200, true)]
    [InlineData(Aspect.AspectFill, 0, -100, 200, 300, false)]
    [InlineData(Aspect.Center, -50, -25, 250, 225, false)]
    public void HandlesPortraitAndOversizedContent(
        Aspect aspect,
        float left,
        float top,
        float right,
        float bottom,
        bool requiresClear)
    {
        var sourceWidth = aspect == Aspect.Center ? 300 : 50;
        var sourceHeight = aspect == Aspect.Center ? 250 : 100;
        var actual = BitmapLayout.CalculateDestination(
            sourceWidth,
            sourceHeight,
            200,
            200,
            aspect);

        Assert.Equal(new SKRect(left, top, right, bottom), actual);
        Assert.Equal(requiresClear, BitmapLayout.RequiresClear(actual, 200, 200));
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void InvalidDimensionsProduceAnEmptyDestination(
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        Assert.Equal(
            SKRect.Empty,
            BitmapLayout.CalculateDestination(
                sourceWidth,
                sourceHeight,
                targetWidth,
                targetHeight,
                Aspect.AspectFit));
    }
}
