extern alias V00;
extern alias V01;
extern alias V02;
extern alias V03;
using BenchmarkDotNet.Attributes;

namespace Tedd.Benchmarks;

[MemoryDiagnoser]
public class RawAccessBenchmarks
{
    private V00::Tedd.Maui.WriteableBitmap _before = null!;
    private V01::Tedd.Maui.WriteableBitmap _after = null!;
    [GlobalSetup] public void Setup() { _before = new(64, 64); _after = new(64, 64); }
    [GlobalCleanup] public void Cleanup() { _before.Dispose(); _after.Dispose(); }
    [Benchmark(Baseline = true, OperationsPerInvoke = 1024)]
    public int Before() { var sum = 0; for (var i = 0; i < 1024; i++) { var span = _before.ToSpanUInt32(); span[i] = (uint)i; sum += span.Length; } return sum; }
    [Benchmark(OperationsPerInvoke = 1024)]
    public int After() { var sum = 0; for (var i = 0; i < 1024; i++) { var span = _after.ToSpanUInt32(); span[i] = (uint)i; sum += span.Length; } return sum; }
}

[MemoryDiagnoser]
public class LeaseAccessBenchmarks
{
    private V01::Tedd.Maui.WriteableBitmap _before = null!;
    private V02::Tedd.Maui.WriteableBitmap _after = null!;
    [GlobalSetup] public void Setup() { _before = new(64, 64); _after = new(64, 64); }
    [GlobalCleanup] public void Cleanup() { _before.Dispose(); _after.Dispose(); }
    [Benchmark(Baseline = true, OperationsPerInvoke = 1024)]
    public uint Before()
    {
        if (!_before.TryBeginWrite(out var lease)) throw new InvalidOperationException();
        try { uint sum = 0; for (var i = 0; i < 1024; i++) { lease.Pixels[i] = (uint)i; sum += lease.Pixels[i]; } return sum; }
        finally { lease.Dispose(); }
    }
    [Benchmark(OperationsPerInvoke = 1024)]
    public uint After()
    {
        if (!_after.TryBeginWrite(out var lease)) throw new InvalidOperationException();
        try { uint sum = 0; for (var i = 0; i < 1024; i++) { lease.Pixels[i] = (uint)i; sum += lease.Pixels[i]; } return sum; }
        finally { lease.Dispose(); }
    }
}

[MemoryDiagnoser]
public class PublicationBenchmarks
{
    private V02::Tedd.Maui.WriteableBitmap _before = null!;
    private V03::Tedd.Maui.WriteableBitmap _after = null!;
    [Params(2, 3, 8)] public int BufferCount { get; set; }
    [GlobalSetup] public void Setup() { _before = new(8, 8, BufferCount); _after = new(8, 8, BufferCount); }
    [GlobalCleanup] public void Cleanup() { _before.Dispose(); _after.Dispose(); }
    [Benchmark(Baseline = true, OperationsPerInvoke = 1024)]
    public void Before()
    {
        for (var i = 0; i < 1024; i++) { if (!_before.TryBeginWrite(out var lease)) throw new InvalidOperationException(); lease.Pixels[0] = (uint)i; lease.Dispose(); }
    }
    [Benchmark(OperationsPerInvoke = 1024)]
    public void After()
    {
        for (var i = 0; i < 1024; i++) { if (!_after.TryBeginWrite(out var lease)) throw new InvalidOperationException(); lease.Pixels[0] = (uint)i; lease.Dispose(); }
    }
}
