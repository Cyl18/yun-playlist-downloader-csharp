using YunPlaylistDownloader.Models;
using YunPlaylistDownloader.Services;

namespace YunPlaylistDownloader.Adapters;

public class AdapterFactory
{
    private readonly NetEaseApiClient _apiClient;

    public AdapterFactory(NetEaseApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public BaseAdapter CreateAdapter(string url)
    {
        var pageType = DetectPageType(url);
        
        return pageType switch
        {
            PageType.Playlist => new PlaylistAdapter(_apiClient, url),
            PageType.Album => new AlbumAdapter(_apiClient, url),
            PageType.DjRadio => new DjRadioAdapter(_apiClient, url),
            _ => throw new ArgumentException($"不支持的URL类型: {url}")
        };
    }

    private static PageType DetectPageType(string url)
    {
        if (url.Contains("playlist") || url.Contains("歌单"))
            return PageType.Playlist;
        
        if (url.Contains("album") || url.Contains("专辑"))
            return PageType.Album;
        
        if (url.Contains("djradio") || url.Contains("radio") || url.Contains("电台"))
            return PageType.DjRadio;

        // Default to playlist for plain IDs
        if (System.Text.RegularExpressions.Regex.IsMatch(url, @"^\d+$"))
            return PageType.Playlist;

        throw new ArgumentException($"无法识别URL类型: {url}");
    }

    public static string GetPageTypeText(PageType pageType)
    {
        return pageType switch
        {
            PageType.Playlist => "歌单",
            PageType.Album => "专辑",
            PageType.DjRadio => "电台",
            _ => "未知"
        };
    }
}
