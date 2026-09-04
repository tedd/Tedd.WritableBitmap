using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Xunit;
using Bitmap = global::Tedd.WriteableBitmap;

namespace Tedd.Wpf.Tests;

public class BitmapTests
{
    public static TheoryData<int, int, PixelFormat> Dimensions => new()
    {
        { 1, 1, PixelFormats.Bgra32 },
        { 3, 7, PixelFormats.Bgr32 },
        { 9, 2, PixelFormats.Pbgra32 },
        { 1, 3, PixelFormats.Bgr24 },
        { 5, 2, PixelFormats.Bgr24 },
        { 7, 3, PixelFormats.Gray8 },
        { 3, 2, PixelFormats.Bgr565 },
    };

    [Theory]
    [MemberData(nameof(Dimensions))]
    public void Constructor_ExposesZeroInitializedCompleteRows(int width, int height, PixelFormat format) => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(width, height, format);
        var expectedStride = (width * format.BitsPerPixel + 31) / 32 * 4;
        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
        Assert.Equal(format, bitmap.PixelFormat);
        Assert.Equal((format.BitsPerPixel + 7) / 8, bitmap.BytesPerPixel);
        Assert.Equal(expectedStride, bitmap.Stride);
        Assert.Equal(expectedStride * height, bitmap.Length);
        Assert.Equal(0, bitmap.Offset);
        Assert.Equal(width, bitmap.BitmapSource.PixelWidth);
        Assert.Equal(height, bitmap.BitmapSource.PixelHeight);
        Assert.Equal(format, bitmap.BitmapSource.Format);
        Assert.All(bitmap.ToSpanByte().ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(bitmap.Length, bitmap.ToSpanByte().Length);
        Assert.Equal(bitmap.Length / 2, bitmap.ToSpanUInt16().Length);
        Assert.Equal(bitmap.Length / 4, bitmap.ToSpanUInt32().Length);
    });

    [Fact]
    public unsafe void SpansAndPointers_ShareWritablePixelMemory() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(3, 2, PixelFormats.Bgra32);
        var address = bitmap.ToUnsafeIntPtr(out var byteCount);
        Assert.NotEqual(IntPtr.Zero, address);
        Assert.Equal(24, byteCount);
        Assert.Equal(address, (IntPtr)bitmap.ToUnsafePointer(out var pointerLength));
        Assert.Equal(byteCount, pointerLength);
        var words = bitmap.ToUnsafeUInt16(out var wordCount);
        var pixels = bitmap.ToUnsafeUInt32(out var pixelCount);
        Assert.Equal(address, (IntPtr)words);
        Assert.Equal(address, (IntPtr)pixels);
        Assert.Equal(12, wordCount);
        Assert.Equal(6, pixelCount);
        bitmap.ToSpanUInt32()[0] = 0xA1B2C3D4;
        Assert.Equal(new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 }, bitmap.ToSpanByte()[..4].ToArray());
        Assert.Equal(0xC3D4, words[0]);
        bitmap.ToSpanUInt16()[2] = 0x1234;
        Assert.Equal(0x1234, Marshal.ReadInt16(address, 4));
        pixels[5] = 0xFF123456;
        Assert.Equal(0xFF123456u, bitmap.ToSpanUInt32()[5]);
        ((byte*)bitmap.ToUnsafePointer(out _))[8] = 0x73;
        Assert.Equal(0x73, bitmap.ToSpanByte()[8]);
    });

    [Theory]
    [MemberData(nameof(Dimensions))]
    public void Clear_ErasesEveryByteIncludingRowPadding(int width, int height, PixelFormat format) => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(width, height, format);
        bitmap.ToSpanByte().Fill(0xCD);
        bitmap.Clear();
        Assert.All(bitmap.ToSpanByte().ToArray(), value => Assert.Equal(0, value));
    });

    [Fact]
    public void BorrowedSectionConstructor_ExposesSharedPixelsAndPreservesHandle() => Sta.Run(() =>
    {
        using var section = MemoryMappedFile.CreateNew(null, 24);
        using var view = section.CreateViewAccessor();
        view.Write(0, 0xFF102030u);
        var handle = section.SafeMemoryMappedFileHandle.DangerousGetHandle();
        using (var bitmap = new Bitmap(handle, 3, 2, PixelFormats.Bgra32))
        {
            Assert.Equal(0xFF102030u, bitmap.ToSpanUInt32()[0]);
            bitmap.ToSpanUInt32()[5] = 0xFFABCDEF;
            Assert.Equal(0xFFABCDEFu, view.ReadUInt32(20));
            var pixels = new uint[6];
            bitmap.BitmapSource.CopyPixels(pixels, bitmap.Stride, 0);
            Assert.Equal(0xFF102030u, pixels[0]);
            Assert.Equal(0xFFABCDEFu, pixels[5]);
        }
        using var secondView = section.CreateViewAccessor();
        secondView.Write(0, 0xFF445566u);
        Assert.Equal(0xFF445566u, view.ReadUInt32(0));
    });

    [Theory]
    [InlineData(0, 16)]
    [InlineData(12, 16)]
    [InlineData(8, 13)]
    public unsafe void BorrowedSectionWithLayout_UsesOffsetAndPaddedStride(int offset, int stride) => Sta.Run(() =>
    {
        const int height = 2;
        var length = stride * height;
        using var section = MemoryMappedFile.CreateNew(null, offset + length + 8);
        using var view = section.CreateViewAccessor();
        var original = Enumerable.Repeat((byte)0x5A, offset + length + 8).ToArray();
        view.WriteArray(0, original, 0, original.Length);
        using var bitmap = new Bitmap(section.SafeMemoryMappedFileHandle.DangerousGetHandle(), 3, height, PixelFormats.Bgra32, stride, offset);
        Assert.Equal(offset, bitmap.Offset);
        Assert.Equal(stride, bitmap.Stride);
        Assert.Equal(length, bitmap.Length);
        var address = bitmap.ToUnsafeIntPtr(out var actualLength);
        Assert.Equal(length, actualLength);
        Assert.Equal(address, (IntPtr)bitmap.ToUnsafePointer(out _));
        Assert.Equal(address, (IntPtr)bitmap.ToUnsafeUInt16(out var words));
        Assert.Equal(address, (IntPtr)bitmap.ToUnsafeUInt32(out var pixels));
        Assert.Equal(length / 2, words);
        Assert.Equal(length / 4, pixels);
        bitmap.ToSpanByte().Fill(0);
        bitmap.ToSpanUInt32()[0] = 0xFF123456;
        Marshal.WriteInt32(address, stride + 8, unchecked((int)0xFFABCDEF));
        Assert.Equal(0xFF123456u, view.ReadUInt32(offset));
        Assert.Equal(0xFFABCDEFu, view.ReadUInt32(offset + stride + 8));
        var copied = new uint[6];
        bitmap.BitmapSource.CopyPixels(copied, 12, 0);
        Assert.Equal(0xFF123456u, copied[0]);
        Assert.Equal(0xFFABCDEFu, copied[5]);
        bitmap.ToSpanByte().Fill(0xCD);
        bitmap.Clear();
        Assert.All(bitmap.ToSpanByte().ToArray(), value => Assert.Equal(0, value));
        for (var i = 0; i < offset; i++) Assert.Equal(0x5A, view.ReadByte(i));
        for (var i = offset + length; i < original.Length; i++) Assert.Equal(0x5A, view.ReadByte(i));
    });

    [Fact]
    public void Invalidate_RefreshesBitmapSourceAfterMemoryChanges() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(2, 2, PixelFormats.Bgra32);
        bitmap.ToSpanUInt32().Fill(0xFF112233);
        bitmap.Invalidate();
        var pixels = new uint[4];
        bitmap.BitmapSource.CopyPixels(pixels, bitmap.Stride, 0);
        Assert.All(pixels, pixel => Assert.Equal(0xFF112233u, pixel));
        bitmap.ToSpanUInt32()[3] = 0xFFABCDEF;
        bitmap.Invalidate();
        bitmap.BitmapSource.CopyPixels(pixels, bitmap.Stride, 0);
        Assert.Equal(0xFFABCDEFu, pixels[3]);
    });

    [Fact]
    public void MemoryWrites_CanOccurOnWorkerThread() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(2, 2, PixelFormats.Bgra32);
        var worker = new Thread(() => bitmap.ToSpanUInt32().Fill(0xFF123456));
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        bitmap.Invalidate();
        var pixels = new uint[4];
        bitmap.BitmapSource.CopyPixels(pixels, bitmap.Stride, 0);
        Assert.All(pixels, value => Assert.Equal(0xFF123456u, value));
    });

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 3)]
    [InlineData(0, 2, 8)]
    [InlineData(3, 2, 11)]
    public void GetIndex_UsesRowMajorPixelOrder(int x, int y, int expected) => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(4, 3, PixelFormats.Bgra32);
        Assert.Equal(expected, bitmap.GetIndex(x, y));
        bitmap.ToSpanUInt32()[bitmap.GetIndex(x, y)] = 0xFF778899;
        var pixels = new uint[12];
        bitmap.BitmapSource.CopyPixels(pixels, bitmap.Stride, 0);
        Assert.Equal(0xFF778899u, pixels[expected]);
    });
}

