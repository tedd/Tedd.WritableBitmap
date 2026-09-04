using System.Runtime.InteropServices;
using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class WriteableBitmapAccessTests
{
    [Theory]
    [InlineData(int.MinValue, 1, "width")]
    [InlineData(1, int.MinValue, "height")]
    [InlineData(0, 0, "width")]
    [InlineData(536_870_912, 1, "width")]
    [InlineData(536_870_911, 2, "height")]
    [InlineData(1, 536_870_912, "height")]
    [InlineData(32_768, 16_384, "height")]
    [InlineData(46_341, 46_341, "height")]
    public void ConstructorRejectsInvalidDimensionsBeforeNativeAllocation(
        int width, int height, string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WriteableBitmap(width, height));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void ConstructorRejectsInsufficientBufferCounts(int bufferCount)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WriteableBitmap(1, 1, bufferCount));

        Assert.Equal("bufferCount", exception.ParamName);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void EveryNewBackBufferIsZeroInitialized(int bufferCount)
    {
        using var bitmap = new WriteableBitmap(3, 2, bufferCount);

        Assert.Equal(bufferCount, bitmap.BufferCount);
        Assert.False(bitmap.IsDisposed);
        for (var i = 0; i < bufferCount - 1; i++)
        {
            Assert.True(bitmap.TryBeginWrite(out var write));
            try
            {
                Assert.Equal(bitmap.Length, write.Bytes.Length);
                Assert.Equal(bitmap.Length / sizeof(uint), write.Pixels.Length);
                Assert.All(write.Bytes.ToArray(), value => Assert.Equal(0, value));
                write.Bytes.Fill((byte)(i + 1));
            }
            finally
            {
                write.Dispose();
            }
        }

        // The raw buffer has not been used as a back buffer yet.
        Assert.All(bitmap.ToSpanByte().ToArray(), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 7)]
    [InlineData(7, 1)]
    [InlineData(3, 5)]
    [InlineData(8, 4)]
    public unsafe void EveryAccessFormatAddressesTheCompleteSameAllocation(int width, int height)
    {
        using var bitmap = new WriteableBitmap(width, height);
        var bytes = bitmap.ToSpanByte();
        var words = bitmap.ToSpanUInt16();
        var pixels = bitmap.ToSpanUInt32();
        var rawPointer = bitmap.ToUnsafePointer(out var rawLength);
        var intPointer = bitmap.ToUnsafeIntPtr(out var intLength);
        var wordPointer = bitmap.ToUnsafeUInt16(out var wordLength);
        var pixelPointer = bitmap.ToUnsafeUInt32(out var pixelLength);

        Assert.Equal(bitmap.Length, bytes.Length);
        Assert.Equal(bitmap.Length, rawLength);
        Assert.Equal(bitmap.Length, intLength);
        Assert.Equal(bytes.Length / sizeof(ushort), words.Length);
        Assert.Equal(words.Length, wordLength);
        Assert.Equal(bytes.Length / sizeof(uint), pixels.Length);
        Assert.Equal(pixels.Length, pixelLength);
        Assert.Equal(intPointer, (IntPtr)rawPointer);
        Assert.Equal(intPointer, (IntPtr)wordPointer);
        Assert.Equal(intPointer, (IntPtr)pixelPointer);

        bytes.Fill(0x12);
        Assert.Equal(0x1212, wordPointer[0]);
        Assert.Equal(0x12121212u, pixelPointer[pixelLength - 1]);

        words[0] = 0xABCD;
        wordPointer[wordLength - 1] = 0x5678;
        Assert.Equal(0xABCD, wordPointer[0]);
        Assert.Equal(0x5678, words[^1]);
        Assert.Equal(BitConverter.GetBytes((ushort)0xABCD), bytes[..sizeof(ushort)].ToArray());
        Assert.Equal(BitConverter.GetBytes((ushort)0x5678), bytes[^sizeof(ushort)..].ToArray());

        Marshal.WriteInt32(intPointer, 0x11223344);
        Assert.Equal(0x11223344u, pixels[0]);
        ((byte*)rawPointer)[rawLength - 1] = 0xEE;
        Assert.Equal(0xEE, bytes[^1]);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 1)]
    [InlineData(3, 4)]
    public void PixelIndicesMatchSkiaCoordinates(int width, int height)
    {
        using var bitmap = new WriteableBitmap(width, height);
        using var skiaView = new SKBitmap();
        Assert.True(skiaView.InstallPixels(
            new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType),
            bitmap.ToUnsafeIntPtr(out _), bitmap.Stride));

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            bitmap.ToSpanUInt32()[bitmap.GetIndex(x, y)] =
                WriteableBitmap.FromRgba((byte)(x + 1), (byte)(y + 1), 0, byte.MaxValue);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            Assert.Equal(new SKColor((byte)(x + 1), (byte)(y + 1), 0), skiaView.GetPixel(x, y));
    }

    [Fact]
    public void RawWritesAndClearRequireExplicitInvalidation()
    {
        using var bitmap = new WriteableBitmap(3, 2);
        var notifications = 0;
        EventHandler handler = (_, _) => notifications++;
        bitmap.Invalidated += handler;

        bitmap.ToSpanByte().Fill(byte.MaxValue);
        bitmap.Clear();

        Assert.All(bitmap.ToSpanByte().ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(0, notifications);
        bitmap.Invalidate();
        Assert.Equal(1, notifications);
        bitmap.Invalidated -= handler;
        bitmap.Invalidate();
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("words")]
    [InlineData("pixels")]
    [InlineData("pointer")]
    [InlineData("intptr")]
    [InlineData("wordPointer")]
    [InlineData("pixelPointer")]
    [InlineData("write")]
    [InlineData("invalidate")]
    [InlineData("clear")]
    public void DisposedBitmapRejectsEveryMutableEntryPoint(string operation)
    {
        using var bitmap = new WriteableBitmap(2, 3);
        bitmap.Dispose();

        var exception = Assert.Throws<ObjectDisposedException>(() => Access(bitmap, operation));

        Assert.Equal(nameof(WriteableBitmap), exception.ObjectName);
        Assert.Equal(2, bitmap.Width);
        Assert.Equal(3, bitmap.Height);
        Assert.Equal(WriteableBitmap.DefaultBufferCount, bitmap.BufferCount);
        Assert.True(bitmap.IsDisposed);
    }

    private static unsafe void Access(WriteableBitmap bitmap, string operation)
    {
        switch (operation)
        {
            case "bytes": _ = bitmap.ToSpanByte(); break;
            case "words": _ = bitmap.ToSpanUInt16(); break;
            case "pixels": _ = bitmap.ToSpanUInt32(); break;
            case "pointer": _ = bitmap.ToUnsafePointer(out _); break;
            case "intptr": _ = bitmap.ToUnsafeIntPtr(out _); break;
            case "wordPointer": _ = bitmap.ToUnsafeUInt16(out _); break;
            case "pixelPointer": _ = bitmap.ToUnsafeUInt32(out _); break;
            case "write": bitmap.TryBeginWrite(out _); break;
            case "invalidate": bitmap.Invalidate(); break;
            case "clear": bitmap.Clear(); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
