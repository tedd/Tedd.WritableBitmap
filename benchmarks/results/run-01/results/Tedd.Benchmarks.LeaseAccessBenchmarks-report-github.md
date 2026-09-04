```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Net10  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=Net10  IterationCount=10  IterationTime=250ms  
LaunchCount=1  WarmupCount=3  

```
| Method | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| Before | 24.156 ns | 3.5081 ns | 2.3204 ns |  1.01 |    0.13 |         - |          NA |
| After  |  3.413 ns | 0.1605 ns | 0.1062 ns |  0.14 |    0.01 |         - |          NA |
