using Spectre.Console;
using Humanizer;

namespace YunPlaylistDownloader.Services;

public class ProgressService
{
    private readonly Dictionary<string, ProgressTask> _tasks = new();
    private ProgressContext? _progressContext;

    public void Initialize(int totalTasks)
    {
        AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                _progressContext = ctx;
                
                // Wait until all tasks are completed or the service is disposed
                while (_tasks.Values.Any(t => !t.IsFinished))
                {
                    await Task.Delay(100);
                }
            });
    }

    public string CreateDownloadTask(string description, long maxValue = 100)
    {
        if (_progressContext == null)
            throw new InvalidOperationException("Progress context not initialized");

        var taskId = Guid.NewGuid().ToString();
        var task = _progressContext.AddTask(description, maxValue: maxValue);
        _tasks[taskId] = task;
        
        return taskId;
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
