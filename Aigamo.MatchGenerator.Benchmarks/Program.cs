using Aigamo.MatchGenerator.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// Run in-process: BenchmarkDotNet's default out-of-process toolchain does not yet recognize the
// .NET 11 preview runtime moniker (throws "GetRuntimeVersion not implemented for NotRecognized").
// The in-process emit toolchain avoids that SDK validation while still producing valid measurements.
var config = DefaultConfig.Instance.AddJob(
	Job.Default.WithToolchain(InProcessEmitToolchain.Instance)
);

BenchmarkRunner.Run<MatchBenchmarks>(config);
