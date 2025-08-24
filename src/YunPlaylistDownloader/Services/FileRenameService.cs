using Microsoft.Extensions.Logging;
using YunPlaylistDownloader.Models;
using System.Security.Cryptography;
using System.Text;

namespace YunPlaylistDownloader.Services;

public class FileRenameService
{
    private readonly ILogger<FileRenameService> _logger;
    private readonly FileNameService _fileNameService;

    public FileRenameService(ILogger<FileRenameService> logger, FileNameService fileNameService)
    {
        _logger = logger;
        _fileNameService = fileNameService;
    }

    public async Task RenamePlaylistFilesAsync(IEnumerable<Song> songs, string playlistName, string format = ":name/:singer - :albumName - :songName.:ext")
    {
        var playlistDir = playlistName;
        Directory.CreateDirectory(playlistDir); // 确保目录存在
        
        var existingFiles = Directory.GetFiles(playlistDir, "*.*", SearchOption.AllDirectories)
            .Where(f => IsAudioFile(f))
            .ToList();

        // 创建文件MD5到文件路径的映射
        var fileMD5Map = new Dictionary<string, string>();
        foreach (var file in existingFiles)
        {
            try
            {
                var fileMD5 = await GetFileMD5Async(file);
                fileMD5Map[fileMD5] = file;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算文件MD5失败: {FilePath}", file);
            }
        }

        var renamedFiles = new HashSet<string>();
        var matchedCount = 0;

        // 根据MD5匹配Song和文件
        foreach (var song in songs.Where(s => !string.IsNullOrEmpty(s.MD5)))
        {
            if (fileMD5Map.TryGetValue(song.MD5!, out var matchingFile))
            {
                var expectedFileName = _fileNameService.GenerateFileName(song, format, playlistName);
                var newFileName = expectedFileName.Replace(":ext", Path.GetExtension(matchingFile));
                var newPath = Path.Combine(playlistDir, newFileName);
                
                if (matchingFile != newPath)
                {
                    try
                    {
                        // 确保目标目录存在
                        var targetDir = Path.GetDirectoryName(newPath)!;
                        Directory.CreateDirectory(targetDir);
                        
                        File.Move(matchingFile, newPath);
                        _logger.LogInformation("MD5匹配重命名: {OldPath} -> {NewPath}", 
                            Path.GetRelativePath(playlistDir, matchingFile), 
                            Path.GetRelativePath(playlistDir, newPath));
                        
                        matchedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "重命名文件失败: {FilePath}", matchingFile);
                    }
                }
                
                renamedFiles.Add(matchingFile);
            }
        }

        // 处理不在歌单内的文件，重命名为MD5
        var remainingFiles = existingFiles.Except(renamedFiles).ToList();
        foreach (var file in remainingFiles)
        {
            try
            {
                var md5Hash = await GetFileMD5Async(file);
                var extension = Path.GetExtension(file);
                var newPath = Path.Combine(playlistDir, $"{md5Hash}{extension}");
                
                if (file != newPath && !File.Exists(newPath))
                {
                    File.Move(file, newPath);
                    _logger.LogInformation("重命名非歌单文件为MD5: {OldPath} -> {NewName}", 
                        Path.GetRelativePath(playlistDir, file), 
                        Path.GetFileName(newPath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重命名文件为MD5失败: {FilePath}", file);
            }
        }

        ProgressService.ShowSuccess($"文件重命名完成！MD5匹配: {matchedCount} 个文件，处理了 {existingFiles.Count} 个文件");
    }

    private async Task<string> GetFileMD5Async(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await Task.Run(() => md5.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".mp3" or ".flac" or ".wav" or ".aac" or ".ogg" or ".m4a";
    }
}
