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
        byte[] off = { 10, 20, 30, 40 };
        byte[] on = { 200, 210, 220, 230 };
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, _classifier.Classify(off, 2, 2));
    }

    [Fact]
    public void Classify_DataMatchingOnTemplate_ReturnsOn()
    {
        byte[] off = { 10, 20, 30, 40 };
        byte[] on = { 200, 210, 220, 230 };
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
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 10, 10, 10, 10 };
        byte[] data = { 5, 5, 5, 5 };
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_CloseToOffButWithinMarginOfOn_ReturnsUnknown()
    {
        // dOff=0, dOn=2 => dOn + margin(2) = 4, dOff=0: 0+2 < 2? no. 2+2 < 0? no => Unknown.
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 4, 4, 4, 4 };
        byte[] data = { 2, 2, 2, 2 }; // dOff=2, dOn=2 => ambiguous
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_DataCloseToOffTemplate_ReturnsOff()
    {
        byte[] off = { 10, 20, 30, 40 };
        byte[] on = { 200, 210, 220, 230 };
        byte[] data = { 12, 22, 32, 42 }; // dOff=2, dOn=188
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_DataCloseToOnTemplate_ReturnsOn()
    {
        byte[] off = { 10, 20, 30, 40 };
        byte[] on = { 200, 210, 220, 230 };
        byte[] data = { 198, 208, 218, 228 }; // dOn=2, dOff=188
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.On, _classifier.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_UnknownGlyph_FarFromBothTemplates_ReturnsUnknown()
    {
        // "×" style glyph: closer to one template by margin, but far from both.
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 200, 200, 200, 200 };
        byte[] cross = { 128, 128, 128, 128 }; // dOff=128, dOn=72, best=72 > threshold
        _classifier.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, _classifier.Classify(cross, 2, 2));
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
        // Injected thresholds: acceptance=10, margin=2.
        var c = new Classifier(_storePath, acceptanceThreshold: 10.0, separationMargin: 2.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 30, 30, 30, 30 };
        byte[] data = { 10, 10, 10, 10 }; // dOff=10 == threshold, dOn=20, |diff|=10 > margin
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_JustAboveAcceptanceThreshold_ReturnsUnknown()
    {
        var c = new Classifier(_storePath, acceptanceThreshold: 10.0, separationMargin: 2.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 40, 40, 40, 40 };
        byte[] data = { 11, 11, 11, 11 }; // dOff=11 > threshold
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_AtExactSeparationMargin_ReturnsUnknown()
    {
        // Injected thresholds: acceptance=100, margin=2.
        var c = new Classifier(_storePath, acceptanceThreshold: 100.0, separationMargin: 2.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 4, 4, 4, 4 };
        byte[] data = { 1, 1, 1, 1 }; // dOff=1, dOn=3, |diff|=2 == margin => ambiguous
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Unknown, c.Classify(data, 2, 2));
    }

    [Fact]
    public void Classify_JustAboveSeparationMargin_Decides()
    {
        var c = new Classifier(_storePath, acceptanceThreshold: 100.0, separationMargin: 2.0);
        byte[] off = { 0, 0, 0, 0 };
        byte[] on = { 5, 5, 5, 5 };
        byte[] data = { 1, 1, 1, 1 }; // dOff=1, dOn=4, |diff|=3 > margin => Off
        c.SetTemplates(off, on, 2, 2);
        Assert.Equal(ImeState.Off, c.Classify(data, 2, 2));
    }

    [Fact]
    public void SetTemplates_PersistsAcrossInstances()
    {
        _classifier.SetTemplates(new byte[] { 10, 20, 30, 40 }, new byte[] { 200, 210, 220, 230 }, 2, 2);
        var reloaded = new Classifier(_storePath);
        Assert.True(reloaded.IsCalibrated);
        Assert.Equal(ImeState.Off, reloaded.Classify(new byte[] { 10, 20, 30, 40 }, 2, 2));
        Assert.Equal(ImeState.On, reloaded.Classify(new byte[] { 200, 210, 220, 230 }, 2, 2));
    }

    [Fact]
    public void Classify_CorruptStore_LoadsAsUncalibrated()
    {
        File.WriteAllText(_storePath, "not json");
        var reloaded = new Classifier(_storePath);
        Assert.False(reloaded.IsCalibrated);
    }
}
