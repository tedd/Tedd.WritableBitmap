extern alias V03;
extern alias V04;
using BenchmarkDotNet.Attributes;

namespace Tedd.Benchmarks;

[MemoryDiagnoser]
public class ColorBenchmarks
{
    private uint[] _colors = null!;
    [Params(128, 255, -1)] public int Alpha { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(741);
        _colors = new uint[4096];
        for (var i = 0; i < _colors.Length; i++) _colors[i] = (uint)random.NextInt64(0, 1L << 32);
    }
    [Benchmark(Baseline = true, OperationsPerInvoke = 4096)]
    public uint Before()
    {
        uint sum = 0;
        foreach (var c in _colors) sum ^= V03::Tedd.Maui.WriteableBitmap.FromRgba((byte)c, (byte)(c >> 8), (byte)(c >> 16), Alpha < 0 ? (byte)(c >> 24) : (byte)Alpha);
        return sum;
    }
    [Benchmark(OperationsPerInvoke = 4096)]
    public uint After()
    {
        uint sum = 0;
        foreach (var c in _colors) sum ^= V04::Tedd.Maui.WriteableBitmap.FromRgba((byte)c, (byte)(c >> 8), (byte)(c >> 16), Alpha < 0 ? (byte)(c >> 24) : (byte)Alpha);
        return sum;
    }
}
