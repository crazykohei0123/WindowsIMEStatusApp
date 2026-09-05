using ImeStatusOverlay.Detection;

namespace ImeStatusOverlay.Tests;

public class ImeStateTrackerTests
{
    [Fact]
    public void Committed_InitiallyUnknown()
    {
        var t = new ImeStateTracker();
        Assert.Equal(ImeState.Unknown, t.Committed);
    }

    [Fact]
    public void OnSample_Unknown_ReturnsFalseWithoutCommit()
    {
        var t = new ImeStateTracker();
        Assert.False(t.OnSample(ImeState.Unknown, out var s));
        Assert.Equal(ImeState.Unknown, s);
        Assert.Equal(ImeState.Unknown, t.Committed);
    }

    [Fact]
    public void OnSample_FirstOccurrenceOfNewState_DoesNotCommitYet()
    {
        var t = new ImeStateTracker();
        Assert.False(t.OnSample(ImeState.On, out _));
        Assert.Equal(ImeState.Unknown, t.Committed);
    }

    [Fact]
    public void OnSample_SecondConsecutiveOccurrence_Commits()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.On, out _);
        Assert.True(t.OnSample(ImeState.On, out var committed));
        Assert.Equal(ImeState.On, committed);
        Assert.Equal(ImeState.On, t.Committed);
    }

    [Fact]
    public void OnSample_SampleMatchingCommitted_ReturnsFalseNoCommit()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.On, out _);
        t.OnSample(ImeState.On, out _); // commit
        Assert.False(t.OnSample(ImeState.On, out _));
        Assert.Equal(ImeState.On, t.Committed);
    }

    [Fact]
    public void OnSample_UnknownMidRunup_ResetsCandidate()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.On, out _);               // candidate=On, count=1
        Assert.False(t.OnSample(ImeState.Unknown, out _)); // resets candidate
        Assert.False(t.OnSample(ImeState.On, out _));      // back to count=1
        Assert.Equal(ImeState.Unknown, t.Committed);
    }

    [Fact]
    public void OnSample_AlternatingStates_NeverCommits()
    {
        var t = new ImeStateTracker();
        foreach (var i in Enumerable.Range(0, 5))
        {
            Assert.False(t.OnSample(ImeState.On, out _));
            Assert.False(t.OnSample(ImeState.Off, out _));
        }
        Assert.Equal(ImeState.Unknown, t.Committed);
    }

    [Fact]
    public void OnSample_TransitionsFromCommittedOnToOffAfterTwo()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.On, out _);
        t.OnSample(ImeState.On, out _);               // commit On
        Assert.False(t.OnSample(ImeState.Off, out _)); // candidate=Off, count=1
        Assert.True(t.OnSample(ImeState.Off, out var committed)); // commit Off
        Assert.Equal(ImeState.Off, committed);
        Assert.Equal(ImeState.Off, t.Committed);
    }

    [Fact]
    public void OnSample_UnknownWhileCommitted_DoesNotChangeOrShow()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.Off, out _);
        t.OnSample(ImeState.Off, out _); // commit Off
        for (int i = 0; i < 3; i++)
        {
            Assert.False(t.OnSample(ImeState.Unknown, out _));
            Assert.Equal(ImeState.Off, t.Committed);
        }
    }

    [Fact]
    public void OnSample_ReturnToSameCommittedAfterUnknown_DoesNotReshow()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.Off, out _);
        t.OnSample(ImeState.Off, out _); // commit Off
        t.OnSample(ImeState.Unknown, out _);
        // Back to Off: same as committed => no commit, no re-show.
        Assert.False(t.OnSample(ImeState.Off, out _));
        Assert.Equal(ImeState.Off, t.Committed);
    }

    [Fact]
    public void Reset_ClearsCommittedAndCandidate()
    {
        var t = new ImeStateTracker();
        t.OnSample(ImeState.On, out _);
        t.OnSample(ImeState.On, out _);               // commit
        t.Reset();
        Assert.Equal(ImeState.Unknown, t.Committed);
        // After reset the first On read must not immediately commit.
        Assert.False(t.OnSample(ImeState.On, out _));
        Assert.Equal(ImeState.Unknown, t.Committed);
    }
}
