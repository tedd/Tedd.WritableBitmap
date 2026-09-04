using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class WriteableBitmapPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearingBeforeDrawControlsPixelsOutsideTheDestination(bool clearBeforeDraw)
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        using var target = new SKBitmap(3, 3);
        target.Erase(SKColors.Blue);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };

        Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
            canvas, new SKRect(1, 1, 2, 2), default, paint, clearBeforeDraw));

        for (var y = 0; y < target.Height; y++)
        for (var x = 0; x < target.Width; x++)
        {
            var expected = x == 1 && y == 1
                ? SKColors.Red
                : clearBeforeDraw ? new SKColor(0, 0, 0, 0) : SKColors.Blue;
            Assert.Equal(expected, target.GetPixel(x, y));
        }
    }

    [Theory]
    [InlineData(false, 128, 0, 127, 255)]
    [InlineData(true, 255, 0, 0, 128)]
    public void PremultipliedPixelsCompositeCorrectlyOverTheTarget(
        bool clearBeforeDraw, byte red, byte green, byte blue, byte alpha)
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromRgba(255, 0, 0, 128);
        using var target = new SKBitmap(1, 1);
        target.Erase(SKColors.Blue);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };

        Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
            canvas, new SKRect(0, 0, 1, 1), default, paint, clearBeforeDraw));

        Assert.Equal(new SKColor(red, green, blue, alpha), target.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposedBitmapDoesNotChangeTheCanvas(bool clearBeforeDraw)
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.Dispose();
        using var target = new SKBitmap(1, 1);
        target.Erase(SKColors.Blue);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint();

        Assert.Equal(BitmapDrawResult.Unavailable, bitmap.TryDraw(
            canvas, new SKRect(0, 0, 1, 1), default, paint, clearBeforeDraw));

        Assert.Equal(SKColors.Blue, target.GetPixel(0, 0));
    }

    [Fact]
    public void NearestSamplingScalesPixelsToTheDestination()
    {
        using var bitmap = new WriteableBitmap(2, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        bitmap.ToSpanUInt32()[1] = WriteableBitmap.FromColor(SKColors.Blue);
        using var target = new SKBitmap(4, 2);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };

        Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
            canvas, new SKRect(0, 0, 4, 2),
            new SKSamplingOptions(SKFilterMode.Nearest), paint, clearBeforeDraw: true));

        for (var y = 0; y < target.Height; y++)
        for (var x = 0; x < target.Width; x++)
            Assert.Equal(x < 2 ? SKColors.Red : SKColors.Blue, target.GetPixel(x, y));
    }

    [Fact]
    public void InvalidateSwitchesPresentationBackToTheRawBuffer()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        Publish(bitmap, SKColors.Blue);
        AssertCurrentColor(bitmap, SKColors.Blue);

        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Lime);
        AssertCurrentColor(bitmap, SKColors.Blue);
        bitmap.Invalidate();

        AssertCurrentColor(bitmap, SKColors.Lime);
    }

    [Fact]
    public void ClearOnlyAffectsTheRawBufferUntilItIsPublished()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        Publish(bitmap, SKColors.Blue);

        bitmap.Clear();

        AssertCurrentColor(bitmap, SKColors.Blue);
        bitmap.Invalidate();
        AssertCurrentColor(bitmap, new SKColor(0, 0, 0, 0));
    }

    [Fact]
    public void RejectedRawInvalidationPreservesThePublishedFrame()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        using var recorded = RecordCurrentFrame(bitmap);
        Publish(bitmap, SKColors.Blue);

        Assert.Throws<InvalidOperationException>(bitmap.Invalidate);
        AssertCurrentColor(bitmap, SKColors.Blue);

        recorded.Dispose();
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Lime);
        bitmap.Invalidate();
        AssertCurrentColor(bitmap, SKColors.Lime);
    }

    [Fact]
    public void ReleasingTheCurrentRecordedFrameDoesNotMakeItWritable()
    {
        using var bitmap = new WriteableBitmap(1, 1, bufferCount: 2);
        using var oldFrame = RecordCurrentFrame(bitmap);
        Publish(bitmap, SKColors.Blue);
        using var currentFrame = RecordCurrentFrame(bitmap);
        Assert.False(bitmap.TryBeginWrite(out _));

        currentFrame.Dispose();

        Assert.False(bitmap.TryBeginWrite(out _));
        AssertCurrentColor(bitmap, SKColors.Blue);
        oldFrame.Dispose();
        Publish(bitmap, SKColors.Lime);
        AssertCurrentColor(bitmap, SKColors.Lime);
    }

    [Fact]
    public async Task RendererSeesOnlyCompletedFramesFromAnotherThread()
    {
        using var bitmap = new WriteableBitmap(2, 2);
        bitmap.ToSpanUInt32().Fill(WriteableBitmap.FromColor(SKColors.Red));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishWrite = new ManualResetEventSlim();
        var producer = Task.Run(() =>
        {
            Assert.True(bitmap.TryBeginWrite(out var lease));
            try
            {
                lease.Pixels.Fill(WriteableBitmap.FromColor(SKColors.Blue));
                started.SetResult();
                Assert.True(finishWrite.Wait(TimeSpan.FromSeconds(10)), "Renderer did not release producer.");
            }
            finally
            {
                lease.Dispose();
            }
        });

        try
        {
            await await Task.WhenAny(started.Task, producer).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(bitmap.TryBeginWrite(out _));
            AssertCurrentColor(bitmap, SKColors.Red);
        }
        finally
        {
            finishWrite.Set();
            await producer.WaitAsync(TimeSpan.FromSeconds(10));
        }

        AssertCurrentColor(bitmap, SKColors.Blue);
    }

    private static void Publish(WriteableBitmap bitmap, SKColor color)
    {
        Assert.True(bitmap.TryBeginWrite(out var lease));
        try
        {
            lease.Pixels.Fill(WriteableBitmap.FromColor(color));
        }
        finally
        {
            lease.Dispose();
        }
    }

    private static SKPicture RecordCurrentFrame(WriteableBitmap bitmap)
    {
        using var recorder = new SKPictureRecorder();
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        var bounds = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        var canvas = recorder.BeginRecording(bounds);
        Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(canvas, bounds, default, paint, false));
        return recorder.EndRecording();
    }

    private static void AssertCurrentColor(WriteableBitmap bitmap, SKColor expected)
    {
        using var target = new SKBitmap(bitmap.Width, bitmap.Height);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
            canvas, new SKRect(0, 0, target.Width, target.Height), default, paint, false));

        for (var y = 0; y < target.Height; y++)
        for (var x = 0; x < target.Width; x++)
            Assert.Equal(expected, target.GetPixel(x, y));
    }
}
