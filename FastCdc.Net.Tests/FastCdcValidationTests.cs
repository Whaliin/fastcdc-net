namespace FastCdc.Net.Tests;

public class FastCdcValidationTests
{
    [Fact]
    public void FastCdcOptions_MinimumTooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(63, 256, 1024));
    }

    [Fact]
    public void FastCdcOptions_MinimumTooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(67_108_867, 256, 1024));
    }

    [Fact]
    public void FastCdcOptions_AverageTooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(64, 255, 1024));
    }

    [Fact]
    public void FastCdcOptions_AverageTooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(64, 268_435_457, 1024));
    }

    [Fact]
    public void FastCdcOptions_MaximumTooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(64, 256, 1023));
    }

    [Fact]
    public void FastCdcOptions_MaximumTooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(64, 256, 1_073_741_825));
    }

    [Fact]
    public void FastCdcOptions_MinGreaterThanAvg_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new FastCdcOptions(512, 256, 1024));
    }

    [Fact]
    public void FastCdcOptions_AvgGreaterThanMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new FastCdcOptions(64, 2048, 1024));
    }

    [Fact]
    public async Task FastCdc_ChunkAsync_NullStream_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in FastCdc.ChunkAsync((Stream)null!)) { }
        });
    }

    [Fact]
    public async Task FastCdc_ChunkAsync_NullPipeReader_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in FastCdc.ChunkAsync((System.IO.Pipelines.PipeReader)null!)) { }
        });
    }

    [Fact]
    public async Task FastCdc_ChunkAsync_UnreadableStream_ThrowsArgumentException()
    {
        using var unreadable = new UnreadableStream();
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in FastCdc.ChunkAsync(unreadable)) { }
        });
        Assert.Contains("Stream must be readable", ex.Message);
    }
}
