namespace FastCdc.Net.Tests;

public class FastCdcValidationTests
{
    [Fact]
    public void FastCdc_EmptySource_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => FastCdc.GetChunks(ReadOnlySpan<byte>.Empty, 64, 256, 1024));
    }

    [Fact]
    public void FastCdc_MinimumTooLow()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.GetChunks(array, 63, 256, 1024));
    }

    [Fact]
    public void FastCdc_MinimumTooHigh()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.GetChunks(array, 67_108_867, 256, 1024));
    }

    [Fact]
    public void FastCdc_AverageTooLow()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.GetChunks(array, 64, 255, 1024));
    }

    [Fact]
    public void FastCdc_AverageTooHigh()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.GetChunks(array, 64, 268_435_457, 1024));
    }

    [Fact]
    public void FastCdc_MaximumTooLow()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.GetChunks(array, 64, 256, 1023));
    }

    [Fact]
    public void FastCdc_MaximumTooHigh()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.GetChunks(array, 64, 256, 1_073_741_825));
    }

    [Fact]
    public void FastCdc_MinGreaterThanAvg_ThrowsArgumentException()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentException>(() => FastCdc.GetChunks(array, 512, 256, 1024));
    }

    [Fact]
    public void FastCdc_AvgGreaterThanMax_ThrowsArgumentException()
    {
        var array = new byte[2048];
        Assert.Throws<ArgumentException>(() => FastCdc.GetChunks(array, 64, 2048, 1024));
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_NullStream_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in FastCdc.GetChunksAsync(null!, 64, 256, 1024)) { }
        });
    }

    [Fact]
    public async Task FastCdc_GetChunksAsync_UnreadableStream_ThrowsArgumentException()
    {
        using var unreadable = new UnreadableStream();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in FastCdc.GetChunksAsync(unreadable, 64, 256, 1024)) { }
        });
        Assert.Contains("Stream must be readable", ex.Message);
    }

    [Theory]
    [InlineData(63u, 256u, 1024u, typeof(ArgumentOutOfRangeException))]
    [InlineData(67_108_867u, 256u, 1024u, typeof(ArgumentOutOfRangeException))]
    [InlineData(64u, 255u, 1024u, typeof(ArgumentOutOfRangeException))]
    [InlineData(64u, 268_435_457u, 1024u, typeof(ArgumentOutOfRangeException))]
    [InlineData(64u, 256u, 1023u, typeof(ArgumentOutOfRangeException))]
    [InlineData(64u, 256u, 1_073_741_825u, typeof(ArgumentOutOfRangeException))]
    [InlineData(512u, 256u, 1024u, typeof(ArgumentException))]
    [InlineData(64u, 2048u, 1024u, typeof(ArgumentException))]
    public async Task FastCdc_GetChunksAsync_InvalidArguments_Throws(uint min, uint avg, uint max, Type exceptionType)
    {
        using var stream = new MemoryStream(new byte[100]);
        await Assert.ThrowsAsync(exceptionType, async () =>
        {
            await foreach (var _ in FastCdc.GetChunksAsync(stream, min, avg, max)) { }
        });
    }
}

