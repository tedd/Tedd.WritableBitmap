extern alias V00;
extern alias V08;
using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Tedd.Benchmarks;

/// <summary>Complete CPU color generation, frame publication and raster presentation.</summary>
[MemoryDiagnoser]
public class FrameBenchmarks
{
    private V00::Tedd.Maui.WriteableBitmap _before = null!;
    private V08::Tedd.Maui.WriteableBitmap _after = null!;
    private SKBitmap _target = null!;
    private SKCanvas _canvas = null!;
    private SKPaint _paint = null!;
    private SKRect _destination;
    private readonly SKSamplingOptions _sampling = new(SKFilterMode.Nearest);
    [Params(256, 1920)] public int Width { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        var height = Width * 9 / 16;
        _before = new(Width, height);
        _after = new(Width, height);
        _target = new SKBitmap(Width, height);
        _canvas = new SKCanvas(_target);
        _paint = new SKPaint { BlendMode = SKBlendMode.Src };
        _destination = new SKRect(0, 0, Width, height);
    }
    [GlobalCleanup]
    public void Cleanup() { _before.Dispose(); _after.Dispose(); _paint.Dispose(); _canvas.Dispose(); _target.Dispose(); }
    [Benchmark(Baseline = true)]
    public int Before()
    {
        if (!_before.TryBeginWrite(out var lease)) throw new InvalidOperationException();
        try
        {
            // Retrieve the span once, as recommended for actual frame producers.
            var pixels = lease.Pixels;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = V00::Tedd.Maui.WriteableBitmap.FromRgba((byte)i, (byte)(i >> 3), (byte)(i >> 7), 128);
        }
        finally { lease.Dispose(); }
        return (int)_before.TryDraw(_canvas, _destination, _sampling, _paint, false);
    }
    [Benchmark]
    public int After()
    {
        if (!_after.TryBeginWrite(out var lease)) throw new InvalidOperationException();
        try
        {
            var pixels = lease.Pixels;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = V08::Tedd.Maui.WriteableBitmap.FromRgba((byte)i, (byte)(i >> 3), (byte)(i >> 7), 128);
        }
        finally { lease.Dispose(); }
        return (int)_after.TryDraw(_canvas, _destination, _sampling, _paint, false);
    }
}
