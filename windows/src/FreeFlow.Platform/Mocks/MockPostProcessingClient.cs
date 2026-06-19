using System.Globalization;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.Platform.Mocks;

/// <summary>
/// Phase-2 placeholder for the LLM cleanup client. Applies a deterministic,
/// offline "cleanup" (trim, capitalize, terminal period) so the demo shows the
/// post-processing stage doing visible work. Replaced by a real chat-completions
/// client in Phase 3.
/// </summary>
public sealed class MockPostProcessingClient : IPostProcessingClient
{
    public Task<string> CleanupAsync(PostProcessingRequest request, CancellationToken ct = default)
    {
        var text = request.RawTranscript.Trim();
        if (text.Length == 0)
        {
            return Task.FromResult(string.Empty);
        }

        text = char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
        if (!text.EndsWith('.') && !text.EndsWith('!') && !text.EndsWith('?'))
        {
            text += ".";
        }

        return Task.FromResult(text);
    }
}
