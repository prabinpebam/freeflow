using System.Globalization;

namespace FreeFlow.Core.Updates;

/// <summary>
/// A pragmatic SemVer 2.0 implementation covering what the updater needs:
/// <c>MAJOR.MINOR.PATCH</c> with an optional dot-separated pre-release tag and an
/// ignored build-metadata suffix. Comparison follows the SemVer precedence rules
/// (a pre-release version is lower than its associated release). Parsing/compare
/// are pure so update decisions are asserted deterministically in the inner loop.
/// </summary>
public sealed record SemanticVersion : IComparable<SemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public IReadOnlyList<string> PreRelease { get; }

    public bool IsPreRelease => PreRelease.Count > 0;

    private SemanticVersion(int major, int minor, int patch, IReadOnlyList<string> preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public static SemanticVersion Parse(string text)
        => TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a valid semantic version.");

    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        // Strip build metadata (everything after '+'); it has no precedence effect.
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string[] pre = Array.Empty<string>();
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            var preText = value[(dash + 1)..];
            value = value[..dash];
            pre = preText.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (pre.Length == 0)
            {
                return false;
            }
        }

        var parts = value.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0)
            {
                return false;
            }

            numbers[i] = n;
        }

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], pre);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;

        // Equal core: a version WITH pre-release is lower than one without.
        if (IsPreRelease && !other.IsPreRelease) return -1;
        if (!IsPreRelease && other.IsPreRelease) return 1;
        if (!IsPreRelease && !other.IsPreRelease) return 0;

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    private static int ComparePreRelease(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var count = Math.Min(a.Count, b.Count);
        for (var i = 0; i < count; i++)
        {
            var an = int.TryParse(a[i], out var ai);
            var bn = int.TryParse(b[i], out var bi);

            int cmp;
            if (an && bn)
            {
                cmp = ai.CompareTo(bi); // numeric identifiers compare numerically
            }
            else if (an != bn)
            {
                cmp = an ? -1 : 1; // numeric identifiers are lower than alphanumeric
            }
            else
            {
                cmp = string.CompareOrdinal(a[i], b[i]);
            }

            if (cmp != 0)
            {
                return Math.Sign(cmp);
            }
        }

        // More identifiers => higher precedence when the common prefix is equal.
        return a.Count.CompareTo(b.Count);
    }

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return IsPreRelease ? $"{core}-{string.Join('.', PreRelease)}" : core;
    }
}
