using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

// Separate child processes, three warmups and ten measured iterations per case.
// The same runtime, dependencies and workload are used for both archived versions.
var config = DefaultConfig.Instance.AddJob(Job.Default
    .WithId("Net10")
    .WithLaunchCount(1)
    .WithWarmupCount(3)
    .WithIterationCount(10)
    .WithIterationTime(TimeInterval.FromMilliseconds(250)));
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
