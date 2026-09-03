using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using SkiaSharp;
using Tedd.Maui;

namespace Tedd.WriteableBitmap.Maui.Compatibility;

public static class ConsumerProbe
{
    public static MauiAppBuilder Configure(MauiAppBuilder builder) =>
        builder.UseTeddWriteableBitmap();

    public static WriteableBitmapView CreateView()
    {
        var bitmap = new Tedd.Maui.WriteableBitmap(8, 8);
        if (!bitmap.TryBeginWrite(out var write))
            throw new InvalidOperationException();

        try
        {
            write.Pixels.Fill(Tedd.Maui.WriteableBitmap.FromRgba(255, 0, 0, 255));
        }
        finally
        {
            write.Dispose();
        }

        return new WriteableBitmapView
        {
            Source = bitmap,
            Aspect = Aspect.AspectFit,
            FilterMode = SKFilterMode.Nearest
        };
    }
}
