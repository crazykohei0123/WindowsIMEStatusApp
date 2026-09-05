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

    // Tuned from real-device samples (scripts/watch-indicator.ps1; 24x24 region,
    // Google 日本語入力, dark taskbar, 3840x2160 @150%):
    //   normal A variants : dOff = 0.00 / 8.23 / 19.36 (hover highlight)
    //   normal あ         : dOn  = 13.83
    //   unknown glyphs    : best >= 26.21 (low-contrast "×" while IME disabled)
    //   blank captures    : dOff = 17.53 with ink = 0 -> rejected by the ink
    //                       gate below, not by this threshold (the blank sits
    //                       closer to the A template than the hovered A does,
    //                       so distance alone cannot separate them).
    // 22.0 keeps headroom above the normal max (19.36) and below the unknown
    // min (26.21). Re-measure via the script when theme/DPI changes.
    internal const double DefaultAcceptanceThreshold = 22.0;

    // A capture whose foreground ink covers less than this fraction of the
    // region is blank/near-blank (input switched away, IME disabled, taskbar
    // hidden). Its flat background can still fall inside the acceptance radius
    // of a template (measured: blank vs A = 17.53), so distance alone cannot
    // reject it. Real あ/A glyphs measure >= 9.4% ink.
    internal const double DefaultMinimumInkFraction = 0.02;

    private readonly JsonStore<TemplateModel> _store;
    private readonly double _acceptanceThreshold;
    private readonly double _separationMargin;
    private readonly double _minimumInkFraction;

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
        : this(storePath, acceptanceThreshold, separationMargin, DefaultMinimumInkFraction)
    {
    }

    internal Classifier(string storePath, double acceptanceThreshold, double separationMargin,
        double minimumInkFraction)
    {
        _store = new JsonStore<TemplateModel>(storePath);
        _acceptanceThreshold = acceptanceThreshold;
        _separationMargin = separationMargin;
        _minimumInkFraction = minimumInkFraction;
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

        // Blank/near-blank captures (input switched away, IME disabled, hidden
        // taskbar) must never be classified even when their flat background
        // happens to fall inside the acceptance radius of a template.
        Glyph.Signature(data, w, h, out int ink);
        if (ink < _minimumInkFraction * data.Length)
            return ImeState.Unknown;

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
