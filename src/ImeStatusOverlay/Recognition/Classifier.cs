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
    private const double SeparationMargin = 2.0;

    // ponytail: single fixed threshold pending real-device distance samples;
    // tune via scripts/watch-indicator.ps1, split per-template only if A/あ jitter diverges.
    internal const double DefaultAcceptanceThreshold = 20.0;

    private readonly JsonStore<TemplateModel> _store;
    private readonly double _acceptanceThreshold;
    private readonly double _separationMargin;

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
        : this(storePath, DefaultAcceptanceThreshold, SeparationMargin)
    {
    }

    internal Classifier(string storePath, double acceptanceThreshold, double separationMargin)
    {
        _store = new JsonStore<TemplateModel>(storePath);
        _acceptanceThreshold = acceptanceThreshold;
        _separationMargin = separationMargin;
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
    /// Classifies the given grayscale capture. Returns Unknown when the glyph
    /// is not close enough to either learned template (e.g. "×", blank, other
    /// icon) or cannot be compared.
    /// </summary>
    public ImeState Classify(byte[] data, int w, int h)
    {
        if (!IsCalibrated)
            return ImeState.Unknown;

        // Defensive validation: malformed captures must never be classified.
        if (data == null ||
            w <= 0 || h <= 0 ||
            w != _w || h != _h ||
            data.Length != w * h ||
            _templateOff!.Length != data.Length ||
            _templateOn!.Length != data.Length)
        {
            return ImeState.Unknown;
        }

        double dOff = MeanAbsDiff(data, _templateOff!);
        double dOn = MeanAbsDiff(data, _templateOn!);
        double bestDistance = Math.Min(dOff, dOn);

        // Reject glyphs far from BOTH templates (unknown indicator, not IME state).
        if (bestDistance > _acceptanceThreshold)
            return ImeState.Unknown;

        // Require a clear separation between the two candidates.
        if (Math.Abs(dOff - dOn) <= _separationMargin)
            return ImeState.Unknown;

        return dOff < dOn ? ImeState.Off : ImeState.On;
    }

    /// <summary>Mean absolute difference (0..255 per pixel) between two buffers.</summary>
    internal static double MeanAbsDiff(byte[] a, byte[] b)
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
