using ImeStatusOverlay.Detection;

namespace ImeStatusOverlay.Recognition;

/// <summary>A distinct glyph pattern observed during calibration sampling.</summary>
/// <param name="Signature">Binary signature (see <see cref="Glyph.Signature"/>).</param>
/// <param name="Gray">Grayscale capture of the pattern.</param>
/// <param name="Ink">Foreground pixel count (あ has more ink than A).</param>
/// <param name="Count">How many times the pattern was observed.</param>
public sealed record CalibrationSample(string Signature, byte[] Gray, int Ink, int Count);

/// <summary>The two templates selected from calibration samples.</summary>
public sealed record CalibrationSelection(CalibrationSample Off, CalibrationSample On);

/// <summary>Why a calibration attempt was rejected.</summary>
public enum CalibrationRejection
{
    None = 0,

    /// <summary>Fewer than two distinct glyph patterns were observed.</summary>
    NotEnoughPatterns,

    /// <summary>One of the top two patterns was not observed often enough.</summary>
    InsufficientOccurrences,

    /// <summary>The two candidate templates are too similar to tell apart.</summary>
    TemplatesTooClose,
}

/// <summary>
/// Pure-logic validation of calibration samples. Guards against mis-learning:
/// transient glyphs (mid-animation, "×", blank) must not become templates, and
/// the two accepted templates must be distinct enough for runtime matching.
/// </summary>
public static class CalibrationValidator
{
    /// <summary>Each of the two patterns must be seen at least this many times.</summary>
    public const int MinimumOccurrences = 3;

    /// <summary>
    /// The two templates must be farther apart (mean absolute difference) than
    /// the classifier's acceptance radius. Within that radius the two states
    /// are indistinguishable in practice, which usually means calibration
    /// captured two renderings of the same glyph instead of あ and A.
    /// </summary>
    public const double MinimumTemplateSeparation = Classifier.DefaultAcceptanceThreshold;

    /// <summary>
    /// Picks the OFF ("A") and ON ("あ") templates from the observed patterns:
    /// the two most frequent patterns, each seen at least
    /// <see cref="MinimumOccurrences"/> times, labeled by ink amount, and
    /// sufficiently separated from each other.
    /// </summary>
    public static bool TrySelect(
        IReadOnlyList<CalibrationSample> samples,
        out CalibrationSelection? selection,
        out CalibrationRejection rejection)
    {
        selection = null;
        rejection = CalibrationRejection.None;

        if (samples == null || samples.Count < 2)
        {
            rejection = CalibrationRejection.NotEnoughPatterns;
            return false;
        }

        var top = samples.OrderByDescending(s => s.Count).Take(2).ToList();
        if (top.Any(s => s.Count < MinimumOccurrences))
        {
            rejection = CalibrationRejection.InsufficientOccurrences;
            return false;
        }

        // あ (ON) has more ink than A (OFF).
        var ordered = top.OrderByDescending(s => s.Ink).ToList();
        var on = ordered[0];
        var off = ordered[1];

        if (Classifier.MeanAbsDiff(off.Gray, on.Gray) <= MinimumTemplateSeparation)
        {
            rejection = CalibrationRejection.TemplatesTooClose;
            return false;
        }

        selection = new CalibrationSelection(off, on);
        return true;
    }
}