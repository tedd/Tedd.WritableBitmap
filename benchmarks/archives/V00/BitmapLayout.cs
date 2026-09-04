using Microsoft.Maui;
using SkiaSharp;

namespace Tedd.Maui;

internal static class BitmapLayout
{
    public static SKRect CalculateDestination(
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        Aspect aspect)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
            return SKRect.Empty;

        if (aspect == Aspect.Fill)
            return new SKRect(0, 0, targetWidth, targetHeight);

        if (aspect == Aspect.Center)
        {
            var left = (targetWidth - sourceWidth) * 0.5f;
            var top = (targetHeight - sourceHeight) * 0.5f;
            return new SKRect(left, top, left + sourceWidth, top + sourceHeight);
        }

        var horizontalScale = (float)targetWidth / sourceWidth;
        var verticalScale = (float)targetHeight / sourceHeight;
        var scale = aspect == Aspect.AspectFill
            ? Math.Max(horizontalScale, verticalScale)
            : Math.Min(horizontalScale, verticalScale);

        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var x = (targetWidth - width) * 0.5f;
        var y = (targetHeight - height) * 0.5f;

        return new SKRect(x, y, x + width, y + height);
    }

    public static bool RequiresClear(SKRect destination, int targetWidth, int targetHeight) =>
        destination.Left > 0
        || destination.Top > 0
        || destination.Right < targetWidth
        || destination.Bottom < targetHeight;
}
