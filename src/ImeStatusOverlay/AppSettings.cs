using System.IO;
using System.Text.Json;

namespace ImeStatusOverlay;

/// <summary>
/// User-facing settings (overlay label texts, etc.) persisted under %APPDATA%.
/// </summary>
public sealed class AppSettings
{
    public const string DefaultOnText = "IME ON";
    public const string DefaultOffText = "IME OFF";

    private readonly string _storePath;

    public string OnText { get; private set; } = DefaultOnText;
    public string OffText { get; private set; } = DefaultOffText;

    public AppSettings()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImeStatusOverlay");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "settings.json");
        Load();
    }

    public void SetTexts(string? onText, string? offText)
    {
        OnText = Normalize(onText, DefaultOnText);
        OffText = Normalize(offText, DefaultOffText);
        Save();
    }

    private static string Normalize(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim();
        return v.Length == 0 ? fallback : v;
    }

    // ------------------------------------------------------------------
    // Persistence
    // ------------------------------------------------------------------

    private sealed class Store
    {
        public string? OnText { get; set; }
        public string? OffText { get; set; }
    }

    private void Save()
    {
        try
        {
            var s = new Store { OnText = OnText, OffText = OffText };
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
            OnText = Normalize(s.OnText, DefaultOnText);
            OffText = Normalize(s.OffText, DefaultOffText);
        }
        catch { /* ignore corrupt store */ }
    }
}
