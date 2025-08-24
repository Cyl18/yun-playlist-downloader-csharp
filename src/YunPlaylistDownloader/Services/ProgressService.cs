using Spectre.Console;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace YunPlaylistDownloader.Services;

public class ProgressService
{
    private readonly Dictionary<string, ProgressTask> _tasks = new();
    private readonly ILogger<ProgressService> _logger;
    private ProgressContext? _progressContext;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private bool _isInitialized = false;
    private bool _isProgressMode = false;
    private bool _isQuietMode = false;
    private readonly List<string> _pendingLogs = new();

    public ProgressService(ILogger<ProgressService> logger)
    {
        _logger = logger;
    }

    public void SetQuietMode(bool quiet)
    {
        _isQuietMode = quiet;
    }

    public async Task InitializeAsync()
    {
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;

            _isInitialized = true;
            // We'll set the context when we actually start the progress display
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public async Task<T> WithProgressAsync<T>(Func<Task<T>> operation)
    {
        if (!_isInitialized)
            await InitializeAsync();

        _isProgressMode = true;
        
        return await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                _progressContext = ctx;
                try
                {
                    var result = await operation();
                    
                    // 输出进度模式期间收集的日志
                    if (_pendingLogs.Any())
                    {
                        AnsiConsole.WriteLine();
                        foreach (var log in _pendingLogs)
                        {
                            AnsiConsole.MarkupLine(log);
                        }
                        _pendingLogs.Clear();
                    }
                    
                    return result;
                }
                finally
                {
                    _progressContext = null;
                    _tasks.Clear();
                    _isProgressMode = false;
                }
            });
    }

    public async Task WithProgressAsync(Func<Task> operation)
    {
        if (!_isInitialized)
            await InitializeAsync();

        _isProgressMode = true;
        
        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                _progressContext = ctx;
                try
                {
                    await operation();
                    
                    // 输出进度模式期间收集的日志
                    if (_pendingLogs.Any())
                    {
                        AnsiConsole.WriteLine();
                        foreach (var log in _pendingLogs)
                        {
                            AnsiConsole.MarkupLine(log);
                        }
                        _pendingLogs.Clear();
                    }
                }
                finally
                {
                    _progressContext = null;
                    _tasks.Clear();
                    _isProgressMode = false;
                }
            });
    }

    public string CreateDownloadTask(string description, long maxValue = 100)
    {
        if (_progressContext == null)
        {
            // Fallback for cases where progress context is not available
            _logger.LogInformation(description);
            return Guid.NewGuid().ToString();
        }

        var taskId = Guid.NewGuid().ToString();
        var task = _progressContext.AddTask(description, maxValue: maxValue);
        _tasks[taskId] = task;
        
        return taskId;
    }

    public string CreateOverallProgressTask(string description, long totalItems)
    {
        if (_progressContext == null)
        {
            _logger.LogInformation(description);
            return Guid.NewGuid().ToString();
        }

        var taskId = Guid.NewGuid().ToString();
        var task = _progressContext.AddTask($"[bold blue]{description}[/]", maxValue: totalItems);
        _tasks[taskId] = task;
        
        return taskId;
    }

    public void LogSafe(LogLevel level, string message)
    {
        // 在安静模式下，只记录错误和警告
        if (_isQuietMode && level != LogLevel.Error && level != LogLevel.Warning)
        {
            return;
        }

        if (_isProgressMode)
        {
            // 在进度模式下，收集日志消息
            var coloredMessage = level switch
            {
                LogLevel.Error => $"[red]✗ {message.EscapeMarkup()}[/]",
                LogLevel.Warning => $"[yellow]⚠ {message.EscapeMarkup()}[/]",
                LogLevel.Information => $"[blue]ℹ {message.EscapeMarkup()}[/]",
                _ => message.EscapeMarkup()
            };
            _pendingLogs.Add(coloredMessage);
        }
        else
        {
            // 非进度模式下直接输出
            _logger.Log(level, message);
        }
    }

    public void UpdateTask(string taskId, double value, string? description = null)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Value = value;
            if (description != null)
            {
                task.Description = description;
            }
        }
    }

    public void IncrementTask(string taskId, double increment = 1, string? description = null)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Increment(increment);
            if (description != null)
            {
                task.Description = description;
            }
        }
    }

    public void CompleteTask(string taskId, string? finalDescription = null)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Value = task.MaxValue;
            if (finalDescription != null)
            {
                task.Description = finalDescription;
            }
            task.StopTask();
        }
    }

    public void FailTask(string taskId, string? errorDescription = null)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Description = errorDescription ?? $"[red]{task.Description} - Failed[/]";
            task.StopTask();
        }
    }

    public static void ShowTable(string title, Dictionary<string, object> data)
    {
        var table = new Table()
            .AddColumn("参数")
            .AddColumn("值")
            .AddColumn("备注");

        table.Title = new TableTitle(title);
        table.Border = TableBorder.Rounded;

        foreach (var kvp in data)
        {
            var value = kvp.Value switch
            {
                bool b => b ? "是" : "否",
                TimeSpan ts => ts.Humanize(),
                _ => kvp.Value.ToString() ?? ""
            };

            table.AddRow(kvp.Key, value, "");
        }

        AnsiConsole.Write(table);
    }

    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {message}");
    }

    public static void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {message}");
    }

    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {message}");
    }

    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {message}");
    }
}
