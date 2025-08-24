using System.Text.Json;
using Microsoft.Extensions.Logging;
using YunPlaylistDownloader.Models;
using Polly;
using Polly.Extensions.Http;
using System.Collections.Concurrent;

namespace YunPlaylistDownloader.Services;

public class NetEaseApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NetEaseApiClient> _logger;
    private readonly CookieService _cookieService;
    
    private const string BaseUrl = "https://music.163.com/api";
    private const int BatchSize = 200;

    // 静态缓存，用于存储歌单详情
    private static readonly ConcurrentDictionary<long, Playlist> _playlistCache = new();

    public NetEaseApiClient(HttpClient httpClient, ILogger<NetEaseApiClient> logger, CookieService cookieService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cookieService = cookieService;
    }

    public async Task<Playlist?> GetPlaylistDetailAsync(long id)
    {
        // 检查缓存
        if (_playlistCache.TryGetValue(id, out var cachedPlaylist))
        {
            _logger.LogDebug("Found cached playlist detail for ID {Id}", id);
            return cachedPlaylist;
        }

        try
        {
            _logger.LogDebug("Fetching playlist detail for ID {Id}", id);
            var url = $"{BaseUrl}/v6/playlist/detail?id={id}&n=100000&s=8";
            var response = await GetWithRetryAsync(url);
            
            if (response?.IsSuccessStatusCode == true)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                
                if (json.RootElement.TryGetProperty("playlist", out var playlistElement))
                {
                    var playlist = ParsePlaylist(playlistElement);
                    
                    // 如果有 TrackIds，使用 TrackIds 来获取完整的歌曲列表
                    if (playlist.TrackIds.Any())
                    {
                        _logger.LogInformation("Using TrackIds to get all {Count} tracks", playlist.TrackIds.Count);
                        
                        // 使用 TrackIds 获取歌曲详情
                        var trackIds = playlist.TrackIds.Select(t => t.Id).ToList();
                        var trackDetails = await GetSongDetailsAsync(trackIds);
                        playlist.Tracks = trackDetails;
                    }
                    
                    // 缓存结果
                    _playlistCache.TryAdd(id, playlist);
                    _logger.LogDebug("Cached playlist detail for ID {Id}", id);
                    
                    return playlist;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get playlist detail for id: {Id}", id);
        }
        
        return null;
    }

    public async Task<List<TrackData>> GetSongDetailsAsync(List<long> songIds)
    {
        var allTracks = new List<TrackData>();
        const int batchSize = 1000; // 歌曲数量不要超过1000

        var chunks = songIds.Chunk(batchSize);
        foreach (var chunk in chunks)
        {
            try
            {
                // 构建符合网易云API格式的参数
                var cValue = "[" + string.Join(",", chunk.Select(id => $"{{\"id\":{id}}}")) + "]";
                
                var requestData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("c", cValue)
                });

                var response = await PostWithRetryAsync($"{BaseUrl}/v3/song/detail", requestData);
                if (response?.IsSuccessStatusCode == true)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);
                    
                    if (json.RootElement.TryGetProperty("songs", out var songsElement))
                    {
                        var tracks = ParseTracks(songsElement);
                        allTracks.AddRange(tracks);
                        
                        _logger.LogInformation("Fetched details for {Count} songs, total: {Total}/{Expected}", 
                            tracks.Count, allTracks.Count, songIds.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get song details for chunk");
            }
            
            // Add delay to avoid rate limiting
            await Task.Delay(100);
        }

        _logger.LogInformation("Successfully fetched details for {Count} songs total", allTracks.Count);
        return allTracks;
    }

    public async Task<Album?> GetAlbumDetailAsync(long id)
    {
        try
        {
            var url = $"{BaseUrl}/v1/album/{id}";
            var response = await GetWithRetryAsync(url);
            
            if (response?.IsSuccessStatusCode == true)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                
                if (json.RootElement.TryGetProperty("album", out var albumElement))
                {
                    return ParseAlbum(albumElement);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get album detail for id: {Id}", id);
        }
        
        return null;
    }

    public async Task<DjRadio?> GetDjRadioDetailAsync(long id)
    {
        try
        {
            var url = $"{BaseUrl}/dj/program/byradio?radioId={id}&limit=1000&offset=0&asc=false";
            var response = await GetWithRetryAsync(url);
            
            if (response?.IsSuccessStatusCode == true)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                
                return ParseDjRadio(json.RootElement, id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get djradio detail for id: {Id}", id);
        }
        
        return null;
    }

    public async Task<List<SongPlayUrlInfo>> GetSongUrlsAsync(IEnumerable<long> ids, int quality = 999)
    {
        var results = new List<SongPlayUrlInfo>();
        var chunks = ids.Chunk(BatchSize);

        foreach (var chunk in chunks)
        {
            try
            {
                var idsParam = string.Join(",", chunk);
                var url = $"{BaseUrl}/song/enhance/player/url?ids=[{idsParam}]&br={quality}000";
                
                var response = await GetWithRetryAsync(url);
                if (response?.IsSuccessStatusCode == true)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);
                    
                    if (json.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        var urlInfos = ParseSongUrls(dataElement);
                        results.AddRange(urlInfos);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get song URLs for chunk");
            }
            
            // Add delay to avoid rate limiting
            await Task.Delay(100);
        }

        return results;
    }

    private async Task<HttpResponseMessage?> GetWithRetryAsync(string url)
    {
        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for URL: {Url} after {Delay}s", 
                        retryCount, url, timespan.TotalSeconds);
                });

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        // Add cookie if available
        var cookie = _cookieService.GetCookieString();
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await retryPolicy.ExecuteAsync(async () => await _httpClient.SendAsync(request));
    }

    private async Task<HttpResponseMessage?> PostWithRetryAsync(string url, HttpContent content)
    {
        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for URL: {Url} after {Delay}s", 
                        retryCount, url, timespan.TotalSeconds);
                });

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        
        // Add cookie if available
        var cookie = _cookieService.GetCookieString();
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await retryPolicy.ExecuteAsync(async () => await _httpClient.SendAsync(request));
    }

    private static Playlist ParsePlaylist(JsonElement playlistElement)
    {
        var playlist = new Playlist
        {
            Id = playlistElement.GetProperty("id").GetInt64(),
            Name = playlistElement.GetProperty("name").GetString() ?? "",
            CoverUrl = playlistElement.TryGetProperty("coverImgUrl", out var cover) ? cover.GetString() : null,
            Description = playlistElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
            TrackCount = playlistElement.TryGetProperty("trackCount", out var trackCount) ? trackCount.GetInt64() : 0
        };

        // 解析 Tracks (可能只有前1000首)
        if (playlistElement.TryGetProperty("tracks", out var tracksElement))
        {
            playlist.Tracks = ParseTracks(tracksElement);
        }

        // 解析 TrackIds (包含所有歌曲ID)
        if (playlistElement.TryGetProperty("trackIds", out var trackIdsElement))
        {
            playlist.TrackIds = ParseTrackIds(trackIdsElement);
        }

        return playlist;
    }

    private static List<TrackIdData> ParseTrackIds(JsonElement trackIdsElement)
    {
        var trackIds = new List<TrackIdData>();
        
        foreach (var trackIdElement in trackIdsElement.EnumerateArray())
        {
            var trackId = new TrackIdData
            {
                Id = trackIdElement.GetProperty("id").GetInt64(),
                V = trackIdElement.TryGetProperty("v", out var v) ? v.GetInt64() : 0,
                T = trackIdElement.TryGetProperty("t", out var t) ? t.GetInt64() : 0,
                At = trackIdElement.TryGetProperty("at", out var at) ? at.GetInt64() : 0,
                Uid = trackIdElement.TryGetProperty("uid", out var uid) ? uid.GetInt64() : 0,
                Alg = trackIdElement.TryGetProperty("alg", out var alg) ? (object)alg : null
            };
            trackIds.Add(trackId);
        }

        return trackIds;
    }

    private static Album ParseAlbum(JsonElement albumElement)
    {
        var album = new Album
        {
            Id = albumElement.GetProperty("id").GetInt64(),
            Name = albumElement.GetProperty("name").GetString() ?? "",
            PicUrl = albumElement.TryGetProperty("picUrl", out var pic) ? pic.GetString() : null
        };

        if (albumElement.TryGetProperty("songs", out var songsElement))
        {
            album.Songs = ParseTracks(songsElement);
        }

        if (albumElement.TryGetProperty("artist", out var artistElement))
        {
            album.Artist = new ArtistData
            {
                Id = artistElement.GetProperty("id").GetInt64(),
                Name = artistElement.GetProperty("name").GetString() ?? ""
            };
        }

        return album;
    }

    private static DjRadio ParseDjRadio(JsonElement rootElement, long id)
    {
        var djRadio = new DjRadio { Id = id };
        
        if (rootElement.TryGetProperty("programs", out var programsElement) && 
            programsElement.GetArrayLength() > 0)
        {
            var firstProgram = programsElement[0];
            if (firstProgram.TryGetProperty("radio", out var radioElement))
            {
                djRadio.Name = radioElement.GetProperty("name").GetString() ?? "";
                djRadio.PicUrl = radioElement.TryGetProperty("picUrl", out var pic) ? pic.GetString() : null;
                djRadio.Desc = radioElement.TryGetProperty("desc", out var desc) ? desc.GetString() ?? "" : "";
            }
        }

        return djRadio;
    }

    private static List<TrackData> ParseTracks(JsonElement tracksElement)
    {
        var tracks = new List<TrackData>();
        
        foreach (var trackElement in tracksElement.EnumerateArray())
        {
            var track = new TrackData
            {
                Id = trackElement.GetProperty("id").GetInt64(),
                Name = trackElement.GetProperty("name").GetString() ?? "",
                Dt = trackElement.TryGetProperty("dt", out var dt) ? dt.GetInt32() : 0
            };

            if (trackElement.TryGetProperty("ar", out var arElement))
            {
                track.Ar = ParseArtists(arElement);
            }

            if (trackElement.TryGetProperty("al", out var alElement))
            {
                track.Al = ParseAlbumData(alElement);
            }

            tracks.Add(track);
        }

        return tracks;
    }

    private static List<ArtistData> ParseArtists(JsonElement artistsElement)
    {
        var artists = new List<ArtistData>();
        
        foreach (var artistElement in artistsElement.EnumerateArray())
        {
            artists.Add(new ArtistData
            {
                Id = artistElement.GetProperty("id").GetInt64(),
                Name = artistElement.GetProperty("name").GetString() ?? ""
            });
        }

        return artists;
    }

    private static AlbumData ParseAlbumData(JsonElement albumElement)
    {
        return new AlbumData
        {
            Id = albumElement.GetProperty("id").GetInt64(),
            Name = albumElement.GetProperty("name").GetString() ?? "",
            PicUrl = albumElement.TryGetProperty("picUrl", out var pic) ? pic.GetString() : null
        };
    }

    private static List<SongPlayUrlInfo> ParseSongUrls(JsonElement dataElement)
    {
        var urlInfos = new List<SongPlayUrlInfo>();
        
        foreach (var urlElement in dataElement.EnumerateArray())
        {
            var urlInfo = new SongPlayUrlInfo
            {
                Id = urlElement.GetProperty("id").GetInt64(),
                Url = urlElement.TryGetProperty("url", out var url) ? url.GetString() : null,
                Br = urlElement.TryGetProperty("br", out var br) ? br.GetInt32() : 0,
                Size = urlElement.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                Md5 = urlElement.TryGetProperty("md5", out var md5) ? md5.GetString() : null,
                Code = urlElement.TryGetProperty("code", out var code) ? code.GetInt32() : 0,
                Type = urlElement.TryGetProperty("type", out var type) ? type.GetString() : null,
                FreeTrialInfo = urlElement.TryGetProperty("freeTrialInfo", out var trial) ? (object)trial : null
            };

            urlInfos.Add(urlInfo);
        }

        return urlInfos;
    }

    /// <summary>
    /// 清除歌单缓存
    /// </summary>
    public static void ClearPlaylistCache()
    {
        _playlistCache.Clear();
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public static int GetCacheCount()
    {
        return _playlistCache.Count;
    }
}
