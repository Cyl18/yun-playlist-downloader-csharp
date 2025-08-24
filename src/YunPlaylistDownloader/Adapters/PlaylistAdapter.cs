using YunPlaylistDownloader.Models;
using YunPlaylistDownloader.Services;

namespace YunPlaylistDownloader.Adapters;

public class PlaylistAdapter : BaseAdapter
{
    private readonly NetEaseApiClient _apiClient;

    public PlaylistAdapter(NetEaseApiClient apiClient) : base("")
    {
        _apiClient = apiClient;
    }

    public PlaylistAdapter(NetEaseApiClient apiClient, string url) : base(url)
    {
        _apiClient = apiClient;
    }

    public override async Task<string?> GetTitleAsync()
    {
        var playlist = await _apiClient.GetPlaylistDetailAsync(Id);
        return playlist?.Name;
    }

    public override async Task<string?> GetCoverUrlAsync()
    {
        var playlist = await _apiClient.GetPlaylistDetailAsync(Id);
        return playlist?.CoverUrl;
    }

    public override async Task<List<Song>> GetSongsAsync(int quality)
    {
        var playlist = await _apiClient.GetPlaylistDetailAsync(Id);
        if (playlist == null)
            return new List<Song>();

        // 现在 playlist.Tracks 已经通过 TrackIds 获取了完整的歌曲列表
        if (!playlist.Tracks.Any())
        {
            return new List<Song>();
        }

        // Get song URLs for all tracks
        var trackIds = playlist.Tracks.Select(t => t.Id).ToList();
        var songUrls = await _apiClient.GetSongUrlsAsync(trackIds, quality);

        // Create a lookup for song URLs, handle potential duplicates
        var urlLookup = songUrls
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());

        // Convert tracks to songs
        return ConvertToSongs(playlist.Tracks, track =>
        {
            urlLookup.TryGetValue(track.Id, out var urlInfo);
            
            return new Song
            {
                Singer = track.Ar.FirstOrDefault()?.Name ?? "",
                SongName = track.Name,
                AlbumName = track.Al.Name,
                Url = urlInfo?.Url,
                Extension = GetFileExtension(urlInfo?.Url),
                IsFreeTrial = urlInfo?.FreeTrialInfo != null,
                MD5 = urlInfo?.Md5,
                RawData = track
            };
        });
    }

    public PlaylistAdapter WithUrl(string url)
    {
        return new PlaylistAdapter(_apiClient, url);
    }
}
