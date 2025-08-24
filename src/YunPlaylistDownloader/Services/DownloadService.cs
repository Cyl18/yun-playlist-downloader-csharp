using Microsoft.Extensions.Logging;
using YunPlaylistDownloader.Models;
using Polly;
using System.Net;
using Humanizer;

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
            _logger.LogWarning("Song {SongName} has no download URL", song.SongName);
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
                _logger.LogInformation("Skipping existing file: {FilePath}", filePath);
                return true;
            }
        }

        var taskId = _progressService.CreateDownloadTask($"下载: {song.Singer} - {song.SongName}");

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
                        _logger.LogWarning("重试下载 {RetryCount}/{MaxRetries}: {SongName} 等待 {Delay}", 
                            retryCount, retryTimes, song.SongName, timespan.Humanize());
                    });

            var success = await retryPolicy.ExecuteAsync(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

                using var response = await _httpClient.GetAsync(song.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                _progressService.UpdateTask(taskId, 0, $"下载: {song.Singer} - {song.SongName} ({totalBytes.Bytes().Humanize()})");

                using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

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
                _progressService.CompleteTask(taskId, $"[green]完成: {song.Singer} - {song.SongName}[/]");
                _logger.LogInformation("Successfully downloaded: {SongName} to {FilePath}", song.SongName, filePath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _progressService.FailTask(taskId, $"[red]失败: {song.Singer} - {song.SongName}[/]");
            _logger.LogError(ex, "Failed to download song: {SongName}", song.SongName);
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

        var semaphore = new SemaphoreSlim(concurrency, concurrency);
        var tasks = songsToDownload.Select(async song =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var fileName = _fileNameService.GenerateFileName(song, format, playlistName);
                await DownloadSongAsync(song, fileName, retryTimes, timeoutMinutes, skipExisting, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        _logger.LogInformation("所有下载任务完成");
    }
}
