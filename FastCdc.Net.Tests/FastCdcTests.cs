namespace FastCdc.Net.Tests;

public class FastCdcTests
{
    [Fact]
    public void FastCdc_TestAllZeros()
    {
        var array = new byte[10240];
        var results = FastCdc.GetChunks(array, 64, 256, 1024).ToList();

        Assert.Equal(10, results.Count);
        foreach (var entry in results)
        {
            Assert.Equal(3106636015u, entry.Hash);
            Assert.Equal(0u, entry.Offset % 1024);
            Assert.Equal(1024u, entry.Length);
        }
    }

    [Fact]
    public void FastCdc_TestSekien16kChunks()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var results = FastCdc.GetChunks(array, 8192, 16384, 32768).ToList();

        Assert.Equal(6, results.Count);

        Assert.Equal(0u, results[0].Offset);
        Assert.Equal(22366u, results[0].Length);
        Assert.Equal(1527472128u, results[0].Hash);

        Assert.Equal(22366u, results[1].Offset);
        Assert.Equal(8282u, results[1].Length);
        Assert.Equal(1174757376u, results[1].Hash);

        Assert.Equal(30648u, results[2].Offset);
        Assert.Equal(16303u, results[2].Length);
        Assert.Equal(2687197184u, results[2].Hash);

        Assert.Equal(46951u, results[3].Offset);
        Assert.Equal(18696u, results[3].Length);
        Assert.Equal(1210105856u, results[3].Hash);

        Assert.Equal(65647u, results[4].Offset);
        Assert.Equal(32768u, results[4].Length);
        Assert.Equal(2984739645u, results[4].Hash);

        Assert.Equal(98415u, results[5].Offset);
        Assert.Equal(11051u, results[5].Length);
        Assert.Equal(1121740051u, results[5].Hash);
    }

    [Fact]
    public void FastCdc_TestSekien16kChunks_Streaming()
    {
        var filePath = "./fixtures/SekienAkashita.jpg";
        var array = File.ReadAllBytes(filePath);

        var chunkOffsets = new uint[] { 0, 22366, 30648, 46951, 65647, 98415 };
        var chunkSizes = new uint[] { 22366, 8282, 16303, 18696, 32768, 11051 };

        const uint bufSize = 32768;
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;

        var filePos = 0;
        var chunkIndex = 0;

        var groupSizes = new[] { 2, 1, 1, 1, 1 };

        foreach (var groupSize in groupSizes)
        {
            var upperBound = filePos + bufSize;
            var (eof, slice) = upperBound >= fileSize
                ? (true, array[filePos..])
                : (false, array[filePos..(int)upperBound]);

            var results = FastCdc.GetChunks(slice, 8192, 16384, 32768, eof).ToList();

            Assert.Equal(groupSize, results.Count);
            for (var idx = 0; idx < groupSize; idx++)
            {
                Assert.Equal(chunkOffsets[chunkIndex], (uint)(results[idx].Offset + filePos));
                Assert.Equal(chunkSizes[chunkIndex], results[idx].Length);
                chunkIndex++;
            }

            foreach (var result in results)
                filePos += (int)result.Length;
        }

        Assert.Equal(fileSize, filePos);
    }

    [Fact]
    public void FastCdc_TestSekien32kChunks()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var results = FastCdc.GetChunks(array, 16384, 32768, 65536).ToList();

        Assert.Equal(3, results.Count);

        Assert.Equal(0u, results[0].Offset);
        Assert.Equal(32857u, results[0].Length);
        Assert.Equal(2772598784u, results[0].Hash);

        Assert.Equal(32857u, results[1].Offset);
        Assert.Equal(16408u, results[1].Length);
        Assert.Equal(1651589120u, results[1].Hash);

        Assert.Equal(49265u, results[2].Offset);
        Assert.Equal(60201u, results[2].Length);
        Assert.Equal(1121740051u, results[2].Hash);
    }

    [Fact]
    public void FastCdc_TestSekien64kChunks()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var results = FastCdc.GetChunks(array, 32768, 65536, 131_072).ToList();

        Assert.Equal(2, results.Count);

        Assert.Equal(0u, results[0].Offset);
        Assert.Equal(32857u, results[0].Length);
        Assert.Equal(2772598784u, results[0].Hash);

        Assert.Equal(32857u, results[1].Offset);
        Assert.Equal(76609u, results[1].Length);
        Assert.Equal(1121740051u, results[1].Hash);
    }
}
