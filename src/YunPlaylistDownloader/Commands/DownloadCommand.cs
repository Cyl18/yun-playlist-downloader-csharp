using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using YunPlaylistDownloader.Models;
using YunPlaylistDownloader.Services;
using YunPlaylistDownloader.Adapters;

namespace YunPlaylistDownloader.Commands;

public class DownloadCommand
{
    private readonly ILogger<DownloadCommand> _logger;
    private readonly NetEaseApiClient _apiClient;
    private readonly DownloadService _downloadService;
    private readonly CookieService _cookieService;
    private readonly AdapterFactory _adapterFactory;
    private readonly ProgressService _progressService;
    private readonly FileRenameService _fileRenameService;

    public DownloadCommand(
        ILogger<DownloadCommand> logger,
        NetEaseApiClient apiClient,
        DownloadService downloadService,
        CookieService cookieService,
        AdapterFactory adapterFactory,
        ProgressService progressService,
        FileRenameService fileRenameService)
    {
        _logger = logger;
        _apiClient = apiClient;
        _downloadService = downloadService;
        _cookieService = cookieService;
        _adapterFactory = adapterFactory;
        _progressService = progressService;
        _fileRenameService = fileRenameService;
    }

    public void ConfigureCommand(RootCommand rootCommand)
    {
        var urlArgument = new Argument<string>("url", "歌单/专辑/电台的链接或ID");

        var concurrencyOption = new Option<int>(
            aliases: new[] { "--concurrency", "-c" },
            description: "同时下载数量",
            getDefaultValue: () => 5);

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            description: "文件格式",
            getDefaultValue: () => ":name/:singer - :songName.:ext");

        var qualityOption = new Option<int>(
            aliases: new[] { "--quality", "-q" },
            description: "音质, 默认 999k 即最大码率",
            getDefaultValue: () => 999);

        var retryTimeoutOption = new Option<int>(
            "--retry-timeout",
            description: "下载超时(分)",
            getDefaultValue: () => 3);

        var retryTimesOption = new Option<int>(
            "--retry-times", 
            description: "下载重试次数",
            getDefaultValue: () => 3);

        var skipOption = new Option<bool>(
            aliases: new[] { "--skip", "-s" },
            description: "对于已存在文件且大小合适则跳过",
            getDefaultValue: () => true);

        var progressOption = new Option<bool>(
            aliases: new[] { "--progress", "-p" },
            description: "是否显示进度条",
            getDefaultValue: () => true);

        var coverOption = new Option<bool>(
            "--cover",
            description: "下载封面",
            getDefaultValue: () => false);

        var cookieOption = new Option<string>(
            "--cookie",
            description: "cookie文件",
            getDefaultValue: () => "yun.cookie.txt");

        var skipTrialOption = new Option<bool>(
            "--skip-trial",
            description: "跳过试听歌曲",
            getDefaultValue: () => false);

        var renameOption = new Option<bool>(
            "--rename",
            description: "重命名模式：重命名现有文件为歌单格式，不在歌单内的文件重命名为MD5",
            getDefaultValue: () => false);

        var quietOption = new Option<bool>(
            "--quiet",
            description: "安静模式：只显示错误和结果信息",
            getDefaultValue: () => false);

        qualityOption.AddValidator(result =>
        {
            var value = result.GetValueForOption(qualityOption);
            if (value != 128 && value != 192 && value != 320 && value != 999)
            {
                result.ErrorMessage = "音质只能选择 128, 192, 320 或 999";
            }
        });

        rootCommand.AddArgument(urlArgument);
        rootCommand.AddOption(concurrencyOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(qualityOption);
        rootCommand.AddOption(retryTimeoutOption);
        rootCommand.AddOption(retryTimesOption);
        rootCommand.AddOption(skipOption);
        rootCommand.AddOption(progressOption);
        rootCommand.AddOption(coverOption);
        rootCommand.AddOption(cookieOption);
        rootCommand.AddOption(skipTrialOption);
        rootCommand.AddOption(renameOption);
        rootCommand.AddOption(quietOption);

        rootCommand.SetHandler(async (context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var concurrency = context.ParseResult.GetValueForOption(concurrencyOption);
            var format = context.ParseResult.GetValueForOption(formatOption);
            var quality = context.ParseResult.GetValueForOption(qualityOption);
            var retryTimeout = context.ParseResult.GetValueForOption(retryTimeoutOption);
            var retryTimes = context.ParseResult.GetValueForOption(retryTimesOption);
            var skip = context.ParseResult.GetValueForOption(skipOption);
            var progress = context.ParseResult.GetValueForOption(progressOption);
            var cover = context.ParseResult.GetValueForOption(coverOption);
            var cookie = context.ParseResult.GetValueForOption(cookieOption);
            var skipTrial = context.ParseResult.GetValueForOption(skipTrialOption);
            var rename = context.ParseResult.GetValueForOption(renameOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);

            var options = new DownloadOptions
            {
                Url = url,
                Concurrency = concurrency,
                Format = format ?? ":name/:singer - :songName.:ext",
                Quality = quality,
                RetryTimeout = retryTimeout,
                RetryTimes = retryTimes,
                Skip = skip,
                Progress = progress,
                Cover = cover,
                Cookie = cookie ?? "yun.cookie.txt",
                SkipTrial = skipTrial,
                Rename = rename,
                Quiet = quiet
            };

            await ExecuteDownloadAsync(options);
        });
    }

    private async Task ExecuteDownloadAsync(DownloadOptions options)
    {
        try
        {
            // 设置安静模式
            _progressService.SetQuietMode(options.Quiet);
            
            // Show current parameters (除非是安静模式)
            if (!options.Quiet)
            {
                ShowParameters(options);
            }

            // Load cookie if specified
            if (!string.IsNullOrEmpty(options.Cookie))
            {
                _cookieService.LoadCookie(options.Cookie);
            }

            // Validate URL
            if (!IsValidUrl(options.Url))
            {
                ProgressService.ShowError("URL 参数错误: 支持 url 或 歌单ID");
                return;
            }

            await _progressService.WithProgressAsync(async () =>
            {
                // Create adapter
                var adapter = _adapterFactory.CreateAdapter(options.Url);
                
                // Get title and songs
                var title = await adapter.GetTitleAsync();
                var songs = await adapter.GetSongsAsync(options.Quality);

                if (!songs.Any())
                {
                    ProgressService.ShowWarning("没有找到可下载的歌曲");
                    return;
                }

                // Handle rename mode
                if (options.Rename)
                {
                    ProgressService.ShowInfo($"重命名模式：重命名 {title} 中的文件");
                    var renameFormat = ":name/:singer - :albumName - :songName.:ext";
                    await _fileRenameService.RenamePlaylistFilesAsync(songs, title ?? "Unknown", renameFormat);
                    return;
                }

                // Normal download mode
                // Filter out invalid songs
                var validSongs = songs.Where(s => !string.IsNullOrEmpty(s.Url)).ToList();
                var invalidSongs = songs.Where(s => string.IsNullOrEmpty(s.Url)).ToList();

                if (invalidSongs.Any())
                {
                    ProgressService.ShowWarning($"有 {invalidSongs.Count} 首歌曲无法获取下载链接");
                }

                if (options.SkipTrial)
                {
                    var trialSongs = validSongs.Where(s => s.IsFreeTrial == true).ToList();
                    validSongs = validSongs.Where(s => s.IsFreeTrial != true).ToList();
                    
                    if (trialSongs.Any())
                    {
                        ProgressService.ShowInfo($"跳过 {trialSongs.Count} 首试听歌曲");
                    }
                }

                if (!validSongs.Any())
                {
                    ProgressService.ShowError("没有可下载的歌曲");
                    return;
                }

                ProgressService.ShowInfo($"准备下载 {validSongs.Count} 首歌曲");

                // Download cover if requested
                if (options.Cover)
                {
                    var coverUrl = await adapter.GetCoverUrlAsync();
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        var coverPath = Path.Combine(title ?? "Unknown", "cover.jpg");
                        await _downloadService.DownloadCoverAsync(coverUrl, coverPath);
                    }
                }

                // Start downloading songs
                await _downloadService.DownloadSongsAsync(
                    validSongs,
                    options.Format,
                    title,
                    options.Concurrency,
                    options.RetryTimes,
                    options.RetryTimeout,
                    options.Skip,
                    options.SkipTrial);
            });

            if (options.Rename)
            {
                ProgressService.ShowSuccess("文件重命名完成!");
            }
            else
            {
                ProgressService.ShowSuccess("所有下载任务完成!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载过程中发生错误");
            ProgressService.ShowError($"下载失败: {ex.Message}");
        }
    }

    private static void ShowParameters(DownloadOptions options)
    {
        var parameters = new Dictionary<string, object>
        {
            { "URL", options.Url },
            { "并发数", options.Concurrency },
            { "文件格式", options.Format },
            { "音质", $"{options.Quality}k" },
            { "重试超时", $"{options.RetryTimeout}分钟" },
            { "重试次数", options.RetryTimes },
            { "跳过已存在", options.Skip },
            { "显示进度", options.Progress },
            { "下载封面", options.Cover },
            { "跳过试听", options.SkipTrial },
            { "重命名模式", options.Rename ? "是（仅重命名文件）" : "否" }
        };

        ProgressService.ShowTable("当前参数", parameters);
    }

    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Check if it's a valid URL
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return true;

        // Check if it's a numeric ID
        return System.Text.RegularExpressions.Regex.IsMatch(url, @"^\d+$");
    }
}
