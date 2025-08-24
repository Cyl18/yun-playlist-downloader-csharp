using Microsoft.Extensions.Logging;
using YunPlaylistDownloader.Models;
using Polly;
using System.Net;
using Humanizer;
using Spectre.Console;

namespace YunPlaylistDownloader.Services;

public class DownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadService> _logger;
    private readonly ProgressService _progressService;
    private readonly FileNameService _fileNameService;

    public DownloadService(
        HttpClient httpClient, 
        ILogger<DownloadService> logger,
        ProgressService progressService,
        FileNameService fileNameService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _progressService = progressService;
        _fileNameService = fileNameService;
    }

    public async Task<bool> DownloadSongAsync(
        Song song, 
        string filePath, 
        int retryTimes = 3, 
        int timeoutMinutes = 3,
        bool skipExisting = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(song.Url))
        {
            _progressService.LogSafe(LogLevel.Warning, $"歌曲 {song.SongName} 没有下载链接");
            return false;
        }

        // Ensure directory exists
        filePath = _fileNameService.EnsureDirectoryExists(filePath);

        // Check if file already exists and skip if requested
        if (skipExisting && File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 0)
            {
                _progressService.LogSafe(LogLevel.Information, $"跳过已存在文件: {Path.GetFileName(filePath)}");
                return true; // Return true for skipped files
            }
        }

        // Create temporary file path
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");

        var taskId = _progressService.CreateDownloadTask($"下载: {song.Singer.EscapeMarkup()} - {song.SongName.EscapeMarkup()}");

        try
        {
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: retryTimes,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _progressService.LogSafe(LogLevel.Warning, 
                            $"重试下载 {retryCount}/{retryTimes}: {song.SongName} 等待 {timespan.Humanize()}");
                    });

            var success = await retryPolicy.ExecuteAsync(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

                using var response = await _httpClient.GetAsync(song.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                _progressService.UpdateTask(taskId, 0, $"下载: {song.Singer.EscapeMarkup()} - {song.SongName.EscapeMarkup()} ({totalBytes.Bytes().Humanize()})");

                using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (double)totalBytesRead / totalBytes * 100;
                        _progressService.UpdateTask(taskId, progress);
                    }
                }

                return true;
            });

            if (success)
            {
                try
                {
                    // Move temporary file to final destination
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    File.Move(tempFilePath, filePath);
                    
                    _progressService.CompleteTask(taskId, $"[green]完成: {song.Singer.EscapeMarkup()} - {song.SongName.EscapeMarkup()}[/]");
                    _progressService.LogSafe(LogLevel.Information, $"下载完成: {song.SongName}");
                    return true;
                }
                catch (Exception ex)
                {
                    _progressService.LogSafe(LogLevel.Error, $"移动临时文件失败: {song.SongName} - {ex.Message}");
                    
                    // Clean up temporary file on failure
                    try
                    {
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                    
                    _progressService.FailTask(taskId, $"[red]失败: {song.Singer.EscapeMarkup()} - {song.SongName.EscapeMarkup()}[/]");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _progressService.FailTask(taskId, $"[red]失败: {song.Singer.EscapeMarkup()} - {song.SongName.EscapeMarkup()}[/]");
            _progressService.LogSafe(LogLevel.Error, $"下载失败: {song.SongName} - {ex.Message}");
            
            // Clean up temporary file on exception
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        return false;
    }

    public async Task<bool> DownloadCoverAsync(string coverUrl, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(coverUrl))
            return false;

        try
        {
            filePath = _fileNameService.EnsureDirectoryExists(filePath);

            if (File.Exists(filePath))
            {
                _logger.LogInformation("Cover already exists: {FilePath}", filePath);
                return true;
            }

            using var response = await _httpClient.GetAsync(coverUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await contentStream.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("Downloaded cover: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download cover: {CoverUrl}", coverUrl);
            return false;
        }
    }

    public async Task DownloadSongsAsync(
        IEnumerable<Song> songs,
        string format,
        string? playlistName,
        int concurrency = 5,
        int retryTimes = 3,
        int timeoutMinutes = 3,
        bool skipExisting = true,
        bool skipTrial = false,
        CancellationToken cancellationToken = default)
    {
        var songsToDownload = songs.Where(s => 
            !string.IsNullOrEmpty(s.Url) && 
            (!skipTrial || s.IsFreeTrial != true)).ToList();

        if (!songsToDownload.Any())
        {
            _logger.LogWarning("No songs to download");
            return;
        }

        _logger.LogInformation("开始下载 {Count} 首歌曲，并发数: {Concurrency}", songsToDownload.Count, concurrency);

        // Create overall progress task
        var overallTaskId = _progressService.CreateOverallProgressTask(
            $"总进度: 0/{songsToDownload.Count} 首歌曲", 
            songsToDownload.Count);

        var completedCount = 0;
        var semaphore = new SemaphoreSlim(concurrency, concurrency);
        var tasks = songsToDownload.Select(async song =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var fileName = _fileNameService.GenerateFileName(song, format, playlistName);
                var success = await DownloadSongAsync(song, fileName, retryTimes, timeoutMinutes, skipExisting, cancellationToken);
                
                // Update overall progress
                var completed = Interlocked.Increment(ref completedCount);
                _progressService.IncrementTask(overallTaskId, 1, 
                    $"总进度: {completed}/{songsToDownload.Count} 首歌曲 {(success ? "✓" : "✗")}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        
        // Complete overall progress
        _progressService.CompleteTask(overallTaskId, $"[green]完成: {songsToDownload.Count}/{songsToDownload.Count} 首歌曲[/]");
        _logger.LogInformation("所有下载任务完成");
    }
}
