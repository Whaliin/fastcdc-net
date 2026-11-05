namespace FastCdc.Tests;

public class FastCdcInternalTests
{
	[Fact]
	public void FastCdcOptions_Logarithm2()
	{
		Assert.Equal(16u, FastCdcOptions.RoundLog2(65537));
		Assert.Equal(16u, FastCdcOptions.RoundLog2(65536));
		Assert.Equal(16u, FastCdcOptions.RoundLog2(65535));

		Assert.Equal(15u, FastCdcOptions.RoundLog2(32769));
		Assert.Equal(15u, FastCdcOptions.RoundLog2(32768));
		Assert.Equal(15u, FastCdcOptions.RoundLog2(32767));

		Assert.True(FastCdcOptions.RoundLog2(FastCdcOptions.AverageMin) >= 8u);
		Assert.True(FastCdcOptions.RoundLog2(FastCdcOptions.AverageMax) <= 28u);
	}

	[Fact]
	public void FastCdcOptions_Logarithm2_Thresholds()
	{
		Assert.Equal(8u, FastCdcOptions.RoundLog2(256));
		Assert.Equal(8u, FastCdcOptions.RoundLog2(362));
		Assert.Equal(9u, FastCdcOptions.RoundLog2(363));
		Assert.Equal(9u, FastCdcOptions.RoundLog2(512));
	}

	[Fact]
	public void FastCdcOptions_CeilDiv()
	{
		Assert.Equal(2, FastCdcOptions.CeilDiv(10, 5));
		Assert.Equal(3, FastCdcOptions.CeilDiv(11, 5));
		Assert.Equal(4, FastCdcOptions.CeilDiv(10, 3));
		Assert.Equal(3, FastCdcOptions.CeilDiv(9, 3));
		Assert.Equal(3, FastCdcOptions.CeilDiv(6, 2));
		Assert.Equal(3, FastCdcOptions.CeilDiv(5, 2));
	}

	[Fact]
	public void FastCdcOptions_MaskLow()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => FastCdcOptions.Mask(0));
	}

	[Fact]
	public void FastCdcOptions_MaskHigh()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => FastCdcOptions.Mask(32));
	}

	[Fact]
	public void FastCdcOptions_Mask()
	{
		Assert.Equal(16_777_215u, FastCdcOptions.Mask(24));
		Assert.Equal(65535u, FastCdcOptions.Mask(16));
		Assert.Equal(1023u, FastCdcOptions.Mask(10));
		Assert.Equal(255u, FastCdcOptions.Mask(8));
	}

	[Fact]
	public void FastCdcOptions_Mask_Boundaries()
	{
		Assert.Equal(1u, FastCdcOptions.Mask(1));
		Assert.Equal(0x7FFF_FFFFu, FastCdcOptions.Mask(31));
	}

	[Fact]
	public void FastCdc_Cut_SourceSizeLessThanOrEqualToMinSize_EofTrue()
	{
		byte[] data = new byte[64];
		var options = new FastCdcOptions(64, 256, 1024);
		var (hash, size) = FastCdc.Cut(data, in options, 50, eof: true);
		Assert.Equal(0u, hash);
		Assert.Equal(50, size);
	}

	[Fact]
	public void FastCdc_Cut_SourceSizeLessThanOrEqualToMinSize_EofFalse()
	{
		byte[] data = new byte[64];
		var options = new FastCdcOptions(64, 256, 1024);
		var (hash, size) = FastCdc.Cut(data, in options, 50, eof: false);
		Assert.Equal(0u, hash);
		Assert.Equal(0, size);
	}

	[Fact]
	public void FastCdc_Cut_NoMatch_EofFalse_ReturnsZeroSize()
	{
		byte[] data = new byte[512];
		var options = new FastCdcOptions(64, 256, 1024);
		var (hash, size) = FastCdc.Cut(data, in options, 512, eof: false);
		Assert.NotEqual(0u, hash);
		Assert.Equal(0, size);
	}

	[Fact]
	public void FastCdc_Cut_NoMatch_EofTrue_ReturnsRemainingSize()
	{
		byte[] data = new byte[512];
		var options = new FastCdcOptions(64, 256, 1024);
		var (hash, size) = FastCdc.Cut(data, in options, 512, eof: true);
		Assert.NotEqual(0u, hash);
		Assert.Equal(512, size);
	}
}
