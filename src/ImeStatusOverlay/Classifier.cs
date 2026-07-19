using System.Drawing;
using System.IO;
using System.Text.Json;

namespace ImeStatusOverlay;

public enum ImeState
{
    Unknown = 0,
    Off,    // "A"
    On      // "あ"
}

/// <summary>
/// Classifies the indicator glyph as あ (On) or A (Off) by template matching.
/// Templates are captured once via calibration and stored on disk so the app
/// works on the user's own theme/DPI.
/// </summary>
public sealed class Classifier
{
    private readonly string _storePath;

    private int _w;
    private int _h;
    private byte[]? _templateOff;   // "A"
    private byte[]? _templateOn;    // "あ"

    public bool IsCalibrated => _templateOff != null && _templateOn != null;

    public Classifier()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeStatusOverlay");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "templates.json");
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
        const double margin = 2.0;
        if (dOff + margin < dOn) return ImeState.Off;
        if (dOn + margin < dOff) return ImeState.On;
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

    private sealed class Store
    {
        public int W { get; set; }
        public int H { get; set; }
        public string? Off { get; set; }
        public string? On { get; set; }
    }

    private void Save()
    {
        try
        {
            var s = new Store
            {
                W = _w,
                H = _h,
                Off = _templateOff == null ? null : Convert.ToBase64String(_templateOff),
                On = _templateOn == null ? null : Convert.ToBase64String(_templateOn),
            };
            File.WriteAllText(_storePath, JsonSerializer.Serialize(s));
        }
        catch { /* persistence is best-effort */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storePath)) return;
            var s = JsonSerializer.Deserialize<Store>(File.ReadAllText(_storePath));
            if (s == null) return;
            _w = s.W; _h = s.H;
            _templateOff = s.Off == null ? null : Convert.FromBase64String(s.Off);
            _templateOn = s.On == null ? null : Convert.FromBase64String(s.On);
        }
        catch { /* ignore corrupt store */ }
    }
}