using FreeFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace FreeFlow.Platform.SystemIntegration;

/// <summary>
/// Per-user "launch at login" via the registry Run key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>). This is the
/// standard mechanism for unpackaged desktop apps and needs no elevation. The
/// registered command is the path to the currently-running executable (the
/// portable launcher exe in a packaged layout), quoted to tolerate spaces.
/// </summary>
public sealed class RegistryLaunchAtLoginService : ILaunchAtLoginService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FreeFlow";

    private readonly ILogger<RegistryLaunchAtLoginService> _logger;
    private readonly Func<string?> _executablePath;

    public RegistryLaunchAtLoginService(
        ILogger<RegistryLaunchAtLoginService>? logger = null,
        Func<string?>? executablePath = null)
    {
        _logger = logger ?? NullLogger<RegistryLaunchAtLoginService>.Instance;
        _executablePath = executablePath ?? (() => Environment.ProcessPath);
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read launch-at-login state.");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var path = _executablePath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    _logger.LogWarning("Cannot enable launch-at-login: executable path is unknown.");
                    return;
                }

                key.SetValue(ValueName, $"\"{path}\"");
                _logger.LogInformation("Enabled launch-at-login at {Path}.", path);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.LogInformation("Disabled launch-at-login.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update launch-at-login state.");
        }
    }
}
