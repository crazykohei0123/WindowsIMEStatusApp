using System.IO;
using ImeStatusOverlay.Detection;
using ImeStatusOverlay.Storage;

namespace ImeStatusOverlay.Recognition;

/// <summary>
/// Classifies the indicator glyph as あ (On) or A (Off) by template matching.
/// Templates are captured once via calibration and stored on disk so the app
/// works on the user's own theme/DPI.
/// </summary>
public sealed class Classifier
{
    private const double DecisionMargin = 2.0;

    private readonly JsonStore<TemplateModel> _store;

    private int _w;
    private int _h;
    private byte[]? _templateOff;   // "A"
    private byte[]? _templateOn;    // "あ"

    public bool IsCalibrated => _templateOff != null && _templateOn != null;

    public Classifier()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeStatusOverlay",
            "templates.json"))
    {
    }

    internal Classifier(string storePath)
    {
        _store = new JsonStore<TemplateModel>(storePath);
        Load();
    }

    /// <summary>Stores the two learned templates and persists them.</summary>
    public void SetTemplates(byte[] off, byte[] on, int w, int h)
    {
        _templateOff = off;
        _templateOn = on;
        _w = w;
        _h = h;
        Save();
    }

    /// <summary>
    /// Classifies the given grayscale capture. Returns Unknown when it cannot
    /// decide confidently.
    /// </summary>
    public ImeState Classify(byte[] data, int w, int h)
    {
        if (!IsCalibrated)
            return ImeState.Unknown;

        // If the capture size changed (e.g., DPI/taskbar change), we cannot compare.
        if (w != _w || h != _h)
            return ImeState.Unknown;

        double dOff = MeanAbsDiff(data, _templateOff!);
        double dOn = MeanAbsDiff(data, _templateOn!);

        // Pick the closer template. Require a small margin to avoid flicker
        // when the glyph is mid-animation or ambiguous.
        if (dOff + DecisionMargin < dOn) return ImeState.Off;
        if (dOn + DecisionMargin < dOff) return ImeState.On;
        return ImeState.Unknown;
    }

    private static double MeanAbsDiff(byte[] a, byte[] b)
    {
        long sum = 0;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
            sum += Math.Abs(a[i] - b[i]);
        return n == 0 ? double.MaxValue : (double)sum / n;
    }

    // ------------------------------------------------------------------
    // Persistence
    // ------------------------------------------------------------------

    private void Save()
    {
        _store.Save(new TemplateModel
        {
            W = _w,
            H = _h,
            Off = _templateOff == null ? null : Convert.ToBase64String(_templateOff),
            On = _templateOn == null ? null : Convert.ToBase64String(_templateOn),
        });
    }

    private void Load()
    {
        var m = _store.Load();
        if (m == null) return;
        _w = m.W;
        _h = m.H;
        _templateOff = m.Off == null ? null : Convert.FromBase64String(m.Off);
        _templateOn = m.On == null ? null : Convert.FromBase64String(m.On);
    }

    private sealed class TemplateModel
    {
        public int W { get; set; }
        public int H { get; set; }
        public string? Off { get; set; }
        public string? On { get; set; }
    }
}
