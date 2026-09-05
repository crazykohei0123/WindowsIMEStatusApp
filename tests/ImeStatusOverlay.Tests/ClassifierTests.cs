using System.IO;
using ImeStatusOverlay.Detection;
using ImeStatusOverlay.Recognition;

namespace ImeStatusOverlay.Tests;

public class ClassifierTests : IDisposable
{
    private readonly string _storePath;
    private readonly Classifier _classifier;

    public ClassifierTests()
    {
        _storePath = Path.Combine(
            Path.GetTempPath(),
            "ImeStatusOverlay_test_" + Guid.NewGuid().ToString("N"),
            "templates.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        _classifier = new Classifier(_storePath);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_storePath)!, true); } catch { }
    }

    [Fact]
    public void IsCalibrated_False_BeforeSetTemplates()
    {
        Assert.False(_classifier.IsCalibrated);
    }

    [Fact]
    public void Classify_BeforeCalibration_ReturnsUnknown()
    {
        Assert.Equal(ImeState.Unknown, _classifier.Classify(new byte[] { 1, 2, 3, 4 }, 2, 2));
    }

    [Fact]
    public void SetTemplates_MarksCalibrated()
    {
        _classifier.SetTemplates(new byte[] { 0, 0, 0, 0 }, new byte[] { 1, 1, 1, 1 }, 2, 2);
        Assert.True(_classifier.IsCalibrated);
    }

    [Fact]
    public void Classify_DataMatchingOffTemplate_ReturnsOff()
    {
        // High-contrast templates so the capture passes the ink gate.
        byte[] off = { 200, 200, 0, 0 };
        byte[] on = { 0, 0, 200, 200 };
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, _classifier.Classify(off, 2, 2));
    }

    [Fact]
    public void Classify_DataMatchingOnTemplate_ReturnsOn()
    {
        byte[] off = { 200, 200, 0, 0 };
        byte[] on = { 0, 0, 200, 200 };
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.On, _classifier.Classify(on, 2, 2));
    }

    [Fact]
    public void Classify_SizeMismatch_ReturnsUnknown()
    {
        _classifier.SetTemplates(new byte[] { 0, 0, 0, 0 }, new byte[] { 1, 1, 1, 1 }, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 3, 3));
    }

    [Fact]
    public void Classify_AmbiguousDistance_ReturnsUnknown()
    {
        // off and on differ by 10; data sits exactly between them so each
        // distance is 5 and neither wins by the margin (2.0).
        // Ink gate disabled to exercise the ambiguity rule in isolation.
        var c = new Classifier(_storePath, acceptanceThreshold: 20.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 10, 10, 10, 10 };
        byte[] data = { 5, 5, 5, 5 };
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_CloseToOffButWithinMarginOfOn_ReturnsUnknown()
    {
        // dOff=2, dOn=2 => ambiguous. Ink gate disabled to isolate the margin rule.
        var c = new Classifier(_storePath, acceptanceThreshold: 20.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 4, 4, 4, 4 };
        byte[] data = { 2, 2, 2, 2 };
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_DataCloseToOffTemplate_ReturnsOff()
    {
        byte[] off = { 200, 200, 0, 0 };
        byte[] on = { 0, 0, 200, 200 };
        byte[] data = { 202, 200, 2, 0 }; // dOff=1, dOn=200, ink=4/4
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_DataCloseToOnTemplate_ReturnsOn()
    {
        byte[] off = { 200, 200, 0, 0 };
        byte[] on = { 0, 0, 200, 200 };
        byte[] data = { 2, 0, 198, 200 }; // dOn=1, dOff=199, ink=4/4
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.On, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_UnknownGlyph_FarFromBothTemplates_ReturnsUnknown()
    {
        // "×" style glyph: closer to one template by margin, but far from both.
        // Uniform gray is also blank; disable the ink gate to isolate the rule.
        var c = new Classifier(_storePath, acceptanceThreshold: 20.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 200, 200, 200, 200 };
        byte[] cross = { 128, 128, 128, 128 }; // dOff=128, dOn=72, best=72 > threshold
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(cross, 2, 2));
    }

    [Theory]
    [InlineData(0)]   // all black
    [InlineData(50)]  // uniform gray
    [InlineData(255)] // all white
    public void Classify_UniformImage_ReturnsUnknown(byte value)
    {
        byte[] off = { 10, 20, 30, 40 };
        byte[] on = { 200, 210, 220, 230 };
        byte[] data = { value, value, value, value };
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_DataLengthMismatch_ReturnsUnknown()
    {
        _classifier.SetTemplates(new byte[] { 0, 0, 0, 0 }, new byte[] { 200, 200, 200, 200 }, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(new byte[] { 0, 0, 0 }, 2, 2));
    }

    [Fact]
    public void Classify_NullOrNonPositiveSize_ReturnsUnknown()
    {
        _classifier.SetTemplates(new byte[] { 0, 0, 0, 0 }, new byte[] { 200, 200, 200, 200 }, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(null!, 2, 2));
        Assert.Equal(ImeState.Unknown, _classifier.Classify(new byte[4], 0, 2));
        Assert.Equal(ImeState.Unknown, _classifier.Classify(new byte[4], 2, -1));
    }

    [Fact]
    public void Classify_AtExactAcceptanceThreshold_IsAccepted()
    {
        // Injected thresholds: acceptance=10, margin=2, ink gate off.
        var c = new Classifier(_storePath, acceptanceThreshold: 10.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 30, 30, 30, 30 };
        byte[] data = { 10, 10, 10, 10 }; // dOff=10 == threshold, dOn=20, |diff|=10 > margin
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_JustAboveAcceptanceThreshold_ReturnsUnknown()
    {
        var c = new Classifier(_storePath, acceptanceThreshold: 10.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 40, 40, 40, 40 };
        byte[] data = { 11, 11, 11, 11 }; // dOff=11 > threshold
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_AtExactSeparationMargin_ReturnsUnknown()
    {
        // Injected thresholds: acceptance=100, margin=2, ink gate off.
        var c = new Classifier(_storePath, acceptanceThreshold: 100.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 4, 4, 4, 4 };
        byte[] data = { 1, 1, 1, 1 }; // dOff=1, dOn=3, |diff|=2 == margin => ambiguous
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_JustAboveSeparationMargin_Decides()
    {
        var c = new Classifier(_storePath, acceptanceThreshold: 100.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 5, 5, 5, 5 };
        byte[] data = { 1, 1, 1, 1 }; // dOff=1, dOn=4, |diff|=3 > margin => Off
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, c.Classify(data, 2, 2));
    }

    [Fact]
    public void SetTemplates_PersistsAcrossInstances()
    {
        _classifier.SetTemplates(new byte[] { 200, 200, 0, 0 }, new byte[] { 0, 0, 200, 200 }, 2, 2);
        var reloaded = new Classifier(_storePath);
        Assert.True(reloaded.IsCalibrated);
        Assert.Equal(ImeState.Off, reloaded.Classify(new byte[] { 200, 200, 0, 0 }, 2, 2));
        Assert.Equal(ImeState.On, reloaded.Classify(new byte[] { 0, 0, 200, 200 }, 2, 2));
    }

    [Fact]
    public void Classify_CorruptStore_LoadsAsUncalibrated()
    {
        File.WriteAllText(_storePath, "not json");
        var reloaded = new Classifier(_storePath);
        Assert.False(reloaded.IsCalibrated);
    }

    // ------------------------------------------------------------------
    // Ink gate (blank/near-blank rejection)
    // ------------------------------------------------------------------

    [Fact]
    public void Classify_BlankCaptureWithinAcceptanceRadius_ReturnsUnknown()
    {
        // A flat blank capture whose distance to the OFF template sits inside
        // the acceptance radius must still be rejected by the ink gate
        // (measured on a real device: blank vs A = 17.53 with ink = 0).
        byte[] off = { 200, 50, 50, 50 };
        byte[] on = { 50, 50, 50, 220 };
        byte[] blank = { 50, 50, 50, 50 }; // dOff=37.5 <= 40, dOn=42.5, ink=0

        var gated = new Classifier(_storePath, acceptanceThreshold: 40.0, separationMargin: 2.0,
            minimumInkFraction: Classifier.DefaultMinimumInkFraction);
        gated.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, gated.Classify(blank, 2, 2));

        // Without the gate the same capture would be classified OFF, proving
        // the gate is what rejects it.
        var ungated = new Classifier(_storePath, acceptanceThreshold: 40.0, separationMargin: 2.0,
            minimumInkFraction: 0.0);
        ungated.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, ungated.Classify(blank, 2, 2));
    }

    [Fact]
    public void Classify_CaptureBelowMinimumInkFraction_ReturnsUnknown()
    {
        // 10x10 grid; the default 2% minimum requires >= 2 ink pixels.
        byte[] off = Grid(100, 200, 0, 1);                          // 2 ink px
        byte[] on = Grid(100, 0, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99); // 10 ink px
        _classifier.SetTemplates(off, on, 10, 10);

        // One ink pixel (1% < 2%): rejected even though dOff=1.0 is well
        // inside the acceptance radius and the separation is clear.
        byte[] data = Grid(100, 200, 0);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(data, 10, 10));

        // Three ink pixels (3% >= 2%): accepted and classified Off.
        byte[] dataOk = Grid(100, 200, 0, 1, 2);
        Assert.Equal(ImeState.Off, _classifier.Classify(dataOk, 10, 10));
    }

    [Fact]
    public void Classify_InkExactlyAtMinimumFraction_IsAccepted()
    {
        // Exactly 2 ink pixels on a 10x10 grid == the 2% minimum: accepted.
        byte[] off = Grid(100, 200, 0, 1);
        byte[] on = Grid(100, 0, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99);
        _classifier.SetTemplates(off, on, 10, 10);

        byte[] data = Grid(100, 200, 0, 1); // ink = 2 == minimum, dOff = 0
        Assert.Equal(ImeState.Off, _classifier.Classify(data, 10, 10));
    }

    /// <summary>Builds a 10x10 grid filled with <paramref name="bg"/>, with
    /// <paramref name="fg"/> at the given indices.</summary>
    private static byte[] Grid(byte bg, byte fg, params int[] inkIndices)
    {
        var g = new byte[100];
        Array.Fill(g, bg);
        foreach (int i in inkIndices) g[i] = fg;
        return g;
    }
}
