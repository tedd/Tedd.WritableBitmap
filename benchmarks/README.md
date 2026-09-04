# Performance experiments

This project uses BenchmarkDotNet 0.15.8 and actual Skia allocations. Each comparison compiles two complete archived implementations into distinct assemblies, selected by C# extern aliases. Both run with the same compiler, .NET 10 runtime, dependency version and workload. Production source is copied from an accepted archive only after tests and measurements pass.

## Reproduce

From the repository root, on Windows with the SDK in `global.json` and both .NET runtimes installed:

```powershell
./benchmarks/Invoke-Benchmarks.ps1
./benchmarks/Invoke-Benchmarks.ps1 -Filter '*Color*', '*Frame*'
```

The script verifies archive SHA-256 manifests, runs all accepted archives against the current MAUI tests, runs the production MAUI/WPF tests on both runtimes, and then runs BenchmarkDotNet. It rejects missing benchmark measurements as well as process failures. Run one benchmark process at a time on an otherwise idle machine.

For a measurement without repeating the test gate:

```powershell
dotnet run --project benchmarks/Tedd.WriteableBitmap.Benchmarks.csproj -c Release -- --filter '*' --artifacts benchmarks/results/local
```

The default job uses separate processes, one launch, three warmups and ten measured iterations of 250 ms. The confirmation run used five warmups and twelve measured iterations of 500 ms. These are local engineering measurements, not hardware-independent performance guarantees.

## Archives

| Archive | Difference from preceding accepted version | Status |
| --- | --- | --- |
| V00 | Original MAUI implementation | Baseline |
| V01 | Cache immutable native pointer/length for raw access | Raw-access optimization |
| V02 | Atomic lease-generation validation and direct native spans | Lease-access optimization |
| V03 | Use `System.Threading.Lock` for lifetime synchronization | Additional modest refinement; gains depend on buffer count |
| V04 | Inline color packing and premultiply red/blue in separate packed lanes | Color optimization |
| V05 | Cache immutable front-frame images | Rejected: native callback rooted the owner permanently |
| V06 | Image cache with independent allocation lifetime and weak owner reference | Superseded: regression for tiny newly published frames |
| V07 | Keep first presentation transient; cache only subsequent unchanged presentations | Redraw optimization; replaces V05/V06 |
| V08 | Replace both circular-index remainder operations with increment-and-wrap | Buffer-selection optimization |

Every archive contains complete source, its own build project, and a SHA-256 manifest. Archives are never overwritten after sealing. `New-Archive.ps1 -Name V09 -From V08` creates a new experiment and refuses an existing destination. Edit the new copy, add a benchmark against its immediate accepted predecessor, run the complete test suite against both, measure, and review the result before changing production. `Verify-Archives.ps1 -Seal` seals new archives without replacing existing manifests.

```powershell
dotnet test tests/Tedd.WriteableBitmap.Maui.Tests -c Release -f net10.0 -p:BitmapArchive=V08
```

The archived projects target .NET 10 to hold the runtime constant. Production projects include separate .NET 10 and .NET 11 assets. Unit tests also execute on .NET 11 Preview.

## Workloads and limits

- `RawAccessBenchmarks`: retrieve and use a raw span repeatedly. This isolates native accessor overhead.
- `LeaseAccessBenchmarks`: repeatedly retrieve and use a lease's pixel span. Normal frame producers should still cache that span once.
- `PublicationBenchmarks`: acquire, touch and publish frames with two, three or eight buffers.
- `AcquisitionBenchmarks`: the same operation, isolating circular-index arithmetic in V07 versus V08.
- `ColorBenchmarks`: varied input colors with mixed, translucent or opaque alpha. Results return a checksum.
- `DrawBenchmarks`: present an unchanged frame or publish before every presentation, at 32×32 and 1024×1024. The final comparison is V04 versus V07; earlier runs used V05 and V06.
- `FrameBenchmarks`: generate every pixel, publish and draw 256×144 or 1920×1080 frames. It caches each lease span once and compares V00 with V08.

Draw and frame benchmarks use CPU raster canvases. They do not measure GPU uploads, device presentation, UI dispatch, or on-screen frame rates. Redraw allocation savings apply after the second presentation of an unchanged frame; each newly presented frame still needs a transient image. Native memory is not included in BenchmarkDotNet's managed-allocation column. Microbenchmark improvements must not be multiplied together or represented as complete-frame speedups.

See [RESULTS.md](RESULTS.md) for measurements, acceptance decisions and test evidence.
