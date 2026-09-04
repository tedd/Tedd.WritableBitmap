using System.Runtime.InteropServices;
using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class WriteableBitmapColorTests
{
    [Theory]
    [InlineData(255, 127, 1, 1, 1, 0, 0)]
    [InlineData(255, 128, 1, 1, 1, 1, 0)]
    [InlineData(255, 128, 64, 127, 127, 64, 32)]
    [InlineData(255, 128, 64, 128, 128, 64, 32)]
    [InlineData(254, 127, 1, 128, 127, 64, 1)]
    [InlineData(255, 254, 1, 254, 254, 253, 1)]
    [InlineData(1, 2, 3, 128, 1, 1, 2)]
    [InlineData(1, 2, 3, 127, 0, 1, 1)]
    [InlineData(255, 128, 64, 0, 0, 0, 0)]
    [InlineData(17, 34, 51, 255, 17, 34, 51)]
    [InlineData(0, 0, 0, 128, 0, 0, 0)]
    [InlineData(255, 255, 255, 255, 255, 255, 255)]
    public void ColorConversionsPremultiplyWithNearestIntegerRounding(
        byte red, byte green, byte blue, byte alpha,
        byte premultipliedRed, byte premultipliedGreen, byte premultipliedBlue)
    {
        var packed = WriteableBitmap.FromRgba(red, green, blue, alpha);

        Assert.Equal(premultipliedRed, (byte)(packed >> SKImageInfo.PlatformColorRedShift));
        Assert.Equal(premultipliedGreen, (byte)(packed >> SKImageInfo.PlatformColorGreenShift));
        Assert.Equal(premultipliedBlue, (byte)(packed >> SKImageInfo.PlatformColorBlueShift));
        Assert.Equal(alpha, (byte)(packed >> SKImageInfo.PlatformColorAlphaShift));
        Assert.Equal(packed, WriteableBitmap.FromColor(new SKColor(red, green, blue, alpha)));
        Assert.Equal(packed, WriteableBitmap.FromColor(
            Microsoft.Maui.Graphics.Color.FromRgba(red, green, blue, alpha)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(191)]
    [InlineData(254)]
    [InlineData(255)]
    public void PackedPixelsMatchSkiaForEveryAlpha(byte component)
    {
        using var nativeBitmap = new SKBitmap(new SKImageInfo(
            1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Premul));

        for (var alpha = 0; alpha <= byte.MaxValue; alpha++)
        {
            var color = new SKColor(component, (byte)(255 - component), component, (byte)alpha);
            nativeBitmap.Erase(color);
            var nativePixel = MemoryMarshal.Cast<byte, uint>(nativeBitmap.GetPixelSpan())[0];

            Assert.Equal(nativePixel, WriteableBitmap.FromColor(color));
        }
    }

    [Fact]
    public void FromMauiColorRejectsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => WriteableBitmap.FromColor((Microsoft.Maui.Graphics.Color)null!));

        Assert.Equal("color", exception.ParamName);
    }
}
