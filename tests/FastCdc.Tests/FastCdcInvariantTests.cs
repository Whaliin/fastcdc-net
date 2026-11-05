using System.IO.Pipelines;
using System.Security.Cryptography;

namespace FastCdc.Tests;

public class FastCdcInvariantTests
{
	private static void AssertChunkInvariants(IReadOnlyList<Chunk> chunks, int expectedTotalBytes, FastCdcOptions options)
	{
		if (expectedTotalBytes == 0)
		{
			Assert.Empty(chunks);
			return;
		}

		Assert.NotEmpty(chunks);

		long expectedOffset = 0L;
		long totalLength = 0L;

		for (int i = 0; i < chunks.Count; i++)
		{
			Chunk chunk = chunks[i];

			Assert.Equal(expectedOffset, chunk.Offset);
			expectedOffset += chunk.Length;
			totalLength += chunk.Length;

			bool isLastChunk = i == chunks.Count - 1;
			if (!isLastChunk)
			{
				Assert.True(chunk.Length >= options.MinSize, $"Chunk at index {i} has length {chunk.Length} which is less than minSize {options.MinSize}.");
			}

			Assert.True(chunk.Length <= options.MaxSize, $"Chunk at index {i} has length {chunk.Length} which exceeds maxSize {options.MaxSize}.");
		}

		Assert.Equal(expectedTotalBytes, totalLength);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(63)]
	[InlineData(64)]
	[InlineData(65)]
	[InlineData(255)]
	[InlineData(256)]
	[InlineData(257)]
	[InlineData(1023)]
	[InlineData(1024)]
	[InlineData(1025)]
	[InlineData(5000)]
	public void FastCdc_BoundaryPayloadSizes_SatisfyInvariants(int size)
	{
		FastCdcOptions options = new(minSize: 64, avgSize: 256, maxSize: 1024);
		byte[] data = new byte[size];
		RandomNumberGenerator.Fill(data);

		List<Chunk> spanChunks = FastCdc.Chunk(data, options).ToList();
		AssertChunkInvariants(spanChunks, size, options);

		using MemoryStream stream = new(data, writable: false);
		List<Chunk> streamChunks = FastCdc.Chunk(stream, options).ToList();
		Assert.Equal(spanChunks, streamChunks);
	}

	[Theory]
	[InlineData(0x00)]
	[InlineData(0xFF)]
	[InlineData(0x5A)]
	public void FastCdc_HomogeneousPayloads_SatisfyInvariants(byte fillByte)
	{
		FastCdcOptions options = new(minSize: 64, avgSize: 256, maxSize: 1024);
		byte[] data = new byte[8192];
		Array.Fill(data, fillByte);

		List<Chunk> chunks = FastCdc.Chunk(data, options).ToList();
		AssertChunkInvariants(chunks, data.Length, options);
	}

	[Fact]
	public void FastCdc_SequentialPattern_SatisfiesInvariants()
	{
		FastCdcOptions options = new(minSize: 64, avgSize: 256, maxSize: 1024);
		byte[] data = new byte[16384];
		for (int i = 0; i < data.Length; i++)
		{
			data[i] = (byte)(i % 256);
		}

		List<Chunk> chunks = FastCdc.Chunk(data, options).ToList();
		AssertChunkInvariants(chunks, data.Length, options);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	public async Task FastCdc_MetamorphicEquivalence_MicroStreams_MatchSpan(int chunkSize)
	{
		FastCdcOptions options = new(minSize: 64, avgSize: 256, maxSize: 1024);
		byte[] data = new byte[2048];
		RandomNumberGenerator.Fill(data);

		List<Chunk> expected = FastCdc.Chunk(data, options).ToList();
		AssertChunkInvariants(expected, data.Length, options);

		// Synchronous ChunkedReadStream
		using (ChunkedReadStream syncStream = new(data, maxChunkSize: chunkSize))
		{
			List<Chunk> syncChunks = FastCdc.Chunk(syncStream, options).ToList();
			Assert.Equal(expected, syncChunks);
		}

		// Asynchronous ChunkedReadStream
		using (ChunkedReadStream asyncStream = new(data, maxChunkSize: chunkSize))
		{
			List<Chunk> asyncChunks = new();
			await foreach (Chunk chunk in FastCdc.ChunkAsync(asyncStream, options))
			{
				asyncChunks.Add(chunk);
			}
			Assert.Equal(expected, asyncChunks);
		}
	}

	[Theory]
	[InlineData(64)]
	[InlineData(128)]
	[InlineData(256)]
	public async Task FastCdc_MetamorphicEquivalence_FragmentedPipe_MatchesSpan(int chunkSize)
	{
		FastCdcOptions options = new(minSize: 64, avgSize: 256, maxSize: 1024);
		byte[] data = new byte[4096];
		RandomNumberGenerator.Fill(data);

		List<Chunk> expected = FastCdc.Chunk(data, options).ToList();
		AssertChunkInvariants(expected, data.Length, options);

		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1024 * 1024));
		Task producerTask = Task.Run(async () =>
		{
			int sent = 0;
			while (sent < data.Length)
			{
				int toWrite = Math.Min(chunkSize, data.Length - sent);
				await pipe.Writer.WriteAsync(data.AsMemory(sent, toWrite));
				sent += toWrite;
			}
			await pipe.Writer.CompleteAsync();
		});

		List<Chunk> pipeChunks = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(pipe.Reader, options))
		{
			pipeChunks.Add(chunk);
		}

		await producerTask;
		Assert.Equal(expected, pipeChunks);
	}

	[Theory]
	[InlineData(512)]
	[InlineData(1024)]
	[InlineData(4096)]
	[InlineData(8192)]
	public async Task FastCdc_MetamorphicEquivalence_AllStreamsAndPipeMatchSpan(int chunkSize)
	{
		byte[] data = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
		FastCdcOptions options = FastCdcOptions.Default16KB;

		List<Chunk> expected = FastCdc.Chunk(data, options).ToList();
		AssertChunkInvariants(expected, data.Length, options);

		// Synchronous ChunkedReadStream
		using (ChunkedReadStream syncStream = new(data, maxChunkSize: chunkSize))
		{
			List<Chunk> syncChunks = FastCdc.Chunk(syncStream, options).ToList();
			Assert.Equal(expected, syncChunks);
		}

		// Asynchronous ChunkedReadStream
		using (ChunkedReadStream asyncStream = new(data, maxChunkSize: chunkSize))
		{
			List<Chunk> asyncChunks = new();
			await foreach (Chunk chunk in FastCdc.ChunkAsync(asyncStream, options))
			{
				asyncChunks.Add(chunk);
			}
			Assert.Equal(expected, asyncChunks);
		}

		// PipeReader with fragmented flushes
		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1024 * 1024));
		Task producerTask = Task.Run(async () =>
		{
			int sent = 0;
			while (sent < data.Length)
			{
				int toWrite = Math.Min(chunkSize, data.Length - sent);
				await pipe.Writer.WriteAsync(data.AsMemory(sent, toWrite));
				sent += toWrite;
			}
			await pipe.Writer.CompleteAsync();
		});

		List<Chunk> pipeChunks = new();
		await foreach (Chunk chunk in FastCdc.ChunkAsync(pipe.Reader, options))
		{
			pipeChunks.Add(chunk);
		}

		await producerTask;
		Assert.Equal(expected, pipeChunks);
	}
}
