using YunPlaylistDownloader.Models;
using YunPlaylistDownloader.Services;

namespace YunPlaylistDownloader.Adapters;

public class DjRadioAdapter : BaseAdapter
{
    private readonly NetEaseApiClient _apiClient;

    public DjRadioAdapter(NetEaseApiClient apiClient) : base("")
    {
        _apiClient = apiClient;
    }

    public DjRadioAdapter(NetEaseApiClient apiClient, string url) : base(url)
    {
        _apiClient = apiClient;
    }

    public override async Task<string?> GetTitleAsync()
    {
        var djRadio = await _apiClient.GetDjRadioDetailAsync(Id);
        return djRadio?.Name;
    }

    public override async Task<string?> GetCoverUrlAsync()
    {
        var djRadio = await _apiClient.GetDjRadioDetailAsync(Id);
        return djRadio?.PicUrl;
    }

    public override async Task<List<Song>> GetSongsAsync(int quality)
    {
        // For DJ Radio, we need to get programs first, then get their songs
        // This is a simplified implementation - in reality you'd need to implement
        // GetDjProgramsAsync in the API client
        var djRadio = await _apiClient.GetDjRadioDetailAsync(Id);
        if (djRadio == null)
            return new List<Song>();

        // TODO: Implement DJ program fetching
        // For now, return empty list
        return new List<Song>();
    }

    public DjRadioAdapter WithUrl(string url)
    {
        return new DjRadioAdapter(_apiClient, url);
    }
}
