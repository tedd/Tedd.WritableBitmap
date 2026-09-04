using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Bitmap = global::Tedd.WriteableBitmap;

namespace Tedd.Wpf.Tests;

public class LoadFileTests
{
    [Theory]
    [InlineData(".png", 0)]
    [InlineData(".PNG", 0)]
    [InlineData(".bmp", 0)]
    [InlineData(".gif", 0)]
    [InlineData(".tif", 0)]
    [InlineData(".jpg", 3)]
    [InlineData(".jpeg", 3)]
    [InlineData(".wmp", 20)]
    [InlineData(".ico", 0)]
    public void LoadFile_DecodesEverySupportedFormat(string extension, int tolerance) => Sta.Run(() =>
    {
        using var file = TestImage.Create(extension, 2, 2, [0xFF336699, 0xFF336699, 0xFF336699, 0xFF336699]);
        using var bitmap = new Bitmap(2, 2, PixelFormats.Bgra32);
        bitmap.LoadFile(file.Path);
        foreach (var pixel in bitmap.ToSpanUInt32())
        {
            Assert.Equal(255u, pixel >> 24);
            Assert.InRange(Math.Abs((int)((pixel >> 16) & 255) - 0x33), 0, tolerance);
            Assert.InRange(Math.Abs((int)((pixel >> 8) & 255) - 0x66), 0, tolerance);
            Assert.InRange(Math.Abs((int)(pixel & 255) - 0x99), 0, tolerance);
        }
    });

    [Fact]
    public void LoadFile_UsesPixelDimensionsRegardlessOfDpi() => Sta.Run(() =>
    {
        uint[] pixels = [0xFF112233, 0xFF445566, 0xFF778899, 0xFF99AABB, 0xFFCCDDEE, 0xFF102030];
        using var file = TestImage.Create(".png", 3, 2, pixels, 192);
        using var bitmap = new Bitmap(3, 2, PixelFormats.Bgra32);
        bitmap.LoadFile(file.Path);
        Assert.Equal(pixels, bitmap.ToSpanUInt32().ToArray());
    });

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 2)]
    [InlineData(4, 4)]
    public void LoadFile_CropsLargerImagesWithoutScaling(int sourceWidth, int sourceHeight) => Sta.Run(() =>
    {
        var pixels = Enumerable.Range(0, sourceWidth * sourceHeight).Select(i => 0xFF102030u + (uint)i).ToArray();
        using var file = TestImage.Create(".png", sourceWidth, sourceHeight, pixels);
        using var bitmap = new Bitmap(2, 2, PixelFormats.Bgra32);
        bitmap.LoadFile(file.Path);
        Assert.Equal(new[] { pixels[0], pixels[1], pixels[sourceWidth], pixels[sourceWidth + 1] }, bitmap.ToSpanUInt32().ToArray());
    });

    [Fact]
    public void LoadFile_PreservesPixelsOutsideSmallerImage() => Sta.Run(() =>
    {
        using var file = TestImage.Create(".png", 1, 2, [0xFF112233, 0xFF445566]);
        using var bitmap = new Bitmap(3, 3, PixelFormats.Bgra32);
        bitmap.ToSpanUInt32().Fill(0xFFABCDEF);
        bitmap.LoadFile(file.Path);
        Assert.Equal(new uint[] { 0xFF112233, 0xFFABCDEF, 0xFFABCDEF, 0xFF445566, 0xFFABCDEF, 0xFFABCDEF, 0xFFABCDEF, 0xFFABCDEF, 0xFFABCDEF }, bitmap.ToSpanUInt32().ToArray());
    });

    [Fact]
    public void LoadFile_ConvertsToPadded24BitRows() => Sta.Run(() =>
    {
        using var file = TestImage.Create(".png", 1, 2, [0xFF112233, 0xFF445566]);
        using var bitmap = new Bitmap(1, 2, PixelFormats.Bgr24);
        bitmap.ToSpanByte().Fill(0xCD);
        bitmap.LoadFile(file.Path);
        Assert.Equal(new byte[] { 0x33, 0x22, 0x11 }, bitmap.ToSpanByte()[..3].ToArray());
        Assert.Equal(new byte[] { 0x66, 0x55, 0x44 }, bitmap.ToSpanByte().Slice(bitmap.Stride, 3).ToArray());
    });

    [Fact]
    public void LoadFile_ConvertsStraightAlphaToPremultipliedAlpha() => Sta.Run(() =>
    {
        using var file = TestImage.Create(".png", 1, 1, [0x80804020]);
        using var bitmap = new Bitmap(1, 1, PixelFormats.Pbgra32);
        bitmap.LoadFile(file.Path);
        Assert.Equal(0x80402010u, bitmap.ToSpanUInt32()[0]);
    });

    [Fact]
    public void LoadFile_RespectsBorrowedSectionOffsetAndStride() => Sta.Run(() =>
    {
        using var file = TestImage.Create(".png", 1, 2, [0xFF112233, 0xFF445566]);
        using var section = MemoryMappedFile.CreateNew(null, 32);
        using var view = section.CreateViewAccessor();
        view.Write(0, 0x12345678u);
        using var bitmap = new Bitmap(section.SafeMemoryMappedFileHandle.DangerousGetHandle(), 1, 2, PixelFormats.Bgra32, 8, 8);
        bitmap.LoadFile(file.Path);
        Assert.Equal(0x12345678u, view.ReadUInt32(0));
        Assert.Equal(0xFF112233u, view.ReadUInt32(8));
        Assert.Equal(0xFF445566u, view.ReadUInt32(16));
    });

    [Fact]
    public void LoadFile_ReleasesInputFileImmediately() => Sta.Run(() =>
    {
        using var file = TestImage.Create(".png", 1, 1, [0xFF123456]);
        using var bitmap = new Bitmap(1, 1, PixelFormats.Bgra32);
        bitmap.LoadFile(file.Path);
        using var exclusive = File.Open(file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    });

    [Fact]
    public void LoadFile_ExtensionMatchingIsCultureIndependent() => Sta.Run(() =>
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            using var file = TestImage.Create(".TIF", 1, 1, [0xFF123456]);
            using var bitmap = new Bitmap(1, 1, PixelFormats.Bgra32);
            bitmap.LoadFile(file.Path);
            Assert.Equal(0xFF123456u, bitmap.ToSpanUInt32()[0]);
        }
        finally { CultureInfo.CurrentCulture = original; }
    });

    [Fact]
    public void LoadFile_RejectsUnsupportedExtension() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(1, 1, PixelFormats.Bgra32);
        Assert.Throws<Exception>(() => bitmap.LoadFile("unsupported.xyz"));
    });

    [Fact]
    public void LoadFile_MissingFilePreservesExistingPixels() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(1, 1, PixelFormats.Bgra32);
        bitmap.ToSpanUInt32()[0] = 0xFF123456;
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".png");
        Assert.Throws<FileNotFoundException>(() => bitmap.LoadFile(missing));
        Assert.Equal(0xFF123456u, bitmap.ToSpanUInt32()[0]);
    });

    private sealed class TestImage(string path) : IDisposable
    {
        public string Path { get; } = path;

        public static TestImage Create(string extension, int width, int height, uint[] pixels, double dpi = 96)
        {
            var source = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgra32, null, pixels, width * 4);
            BitmapEncoder encoder = extension.ToLowerInvariant() switch
            {
                ".bmp" => new BmpBitmapEncoder(),
                ".gif" => new GifBitmapEncoder(),
                ".tif" => new TiffBitmapEncoder(),
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 100 },
                ".wmp" => new WmpBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var encoded = new MemoryStream();
            encoder.Save(encoded);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + extension);
            using (var file = File.Create(path))
            {
                if (extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true);
                    writer.Write((ushort)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)1);
                    writer.Write((byte)width);
                    writer.Write((byte)height);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)encoded.Length);
                    writer.Write(22u);
                }
                encoded.Position = 0;
                encoded.CopyTo(file);
            }
            return new TestImage(path);
        }

        public void Dispose() => File.Delete(Path);
    }
}
