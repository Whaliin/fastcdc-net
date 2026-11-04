namespace FastCdc.Net.Tests;

public class FastCdcSyncStreamTests
{
    [Fact]
    public void FastCdc_GetChunks_SyncStream_Sekien16k_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 8192, 16384, 32768).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.GetChunks(stream, 8192, 16384, 32768).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_GetChunks_SyncStream_Sekien32k_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 16384, 32768, 65536).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.GetChunks(stream, 16384, 32768, 65536).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_GetChunks_SyncStream_Sekien64k_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 32768, 65536, 131_072).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.GetChunks(stream, 32768, 65536, 131_072).ToList();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(32768)]
    [InlineData(65536)]
    public void FastCdc_GetChunks_SyncStream_DifferentBufferSizes_MatchExpected(int bufferSize)
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 8192, 16384, 32768).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.GetChunks(stream, 8192, 16384, 32768, bufferSize: bufferSize).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_GetChunks_SyncStream_ChunkedStreamReads_MatchesSpan()
    {
        var data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var expected = FastCdc.GetChunks(data, 8192, 16384, 32768).ToList();

        using var stream = new ChunkedReadStream(data, maxChunkSize: 127);
        var actual = FastCdc.GetChunks(stream, 8192, 16384, 32768, bufferSize: 1024).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_GetChunks_SyncStream_AllZeros_MatchesSpan()
    {
        var data = new byte[10240];
        var expected = FastCdc.GetChunks(data, 64, 256, 1024).ToList();

        using var stream = new MemoryStream(data, writable: false);
        var actual = FastCdc.GetChunks(stream, 64, 256, 1024).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FastCdc_GetChunks_SyncStream_EmptyStream_YieldsNoChunks()
    {
        using var stream = new MemoryStream([]);
        var chunks = FastCdc.GetChunks(stream, 64, 256, 1024).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public void FastCdc_GetChunks_SyncStream_NullOrUnreadable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FastCdc.GetChunks((Stream)null!, 64, 256, 1024).ToList());

        using var unreadableStream = new UnreadableStream();
        Assert.Throws<ArgumentException>(() => FastCdc.GetChunks(unreadableStream, 64, 256, 1024).ToList());
    }
}
