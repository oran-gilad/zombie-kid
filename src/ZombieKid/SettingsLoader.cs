using System.Text.Json;

namespace ZombieKid;

public static class SettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "settings.json");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var defaults = new AppSettings();
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
        settings.ProcessNames = settings.ProcessNames
            .Select(NormalizeProcessName)
            .Where(static n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (settings.ProcessNames.Count == 0)
        {
            settings.ProcessNames.AddRange(["notepad.exe", "notepad++.exe"]);
        }

        return settings;
    }

    public static string NormalizeProcessName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + ".exe";
    }
}
