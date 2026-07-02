using Aetheria.Engine.Abilities;

namespace Aetheria.Engine.Persistence;

/// <summary>
/// Reads/writes <see cref="SaveData"/> to disk in a tiny, dependency-free
/// line-based format (<c>key=value</c>). Robust to a missing or corrupt file
/// (returns null). The default location is per-user application data.
/// </summary>
public static class SaveStore
{
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aetheria", "save.dat");

    public static bool Exists(string? path = null) => File.Exists(path ?? DefaultPath);

    public static void Delete(string? path = null)
    {
        path ??= DefaultPath;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }

    public static void Save(SaveData data, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var lines = new[]
            {
                "version=1",
                $"room={data.RoomId}",
                $"deaths={data.Deaths}",
                "abilities=" + string.Join(",", data.Abilities.Select(a => a.ToString())),
            };
            File.WriteAllLines(path, lines);
        }
        catch { /* saving must never crash the game */ }
    }

    public static SaveData? Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path)) return null;
            var data = new SaveData();
            foreach (var raw in File.ReadAllLines(path))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string key = raw[..eq].Trim();
                string val = raw[(eq + 1)..].Trim();
                switch (key)
                {
                    case "room": int.TryParse(val, out var r); data.RoomId = r; break;
                    case "deaths": int.TryParse(val, out var d); data.Deaths = d; break;
                    case "abilities":
                        foreach (var tok in val.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            if (Enum.TryParse<AbilityType>(tok.Trim(), out var ab) && !data.Abilities.Contains(ab))
                                data.Abilities.Add(ab);
                        break;
                }
            }
            return data;
        }
        catch { return null; }
    }
}
