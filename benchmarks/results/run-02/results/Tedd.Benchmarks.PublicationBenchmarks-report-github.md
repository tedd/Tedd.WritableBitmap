```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=12  IterationTime=500ms  
LaunchCount=1  WarmupCount=5  

```
| Method | BufferCount | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |------------ |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Before** | **2**           | **13.37 ns** | **0.583 ns** | **0.386 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| After  | 2           | 13.34 ns | 0.628 ns | 0.491 ns |  1.00 |    0.04 |         - |          NA |
|        |             |          |          |          |       |         |           |             |
| **Before** | **3**           | **14.74 ns** | **1.374 ns** | **1.073 ns** |  **1.00** |    **0.10** |         **-** |          **NA** |
| After  | 3           | 13.45 ns | 0.802 ns | 0.626 ns |  0.92 |    0.07 |         - |          NA |
|        |             |          |          |          |       |         |           |             |
| **Before** | **8**           | **17.15 ns** | **2.129 ns** | **1.662 ns** |  **1.01** |    **0.14** |         **-** |          **NA** |
| After  | 8           | 13.97 ns | 0.673 ns | 0.486 ns |  0.82 |    0.09 |         - |          NA |
