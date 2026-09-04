```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=10  IterationTime=250ms  
LaunchCount=1  WarmupCount=3  

```
| Method | Alpha | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |------ |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| **Before** | **-1**    | **3.349 ns** | **0.1190 ns** | **0.0623 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| After  | -1    | 2.683 ns | 0.1857 ns | 0.1228 ns |  0.80 |    0.04 |         - |          NA |
|        |       |          |           |           |       |         |           |             |
| **Before** | **128**   | **3.215 ns** | **0.3130 ns** | **0.2070 ns** |  **1.00** |    **0.08** |         **-** |          **NA** |
| After  | 128   | 2.221 ns | 0.1293 ns | 0.0855 ns |  0.69 |    0.05 |         - |          NA |
|        |       |          |           |           |       |         |           |             |
| **Before** | **255**   | **1.165 ns** | **0.1311 ns** | **0.0867 ns** |  **1.00** |    **0.10** |         **-** |          **NA** |
| After  | 255   | 1.116 ns | 0.0589 ns | 0.0389 ns |  0.96 |    0.07 |         - |          NA |
