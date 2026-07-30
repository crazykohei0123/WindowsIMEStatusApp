using System.IO;

namespace ImeStatusOverlay.Storage;

/// <summary>
/// User-facing settings (overlay label texts, etc.) persisted under %APPDATA%.
/// </summary>
public sealed class AppSettings
{
    public const string DefaultOnText = "IME ON";
    public const string DefaultOffText = "IME OFF";

    private readonly JsonStore<SettingsModel> _store;

    public string OnText { get; private set; } = DefaultOnText;
    public string OffText { get; private set; } = DefaultOffText;

    public AppSettings()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeStatusOverlay",
            "settings.json"))
    {
    }

    internal AppSettings(string storePath)
    {
        _store = new JsonStore<SettingsModel>(storePath);
        var loaded = _store.Load();
        if (loaded != null)
        {
            OnText = Normalize(loaded.OnText, DefaultOnText);
            OffText = Normalize(loaded.OffText, DefaultOffText);
        }
    }

    public void SetTexts(string? onText, string? offText)
    {
        OnText = Normalize(onText, DefaultOnText);
        OffText = Normalize(offText, DefaultOffText);
        _store.Save(new SettingsModel { OnText = OnText, OffText = OffText });
    }

    private static string Normalize(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim();
        return v.Length == 0 ? fallback : v;
    }

    private sealed class SettingsModel
    {
        public string? OnText { get; set; }
        public string? OffText { get; set; }
    }
}
