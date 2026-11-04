namespace FastCdc.Net.Tests;

public class FastCdcAsyncTests
{
    [Fact]
    public async Task FastCdc_TestSekien16kChunks_Async()
    {
        await using var stream = File.OpenRead("./fixtures/SekienAkashita.jpg");
        var results = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 8192, 16384, 32768))
            results.Add(chunk);

        Assert.Equal(6, results.Count);

        Assert.Equal(0u, results[0].Offset);
        Assert.Equal(22366u, results[0].Length);
        Assert.Equal(1527472128u, results[0].Hash);

        Assert.Equal(22366u, results[1].Offset);
        Assert.Equal(8282u, results[1].Length);
        Assert.Equal(1174757376u, results[1].Hash);

        Assert.Equal(30648u, results[2].Offset);
        Assert.Equal(16303u, results[2].Length);
        Assert.Equal(2687197184u, results[2].Hash);

        Assert.Equal(46951u, results[3].Offset);
        Assert.Equal(18696u, results[3].Length);
        Assert.Equal(1210105856u, results[3].Hash);

        Assert.Equal(65647u, results[4].Offset);
        Assert.Equal(32768u, results[4].Length);
        Assert.Equal(2984739645u, results[4].Hash);

        Assert.Equal(98415u, results[5].Offset);
        Assert.Equal(11051u, results[5].Length);
        Assert.Equal(1121740051u, results[5].Hash);
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_MatchesSpan_Sekien32k()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 16384, 32768, 65536).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 16384, 32768, 65536))
        {
            actual.Add(chunk);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_MatchesSpan_Sekien64k()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 32768, 65536, 131_072).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 32768, 65536, 131_072))
        {
            actual.Add(chunk);
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(32768)]
    [InlineData(65536)]
    public async Task FastCdc_GetChunksAsync_DifferentBufferSizes_MatchExpected(int bufferSize)
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 8192, 16384, 32768).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 8192, 16384, 32768, bufferSize: bufferSize))
        {
            actual.Add(chunk);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_ChunkedStreamReads_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 8192, 16384, 32768).ToList();

        using var stream = new ChunkedReadStream(data, maxChunkSize: 127);
        var actual = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 8192, 16384, 32768, bufferSize: 1024))
        {
            actual.Add(chunk);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_AllZeros_MatchesSpan()
    {
        var data = new byte[10240];
        var expected = FastCdc.GetChunks(data, 64, 256, 1024).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 64, 256, 1024))
        {
            actual.Add(chunk);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_EmptyStream_YieldsNoChunks()
    {
        using var stream = new MemoryStream([]);
        var chunks = new List<Chunk>();
        await foreach (var chunk in FastCdc.GetChunksAsync(stream, 64, 256, 1024))
        {
            chunks.Add(chunk);
        }
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_CancellationToken_CancelsExecution()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream(new byte[10240]);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in FastCdc.GetChunksAsync(stream, 64, 256, 1024, cancellationToken: cts.Token)) { }
        });
    }
}

