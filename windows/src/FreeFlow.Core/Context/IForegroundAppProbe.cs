namespace FreeFlow.Core.Context;

/// <summary>
/// Identifies the foreground application so selection extraction can pick a safe
/// strategy. Real implementation (GetForegroundWindow + process/title) lives in the
/// Platform layer at L4; the inner loop uses a scripted fake. Returns null when the
/// foreground app cannot be determined.
/// </summary>
public interface IForegroundAppProbe
{
    ForegroundAppInfo? TryGetForegroundApp();
}
