```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=10  IterationTime=250ms  
LaunchCount=1  WarmupCount=3  

```
| Method | BufferCount | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |------------ |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Before** | **2**           | **13.07 ns** | **0.453 ns** | **0.299 ns** |  **1.00** |    **0.03** |         **-** |          **NA** |
| After  | 2           | 13.34 ns | 0.745 ns | 0.493 ns |  1.02 |    0.04 |         - |          NA |
|        |             |          |          |          |       |         |           |             |
| **Before** | **3**           | **17.62 ns** | **2.996 ns** | **1.981 ns** |  **1.01** |    **0.16** |         **-** |          **NA** |
| After  | 3           | 13.23 ns | 1.136 ns | 0.751 ns |  0.76 |    0.09 |         - |          NA |
|        |             |          |          |          |       |         |           |             |
| **Before** | **8**           | **19.45 ns** | **4.934 ns** | **3.263 ns** |  **1.03** |    **0.27** |         **-** |          **NA** |
| After  | 8           | 14.50 ns | 4.375 ns | 2.894 ns |  0.77 |    0.21 |         - |          NA |
