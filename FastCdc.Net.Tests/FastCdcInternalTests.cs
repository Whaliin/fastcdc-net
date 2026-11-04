namespace FastCdc.Net.Tests;

public class FastCdcInternalTests
{
    [Fact]
    public void FastCdc_Logarithm2()
    {
        Assert.Equal(16u, FastCdc.Logarithm2(65537));
        Assert.Equal(16u, FastCdc.Logarithm2(65536));
        Assert.Equal(16u, FastCdc.Logarithm2(65535));

        Assert.Equal(15u, FastCdc.Logarithm2(32769));
        Assert.Equal(15u, FastCdc.Logarithm2(32768));
        Assert.Equal(15u, FastCdc.Logarithm2(32767));

        Assert.True(FastCdc.Logarithm2(FastCdc.AverageMin) >= 8u);
        Assert.True(FastCdc.Logarithm2(FastCdc.AverageMax) <= 28u);
    }

    [Fact]
    public void FastCdc_Logarithm2_Thresholds()
    {
        Assert.Equal(8u, FastCdc.Logarithm2(256));
        Assert.Equal(8u, FastCdc.Logarithm2(362));
        Assert.Equal(9u, FastCdc.Logarithm2(363));
        Assert.Equal(9u, FastCdc.Logarithm2(512));
    }

    [Fact]
    public void FastCdc_CeilDiv()
    {
        Assert.Equal(2u, FastCdc.CeilDiv(10, 5));
        Assert.Equal(3u, FastCdc.CeilDiv(11, 5));
        Assert.Equal(4u, FastCdc.CeilDiv(10, 3));
        Assert.Equal(3u, FastCdc.CeilDiv(9, 3));
        Assert.Equal(3u, FastCdc.CeilDiv(6, 2));
        Assert.Equal(3u, FastCdc.CeilDiv(5, 2));
    }

    [Fact]
    public void FastCdc_CenterSize()
    {
        Assert.Equal(0u, FastCdc.CenterSize(50, 100, 50));
        Assert.Equal(50u, FastCdc.CenterSize(200, 100, 50));
        Assert.Equal(40u, FastCdc.CenterSize(200, 100, 40));
    }

    [Fact]
    public void FastCdc_MaskLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.Mask(0));
    }

    [Fact]
    public void FastCdc_MaskHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.Mask(32));
    }

    [Fact]
    public void FastCdc_Mask()
    {
        Assert.Equal(16_777_215u, FastCdc.Mask(24));
        Assert.Equal(65535u, FastCdc.Mask(16));
        Assert.Equal(1023u, FastCdc.Mask(10));
        Assert.Equal(255u, FastCdc.Mask(8));
    }

    [Fact]
    public void FastCdc_Mask_Boundaries()
    {
        Assert.Equal(1u, FastCdc.Mask(1));
        Assert.Equal(0x7FFF_FFFFu, FastCdc.Mask(31));
    }

    [Fact]
    public void FastCdc_Cut_SourceSizeLessThanOrEqualToMinSize_EofTrue()
    {
        var data = new byte[64];
        var (hash, size) = FastCdc.Cut(data, 64, 256, 1024, 0x1ff, 0x7f, 50, eof: true);
        Assert.Equal(0u, hash);
        Assert.Equal(50u, size);
    }

    [Fact]
    public void FastCdc_Cut_SourceSizeLessThanOrEqualToMinSize_EofFalse()
    {
        var data = new byte[64];
        var (hash, size) = FastCdc.Cut(data, 64, 256, 1024, 0x1ff, 0x7f, 50, eof: false);
        Assert.Equal(0u, hash);
        Assert.Equal(0u, size);
    }

    [Fact]
    public void FastCdc_Cut_NoMatch_EofFalse_ReturnsZeroSize()
    {
        var data = new byte[512];
        var (hash, size) = FastCdc.Cut(data, 64, 256, 1024, 0xFFFF_FFFF, 0xFFFF_FFFF, 512, eof: false);
        Assert.NotEqual(0u, hash);
        Assert.Equal(0u, size);
    }

    [Fact]
    public void FastCdc_Cut_NoMatch_EofTrue_ReturnsRemainingSize()
    {
        var data = new byte[512];
        var (hash, size) = FastCdc.Cut(data, 64, 256, 1024, 0xFFFF_FFFF, 0xFFFF_FFFF, 512, eof: true);
        Assert.NotEqual(0u, hash);
        Assert.Equal(512u, size);
    }
}

