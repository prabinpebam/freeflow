using System.Security.Cryptography;
using System.Text;

namespace FreeFlow.Platform.Settings;

/// <summary>
/// Encrypts/decrypts sensitive settings (e.g. provider API keys) at rest using
/// Windows DPAPI scoped to the current user (porting-plan Section 8.4). The
/// ciphertext is Base64 so it can live in a JSON settings file. Decryption is
/// resilient: an unreadable blob returns empty rather than throwing.
/// </summary>
public sealed class DpapiSecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FreeFlow.Windows.Settings.v1");

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext),
            Entropy,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
        {
            return string.Empty;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedBase64),
                Entropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Blob written by a different user/machine, or corrupted.
            return string.Empty;
        }
    }
}
