```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=12  IterationTime=500ms  
LaunchCount=1  WarmupCount=5  

```
| Method | Width | Mean        | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |------ |------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **Before** | **256**   |   **128.46 μs** |  **22.163 μs** |  **17.304 μs** |  **1.02** |    **0.19** |     **136 B** |        **1.00** |
| After  | 256   |    69.96 μs |   2.001 μs |   1.447 μs |  0.55 |    0.07 |     136 B |        1.00 |
|        |       |             |            |            |       |         |           |             |
| **Before** | **1920**  | **6,413.36 μs** | **266.350 μs** | **207.948 μs** |  **1.00** |    **0.04** |     **136 B** |        **1.00** |
| After  | 1920  | 4,978.37 μs | 751.744 μs | 586.913 μs |  0.78 |    0.09 |     136 B |        1.00 |
