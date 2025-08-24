using YunPlaylistDownloader.Models;
using YunPlaylistDownloader.Services;

namespace YunPlaylistDownloader.Adapters;

public class AlbumAdapter : BaseAdapter
{
    private readonly NetEaseApiClient _apiClient;

    public AlbumAdapter(NetEaseApiClient apiClient) : base("")
    {
        _apiClient = apiClient;
    }

    public AlbumAdapter(NetEaseApiClient apiClient, string url) : base(url)
    {
        _apiClient = apiClient;
    }

    public override async Task<string?> GetTitleAsync()
    {
        var album = await _apiClient.GetAlbumDetailAsync(Id);
        return album?.Name;
    }

    public override async Task<string?> GetCoverUrlAsync()
    {
        var album = await _apiClient.GetAlbumDetailAsync(Id);
        return album?.PicUrl;
    }

    public override async Task<List<Song>> GetSongsAsync(int quality)
    {
        var album = await _apiClient.GetAlbumDetailAsync(Id);
        if (album == null)
            return new List<Song>();

        // Get song URLs
        var trackIds = album.Songs.Select(t => t.Id).ToList();
        var songUrls = await _apiClient.GetSongUrlsAsync(trackIds, quality);

        // Create a lookup for song URLs
        var urlLookup = songUrls.ToDictionary(s => s.Id, s => s);

        // Convert tracks to songs
        return ConvertToSongs(album.Songs, track =>
        {
            urlLookup.TryGetValue(track.Id, out var urlInfo);
            
            return new Song
            {
                Singer = track.Ar.FirstOrDefault()?.Name ?? album.Artist.Name,
                SongName = track.Name,
                AlbumName = album.Name,
                Url = urlInfo?.Url,
                Extension = GetFileExtension(urlInfo?.Url),
                IsFreeTrial = urlInfo?.FreeTrialInfo != null,
                RawData = track
            };
        });
    }

    public AlbumAdapter WithUrl(string url)
    {
        return new AlbumAdapter(_apiClient, url);
    }
}
