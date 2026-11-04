using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using FastCdc.Net.Benchmark;

var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var artifactsPath = Path.Combine("BenchmarkDotNet.Artifacts", timestamp);

var config = DefaultConfig.Instance
    .WithArtifactsPath(artifactsPath)
    .WithOptions(ConfigOptions.DontOverwriteResults);

BenchmarkRunner.Run<FastCdcBenchmark>(config, args);
