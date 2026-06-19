namespace FreeFlow.Core.Pipeline;

/// <summary>
/// Explicit, guarded state machine for a single dictation session.
/// Every transition is validated; illegal transitions throw.
/// </summary>
public sealed class DictationStateMachine
{
    private static readonly IReadOnlyDictionary<DictationState, DictationState[]> Allowed =
        new Dictionary<DictationState, DictationState[]>
        {
            [DictationState.Idle] = new[] { DictationState.Arming },
            [DictationState.Arming] = new[] { DictationState.Recording, DictationState.Cancelled, DictationState.Error },
            [DictationState.Recording] = new[] { DictationState.Stopping, DictationState.Cancelled, DictationState.Error },
            [DictationState.Stopping] = new[] { DictationState.Transcribing, DictationState.Cancelled, DictationState.Error },
            [DictationState.Transcribing] = new[] { DictationState.PostProcessing, DictationState.Cancelled, DictationState.Error },
            [DictationState.PostProcessing] = new[] { DictationState.Pasting, DictationState.Cancelled, DictationState.Error },
            [DictationState.Pasting] = new[] { DictationState.Completed, DictationState.Cancelled, DictationState.Error },
            [DictationState.Completed] = new[] { DictationState.Idle },
            [DictationState.Cancelled] = new[] { DictationState.Idle },
            [DictationState.Error] = new[] { DictationState.Idle },
        };

    public DictationState State { get; private set; } = DictationState.Idle;

    /// <summary>Raised after a successful transition with (previous, next).</summary>
    public event Action<DictationState, DictationState>? Transitioned;

    public bool IsTerminal =>
        State is DictationState.Completed or DictationState.Cancelled or DictationState.Error;

    public bool IsActive =>
        State is not DictationState.Idle && !IsTerminal;

    public bool CanTransitionTo(DictationState next) => Allowed[State].Contains(next);

    public void TransitionTo(DictationState next)
    {
        if (!CanTransitionTo(next))
        {
            throw new InvalidStateTransitionException(State, next);
        }

        var previous = State;
        State = next;
        Transitioned?.Invoke(previous, next);
    }

    /// <summary>Cancel from any active state. Illegal from Idle/terminal states.</summary>
    public void Cancel() => TransitionTo(DictationState.Cancelled);

    /// <summary>Return a terminal session to Idle so a new session can start.</summary>
    public void Reset()
    {
        if (IsTerminal)
        {
            TransitionTo(DictationState.Idle);
        }
    }
}
