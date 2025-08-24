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
        { "|", "｜" },
        { "\t", " " },
        { "\r", "" },
        { "\n", "" }
    };

    // Windows 保留名称
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
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

        // Remove multiple consecutive spaces and replace with single space
        sanitized = Regex.Replace(sanitized, @"\s+", " ");

        // Trim spaces and dots from the end
        sanitized = sanitized.TrimEnd(' ', '.');

        // Ensure it's not empty
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "Unknown";

        // Check for reserved names
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
        if (ReservedNames.Contains(nameWithoutExt))
        {
            sanitized = "_" + sanitized;
        }

        // Limit length to avoid filesystem issues (考虑到可能的路径前缀，保守一些)
        if (sanitized.Length > 90)
        {
            var extension = Path.GetExtension(sanitized);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            
            // 保留扩展名，截断文件名主体
            var maxNameLength = 90 - extension.Length;
            if (maxNameLength > 0)
            {
                nameWithoutExtension = nameWithoutExtension.Substring(0, Math.Min(nameWithoutExtension.Length, maxNameLength));
                sanitized = nameWithoutExtension + extension;
            }
            else
            {
                sanitized = sanitized.Substring(0, 90);
            }
        }

        return sanitized;
    }

    public string EnsureDirectoryExists(string filePath)
    {
        // 先清理整个路径
        var sanitizedPath = SanitizeFilePath(filePath);
        
        var directory = Path.GetDirectoryName(sanitizedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return sanitizedPath;
    }

    /// <summary>
    /// 清理完整的文件路径，包括目录名和文件名
    /// </summary>
    public string SanitizeFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "Unknown";

        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var sanitizedParts = new List<string>();

        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part))
            {
                sanitizedParts.Add(SanitizeFileName(part));
            }
        }

        var result = string.Join(Path.DirectorySeparatorChar.ToString(), sanitizedParts);
        
        // 确保总路径长度不超过 Windows 限制（保守估计）
        if (result.Length > 200)
        {
            // 如果路径太长，尝试缩短文件名部分
            var directory = Path.GetDirectoryName(result);
            var fileName = Path.GetFileName(result);
            var extension = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            var maxFileNameLength = 200 - (directory?.Length ?? 0) - extension.Length - 1; // -1 for separator
            if (maxFileNameLength > 10) // 保证至少有一些字符
            {
                nameWithoutExt = nameWithoutExt.Substring(0, Math.Min(nameWithoutExt.Length, maxFileNameLength));
                fileName = nameWithoutExt + extension;
                result = string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
            }
        }

        return result;
    }
}
