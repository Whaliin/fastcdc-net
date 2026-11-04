namespace FastCdc.Net.Tests;

public class FastCdcSyncStreamTests
{
    [Fact]
    public void FastCdc_Chunk_SyncStream_Sekien16k_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.Chunk(stream, FastCdcOptions.Default16KB).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_Chunk_SyncStream_Sekien32k_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.Chunk(data, FastCdcOptions.Default32KB).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.Chunk(stream, FastCdcOptions.Default32KB).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_Chunk_SyncStream_Sekien64k_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.Chunk(data, FastCdcOptions.Default64KB).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.Chunk(stream, FastCdcOptions.Default64KB).ToList();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(32768)]
    [InlineData(65536)]
    public void FastCdc_Chunk_SyncStream_DifferentBufferSizes_MatchExpected(int bufferSize)
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.Chunk(stream, FastCdcOptions.Default16KB, bufferSize: bufferSize).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_Chunk_SyncStream_ChunkedStreamReads_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

        using var stream = new ChunkedReadStream(data, maxChunkSize: 127);
        var actual = FastCdc.Chunk(stream, FastCdcOptions.Default16KB, bufferSize: 1024).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_Chunk_SyncStream_AllZeros_MatchesSpan()
    {
        var data = new byte[10240];
        var options = new FastCdcOptions(64, 256, 1024);
        var expected = FastCdc.Chunk(data, options).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.Chunk(stream, options).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_Chunk_SyncStream_EmptyStream_YieldsNoChunks()
    {
        using var stream = new MemoryStream([]);
        var chunks = FastCdc.Chunk(stream).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public void FastCdc_Chunk_SyncStream_NullOrUnreadable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FastCdc.Chunk((Stream)null!).ToList());

        using var unreadableStream = new UnreadableStream();
        Assert.Throws<ArgumentException>(() => FastCdc.Chunk(unreadableStream).ToList());
    }
}
