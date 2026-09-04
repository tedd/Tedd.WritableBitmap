extern alias V04;
extern alias V07;
using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Tedd.Benchmarks;

[MemoryDiagnoser]
public class DrawBenchmarks
{
    private V04::Tedd.Maui.WriteableBitmap _before = null!;
    private V07::Tedd.Maui.WriteableBitmap _after = null!;
    private SKBitmap _target = null!;
    private SKCanvas _canvas = null!;
    private SKPaint _paint = null!;
    private SKRect _destination;
    private readonly SKSamplingOptions _sampling = new(SKFilterMode.Nearest);
    [Params(32, 1024)] public int Size { get; set; }
    [Params(false, true)] public bool PublishEachDraw { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        _before = new(Size, Size); _after = new(Size, Size);
        _target = new SKBitmap(Size, Size);
        _canvas = new SKCanvas(_target);
        _paint = new SKPaint { BlendMode = SKBlendMode.Src };
        _destination = new SKRect(0, 0, Size, Size);
    }
    [GlobalCleanup]
    public void Cleanup() { _before.Dispose(); _after.Dispose(); _paint.Dispose(); _canvas.Dispose(); _target.Dispose(); }
    [Benchmark(Baseline = true)]
    public int Before()
    {
        if (PublishEachDraw) { if (!_before.TryBeginWrite(out var lease)) throw new InvalidOperationException(); lease.Dispose(); }
        return (int)_before.TryDraw(_canvas, _destination, _sampling, _paint, false);
    }
    [Benchmark]
    public int After()
    {
        if (PublishEachDraw) { if (!_after.TryBeginWrite(out var lease)) throw new InvalidOperationException(); lease.Dispose(); }
        return (int)_after.TryDraw(_canvas, _destination, _sampling, _paint, false);
    }
}
