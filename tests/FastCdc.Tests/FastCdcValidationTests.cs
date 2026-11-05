namespace FastCdc.Tests;

public class FastCdcValidationTests
{
	[Theory]
	[InlineData(63, 256, 1024, FastCdcFormat.V2020)]
	[InlineData(1_048_578, 2_097_152, 4_194_304, FastCdcFormat.V2020)]
	[InlineData(67_108_867, 256, 1024, FastCdcFormat.Ronomon)]
	[InlineData(64, 254, 1024, FastCdcFormat.V2020)]
	[InlineData(64, 4_194_306, 8_388_608, FastCdcFormat.V2020)]
	[InlineData(64, 268_435_457, 1024, FastCdcFormat.Ronomon)]
	[InlineData(64, 256, 1022, FastCdcFormat.V2020)]
	[InlineData(64, 256, 16_777_218, FastCdcFormat.V2020)]
	[InlineData(64, 256, 1_073_741_825, FastCdcFormat.Ronomon)]
	public void FastCdcOptions_SizeOutOfRange_ThrowsArgumentOutOfRangeException(
		int minSize,
		int avgSize,
		int maxSize,
		FastCdcFormat format)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(minSize, avgSize, maxSize, format));
	}

	[Theory]
	[InlineData(512, 256, 1024, FastCdcFormat.V2020)]
	[InlineData(64, 2048, 1024, FastCdcFormat.V2020)]
	[InlineData(65, 256, 1024, FastCdcFormat.V2020)]
	[InlineData(64, 257, 1024, FastCdcFormat.V2020)]
	[InlineData(64, 256, 1025, FastCdcFormat.V2020)]
	public void FastCdcOptions_InvalidSizeOrderOrParity_ThrowsArgumentException(
		int minSize,
		int avgSize,
		int maxSize,
		FastCdcFormat format)
	{
		Assert.Throws<ArgumentException>(() => new FastCdcOptions(minSize, avgSize, maxSize, format));
	}

	[Fact]
	public void FastCdcOptions_InvalidEnumValues_ThrowArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(64, 256, 1024, (FastCdcFormat)99));
		Assert.Throws<ArgumentOutOfRangeException>(() => new FastCdcOptions(64, 256, 1024, FastCdcFormat.V2020, (NormalizationLevel)99));
	}

	[Fact]
	public async Task FastCdc_NullOrUnreadableInputs_ThrowExpectedExceptions()
	{
		Assert.Throws<ArgumentNullException>(() => FastCdc.Chunk((Stream)null!).ToList());
		Assert.Throws<ArgumentNullException>(() => FastCdc.ChunkData((Stream)null!).ToList());

		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
		{
			await foreach (Chunk _ in FastCdc.ChunkAsync((Stream)null!)) { }
		});
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
		{
			await foreach (ChunkData _ in FastCdc.ChunkDataAsync((Stream)null!)) { }
		});
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
		{
			await foreach (Chunk _ in FastCdc.ChunkAsync((System.IO.Pipelines.PipeReader)null!)) { }
		});
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
		{
			await foreach (ChunkData _ in FastCdc.ChunkDataAsync((System.IO.Pipelines.PipeReader)null!)) { }
		});

		using UnreadableStream unreadable = new();
		Assert.Throws<ArgumentException>(() => FastCdc.Chunk(unreadable).ToList());
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkData(unreadable).ToList());

		ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
		{
			await foreach (Chunk _ in FastCdc.ChunkAsync(unreadable)) { }
		});
		Assert.Contains("Stream must be readable", ex.Message);

		ArgumentException exData = await Assert.ThrowsAsync<ArgumentException>(async () =>
		{
			await foreach (ChunkData _ in FastCdc.ChunkDataAsync(unreadable)) { }
		});
		Assert.Contains("Stream must be readable", exData.Message);
	}

	[Fact]
	public async Task FastCdc_UninitializedOptionsOrNegativeBufferSize_ThrowExpectedExceptions()
	{
		FastCdcOptions uninitialized = default;
		using MemoryStream stream = new([1, 2, 3]);
		System.IO.Pipelines.Pipe pipe = new();

		Assert.Throws<ArgumentException>(() => FastCdc.Chunk([1, 2, 3], uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkData(new byte[] { 1, 2, 3 }, uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.Chunk(stream, uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkData(stream, uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkAsync(stream, uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkDataAsync(stream, uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkAsync(pipe.Reader, uninitialized));
		Assert.Throws<ArgumentException>(() => FastCdc.ChunkDataAsync(pipe.Reader, uninitialized));

		Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.Chunk(stream, bufferSize: -1));
		Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.ChunkData(stream, bufferSize: -1));
		Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.ChunkAsync(stream, bufferSize: -1));
		Assert.Throws<ArgumentOutOfRangeException>(() => FastCdc.ChunkDataAsync(stream, bufferSize: -1));

		await pipe.Writer.CompleteAsync();
	}
}
