# Optimization measurements — 2026-09-03

Measurements use BenchmarkDotNet 0.15.8, .NET 10.0.11 x64, Windows 11 and an AMD Ryzen 9 5950X. All comparisons compile archived source with SDK 11.0.100-preview.7.26381.103. Numbers below are elapsed-time reductions, not increases in throughput. Confidence intervals and all parameter combinations remain in the linked reports.

## Accepted changes

| Change | Compared archives | Workload | Before | After | Time reduction |
| --- | --- | --- | ---: | ---: | ---: |
| Cached native pointer and length | V00 → V01 | Repeated raw span access | 7.059 ns | 1.628 ns | 77% |
| Atomic lease validation and direct spans | V01 → V02 | Two span accesses plus pixel write/read | 24.156 ns | 3.413 ns | 86% |
| Inlined packed color arithmetic | V03 → V04 | Translucent color, alpha 128 | 3.215 ns | 2.221 ns | 31% |
| Adaptive immutable-image cache | V04 → V07 | Repeated 32×32 presentation | 1,050.9 ns | 397.0 ns | 62% |
| Division-free circular indexing | V07 → V08 | Acquire/write/publish with 2 buffers | 15.46 ns | 12.96 ns | 16% |

Raw and lease access remain allocation-free. Mixed-alpha color conversion improves 20%; opaque conversion is effectively unchanged. The tests exhaustively compare rounding for every channel/alpha pair in both RGBA and BGRA layouts.

The image cache removes 136 managed bytes per steady-state unchanged redraw. It activates on the second presentation; subsequent redraws reuse that image. Each new frame still creates a transient image and allocates 136 managed bytes. New-frame 32×32 presentation measured 1,049.1 → 1,018.0 ns, eliminating V06's regression. At 1024×1024, CPU raster copying dominates and no material draw-time difference is established; unchanged redraws still avoid allocation.

The separate `System.Threading.Lock` refinement (V02 → V03) measured approximately 0%, 9%, and 18% time reduction at 2, 3, and 8 buffers respectively in the confirmation run. Its default-buffer result overlaps the reported confidence intervals, so it is not counted among the five principal gains.

Circular indexing also measured 15.52 → 14.04 ns at the default 3 buffers and 16.57 → 14.73 ns at 8 buffers. Those smaller differences overlap the confidence intervals; the statistically clearer 2-buffer case is reported in the table. These acquisition results include lease acquisition, a pixel write, and publication, rather than measuring integer arithmetic in isolation.

## Complete frames and final validation

The final V08 comparison generates all translucent pixels, publishes the frame, and draws it to a real CPU raster canvas, retrieving the lease span only once:

| Frame | Original V00 | Final V08 | Time reduction |
| --- | ---: | ---: | ---: |
| 256×144 | 126.88 µs | 74.30 µs | 41% |
| 1920×1080 | 6.474 ms | 4.371 ms | 32% |

The longer preceding V07 run measured approximately 30% reduction at both sizes. This variation is why the individual distributions and confidence intervals are retained rather than treating one percentage as universal. Complete new frames still allocate 136 managed bytes for their transient image.

Production source now matches `archives/V08/WriteableBitmap.cs` byte for byte. All seven accepted snapshots passed the complete 193-case MAUI suite before promotion. The final Release solution build succeeds; its 33 warnings concern the existing GUI samples and their legacy targets. The production packages pass **580 unit-test executions**: 193 MAUI and 97 WPF cases on each of .NET 10 and .NET 11 Preview. [Final test logs and TRX files](results/production-tests/) record the post-promotion runs.

Package versions are WPF 1.0.5 and MAUI 1.1.1. Both include explicit .NET 10 and .NET 11 assets while the WPF package retains its older targets. [Package inspection](results/package-assets.json) records the actual DLL entries. Local packages are in `artifacts/releases`; publication to NuGet was not performed.

## Evidence and rejected experiments

- [Initial measurements](results/run-01/results/): raw access, lease access, lock refinement, color packing, and the original V05 image cache. One launch, three warmups, ten measured iterations of 250 ms.
- [Confirmation measurements](results/run-02/results/): lock refinement, V06 drawing, and V06 complete frames. One launch, five warmups, twelve measured iterations of 500 ms.
- [Adaptive-cache measurements](results/run-03/results/): V04 versus V07 drawing, plus V00 versus V07 complete frames. Same longer job.
- [Final measurements](results/run-04/results/): V07 versus V08 buffer acquisition, and V00 versus V08 complete frames. Original 250 ms job.
- [Pre-promotion test gates](results/final-gates/): the same 193 MAUI cases pass against every accepted archive; both WPF runtime suites pass 97 cases each.

V05 was rejected because its native release callback permanently rooted the bitmap owner. The failure was reproduced in [the collection regression test](results/gates/V05-rejected.log); V06 breaks that root through independent native-allocation state and a weak owner reference. V06 passed correctness tests but regressed tiny new-frame presentation by 43% in run 02. V07 keeps the original transient first-draw path and caches repeated presentation only. V05 and V06 remain archived, with their results, instead of being overwritten.

The initial BenchmarkDotNet invocation produced no measurements because generated build outputs collided between identically named archive projects. The projects were given unique names before run 01. The failed invocation is retained under `results/initial` and excluded from every comparison above.

## Interpretation

The experiments isolate actual implementation changes, using complete archived classes rather than substitute algorithms. The microbenchmarks target specific operations; applications that already retrieve a span only once per frame will not realize the repeated-access percentage across their entire frame. Full-frame tests use one span acquisition and perform real pixel generation and Skia raster presentation.

These are local CPU measurements, with observable timing variation. GPU upload reuse, MAUI dispatch, device rendering and on-screen frame rates were not benchmarked. .NET 11 preview library assets and unit tests are validated separately; its runtime performance and MAUI platform consumers are not established by these .NET 10 measurements.
