```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=10  IterationTime=250ms  
LaunchCount=1  WarmupCount=3  

```
| Method | Width | Mean        | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |------ |------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **Before** | **256**   |   **126.88 μs** |  **24.735 μs** |  **16.360 μs** |  **1.01** |    **0.17** |     **136 B** |        **1.00** |
| After  | 256   |    74.30 μs |   6.531 μs |   4.320 μs |  0.59 |    0.08 |     136 B |        1.00 |
|        |       |             |            |            |       |         |           |             |
| **Before** | **1920**  | **6,474.05 μs** | **386.299 μs** | **255.513 μs** |  **1.00** |    **0.05** |     **136 B** |        **1.00** |
| After  | 1920  | 4,371.04 μs | 243.012 μs | 160.737 μs |  0.68 |    0.03 |     136 B |        1.00 |
