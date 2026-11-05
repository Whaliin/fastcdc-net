namespace FastCdc.Tests;

public class FastCdcTests
{
	[Theory]
	[InlineData(NormalizationLevel.Level0)]
	[InlineData(NormalizationLevel.Level1)]
	[InlineData(NormalizationLevel.Level2)]
	[InlineData(NormalizationLevel.Level3)]
	public void FastCdc_V2020_And_V2016_ProduceIdenticalBoundaries(NormalizationLevel level)
	{
		byte[] array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		FastCdcOptions options2020 = new(8192, 16384, 32768, FastCdcFormat.V2020, level);
		FastCdcOptions options2016 = new(8192, 16384, 32768, FastCdcFormat.V2016, level);

		List<Chunk> chunks2020 = FastCdc.Chunk(array, options2020).ToList();
		List<Chunk> chunks2016 = FastCdc.Chunk(array, options2016).ToList();

		Assert.Equal(chunks2016.Count, chunks2020.Count);
		for (int i = 0; i < chunks2020.Count; i++)
		{
			Assert.Equal(chunks2016[i].Offset, chunks2020[i].Offset);
			Assert.Equal(chunks2016[i].Length, chunks2020[i].Length);
		}
	}

	[Fact]
	public void FastCdc_Ronomon_TestAllZeros()
	{
		byte[] array = new byte[10240];
		FastCdcOptions options = new(64, 256, 1024, FastCdcFormat.Ronomon);
		List<Chunk> results = FastCdc.Chunk(array, options).ToList();

		Assert.Equal(10, results.Count);
		foreach (Chunk entry in results)
		{
			Assert.Equal(3106636015UL, entry.Hash);
			Assert.Equal(0, entry.Offset % 1024);
			Assert.Equal(1024, entry.Length);
		}
	}

	[Fact]
	public void FastCdc_Ronomon_TestSekien16kChunks_StreamingSpan()
	{
		string filePath = "./fixtures/SekienAkashita.jpg";
		byte[] array = File.ReadAllBytes(filePath);
		FastCdcOptions options = new(8192, 16384, 32768, FastCdcFormat.Ronomon);

		long[] chunkOffsets = [0, 22366, 30648, 46951, 65647, 98415];
		int[] chunkSizes = [22366, 8282, 16303, 18696, 32768, 11051];

		const int bufSize = 32768;
		FileInfo fileInfo = new(filePath);
		long fileSize = fileInfo.Length;

		int filePos = 0;
		int chunkIndex = 0;

		int[] groupSizes = [2, 1, 1, 1, 1];

		foreach (int groupSize in groupSizes)
		{
			int upperBound = filePos + bufSize;
			(bool eof, byte[] slice) = upperBound >= fileSize
				? (true, array[filePos..])
				: (false, array[filePos..upperBound]);

			List<Chunk> results = FastCdc.Chunk(slice, options, eof).ToList();

			Assert.Equal(groupSize, results.Count);
			for (int idx = 0; idx < groupSize; idx++)
			{
				Assert.Equal(chunkOffsets[chunkIndex], results[idx].Offset + filePos);
				Assert.Equal(chunkSizes[chunkIndex], results[idx].Length);
				chunkIndex++;
			}

			foreach (Chunk result in results)
				filePos += result.Length;
		}

		Assert.Equal(fileSize, filePos);
	}
}
