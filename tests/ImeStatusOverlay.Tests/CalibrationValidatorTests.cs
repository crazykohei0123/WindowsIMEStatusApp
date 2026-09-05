using ImeStatusOverlay.Recognition;

namespace ImeStatusOverlay.Tests;

public class CalibrationValidatorTests
{
    private static CalibrationSample Sample(
        string signature, byte[] gray, int ink, int count)
        => new(signature, gray, ink, count);

    // Off ("A") and On ("あ") templates differing by a mean absolute
    // difference well above MinimumTemplateSeparation (20.0).
    private static readonly byte[] OffGray = { 0, 0, 0, 0 };
    private static readonly byte[] OnGray = { 90, 90, 90, 90 };

    [Fact]
    public void TrySelect_NullSamples_RejectsNotEnoughPatterns()
    {
        Assert.False(CalibrationValidator.TrySelect(
            null!, out var selection, out var rejection));
        Assert.Null(selection);
        Assert.Equal(CalibrationRejection.NotEnoughPatterns, rejection);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TrySelect_FewerThanTwoSamples_RejectsNotEnoughPatterns(int count)
    {
        var samples = Enumerable.Range(0, count)
            .Select(i => Sample($"s{i}", OffGray, ink: 10, count: 5))
            .ToList();

        Assert.False(CalibrationValidator.TrySelect(samples, out var selection, out var rejection));
        Assert.Null(selection);
        Assert.Equal(CalibrationRejection.NotEnoughPatterns, rejection);
    }

    [Fact]
    public void TrySelect_TwoWellSeparatedFrequentPatterns_SelectsByInk()
    {
        var off = Sample("A", OffGray, ink: 10, count: 20);
        var on = Sample("あ", OnGray, ink: 50, count: 15);

        Assert.True(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { off, on }, out var selection, out var rejection));
        Assert.Equal(CalibrationRejection.None, rejection);
        Assert.NotNull(selection);
        Assert.Same(off, selection!.Off); // less ink => OFF
        Assert.Same(on, selection.On);    // more ink => ON
    }

    [Fact]
    public void TrySelect_RareNoisyPattern_IsIgnored()
    {
        // A transient glyph seen only once must not become a template even
        // though it has the most ink.
        var noise = Sample("transition", new byte[] { 200, 200, 200, 200 }, ink: 99, count: 1);
        var off = Sample("A", OffGray, ink: 10, count: 20);
        var on = Sample("あ", OnGray, ink: 50, count: 15);

        Assert.True(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { noise, off, on }, out var selection, out _));
        Assert.Same(off, selection!.Off);
        Assert.Same(on, selection.On);
    }

    [Fact]
    public void TrySelect_PatternSeenTooFewTimes_RejectsInsufficientOccurrences()
    {
        var off = Sample("A", OffGray, ink: 10, count: 20);
        var on = Sample("あ", OnGray, ink: 50, count: 2); // below MinimumOccurrences

        Assert.False(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { off, on }, out var selection, out var rejection));
        Assert.Null(selection);
        Assert.Equal(CalibrationRejection.InsufficientOccurrences, rejection);
    }

    [Fact]
    public void TrySelect_ExactlyMinimumOccurrences_Succeeds()
    {
        var off = Sample("A", OffGray, ink: 10, count: CalibrationValidator.MinimumOccurrences);
        var on = Sample("あ", OnGray, ink: 50, count: CalibrationValidator.MinimumOccurrences);

        Assert.True(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { off, on }, out var selection, out var rejection));
        Assert.Equal(CalibrationRejection.None, rejection);
        Assert.NotNull(selection);
    }

    [Fact]
    public void TrySelect_IdenticalTemplates_RejectsTemplatesTooClose()
    {
        // Two variants of the same glyph (distance 0) must not be learned.
        var a = Sample("A1", OffGray, ink: 10, count: 20);
        var b = Sample("A2", OffGray, ink: 11, count: 15);

        Assert.False(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { a, b }, out var selection, out var rejection));
        Assert.Null(selection);
        Assert.Equal(CalibrationRejection.TemplatesTooClose, rejection);
    }

    [Fact]
    public void TrySelect_TemplatesAtExactSeparation_RejectsTemplatesTooClose()
    {
        // Mean absolute difference == MinimumTemplateSeparation (20.0): rejected.
        var off = Sample("A", OffGray, ink: 10, count: 20);
        var on = Sample("あ", new byte[] { 20, 20, 20, 20 }, ink: 50, count: 15);

        Assert.False(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { off, on }, out var selection, out var rejection));
        Assert.Null(selection);
        Assert.Equal(CalibrationRejection.TemplatesTooClose, rejection);
    }

    [Fact]
    public void TrySelect_TemplatesJustAboveSeparation_Succeeds()
    {
        // Mean absolute difference 21 > MinimumTemplateSeparation (20.0): accepted.
        var off = Sample("A", OffGray, ink: 10, count: 20);
        var on = Sample("あ", new byte[] { 21, 21, 21, 21 }, ink: 50, count: 15);

        Assert.True(CalibrationValidator.TrySelect(
            new List<CalibrationSample> { off, on }, out var selection, out var rejection));
        Assert.Equal(CalibrationRejection.None, rejection);
        Assert.Same(off, selection!.Off);
        Assert.Same(on, selection.On);
    }

    [Fact]
    public void MinimumTemplateSeparation_FollowsClassifierAcceptanceThreshold()
    {
        // The two templates must stay farther apart than the radius within
        // which the classifier accepts a glyph, otherwise the states overlap.
        Assert.Equal(Classifier.DefaultAcceptanceThreshold,
            CalibrationValidator.MinimumTemplateSeparation);
    }
}