using YunPlaylistDownloader.Models;

namespace YunPlaylistDownloader.Adapters;

public abstract class BaseAdapter
{
    protected readonly string Url;
    protected readonly long Id;

    protected BaseAdapter(string url)
    {
        Url = url;
        Id = ExtractId(url);
    }

    public abstract Task<string?> GetTitleAsync();
    public abstract Task<string?> GetCoverUrlAsync();
    public abstract Task<List<Song>> GetSongsAsync(int quality);

    protected static long ExtractId(string url)
    {
        // Extract ID from various URL formats
        var patterns = new[]
        {
            @"[?&]id=(\d+)",
            @"/(\d+)",
            @"^(\d+)$"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, pattern);
            if (match.Success && long.TryParse(match.Groups[1].Value, out var id))
            {
                return id;
            }
        }

        throw new ArgumentException($"无法从URL中提取ID: {url}");
    }

    protected static List<Song> ConvertToSongs<T>(IEnumerable<T> items, Func<T, Song> converter)
    {
        var songs = items.Select(converter).ToList();
        
        // Set index for songs
        var indexLength = songs.Count.ToString().Length;
        for (int i = 0; i < songs.Count; i++)
        {
            songs[i].Index = (i + 1).ToString().PadLeft(indexLength, '0');
            songs[i].RawIndex = i;
        }

        return songs;
    }

    protected static string GetFileExtension(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "mp3";

        try
        {
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.LocalPath);
            return extension.TrimStart('.').ToLowerInvariant() switch
            {
                "mp3" => "mp3",
                "flac" => "flac",
                "m4a" => "m4a",
                _ => "mp3"
            };
        }
        catch
        {
            return "mp3";
        }
    }
}
