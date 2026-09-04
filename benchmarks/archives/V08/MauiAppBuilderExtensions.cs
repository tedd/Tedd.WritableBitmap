using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Tedd.Maui;

public static class MauiAppBuilderExtensions
{
    /// <summary>Registers the SkiaSharp handlers required by <see cref="WriteableBitmapView"/>.</summary>
    public static MauiAppBuilder UseTeddWriteableBitmap(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSkiaSharp();
    }
}
