using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;

namespace FastCdc.Benchmark;

[MemoryDiagnoser]
public class FastCdcBenchmark
{
    private byte[] _data = [];

    [GlobalSetup]
    public void Setup()
    {
        _data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
    }

    [Benchmark(Baseline = true)]
    public void Benchmark16K()
    {
        foreach (var _ in FastCdc.Chunk(_data, FastCdcOptions.Default16KB)) { }
    }

    [Benchmark]
    public void Benchmark32K()
    {
        foreach (var _ in FastCdc.Chunk(_data, FastCdcOptions.Default32KB)) { }
    }

    [Benchmark]
    public void Benchmark64K()
    {
        foreach (var _ in FastCdc.Chunk(_data, FastCdcOptions.Default64KB)) { }
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
        using var stream = new MemoryStream(_data, writable: false);
        foreach (var _ in FastCdc.Chunk(stream, FastCdcOptions.Default16KB)) { }
    }

    [Benchmark]
    public async Task Benchmark16K_AsyncStream()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.ChunkAsync(stream, FastCdcOptions.Default16KB)) { }
    }

    [Benchmark]
    public async Task Benchmark32K_AsyncStream()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.ChunkAsync(stream, FastCdcOptions.Default32KB)) { }
    }

    [Benchmark]
    public async Task Benchmark64K_AsyncStream()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.ChunkAsync(stream, FastCdcOptions.Default64KB)) { }
    }

    [Benchmark]
    public async Task Benchmark16K_PipeReader()
    {
        var pipe = new Pipe();
        _ = Task.Run(async () =>
        {
            await pipe.Writer.WriteAsync(_data);
            await pipe.Writer.CompleteAsync();
        });

        await foreach (var _ in FastCdc.ChunkAsync(pipe.Reader, FastCdcOptions.Default16KB)) { }
    }
}
