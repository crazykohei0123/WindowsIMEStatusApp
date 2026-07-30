using System.IO;
using ImeStatusOverlay;

namespace ImeStatusOverlay.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _storePath;
    private readonly AppSettings _settings;

    public AppSettingsTests()
    {
        _storePath = Path.Combine(
            Path.GetTempPath(),
            "ImeStatusOverlay_settings_" + Guid.NewGuid().ToString("N"),
            "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        _settings = new AppSettings(_storePath);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_storePath)!, true); } catch { }
    }

    [Fact]
    public void Defaults_AreImeOnAndImeOff()
    {
        Assert.Equal(AppSettings.DefaultOnText, _settings.OnText);
        Assert.Equal(AppSettings.DefaultOffText, _settings.OffText);
        Assert.Equal("IME ON", _settings.OnText);
        Assert.Equal("IME OFF", _settings.OffText);
    }

    [Fact]
    public void SetTexts_StoresValues()
    {
        _settings.SetTexts("あ", "A");
        Assert.Equal("あ", _settings.OnText);
        Assert.Equal("A", _settings.OffText);
    }

    [Fact]
    public void SetTexts_NullOrEmpty_FallsBackToDefaults()
    {
        _settings.SetTexts(null, "");
        Assert.Equal(AppSettings.DefaultOnText, _settings.OnText);
        Assert.Equal(AppSettings.DefaultOffText, _settings.OffText);
    }

    [Fact]
    public void SetTexts_WhitespaceOnly_FallsBackToDefaults()
    {
        _settings.SetTexts("   ", "\t");
        Assert.Equal(AppSettings.DefaultOnText, _settings.OnText);
        Assert.Equal(AppSettings.DefaultOffText, _settings.OffText);
    }

    [Fact]
    public void SetTexts_TrimsSurroundingWhitespace()
    {
        _settings.SetTexts("  あ  ", "  A  ");
        Assert.Equal("あ", _settings.OnText);
        Assert.Equal("A", _settings.OffText);
    }

    [Fact]
    public void SetTexts_PersistsAcrossInstances()
    {
        _settings.SetTexts("オン", "オフ");
        var reloaded = new AppSettings(_storePath);
        Assert.Equal("オン", reloaded.OnText);
        Assert.Equal("オフ", reloaded.OffText);
    }

    [Fact]
    public void Constructor_CorruptStore_KeepsDefaults()
    {
        File.WriteAllText(_storePath, "not json");
        var reloaded = new AppSettings(_storePath);
        Assert.Equal(AppSettings.DefaultOnText, reloaded.OnText);
        Assert.Equal(AppSettings.DefaultOffText, reloaded.OffText);
    }
}
