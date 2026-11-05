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
		Assert.True(FastCdcOptions.RoundLog2(FastCdcOptions.AverageMax) <= 22u);
		Assert.True(FastCdcOptions.RoundLog2(FastCdcOptions.RonomonAverageMax) <= 28u);
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

	[Theory]
	[InlineData(FastCdcFormat.V2020)]
	[InlineData(FastCdcFormat.V2016)]
	[InlineData(FastCdcFormat.Ronomon)]
	public void FastCdc_Cut_SourceSizeLessThanOrEqualToMinSize_EofTrue(FastCdcFormat format)
	{
		byte[] data = new byte[64];
		FastCdcOptions options = new(64, 256, 1024, format);
		(ulong hash, int size) = FastCdc.Cut(data, in options, 50, eof: true);
		Assert.Equal(0UL, hash);
		Assert.Equal(50, size);
	}

	[Theory]
	[InlineData(FastCdcFormat.V2020)]
	[InlineData(FastCdcFormat.V2016)]
	[InlineData(FastCdcFormat.Ronomon)]
	public void FastCdc_Cut_SourceSizeLessThanOrEqualToMinSize_EofFalse(FastCdcFormat format)
	{
		byte[] data = new byte[64];
		FastCdcOptions options = new(64, 256, 1024, format);
		(ulong hash, int size) = FastCdc.Cut(data, in options, 50, eof: false);
		Assert.Equal(0UL, hash);
		Assert.Equal(0, size);
	}

	[Theory]
	[InlineData(FastCdcFormat.V2020)]
	[InlineData(FastCdcFormat.V2016)]
	[InlineData(FastCdcFormat.Ronomon)]
	public void FastCdc_Cut_NoMatch_EofFalse_ReturnsZeroSize(FastCdcFormat format)
	{
		byte[] data = new byte[512];
		FastCdcOptions options = new(64, 256, 1024, format);
		(ulong hash, int size) = FastCdc.Cut(data, in options, 512, eof: false);
		Assert.NotEqual(0UL, hash);
		Assert.Equal(0, size);
	}

	[Theory]
	[InlineData(FastCdcFormat.V2020)]
	[InlineData(FastCdcFormat.V2016)]
	[InlineData(FastCdcFormat.Ronomon)]
	public void FastCdc_Cut_NoMatch_EofTrue_ReturnsRemainingSize(FastCdcFormat format)
	{
		byte[] data = new byte[512];
		FastCdcOptions options = new(64, 256, 1024, format);
		(ulong hash, int size) = FastCdc.Cut(data, in options, 512, eof: true);
		Assert.NotEqual(0UL, hash);
		Assert.Equal(512, size);
	}

	[Fact]
	public void FastCdc_Cut_V2020_OddTerminalLength_MatchesV2016Hash()
	{
		// When no cut point is found and the remaining EOF window has an odd length,
		// V2020 folds the final odd byte into the Gear hash identically to V2016.
		byte[] data = new byte[513];
		FastCdcOptions options2020 = new(64, 256, 1024, FastCdcFormat.V2020);
		FastCdcOptions options2016 = new(64, 256, 1024, FastCdcFormat.V2016);

		(ulong hash2020, int size2020) = FastCdc.Cut(data, in options2020, data.Length, eof: true);
		(ulong hash2016, int size2016) = FastCdc.Cut(data, in options2016, data.Length, eof: true);

		Assert.Equal(513, size2020);
		Assert.Equal(size2016, size2020);
		Assert.Equal(hash2016, hash2020);
	}
}
