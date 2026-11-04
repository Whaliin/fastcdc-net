using BenchmarkDotNet.Attributes;

namespace FastCdc.Net.Benchmark;

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
        foreach (var _ in FastCdc.GetChunks(_data, 8192, 16384, 32768)) { }
    }

    [Benchmark]
    public void Benchmark32K()
    {
        foreach (var _ in FastCdc.GetChunks(_data, 16384, 32768, 65536)) { }
    }

    [Benchmark]
    public void Benchmark64K()
    {
        foreach (var _ in FastCdc.GetChunks(_data, 32768, 65536, 131_072)) { }
    }

    [Benchmark]
    public List<Chunk> Benchmark16K_ToList()
    {
        return FastCdc.GetChunks(_data, 8192, 16384, 32768).ToList();
    }

    [Benchmark]
    public Chunk[] Benchmark16K_ToArray()
    {
        return FastCdc.GetChunks(_data, 8192, 16384, 32768).ToArray();
    }

    [Benchmark]
    public async Task Benchmark16K_Async()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.GetChunksAsync(stream, 8192, 16384, 32768)) { }
    }

    [Benchmark]
    public void Benchmark16K_SyncStream()
    {
        using var stream = new MemoryStream(_data, writable: false);
        foreach (var _ in FastCdc.GetChunks(stream, 8192, 16384, 32768)) { }
    }

    [Benchmark]
    public async Task Benchmark32K_Async()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.GetChunksAsync(stream, 16384, 32768, 65536)) { }
    }

    [Benchmark]
    public async Task Benchmark64K_Async()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.GetChunksAsync(stream, 32768, 65536, 131_072)) { }
    }

    [Benchmark]
    public async Task Benchmark16K_Async_CustomBuffer()
    {
        using var stream = new MemoryStream(_data, writable: false);
        await foreach (var _ in FastCdc.GetChunksAsync(stream, 8192, 16384, 32768, bufferSize: 65536)) { }
    }
}
