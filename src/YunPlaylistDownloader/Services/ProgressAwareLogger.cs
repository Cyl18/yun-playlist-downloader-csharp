using Microsoft.Extensions.Logging;

namespace YunPlaylistDownloader.Services;

public class ProgressAwareLogger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly ProgressService _progressService;

    public ProgressAwareLogger(ILogger innerLogger, ProgressService progressService)
    {
        _innerLogger = innerLogger;
        _progressService = progressService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _innerLogger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        
        var message = formatter(state, exception);
        _progressService.LogSafe(logLevel, message);
    }
}

public class ProgressAwareLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider _innerProvider;
    private readonly ProgressService _progressService;

    public ProgressAwareLoggerProvider(ILoggerProvider innerProvider, ProgressService progressService)
    {
        _innerProvider = innerProvider;
        _progressService = progressService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var innerLogger = _innerProvider.CreateLogger(categoryName);
        return new ProgressAwareLogger(innerLogger, _progressService);
    }

    public void Dispose()
    {
        _innerProvider.Dispose();
    }
}
