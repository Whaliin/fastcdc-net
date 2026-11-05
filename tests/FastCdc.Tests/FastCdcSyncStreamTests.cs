namespace FastCdc.Tests;

public class FastCdcSyncStreamTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(512)]
	[InlineData(1024)]
	[InlineData(8192)]
	[InlineData(32768)]
	[InlineData(65536)]
	public void FastCdc_Chunk_SyncStream_DifferentBufferSizes_MatchExpected(int bufferSize)
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		List<Chunk> expected = FastCdc.Chunk(data, FastCdcOptions.Default16KB).ToList();

		using MemoryStream stream = new(data, writable: false);
		List<Chunk> actual = FastCdc.Chunk(stream, FastCdcOptions.Default16KB, bufferSize: bufferSize).ToList();

		Assert.Equal(expected, actual);
	}

	[Fact]
	public void FastCdc_Chunk_SyncStream_EmptyStream_YieldsNoChunks()
	{
		using MemoryStream stream = new([]);
		List<Chunk> chunks = FastCdc.Chunk(stream).ToList();
		Assert.Empty(chunks);
	}
}
