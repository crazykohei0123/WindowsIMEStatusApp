using System.IO;
using System.Text.Json;

namespace ImeStatusOverlay.Storage;

/// <summary>
/// Best-effort JSON persistence to a single file. Read/write failures are
/// swallowed so a corrupt or inaccessible store never crashes the app.
/// </summary>
internal sealed class JsonStore<T> where T : class
{
    private readonly string _path;

    public JsonStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public T? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path));
        }
        catch
        {
            // ignore corrupt/inaccessible store
            return null;
        }
    }

    public void Save(T model)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(model));
        }
        catch
        {
            // persistence is best-effort
        }
    }
}
