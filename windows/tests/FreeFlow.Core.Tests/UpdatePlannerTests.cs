using FluentAssertions;
using FreeFlow.Core.Updates;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class UpdatePlannerTests
{
    private static ReleaseInfo Rel(string version)
        => new(SemanticVersion.Parse(version), $"https://example/{version}");

    [Fact]
    public void No_releases_means_no_update()
    {
        var decision = UpdatePlanner.Plan(SemanticVersion.Parse("1.0.0"), Array.Empty<ReleaseInfo>());
        decision.Should().Be(UpdateDecision.None);
        decision.UpdateAvailable.Should().BeFalse();
        decision.Target.Should().BeNull();
    }

    [Fact]
    public void Picks_highest_newer_stable()
    {
        var decision = UpdatePlanner.Plan(
            SemanticVersion.Parse("1.0.0"),
            new[] { Rel("1.0.1"), Rel("1.2.0"), Rel("1.1.5") });

        decision.UpdateAvailable.Should().BeTrue();
        decision.Target!.Version.ToString().Should().Be("1.2.0");
    }

    [Fact]
    public void Equal_or_lower_versions_are_not_offered()
    {
        var decision = UpdatePlanner.Plan(
            SemanticVersion.Parse("2.0.0"),
            new[] { Rel("1.9.9"), Rel("2.0.0") });

        decision.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void Stable_channel_ignores_prereleases()
    {
        var decision = UpdatePlanner.Plan(
            SemanticVersion.Parse("1.0.0"),
            new[] { Rel("1.1.0-beta.1"), Rel("2.0.0-rc.1") },
            ReleaseChannel.Stable);

        decision.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void Beta_channel_accepts_prereleases()
    {
        var decision = UpdatePlanner.Plan(
            SemanticVersion.Parse("1.0.0"),
            new[] { Rel("1.1.0-beta.1"), Rel("1.0.5") },
            ReleaseChannel.Beta);

        decision.UpdateAvailable.Should().BeTrue();
        decision.Target!.Version.ToString().Should().Be("1.1.0-beta.1");
    }

    [Fact]
    public void Beta_channel_still_prefers_a_higher_stable()
    {
        var decision = UpdatePlanner.Plan(
            SemanticVersion.Parse("1.0.0"),
            new[] { Rel("1.1.0-beta.1"), Rel("1.1.0") },
            ReleaseChannel.Beta);

        decision.Target!.Version.ToString().Should().Be("1.1.0");
    }
}
