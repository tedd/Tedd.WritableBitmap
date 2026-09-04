extern alias V07;
extern alias V08;
using BenchmarkDotNet.Attributes;

namespace Tedd.Benchmarks;

/// <summary>Isolates division-free circular buffer selection from all other changes.</summary>
[MemoryDiagnoser]
public class AcquisitionBenchmarks
{
    private V07::Tedd.Maui.WriteableBitmap _before = null!;
    private V08::Tedd.Maui.WriteableBitmap _after = null!;
    [Params(2, 3, 8)] public int BufferCount { get; set; }
    [GlobalSetup] public void Setup() { _before = new(8, 8, BufferCount); _after = new(8, 8, BufferCount); }
    [GlobalCleanup] public void Cleanup() { _before.Dispose(); _after.Dispose(); }
    [Benchmark(Baseline = true, OperationsPerInvoke = 1024)]
    public void Before()
    {
        for (var i = 0; i < 1024; i++)
        {
            if (!_before.TryBeginWrite(out var lease)) throw new InvalidOperationException();
            lease.Pixels[0] = (uint)i;
            lease.Dispose();
        }
    }
    [Benchmark(OperationsPerInvoke = 1024)]
    public void After()
    {
        for (var i = 0; i < 1024; i++)
        {
            if (!_after.TryBeginWrite(out var lease)) throw new InvalidOperationException();
            lease.Pixels[0] = (uint)i;
            lease.Dispose();
        }
    }
}
