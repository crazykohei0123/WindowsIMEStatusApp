namespace ImeStatusOverlay.Detection;

/// <summary>
/// Debounces raw IME samples into a committed state. A new state must be
/// observed <see cref="StableCountRequired"/> times in a row before it is
/// committed, suppressing flicker while the glyph is mid-animation/transition.
/// </summary>
public sealed class ImeStateTracker
{
    private const int StableCountRequired = 2;

    private ImeState _committed = ImeState.Unknown;
    private ImeState _candidate = ImeState.Unknown;
    private int _candidateCount;

    /// <summary>The last committed (stable) state.</summary>
    public ImeState Committed => _committed;

    /// <summary>
    /// Feeds a fresh sample. Returns <c>true</c> (with
    /// <paramref name="committedState"/>) when a new state becomes stable;
    /// the caller should then present the overlay.
    /// </summary>
    public bool OnSample(ImeState sample, out ImeState committedState)
    {
        committedState = _committed;

        // Ambiguous/size-mismatched samples never change the committed state.
        if (sample == ImeState.Unknown)
        {
            _candidate = ImeState.Unknown;
            _candidateCount = 0;
            return false;
        }

        // No change since the last committed state.
        if (sample == _committed)
        {
            _candidate = ImeState.Unknown;
            _candidateCount = 0;
            return false;
        }

        if (sample == _candidate)
            _candidateCount++;
        else
        {
            _candidate = sample;
            _candidateCount = 1;
        }

        if (_candidateCount >= StableCountRequired)
        {
            _committed = sample;
            committedState = sample;
            _candidate = ImeState.Unknown;
            _candidateCount = 0;
            return true;
        }

        return false;
    }

    /// <summary>Clears all committed/candidate state (e.g. after recalibration).</summary>
    public void Reset()
    {
        _committed = ImeState.Unknown;
        _candidate = ImeState.Unknown;
        _candidateCount = 0;
    }
}
