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
| **Before** | **2**           | **15.46 ns** | **0.391 ns** | **0.205 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| After  | 2           | 12.96 ns | 0.933 ns | 0.555 ns |  0.84 |    0.04 |         - |          NA |
|        |             |          |          |          |       |         |           |             |
| **Before** | **3**           | **15.52 ns** | **1.631 ns** | **1.079 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| After  | 3           | 14.04 ns | 2.706 ns | 1.790 ns |  0.91 |    0.13 |         - |          NA |
|        |             |          |          |          |       |         |           |             |
| **Before** | **8**           | **16.57 ns** | **1.944 ns** | **1.157 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| After  | 8           | 14.73 ns | 1.950 ns | 1.290 ns |  0.89 |    0.09 |         - |          NA |
