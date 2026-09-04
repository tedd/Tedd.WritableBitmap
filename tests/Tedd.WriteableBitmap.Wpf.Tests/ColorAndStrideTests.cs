using System.Windows.Media;
using Xunit;
using Bitmap = global::Tedd.WriteableBitmap;

namespace Tedd.Wpf.Tests;

public class ColorAndStrideTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0u)]
    [InlineData(255, 255, 255, 255, 0xFFFFFFFFu)]
    [InlineData(255, 0, 0, 255, 0xFFFF0000u)]
    [InlineData(0, 255, 0, 255, 0xFF00FF00u)]
    [InlineData(0, 0, 255, 255, 0xFF0000FFu)]
    [InlineData(18, 52, 86, 120, 0x78123456u)]
    [InlineData(255, 128, 1, 0, 0x00FF8001u)]
    public void ColorPacking_PreservesEveryChannel(byte red, byte green, byte blue, byte alpha, uint expected)
    {
        Assert.Equal(expected, Bitmap.FromRgba(red, green, blue, alpha));
        Assert.Equal(expected, Bitmap.FromColor(Color.FromArgb(alpha, red, green, blue)));
    }

    public static TheoryData<PixelFormat, int, int> Strides => new()
    {
        { PixelFormats.Bgra32, 1, 4 }, { PixelFormats.Bgra32, 3, 12 },
        { PixelFormats.Bgr24, 1, 4 }, { PixelFormats.Bgr24, 2, 8 },
        { PixelFormats.Bgr24, 3, 12 }, { PixelFormats.Bgr24, 4, 12 },
        { PixelFormats.Gray8, 1, 4 }, { PixelFormats.Gray8, 4, 4 }, { PixelFormats.Gray8, 5, 8 },
        { PixelFormats.Bgr565, 1, 4 }, { PixelFormats.Bgr565, 2, 4 }, { PixelFormats.Bgr565, 3, 8 },
        { PixelFormats.BlackWhite, 8, 4 }, { PixelFormats.BlackWhite, 33, 8 },
        { PixelFormats.Gray2, 17, 8 }, { PixelFormats.Gray4, 9, 8 },
        { PixelFormats.Rgb48, 3, 20 }, { PixelFormats.Rgba64, 3, 24 },
        { PixelFormats.Rgba128Float, 3, 48 },
    };

    [Theory]
    [MemberData(nameof(Strides))]
    public void CalculateStride_AlignsPackedScanlinesToFourBytes(PixelFormat format, int width, int expected) =>
        Assert.Equal(expected, Bitmap.CalculateStride(format, width));
}

