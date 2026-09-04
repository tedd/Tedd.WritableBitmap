```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=10  IterationTime=250ms  
LaunchCount=1  WarmupCount=3  

```
| Method | Alpha | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|------- |------ |-----:|------:|------:|--------:|------------:|
| Before | -1    |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  ColorBenchmarks.Before: Net10(IterationCount=10, IterationTime=250ms, LaunchCount=1, WarmupCount=3) [Alpha=-1]
