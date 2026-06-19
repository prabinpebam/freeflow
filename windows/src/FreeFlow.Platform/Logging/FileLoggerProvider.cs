using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FreeFlow.Platform.Logging;

/// <summary>
/// Minimal, dependency-free rolling file logger. Writes one line per event to a
/// daily file under the configured directory. Thread-safe; flushes on every
/// write so logs survive an unclean shutdown. Intended for the unpackaged
/// desktop host where no external logging sink is wired up yet.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minLevel;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string directory, LogLevel minLevel = LogLevel.Information)
    {
        _directory = directory;
        _minLevel = minLevel;
        Directory.CreateDirectory(_directory);
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => _loggers.Clear();

    private void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        var fileName = $"freeflow-{DateTime.UtcNow:yyyyMMdd}.log";
        var path = Path.Combine(_directory, fileName);

        var sb = new StringBuilder();
        sb.Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(LevelLabel(level)).Append("] ");
        sb.Append(category);
        if (eventId.Id != 0)
        {
            sb.Append(" (").Append(eventId.Id.ToString(CultureInfo.InvariantCulture)).Append(')');
        }

        sb.Append(": ").Append(message);
        if (exception is not null)
        {
            sb.Append(Environment.NewLine).Append(exception);
        }

        sb.Append(Environment.NewLine);

        lock (_gate)
        {
            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "NON",
    };

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None && logLevel >= _provider._minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            _provider.Write(_category, logLevel, eventId, message, exception);
        }
    }
}
