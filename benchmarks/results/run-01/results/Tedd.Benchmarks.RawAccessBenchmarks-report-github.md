```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=10  IterationTime=250ms  
LaunchCount=1  WarmupCount=3  

```
| Method | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| Before | 7.059 ns | 1.7663 ns | 1.1683 ns |  1.02 |    0.22 |         - |          NA |
| After  | 1.628 ns | 0.1123 ns | 0.0668 ns |  0.24 |    0.04 |         - |          NA |
