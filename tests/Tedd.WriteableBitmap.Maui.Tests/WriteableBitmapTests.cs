using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class WriteableBitmapTests
{
    [Theory]
    [InlineData(0, 1, "width")]
    [InlineData(1, 0, "height")]
    [InlineData(-1, 1, "width")]
    [InlineData(1, -1, "height")]
    [InlineData(int.MaxValue, 1, "width")]
    [InlineData(1, int.MaxValue, "height")]
    public void ConstructorRejectsInvalidOrOversizedDimensions(
        int width,
        int height,
        string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WriteableBitmap(width, height));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public unsafe void LayoutMatchesTheNativeAllocation()
    {
        using var bitmap = new WriteableBitmap(3, 2);

        Assert.Equal(3, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
        Assert.Equal(3, bitmap.BufferCount);
        Assert.Equal(sizeof(uint), bitmap.BytesPerPixel);
        Assert.True(bitmap.Stride >= bitmap.Width * bitmap.BytesPerPixel);
        Assert.Equal(bitmap.Stride * bitmap.Height, bitmap.Length);
        Assert.Equal(SKImageInfo.PlatformColorType, bitmap.ColorType);
        Assert.Equal(SKAlphaType.Premul, bitmap.AlphaType);

        foreach (var value in bitmap.ToSpanByte())
            Assert.Equal(0, value);

        var pointer = bitmap.ToUnsafePointer(out var length);
        Assert.NotEqual(IntPtr.Zero, (IntPtr)pointer);
        Assert.Equal(bitmap.Length, length);
    }

    [Fact]
    public void ConstructorRequiresAtLeastTwoBuffers()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WriteableBitmap(1, 1, 1));

        Assert.Equal("bufferCount", exception.ParamName);
        using var bitmap = new WriteableBitmap(1, 1, 2);
        Assert.Equal(2, bitmap.BufferCount);
    }

    [Fact]
    public unsafe void SpansAndPointersAliasTheSameNativeBuffer()
    {
        using var bitmap = new WriteableBitmap(3, 2);
        var pixels = bitmap.ToSpanUInt32();
        var pointer = bitmap.ToUnsafeUInt32(out var length);

        Assert.Equal(bitmap.Length / sizeof(uint), length);

        const uint first = 0x11223344;
        const uint last = 0xA1B2C3D4;
        pixels[0] = first;
        pointer[bitmap.GetIndex(2, 1)] = last;

        Assert.Equal(first, pointer[0]);
        Assert.Equal(last, pixels[bitmap.GetIndex(2, 1)]);
    }

    [Fact]
    public void RawCompatibilityBufferAddressDoesNotChangeAfterFrameSwaps()
    {
        using var bitmap = new WriteableBitmap(2, 2);
        var original = bitmap.ToUnsafeIntPtr(out _);

        for (var i = 0; i < 8; i++)
        {
            Assert.True(bitmap.TryBeginWrite(out var write));
            write.Dispose();
        }

        Assert.Equal(original, bitmap.ToUnsafeIntPtr(out _));
    }

    [Fact]
    public void ClearZeroesTheEntireAllocation()
    {
        using var bitmap = new WriteableBitmap(3, 2);
        bitmap.ToSpanByte().Fill(byte.MaxValue);

        bitmap.Clear();

        foreach (var value in bitmap.ToSpanByte())
            Assert.Equal(0, value);
    }

    [Fact]
    public void FromRgbaUsesThePlatformNativeShifts()
    {
        var expected = (0x11u << SKImageInfo.PlatformColorRedShift)
                     | (0x22u << SKImageInfo.PlatformColorGreenShift)
                     | (0x33u << SKImageInfo.PlatformColorBlueShift)
                     | (0xFFu << SKImageInfo.PlatformColorAlphaShift);

        Assert.Equal(expected, WriteableBitmap.FromRgba(0x11, 0x22, 0x33, 0xFF));
        Assert.Equal(expected, WriteableBitmap.FromColor(new SKColor(0x11, 0x22, 0x33)));
        Assert.Equal(
            expected,
            WriteableBitmap.FromColor(
                Microsoft.Maui.Graphics.Color.FromRgba(0x11, 0x22, 0x33, 0xFF)));
    }

    [Theory]
    [InlineData(0, 8, 16, 24, 0x80204060u)]
    [InlineData(16, 8, 0, 24, 0x80604020u)]
    public void PackRgbaPremultipliesBothCommonNativeLayouts(
        int redShift,
        int greenShift,
        int blueShift,
        int alphaShift,
        uint expected)
    {
        var actual = WriteableBitmap.PackRgba(
            0xBF,
            0x7F,
            0x3F,
            0x80,
            redShift,
            greenShift,
            blueShift,
            alphaShift);

        Assert.Equal(expected, actual);
        Assert.Equal(
            0u,
            WriteableBitmap.PackRgba(
                byte.MaxValue,
                byte.MaxValue,
                byte.MaxValue,
                0,
                redShift,
                greenShift,
                blueShift,
                alphaShift));
    }

    [Fact]
    public unsafe void PackedPixelDecodesThroughSkia()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromRgba(0xBF, 0x7F, 0x3F, 0x80);

        using var skiaView = new SKBitmap();
        var info = new SKImageInfo(1, 1, bitmap.ColorType, bitmap.AlphaType);
        Assert.True(
            skiaView.InstallPixels(
                info,
                bitmap.ToUnsafeIntPtr(out _),
                bitmap.Stride));

        var decoded = skiaView.GetPixel(0, 0);
        Assert.InRange(decoded.Red, 0xBE, 0xC0);
        Assert.InRange(decoded.Green, 0x7E, 0x80);
        Assert.InRange(decoded.Blue, 0x3E, 0x40);
        Assert.Equal(0x80, decoded.Alpha);
    }

    [Fact]
    public void TryDrawRendersTheNativeBufferPixels()
    {
        using var bitmap = new WriteableBitmap(2, 1);
        var pixels = bitmap.ToSpanUInt32();
        pixels[0] = WriteableBitmap.FromRgba(0xFF, 0, 0, 0xFF);
        pixels[1] = WriteableBitmap.FromRgba(0, 0xFF, 0, 0xFF);

        using var target = new SKBitmap(2, 1);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(
                canvas,
                new SKRect(0, 0, 2, 1),
                new SKSamplingOptions(SKFilterMode.Nearest),
                paint,
                clearBeforeDraw: false));

        Assert.Equal(SKColors.Red, target.GetPixel(0, 0));
        Assert.Equal(SKColors.Lime, target.GetPixel(1, 0));
    }

    [Fact]
    public void InvalidateRaisesOneNotificationPerCompletedBatch()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;

        bitmap.Invalidate();
        bitmap.Invalidate();

        Assert.Equal(2, notifications);
    }

    [Fact]
    public void WriteLeaseSerializesAndPublishesACompletedFrame()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;

        Assert.True(bitmap.TryBeginWrite(out var write));
        try
        {
            write.Pixels[0] = WriteableBitmap.FromRgba(1, 2, 3, byte.MaxValue);
            Assert.False(bitmap.TryBeginWrite(out _));
            Assert.Equal(0, notifications);
        }
        finally
        {
            write.Dispose();
        }

        Assert.Equal(1, notifications);
        using var target = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(
                canvas,
                new SKRect(0, 0, 1, 1),
                new SKSamplingOptions(SKFilterMode.Nearest),
                paint,
                clearBeforeDraw: false));
        Assert.Equal(new SKColor(1, 2, 3), target.GetPixel(0, 0));
    }

    [Fact]
    public void RendererReadsTheFrontBufferWhileTheProducerWritesTheBackBuffer()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        using var target = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        Assert.True(bitmap.TryBeginWrite(out var write));
        write.Pixels[0] = WriteableBitmap.FromRgba(0xFF, 0, 0, 0xFF);

        var result = bitmap.TryDraw(
            canvas,
            new SKRect(0, 0, 1, 1),
            new SKSamplingOptions(SKFilterMode.Nearest),
            paint,
            clearBeforeDraw: false);

        Assert.Equal(BitmapDrawResult.Drawn, result);
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(0, 0));
        write.Dispose();

        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(
                canvas,
                new SKRect(0, 0, 1, 1),
                new SKSamplingOptions(SKFilterMode.Nearest),
                paint,
                clearBeforeDraw: false));
        Assert.Equal(SKColors.Red, target.GetPixel(0, 0));
    }

    [Fact]
    public void ProducerCanReplaceUnpaintedFramesWithoutBlocking()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        for (byte value = 1; value <= 10; value++)
        {
            Assert.True(bitmap.TryBeginWrite(out var write));
            write.Pixels[0] = WriteableBitmap.FromRgba(value, 0, 0, byte.MaxValue);
            write.Dispose();
        }

        using var target = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(
                canvas,
                new SKRect(0, 0, 1, 1),
                new SKSamplingOptions(SKFilterMode.Nearest),
                paint,
                clearBeforeDraw: false));
        Assert.Equal(new SKColor(10, 0, 0), target.GetPixel(0, 0));
    }

    [Fact]
    public void RecordedDrawRetainsPixelsAfterLogicalDisposal()
    {
        var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromRgba(0xFF, 0, 0, 0xFF);
        using var recorder = new SKPictureRecorder();
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        var recordingCanvas = recorder.BeginRecording(new SKRect(0, 0, 1, 1));

        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(
                recordingCanvas,
                new SKRect(0, 0, 1, 1),
                new SKSamplingOptions(SKFilterMode.Nearest),
                paint,
                clearBeforeDraw: false));

        using var picture = recorder.EndRecording();
        Assert.True(bitmap.TryBeginWrite(out var write));
        write.Pixels[0] = WriteableBitmap.FromRgba(0, 0xFF, 0, 0xFF);
        write.Dispose();
        bitmap.Dispose();

        using var target = new SKBitmap(1, 1);
        using var targetCanvas = new SKCanvas(target);
        targetCanvas.DrawPicture(picture);

        Assert.Equal(SKColors.Red, target.GetPixel(0, 0));
    }

    [Fact]
    public void RawInvalidationRejectsABufferRetainedBySkia()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        using var picture = RecordCurrentFrame(bitmap);

        Assert.Throws<InvalidOperationException>(() => bitmap.Invalidate());
    }

    [Fact]
    public void ProducerStopsOnlyWhenEverySwapChainBufferIsRetained()
    {
        using var bitmap = new WriteableBitmap(1, 1, bufferCount: 3);
        using var first = RecordCurrentFrame(bitmap);

        Assert.True(bitmap.TryBeginWrite(out var write));
        write.Dispose();
        using var second = RecordCurrentFrame(bitmap);

        Assert.True(bitmap.TryBeginWrite(out write));
        write.Dispose();
        using var third = RecordCurrentFrame(bitmap);

        Assert.False(bitmap.TryBeginWrite(out _));
        first.Dispose();
        Assert.True(bitmap.TryBeginWrite(out write));
        write.Dispose();
    }

    [Fact]
    public void DisposingACopiedLeaseInvalidatesItsOtherCopy()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        Assert.True(bitmap.TryBeginWrite(out var first));
        var second = first;

        first.Dispose();

        var rejected = false;
        try
        {
            _ = second.Bytes;
        }
        catch (ObjectDisposedException)
        {
            rejected = true;
        }
        finally
        {
            second.Dispose();
        }

        Assert.True(rejected);
    }

    [Fact]
    public void DisposeDefersNativeReleaseUntilTheWriteLeaseCompletes()
    {
        var bitmap = new WriteableBitmap(1, 1);
        Assert.True(bitmap.TryBeginWrite(out var write));

        bitmap.Dispose();

        Assert.True(bitmap.IsDisposed);
        write.Dispose();
    }

    [Fact]
    public unsafe void DisposeIsIdempotentAndInvalidatesBorrowedAccess()
    {
        var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;

        bitmap.Dispose();
        bitmap.Dispose();

        Assert.True(bitmap.IsDisposed);
        Assert.Equal(1, notifications);
        Assert.Throws<ObjectDisposedException>(() => bitmap.Clear());
        Assert.Throws<ObjectDisposedException>(() => bitmap.Invalidate());
        Assert.Throws<ObjectDisposedException>(() => InvokePointerAccess(bitmap));
    }

    [Fact]
    public void SteadyStateSpanAccessDoesNotAllocateManagedMemory()
    {
        using var bitmap = new WriteableBitmap(8, 8);

        for (var i = 0; i < 128; i++)
            bitmap.ToSpanUInt32()[0] = (uint)i;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_024; i++)
            bitmap.ToSpanUInt32()[0] = (uint)i;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SteadyStateInvalidationDoesNotAllocateManagedMemory()
    {
        using var bitmap = new WriteableBitmap(8, 8);

        for (var i = 0; i < 128; i++)
            bitmap.Invalidate();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_024; i++)
            bitmap.Invalidate();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SteadyStateWriteLeasesDoNotAllocateManagedMemory()
    {
        using var bitmap = new WriteableBitmap(8, 8);

        for (var i = 0; i < 128; i++)
        {
            if (!bitmap.TryBeginWrite(out var warmup))
                throw new InvalidOperationException();

            warmup.Pixels[0] = (uint)i;
            warmup.Dispose();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_024; i++)
        {
            if (!bitmap.TryBeginWrite(out var write))
                throw new InvalidOperationException();

            write.Pixels[0] = (uint)i;
            write.Dispose();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static SKPicture RecordCurrentFrame(WriteableBitmap bitmap)
    {
        using var recorder = new SKPictureRecorder();
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        var canvas = recorder.BeginRecording(new SKRect(0, 0, 1, 1));
        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(
                canvas,
                new SKRect(0, 0, 1, 1),
                new SKSamplingOptions(SKFilterMode.Nearest),
                paint,
                clearBeforeDraw: false));
        return recorder.EndRecording();
    }

    private static unsafe void InvokePointerAccess(WriteableBitmap bitmap) =>
        bitmap.ToUnsafePointer(out _);
}
