using FreeFlow.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FreeFlow.Platform.DependencyInjection;

/// <summary>
/// Structured logging host for the FreeFlow desktop app. Registers the
/// <see cref="Microsoft.Extensions.Logging"/> stack so any service can take an
/// <c>ILogger&lt;T&gt;</c>, with a Debug sink and a rolling file sink under
/// <c>%LOCALAPPDATA%\FreeFlow\logs</c> by default.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>Default per-user log directory for the unpackaged app.</summary>
    public static string DefaultLogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FreeFlow",
        "logs");

    /// <summary>
    /// Registers the logging stack. Pass <paramref name="configure"/> to add or
    /// replace providers (e.g. in tests), or <paramref name="logDirectory"/> to
    /// redirect the file sink. When both are null the defaults are used.
    /// </summary>
    public static IServiceCollection AddFreeFlowLogging(
        this IServiceCollection services,
        Action<ILoggingBuilder>? configure = null,
        string? logDirectory = null,
        LogLevel minLevel = LogLevel.Information)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(minLevel);
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(
                logDirectory ?? DefaultLogDirectory,
                minLevel));
            configure?.Invoke(builder);
        });

        return services;
    }
}
