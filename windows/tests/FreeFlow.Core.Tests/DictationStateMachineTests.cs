using FluentAssertions;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class DictationStateMachineTests
{
    [Fact]
    public void Happy_path_sequence_is_legal()
    {
        var sm = new DictationStateMachine();
        DictationState[] path =
        {
            DictationState.Arming, DictationState.Recording, DictationState.Stopping,
            DictationState.Transcribing, DictationState.PostProcessing, DictationState.Pasting,
            DictationState.Completed, DictationState.Idle,
        };

        foreach (var next in path)
        {
            sm.TransitionTo(next);
        }

        sm.State.Should().Be(DictationState.Idle);
    }

    [Fact]
    public void Illegal_transition_throws_with_from_and_to()
    {
        var sm = new DictationStateMachine();

        var act = () => sm.TransitionTo(DictationState.Pasting);

        act.Should().Throw<InvalidStateTransitionException>()
            .Which.To.Should().Be(DictationState.Pasting);
    }

    [Theory]
    [InlineData(DictationState.Arming)]
    [InlineData(DictationState.Recording)]
    [InlineData(DictationState.Stopping)]
    [InlineData(DictationState.Transcribing)]
    [InlineData(DictationState.PostProcessing)]
    [InlineData(DictationState.Pasting)]
    public void Cancellation_is_allowed_from_any_active_state(DictationState active)
    {
        var sm = DriveTo(active);

        sm.Cancel();

        sm.State.Should().Be(DictationState.Cancelled);
        sm.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Cannot_cancel_when_idle()
    {
        var sm = new DictationStateMachine();

        var act = () => sm.Cancel();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Only_one_session_can_be_active_at_a_time()
    {
        var sm = DriveTo(DictationState.Recording);

        var act = () => sm.TransitionTo(DictationState.Arming);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Transitioned_event_reports_previous_and_next()
    {
        var sm = new DictationStateMachine();
        (DictationState Previous, DictationState Next)? seen = null;
        sm.Transitioned += (p, n) => seen = (p, n);

        sm.TransitionTo(DictationState.Arming);

        seen.Should().Be((DictationState.Idle, DictationState.Arming));
    }

    private static DictationStateMachine DriveTo(DictationState target)
    {
        var sm = new DictationStateMachine();
        DictationState[] order =
        {
            DictationState.Arming, DictationState.Recording, DictationState.Stopping,
            DictationState.Transcribing, DictationState.PostProcessing, DictationState.Pasting,
        };

        foreach (var next in order)
        {
            sm.TransitionTo(next);
            if (next == target)
            {
                break;
            }
        }

        return sm;
    }
}
