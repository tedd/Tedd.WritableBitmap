using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class OptimizationRegressionTests
{
    [Theory]
    [InlineData(0, 8, 16, 24)]
    [InlineData(16, 8, 0, 24)]
    public void PackingMatchesRoundedDivisionForEveryChannelAndAlpha(
        int redShift,
        int greenShift,
        int blueShift,
        int alphaShift)
    {
        // Every channel visits all 65,536 component/alpha pairs. Distinct channel
        // values also expose accidental carry between packed arithmetic lanes.
        for (var alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            for (var component = 0; component <= byte.MaxValue; component++)
            {
                var red = (byte)component;
                var green = (byte)(byte.MaxValue - component);
                var blue = (byte)(component ^ 0xA5);
                var expected = ((uint)((red * alpha + 127) / 255) << redShift)
                             | ((uint)((green * alpha + 127) / 255) << greenShift)
                             | ((uint)((blue * alpha + 127) / 255) << blueShift)
                             | ((uint)alpha << alphaShift);

                Assert.Equal(
                    expected,
                    WriteableBitmap.PackRgba(
                        red, green, blue, (byte)alpha,
                        redShift, greenShift, blueShift, alphaShift));
            }
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void BufferReuseWaitsForEveryRecordingAndPreservesOlderFrames(int bufferCount)
    {
        using var bitmap = new WriteableBitmap(1, 1, bufferCount);
        var pictures = new List<SKPicture>();
        var expectedColors = new List<SKColor>();
        try
        {
            for (var frame = 0; frame < bufferCount; frame++)
            {
                var color = new SKColor((byte)(frame + 1), (byte)(frame + 20), 80);
                if (frame == 0)
                    bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(color);
                else
                    Publish(bitmap, color);

                // Repeated draws of the same front buffer must retain it independently.
                for (var recording = 0; recording < 2; recording++)
                {
                    pictures.Add(RecordCurrentFrame(bitmap));
                    expectedColors.Add(color);
                }
            }

            Assert.False(bitmap.TryBeginWrite(out _));
            pictures[0].Dispose();
            Assert.False(bitmap.TryBeginWrite(out _));
            pictures[1].Dispose();

            var replacement = new SKColor(230, 170, 110);
            Publish(bitmap, replacement);
            using var replacementPicture = RecordCurrentFrame(bitmap);
            Assert.False(bitmap.TryBeginWrite(out _));

            bitmap.Dispose();
            AssertPictureColor(replacementPicture, replacement);
            for (var index = 2; index < pictures.Count; index++)
                AssertPictureColor(pictures[index], expectedColors[index]);
        }
        finally
        {
            foreach (var picture in pictures)
                picture.Dispose();
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void StaleLeaseCannotAccessOrCompleteALaterGeneration(int bufferCount)
    {
        using var bitmap = new WriteableBitmap(1, 1, bufferCount);
        Assert.True(bitmap.TryBeginWrite(out var first));
        var stale = first;
        first.Dispose();

        // More than two rotations ensures that the stale lease's original slot
        // has been reused, exercising the generation check as well as its index.
        for (var frame = 0; frame < bufferCount * 2; frame++)
        {
            Assert.True(bitmap.TryBeginWrite(out var current));
            try
            {
                var expected = WriteableBitmap.FromRgba((byte)frame, 20, 80, 255);
                current.Pixels[0] = expected;
                AssertLeaseAccessThrows(ref stale);
                var staleCopy = stale;
                staleCopy.Dispose();
                Assert.False(bitmap.TryBeginWrite(out _));
                Assert.Equal(expected, current.Pixels[0]);
            }
            finally
            {
                current.Dispose();
            }
        }

        stale.Dispose();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 3)]
    public unsafe void EveryRawViewAliasesTheCompleteNativeAllocation(int width, int height)
    {
        using var bitmap = new WriteableBitmap(width, height);
        var bytes = bitmap.ToSpanByte();
        var words = bitmap.ToSpanUInt16();
        var pixels = bitmap.ToSpanUInt32();
        var native = bitmap.ToUnsafeIntPtr(out var nativeLength);
        var pointer = bitmap.ToUnsafePointer(out var pointerLength);
        var wordPointer = bitmap.ToUnsafeUInt16(out var wordLength);
        var pixelPointer = bitmap.ToUnsafeUInt32(out var pixelLength);

        Assert.Equal(bitmap.Stride * height, nativeLength);
        Assert.Equal(nativeLength, pointerLength);
        Assert.Equal(nativeLength, bytes.Length);
        Assert.Equal(nativeLength / sizeof(ushort), wordLength);
        Assert.Equal(wordLength, words.Length);
        Assert.Equal(nativeLength / sizeof(uint), pixelLength);
        Assert.Equal(pixelLength, pixels.Length);
        Assert.Equal(native, (IntPtr)pointer);
        Assert.Equal(native, (IntPtr)wordPointer);
        Assert.Equal(native, (IntPtr)pixelPointer);
        fixed (byte* byteSpanPointer = bytes)
        fixed (ushort* wordSpanPointer = words)
        fixed (uint* pixelSpanPointer = pixels)
        {
            Assert.Equal(native, (IntPtr)byteSpanPointer);
            Assert.Equal(native, (IntPtr)wordSpanPointer);
            Assert.Equal(native, (IntPtr)pixelSpanPointer);
        }

        words[^1] = 0xA1B2;
        Assert.Equal((ushort)0xA1B2, wordPointer[wordLength - 1]);
        pixelPointer[pixelLength - 1] = 0x12345678;
        Assert.Equal(0x12345678u, pixels[^1]);
        bytes[^1] = 0x9A;
        Assert.Equal((byte)0x9A, ((byte*)native)[nativeLength - 1]);
    }

    [Fact]
    public unsafe void DisposalRejectsEveryRawAccessor()
    {
        var bitmap = new WriteableBitmap(1, 1);
        bitmap.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToSpanByte(); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToSpanUInt16(); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToSpanUInt32(); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToUnsafePointer(out _); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToUnsafeIntPtr(out _); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToUnsafeUInt16(out _); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.ToUnsafeUInt32(out _); });
        Assert.Throws<ObjectDisposedException>(() => { bitmap.TryBeginWrite(out _); });
        Assert.Throws<ObjectDisposedException>(() => bitmap.Clear());
        Assert.Throws<ObjectDisposedException>(() => bitmap.Invalidate());
    }

    [Fact]
    public async Task DisposalWhileAProducerOwnsALeasePreservesRecordedPixels()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        var producerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishProducer = new ManualResetEventSlim();
        var producer = Task.Run(() =>
        {
            Assert.True(bitmap.TryBeginWrite(out var write));
            try
            {
                write.Pixels[0] = WriteableBitmap.FromColor(SKColors.Lime);
                producerReady.SetResult();
                finishProducer.Wait();
                AssertLeaseAccessThrows(ref write);
            }
            finally
            {
                write.Dispose();
            }
        });

        try
        {
            // Propagate acquisition failures instead of waiting indefinitely for readiness.
            await await Task.WhenAny(producerReady.Task, producer);
            using var picture = RecordCurrentFrame(bitmap);
            bitmap.Dispose();
            finishProducer.Set();
            await producer;

            AssertPictureColor(picture, SKColors.Red);
            using var target = new SKBitmap(1, 1);
            using var canvas = new SKCanvas(target);
            using var paint = new SKPaint();
            Assert.Equal(
                BitmapDrawResult.Unavailable,
                bitmap.TryDraw(canvas, new SKRect(0, 0, 1, 1), default, paint, false));
        }
        finally
        {
            finishProducer.Set();
            await producer;
        }
    }

    private static void AssertLeaseAccessThrows(ref WriteableBitmap.WriteLease lease)
    {
        var bytesRejected = false;
        try
        {
            _ = lease.Bytes;
        }
        catch (ObjectDisposedException)
        {
            bytesRejected = true;
        }

        var pixelsRejected = false;
        try
        {
            _ = lease.Pixels;
        }
        catch (ObjectDisposedException)
        {
            pixelsRejected = true;
        }

        Assert.True(bytesRejected);
        Assert.True(pixelsRejected);
    }

    private static void Publish(WriteableBitmap bitmap, SKColor color)
    {
        Assert.True(bitmap.TryBeginWrite(out var write));
        try
        {
            Assert.Equal(bitmap.Length, write.Bytes.Length);
            Assert.Equal(bitmap.Length / sizeof(uint), write.Pixels.Length);
            write.Pixels[0] = WriteableBitmap.FromColor(color);
        }
        finally
        {
            write.Dispose();
        }
    }

    private static SKPicture RecordCurrentFrame(WriteableBitmap bitmap)
    {
        using var recorder = new SKPictureRecorder();
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        var canvas = recorder.BeginRecording(new SKRect(0, 0, 1, 1));
        Assert.Equal(
            BitmapDrawResult.Drawn,
            bitmap.TryDraw(canvas, new SKRect(0, 0, 1, 1), default, paint, false));
        return recorder.EndRecording();
    }

    private static void AssertPictureColor(SKPicture picture, SKColor expected)
    {
        using var target = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(target);
        canvas.DrawPicture(picture);
        Assert.Equal(expected, target.GetPixel(0, 0));
    }
}
