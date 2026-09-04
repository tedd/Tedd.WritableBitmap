# Tedd.WriteableBitmap.Maui

`Tedd.WriteableBitmap.Maui` provides a directly mutable native pixel swap chain and a dedicated GPU-backed view for .NET MAUI on Android, iOS, Mac Catalyst, and Windows.

The package includes explicit `net10.0` and `net11.0` assemblies so both .NET versions appear in NuGet's included frameworks. .NET 11 support is preliminary while [.NET 11 remains in preview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview). The library uses platform-neutral MAUI APIs; MAUI and SkiaSharp resolve their native assets for each application's target platform.

It does not encode frames, allocate `ImageSource` streams, or copy pixels into managed staging images. SkiaSharp owns the native buffers; `WriteableBitmapView` submits the newest completed frame to its GPU-backed surface. Normal CPU-to-GPU upload still occurs.

The first presentation of each published frame uses a transient image. A second presentation creates a reusable immutable image; further unchanged redraws reuse it without managed allocation. Publishing new pixels retires the corresponding cached image before that buffer can be written again. Spans use cached allocation metadata, and lease access validates an atomic generation while acquisition and publication remain synchronized.

The repository's `benchmarks` directory contains BenchmarkDotNet comparisons, archived source for every optimization, and test results. Measurements cover CPU access, color generation and raster drawing; they do not establish GPU or device frame rates.

## Configure MAUI

Install `Tedd.WriteableBitmap.Maui`, then register its handler in `MauiProgram.cs`:

```csharp
using Tedd.Maui;

var builder = MauiApp.CreateBuilder();

builder
    .UseMauiApp<App>()
    .UseTeddWriteableBitmap();
```

## Bind the view

```xml
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:tedd="clr-namespace:Tedd.Maui;assembly=Tedd.WriteableBitmap.Maui">
    <tedd:WriteableBitmapView
        Source="{Binding Bitmap}"
        Aspect="AspectFit"
        FilterMode="Nearest" />
</ContentPage>
```

The bound property is a `Tedd.Maui.WriteableBitmap` instance:

```csharp
using Tedd.Maui;

public sealed class ViewModel : IDisposable
{
    public WriteableBitmap Bitmap { get; } = new(1920, 1080);

    public void RenderFrame()
    {
        // Drop this producer tick only if every back buffer is still occupied.
        if (!Bitmap.TryBeginWrite(out var write))
            return;

        try
        {
            write.Pixels.Fill(WriteableBitmap.FromRgba(255, 0, 0, 255));
        }
        finally
        {
            // Disposal publishes the frame and requests one coalesced redraw.
            write.Dispose();
        }
    }

    public void Dispose() => Bitmap.Dispose();
}
```

`TryBeginWrite()` is nonblocking. It writes a free back buffer while views retain older frames, and returns `false` only when another writer is active or every back buffer is occupied. Lease disposal atomically publishes that buffer as the newest frame and may occur on a worker thread. Each attached view coalesces pending requests and dispatches the eventual redraw to its UI dispatcher. `HasRenderLoop` remains disabled, so unchanged frames consume no rendering work.

## Pixel contract

- Every buffer uses `SKImageInfo.PlatformColorType`, four bytes per pixel, and premultiplied alpha. Use `FromRgba` or `FromColor` rather than assuming RGBA or BGRA byte order.
- `Stride` is authoritative. Use `GetIndex(x, y)` when indexing a lease's `Pixels` span.
- `TryBeginWrite()` is the safe concurrent path. Its stack-only lease adds no managed allocation. Cache `write.Pixels` once before a per-pixel loop; do not copy the lease, and do not use derived spans after it is disposed.
- A leased back buffer may contain an older frame. Write every pixel required by the new frame; the fast path deliberately does not copy the current front buffer. Use the externally synchronized raw compatibility buffer when persistent partial updates are required.
- The default constructor allocates three native buffers so CPU production and GPU presentation can overlap. Native storage is `Stride * Height * BufferCount`; pass `bufferCount: 2` to minimize memory or a larger count for unusually deep GPU pipelines.
- Raw span and pointer APIs always expose compatibility buffer zero; their address does not change when leased frames swap. Do not mix raw and leased writes. Externally synchronize raw access, then call `Invalidate()` once; it throws if Skia still retains buffer zero.
- Borrowed spans and pointers become invalid immediately when the bitmap or their lease is disposed. Physical release is deferred until already-submitted GPU work releases the corresponding frame.
- `Nearest` is the fastest sampling mode and preserves exact pixels. Select `Linear` only when scaled interpolation is required.

The existing `Tedd.WriteableBitmap` package remains the WPF-specific implementation. The packages are separate because WPF and MAUI-Windows cannot be distinguished reliably by NuGet target-framework selection.
