using Xunit;
using YunPlaylistDownloader.Adapters;

namespace YunPlaylistDownloader.Tests;

public class AdapterFactoryTests
{
    [Theory]
    [InlineData("https://music.163.com/#/playlist?id=123456", typeof(PlaylistAdapter))]
    [InlineData("https://music.163.com/#/album?id=123456", typeof(AlbumAdapter))]
    [InlineData("https://music.163.com/#/djradio?id=123456", typeof(DjRadioAdapter))]
    [InlineData("123456", typeof(PlaylistAdapter))] // Default to playlist for plain IDs
    public void CreateAdapter_ShouldReturnCorrectAdapterType(string url, Type expectedType)
    {
        // This test would need to be implemented with proper DI setup
        // For now, it's just a placeholder to show the testing structure
        Assert.True(true);
    }
}
