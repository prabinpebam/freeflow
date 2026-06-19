using FluentAssertions;
using FreeFlow.Core.Updates;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class SemanticVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("0.1.0", 0, 1, 0)]
    [InlineData("2.0", 2, 0, 0)]
    [InlineData("3", 3, 0, 0)]
    [InlineData("1.2.3+build.7", 1, 2, 3)]
    public void Parses_core_numbers(string text, int major, int minor, int patch)
    {
        var v = SemanticVersion.Parse(text);
        v.Major.Should().Be(major);
        v.Minor.Should().Be(minor);
        v.Patch.Should().Be(patch);
        v.IsPreRelease.Should().BeFalse();
    }

    [Fact]
    public void Parses_prerelease_tag()
    {
        var v = SemanticVersion.Parse("1.0.0-beta.2");
        v.IsPreRelease.Should().BeTrue();
        v.PreRelease.Should().Equal("beta", "2");
        v.ToString().Should().Be("1.0.0-beta.2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.x")]
    [InlineData("1.0.0-")]
    public void Rejects_invalid(string text)
    {
        SemanticVersion.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Higher_core_is_greater()
    {
        SemanticVersion.Parse("1.2.4").CompareTo(SemanticVersion.Parse("1.2.3")).Should().BePositive();
        SemanticVersion.Parse("1.3.0").CompareTo(SemanticVersion.Parse("1.2.9")).Should().BePositive();
        SemanticVersion.Parse("2.0.0").CompareTo(SemanticVersion.Parse("1.9.9")).Should().BePositive();
    }

    [Fact]
    public void Prerelease_is_lower_than_release()
    {
        SemanticVersion.Parse("1.0.0-beta").CompareTo(SemanticVersion.Parse("1.0.0")).Should().BeNegative();
        SemanticVersion.Parse("1.0.0").CompareTo(SemanticVersion.Parse("1.0.0-beta")).Should().BePositive();
    }

    [Fact]
    public void Prerelease_precedence_follows_semver()
    {
        // 1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta < 1.0.0-beta.2 < 1.0.0-beta.11
        SemanticVersion.Parse("1.0.0-alpha").CompareTo(SemanticVersion.Parse("1.0.0-alpha.1")).Should().BeNegative();
        SemanticVersion.Parse("1.0.0-alpha.1").CompareTo(SemanticVersion.Parse("1.0.0-alpha.beta")).Should().BeNegative();
        SemanticVersion.Parse("1.0.0-alpha.beta").CompareTo(SemanticVersion.Parse("1.0.0-beta")).Should().BeNegative();
        SemanticVersion.Parse("1.0.0-beta.2").CompareTo(SemanticVersion.Parse("1.0.0-beta.11")).Should().BeNegative();
    }

    [Fact]
    public void Equal_versions_compare_equal()
    {
        SemanticVersion.Parse("1.2.3").CompareTo(SemanticVersion.Parse("1.2.3")).Should().Be(0);
        SemanticVersion.Parse("1.0.0-beta.1").CompareTo(SemanticVersion.Parse("1.0.0-beta.1")).Should().Be(0);
    }

    [Fact]
    public void Build_metadata_is_ignored_for_precedence()
    {
        SemanticVersion.Parse("1.2.3+a").CompareTo(SemanticVersion.Parse("1.2.3+b")).Should().Be(0);
    }
}
