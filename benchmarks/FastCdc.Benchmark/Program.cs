using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using FastCdc.Benchmark;

string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
string artifactsPath = Path.Combine("BenchmarkDotNet.Artifacts", timestamp);

var config = DefaultConfig.Instance
	.WithArtifactsPath(artifactsPath)
	.WithOptions(ConfigOptions.DontOverwriteResults);

BenchmarkRunner.Run<FastCdcBenchmark>(config, args);
