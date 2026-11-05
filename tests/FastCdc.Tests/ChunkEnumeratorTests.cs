namespace FastCdc.Tests;

public class ChunkEnumeratorTests
{
	[Fact]
	public void ChunkEnumerator_ToList_And_ToArray_MatchForeach()
	{
		byte[] array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		List<Chunk> foreachChunks = new();
		foreach (Chunk chunk in FastCdc.Chunk(array, FastCdcOptions.Default16KB))
		{
			foreachChunks.Add(chunk);
		}

		List<Chunk> listChunks = FastCdc.Chunk(array, FastCdcOptions.Default16KB).ToList();
		Chunk[] arrayChunks = FastCdc.Chunk(array, FastCdcOptions.Default16KB).ToArray();

		Assert.Equal(foreachChunks, listChunks);
		Assert.Equal(foreachChunks, arrayChunks);
	}

	[Fact]
	public void ChunkDataEnumerator_ToList_And_ToArray_MatchForeach()
	{
		byte[] array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		List<ChunkData> foreachChunks = new();
		foreach (ChunkData chunk in FastCdc.ChunkData(array, FastCdcOptions.Default16KB))
		{
			foreachChunks.Add(chunk);
		}

		List<ChunkData> listChunks = FastCdc.ChunkData(array, FastCdcOptions.Default16KB).ToList();
		ChunkData[] arrayChunks = FastCdc.ChunkData(array, FastCdcOptions.Default16KB).ToArray();

		Assert.Equal(foreachChunks.Count, listChunks.Count);
		Assert.Equal(foreachChunks.Count, arrayChunks.Length);
		for (int i = 0; i < foreachChunks.Count; i++)
		{
			Assert.Equal((Chunk)foreachChunks[i], (Chunk)listChunks[i]);
			Assert.Equal((Chunk)foreachChunks[i], (Chunk)arrayChunks[i]);
			Assert.True(foreachChunks[i].Span.SequenceEqual(listChunks[i].Span));
			Assert.True(foreachChunks[i].Span.SequenceEqual(arrayChunks[i].Span));
		}
	}
}
