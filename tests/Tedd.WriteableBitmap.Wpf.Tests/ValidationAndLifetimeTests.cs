using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media;
using Xunit;
using Bitmap = global::Tedd.WriteableBitmap;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tedd.Wpf.Tests;

public class ValidationAndLifetimeTests
{
    [Theory]
    [InlineData(0, 1, "width")]
    [InlineData(-1, 1, "width")]
    [InlineData(1, 0, "height")]
    [InlineData(1, -1, "height")]
    public void Constructor_RejectsInvalidDimensions(int width, int height, string parameter) => Sta.Run(() =>
        Assert.Throws<ArgumentOutOfRangeException>(parameter, () => new Bitmap(width, height, PixelFormats.Bgra32)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateStride_RejectsInvalidWidth(int width) =>
        Assert.Throws<ArgumentOutOfRangeException>("width", () => Bitmap.CalculateStride(PixelFormats.Bgra32, width));

    [Fact]
    public void CalculateStride_RejectsUnspecifiedFormat() =>
        Assert.Throws<ArgumentException>("pixelFormat", () => Bitmap.CalculateStride(default, 2));

    [Fact]
    public void CalculateStride_RejectsOverflow() =>
        Assert.Throws<OverflowException>(() => Bitmap.CalculateStride(PixelFormats.Bgra32, int.MaxValue));

    [Fact]
    public void Constructor_RejectsBufferLengthOverflow() => Sta.Run(() =>
        Assert.Throws<OverflowException>(() => new Bitmap(1, int.MaxValue, PixelFormats.Bgra32)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BorrowedConstructor_RejectsInvalidHandles(int handle) => Sta.Run(() =>
    {
        Assert.Throws<ArgumentException>("intPtr", () => new Bitmap(new IntPtr(handle), 1, 1, PixelFormats.Bgra32));
        Assert.Throws<ArgumentException>("intPtr", () => new Bitmap(new IntPtr(handle), 1, 1, PixelFormats.Bgra32, 4, 0));
    });

    [Theory]
    [InlineData(0, 1, 4, 0, "width")]
    [InlineData(1, 0, 4, 0, "height")]
    [InlineData(2, 1, 4, 0, "stride")]
    [InlineData(1, 1, 0, 0, "stride")]
    [InlineData(1, 1, -4, 0, "stride")]
    [InlineData(1, 1, 4, -1, "offset")]
    public void ExplicitLayoutConstructor_RejectsInvalidLayout(int width, int height, int stride, int offset, string parameter) => Sta.Run(() =>
    {
        using var section = MemoryMappedFile.CreateNew(null, 64);
        Assert.Throws<ArgumentOutOfRangeException>(parameter, () => new Bitmap(section.SafeMemoryMappedFileHandle.DangerousGetHandle(), width, height, PixelFormats.Bgra32, stride, offset));
    });

    [Fact]
    public void ExplicitLayoutConstructor_RejectsUnspecifiedFormat() => Sta.Run(() =>
    {
        using var section = MemoryMappedFile.CreateNew(null, 64);
        Assert.Throws<ArgumentException>("pixelFormat", () => new Bitmap(section.SafeMemoryMappedFileHandle.DangerousGetHandle(), 1, 1, default, 4, 0));
    });

    [Fact]
    public void ExplicitLayoutConstructor_RejectsOffsetOverflow() => Sta.Run(() =>
    {
        using var section = MemoryMappedFile.CreateNew(null, 64);
        Assert.Throws<OverflowException>(() => new Bitmap(section.SafeMemoryMappedFileHandle.DangerousGetHandle(), 1, 1, PixelFormats.Bgra32, 4, int.MaxValue));
    });

    [Fact]
    public void BorrowedConstructor_ReportsNativeMappingFailureAndPreservesHandle() => Sta.Run(() =>
    {
        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        var handle = eventHandle.SafeWaitHandle.DangerousGetHandle();
        Assert.Throws<Win32Exception>(() => new Bitmap(handle, 1, 1, PixelFormats.Bgra32));
        Assert.True(eventHandle.Set());
        Assert.True(eventHandle.WaitOne(0));
    });

    [Fact]
    public void BorrowedConstructor_RejectsMappingLargerThanSection() => Sta.Run(() =>
    {
        using var section = MemoryMappedFile.CreateNew(null, 4096);
        Assert.Throws<Win32Exception>(() => new Bitmap(section.SafeMemoryMappedFileHandle.DangerousGetHandle(), 1024, 2, PixelFormats.Bgra32));
        using var view = section.CreateViewAccessor();
        view.Write(0, 0x12345678);
        Assert.Equal(0x12345678, view.ReadInt32(0));
    });

    [Fact]
    public void Dispose_ReleasesOwnedSectionAndIsIdempotent() => Sta.Run(() =>
    {
        var bitmap = new Bitmap(3, 2, PixelFormats.Bgra32);
        var handle = GetSection(bitmap);
        Assert.True(GetHandleInformation(handle, out _));
        bitmap.Dispose();
        Assert.False(GetHandleInformation(handle, out _));
        bitmap.Dispose();
        Assert.False(GetHandleInformation(handle, out _));
        GC.KeepAlive(bitmap.BitmapSource);
    });

    [Fact]
    public void Finalizer_ReleasesOwnedSection()
    {
        WeakReference? bitmap = null;
        IntPtr handle = default;
        Sta.Run(() => (bitmap, handle) = CreateUnrootedBitmap());
        CollectFinalizers(bitmap!);
        Assert.False(GetHandleInformation(handle, out _));
    }

    [Fact]
    public void Finalizer_PreservesBorrowedSection()
    {
        using var section = MemoryMappedFile.CreateNew(null, 24);
        var handle = section.SafeMemoryMappedFileHandle.DangerousGetHandle();
        WeakReference? bitmap = null;
        Sta.Run(() => bitmap = CreateUnrootedBorrowedBitmap(handle));
        CollectFinalizers(bitmap!);
        Assert.True(GetHandleInformation(handle, out _));
        using var view = section.CreateViewAccessor();
        Assert.Equal(0xFF123456u, view.ReadUInt32(0));
    }

    private static void CollectFinalizers(WeakReference reference)
    {
        for (var attempt = 0; attempt < 3 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(reference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Reference, IntPtr Handle) CreateUnrootedBitmap()
    {
        var bitmap = new Bitmap(3, 2, PixelFormats.Bgra32);
        return (new WeakReference(bitmap), GetSection(bitmap));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateUnrootedBorrowedBitmap(IntPtr handle)
    {
        var bitmap = new Bitmap(handle, 3, 2, PixelFormats.Bgra32);
        bitmap.ToSpanUInt32()[0] = 0xFF123456;
        return new WeakReference(bitmap);
    }

    private static IntPtr GetSection(Bitmap bitmap) =>
        (IntPtr)typeof(Bitmap).GetField("_memoryMapSection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bitmap)!;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(IntPtr handle, out uint flags);
}
