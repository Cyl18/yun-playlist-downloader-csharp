using System.Text.RegularExpressions;
using YunPlaylistDownloader.Models;

namespace YunPlaylistDownloader.Services;

public class FileNameService
{
    private static readonly Dictionary<string, string> IllegalChars = new()
    {
        { "/", "／" },
        { "\\", "＼" },
        { ":", "：" },
        { "*", "＊" },
        { "?", "？" },
        { "\"", "＂" },
        { "<", "＜" },
        { ">", "＞" },
        { "|", "｜" }
    };

    public string GenerateFileName(Song song, string format, string? playlistName = null, DateTime? programDate = null, int? programOrder = null)
    {
        var fileName = format;

        // Replace placeholders
        fileName = fileName.Replace(":name", SanitizeFileName(playlistName ?? "Unknown"))
                           .Replace(":singer", SanitizeFileName(song.Singer))
                           .Replace(":songName", SanitizeFileName(song.SongName))
                           .Replace(":albumName", SanitizeFileName(song.AlbumName))
                           .Replace(":ext", song.Extension ?? "mp3")
                           .Replace(":index", song.Index);

        // For DJ radio programs
        if (programDate.HasValue)
        {
            fileName = fileName.Replace(":programDate", programDate.Value.ToString("yyyy-MM-dd"));
        }
        
        if (programOrder.HasValue)
        {
            fileName = fileName.Replace(":programOrder", programOrder.Value.ToString());
        }

        return fileName;
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "Unknown";

        var sanitized = fileName;

        // Replace illegal characters
        foreach (var kvp in IllegalChars)
        {
            sanitized = sanitized.Replace(kvp.Key, kvp.Value);
        }

        // Remove control characters
        sanitized = Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

        // Trim spaces and dots from the end
        sanitized = sanitized.TrimEnd(' ', '.');

        // Ensure it's not empty
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "Unknown";

        // Limit length to avoid filesystem issues
        if (sanitized.Length > 200)
            sanitized = sanitized.Substring(0, 200);

        return sanitized;
    }

    public string EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return filePath;
    }
}
