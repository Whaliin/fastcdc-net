namespace FastCdc.Net.Tests;

public class ChunkEnumeratorTests
{
    [Fact]
    public void ChunkEnumerator_ToList_And_ToArray_MatchForeach()
    {
        var array = File.ReadAllBytes("./fixtures/SekienAkashita.jpg");
        var foreachChunks = new List<Chunk>();
        foreach (var chunk in FastCdc.Chunk(array, FastCdcOptions.Default16KB))
        {
            foreachChunks.Add(chunk);
        }

        var listChunks = FastCdc.Chunk(array, FastCdcOptions.Default16KB).ToList();
        var arrayChunks = FastCdc.Chunk(array, FastCdcOptions.Default16KB).ToArray();

        Assert.Equal(foreachChunks, listChunks);
        Assert.Equal(foreachChunks, arrayChunks);
    }
}
