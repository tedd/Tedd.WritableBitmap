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
| **Before** | **256**   |   **103.47 μs** |   **5.911 μs** |   **4.615 μs** |  **1.00** |    **0.06** |     **136 B** |        **1.00** |
| After  | 256   |    73.01 μs |   3.224 μs |   2.331 μs |  0.71 |    0.04 |     136 B |        1.00 |
|        |       |             |            |            |       |         |           |             |
| **Before** | **1920**  | **6,186.14 μs** | **447.694 μs** | **349.530 μs** |  **1.00** |    **0.08** |     **136 B** |        **1.00** |
| After  | 1920  | 4,329.11 μs | 174.654 μs | 126.286 μs |  0.70 |    0.04 |     136 B |        1.00 |
