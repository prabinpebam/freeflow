namespace FreeFlow.Core.Settings;

/// <summary>
/// Reversible protection for sensitive settings (provider API keys) at rest.
/// The real implementation uses Windows DPAPI in the Platform layer; tests use a
/// transparent fake. Keeping the seam in Core lets the JSON settings store
/// encrypt keys without depending on any OS API.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Protect a plaintext secret, returning an opaque, storable string.</summary>
    string Protect(string plaintext);

    /// <summary>Reverse <see cref="Protect"/>. Returns empty for unreadable input.</summary>
    string Unprotect(string protectedValue);
}
