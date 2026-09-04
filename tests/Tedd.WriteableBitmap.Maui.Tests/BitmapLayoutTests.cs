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
    [InlineData(-1, 1, 1, 1)]
    [InlineData(1, -1, 1, 1)]
    [InlineData(1, 1, -1, 1)]
    [InlineData(1, 1, 1, -1)]
    [InlineData(int.MinValue, int.MinValue, int.MinValue, int.MinValue)]
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

    [Theory]
    [InlineData(Aspect.Fill)]
    [InlineData(Aspect.AspectFit)]
    [InlineData(Aspect.AspectFill)]
    [InlineData(Aspect.Center)]
    public void EqualSourceAndTargetDimensionsNeedNoClear(Aspect aspect)
    {
        var destination = BitmapLayout.CalculateDestination(13, 7, 13, 7, aspect);

        Assert.Equal(new SKRect(0, 0, 13, 7), destination);
        Assert.False(BitmapLayout.RequiresClear(destination, 13, 7));
    }

    [Theory]
    [InlineData(Aspect.AspectFit, 0, 2.5f, 20, 12.5f)]
    [InlineData(Aspect.AspectFill, -5, 0, 25, 15)]
    [InlineData(Aspect.Center, -10, -2.5f, 30, 17.5f)]
    [InlineData(Aspect.Fill, 0, 0, 20, 15)]
    public void DownscalingPreservesSubpixelCentering(
        Aspect aspect, float left, float top, float right, float bottom)
    {
        Assert.Equal(new SKRect(left, top, right, bottom),
            BitmapLayout.CalculateDestination(40, 20, 20, 15, aspect));
    }

    [Fact]
    public void CenterKeepsOriginalSizeWithHalfPixelOffsets()
    {
        Assert.Equal(new SKRect(3.5f, 2.5f, 6.5f, 5.5f),
            BitmapLayout.CalculateDestination(3, 3, 10, 8, Aspect.Center));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void UnknownAspectFallsBackToAspectFit(int aspect)
    {
        Assert.Equal(new SKRect(0, 50, 200, 150),
            BitmapLayout.CalculateDestination(100, 50, 200, 200, (Aspect)aspect));
    }

    [Theory]
    [InlineData(1, 0, 100, 80, true)]
    [InlineData(0, 1, 100, 80, true)]
    [InlineData(0, 0, 99, 80, true)]
    [InlineData(0, 0, 100, 79, true)]
    [InlineData(0, 0, 100, 80, false)]
    [InlineData(-1, -1, 101, 81, false)]
    [InlineData(0, 0, 0, 0, true)]
    [InlineData(0.001f, 0, 100, 80, true)]
    [InlineData(0, 0, 100, 79.999f, true)]
    public void ClearIsRequiredForAnyUncoveredEdge(
        float left, float top, float right, float bottom, bool expected)
    {
        Assert.Equal(expected,
            BitmapLayout.RequiresClear(new SKRect(left, top, right, bottom), 100, 80));
    }
}
