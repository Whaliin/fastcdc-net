using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;

namespace FastCdc.Benchmark;

[MemoryDiagnoser]
public class FastCdcBenchmark
{
	private byte[] _data = [];
	private MemoryStream _memoryStream = null!;
	private Pipe _pipe = null!;

	private static readonly FastCdcOptions Options16KV2020 = new(8192, 16384, 32768, FastCdcFormat.V2020);
	private static readonly FastCdcOptions Options16KV2016 = new(8192, 16384, 32768, FastCdcFormat.V2016);
	private static readonly FastCdcOptions Options16KRonomon = new(8192, 16384, 32768, FastCdcFormat.Ronomon);

	[Params("tsunami.tiff", "shakespeare.txt")]
	public string FixtureName { get; set; } = "tsunami.tiff";

	[GlobalSetup]
	public void Setup()
	{
		_data = File.ReadAllBytes(Path.Combine("fixtures", FixtureName));
		_memoryStream = new MemoryStream(_data, writable: false);
	}

	[IterationSetup(Target = nameof(Benchmark16K_PipeReader))]
	public void SetupPipe()
	{
		_pipe = new Pipe();
		_data.AsSpan().CopyTo(_pipe.Writer.GetSpan(_data.Length));
		_pipe.Writer.Advance(_data.Length);
		_pipe.Writer.Complete();
	}

	[Benchmark(Baseline = true)]
	public void Benchmark16K_V2020()
	{
		foreach (Chunk _ in FastCdc.Chunk(_data, Options16KV2020)) { }
	}

	[Benchmark]
	public void Benchmark16K_V2016()
	{
		foreach (Chunk _ in FastCdc.Chunk(_data, Options16KV2016)) { }
	}

	[Benchmark]
	public void Benchmark16K_Ronomon()
	{
		foreach (Chunk _ in FastCdc.Chunk(_data, Options16KRonomon)) { }
	}

	[Benchmark]
	public void Benchmark32K()
	{
		foreach (Chunk _ in FastCdc.Chunk(_data, FastCdcOptions.Default32KB)) { }
	}

	[Benchmark]
	public void Benchmark64K()
	{
		foreach (Chunk _ in FastCdc.Chunk(_data, FastCdcOptions.Default64KB)) { }
	}

	[Benchmark]
	public List<Chunk> Benchmark16K_ToList()
	{
		return FastCdc.Chunk(_data, FastCdcOptions.Default16KB).ToList();
	}

	[Benchmark]
	public Chunk[] Benchmark16K_ToArray()
	{
		return FastCdc.Chunk(_data, FastCdcOptions.Default16KB).ToArray();
	}

	[Benchmark]
	public void Benchmark16K_SyncStream()
	{
		_memoryStream.Position = 0;
		foreach (Chunk _ in FastCdc.Chunk(_memoryStream, FastCdcOptions.Default16KB)) { }
	}

	[Benchmark]
	public async Task Benchmark16K_AsyncStream()
	{
		_memoryStream.Position = 0;
		await foreach (Chunk _ in FastCdc.ChunkAsync(_memoryStream, FastCdcOptions.Default16KB)) { }
	}

	[Benchmark]
	public async Task Benchmark32K_AsyncStream()
	{
		_memoryStream.Position = 0;
		await foreach (Chunk _ in FastCdc.ChunkAsync(_memoryStream, FastCdcOptions.Default32KB)) { }
	}

	[Benchmark]
	public async Task Benchmark64K_AsyncStream()
	{
		_memoryStream.Position = 0;
		await foreach (Chunk _ in FastCdc.ChunkAsync(_memoryStream, FastCdcOptions.Default64KB)) { }
	}

	[Benchmark]
	public async Task Benchmark16K_PipeReader()
	{
		await foreach (Chunk _ in FastCdc.ChunkAsync(_pipe.Reader, FastCdcOptions.Default16KB)) { }
	}
}
