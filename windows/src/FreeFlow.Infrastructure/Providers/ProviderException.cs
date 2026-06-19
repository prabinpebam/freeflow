namespace FreeFlow.Infrastructure.Providers;

/// <summary>
/// Raised when a provider call fails (non-success HTTP status or unparseable
/// body). Carries the status code so the pipeline/UX can distinguish auth,
/// rate-limit, and transient failures.
/// </summary>
public sealed class ProviderException : Exception
{
    public ProviderException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
