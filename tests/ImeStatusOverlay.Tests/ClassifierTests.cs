using System.IO;
using ImeStatusOverlay;

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
