using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastCdc.Tests;

public class FastCdcParityVectorTests
{
	private sealed class VectorFixture
	{
		[JsonPropertyName("fixture")]
		public string Fixture { get; set; } = "";

		[JsonPropertyName("variant")]
		public string Variant { get; set; } = "";

		[JsonPropertyName("min_size")]
		public int MinSize { get; set; }

		[JsonPropertyName("avg_size")]
		public int AvgSize { get; set; }

		[JsonPropertyName("max_size")]
		public int MaxSize { get; set; }

		[JsonPropertyName("normalization_level")]
		public int NormalizationLevel { get; set; } = 1;

		[JsonPropertyName("total_bytes")]
		public int TotalBytes { get; set; }

		[JsonPropertyName("chunk_count")]
		public int ChunkCount { get; set; }

		[JsonPropertyName("chunks")]
		public List<VectorChunk> Chunks { get; set; } = [];
	}

	private sealed class VectorChunk
	{
		[JsonPropertyName("offset")]
		public long Offset { get; set; }

		[JsonPropertyName("length")]
		public int Length { get; set; }

		[JsonPropertyName("hash")]
		public string Hash { get; set; } = "";
	}

	public static IEnumerable<object[]> GetSekienVectorFiles()
	{
		string vectorsDir = Path.Combine("fixtures", "vectors");
		string[] files = Directory.GetFiles(vectorsDir, "SekienAkashita_*.json");
		foreach (string file in files)
		{
			yield return [Path.GetFileName(file)];
		}
	}

	[Theory]
	[MemberData(nameof(GetSekienVectorFiles))]
	public void FastCdc_SekienAkashita_MatchesRustVector(string vectorFileName)
	{
		string jsonPath = Path.Combine("fixtures", "vectors", vectorFileName);
		string json = File.ReadAllText(jsonPath);
		VectorFixture? fixture = JsonSerializer.Deserialize<VectorFixture>(json);
		Assert.NotNull(fixture);

		byte[] sourceData = File.ReadAllBytes(Path.Combine("fixtures", fixture.Fixture));
		Assert.Equal(fixture.TotalBytes, sourceData.Length);

		FastCdcFormat format = fixture.Variant switch
		{
			"v2020" => FastCdcFormat.V2020,
			"v2016" => FastCdcFormat.V2016,
			"ronomon" => FastCdcFormat.Ronomon,
			_ => throw new InvalidOperationException($"Unknown variant: {fixture.Variant}")
		};

		NormalizationLevel level = (NormalizationLevel)fixture.NormalizationLevel;

		FastCdcOptions options = new(
			minSize: fixture.MinSize,
			avgSize: fixture.AvgSize,
			maxSize: fixture.MaxSize,
			format: format,
			normalization: level);

		List<Chunk> actualChunks = FastCdc.Chunk(sourceData, options).ToList();

		Assert.Equal(fixture.ChunkCount, actualChunks.Count);
		Assert.Equal(fixture.Chunks.Count, actualChunks.Count);

		for (int i = 0; i < fixture.Chunks.Count; i++)
		{
			VectorChunk expected = fixture.Chunks[i];
			Chunk actual = actualChunks[i];
			ulong expectedHash = ulong.Parse(expected.Hash);

			Assert.Equal(expected.Offset, actual.Offset);
			Assert.Equal(expected.Length, actual.Length);
			Assert.Equal(expectedHash, actual.Hash);
		}
	}
}

