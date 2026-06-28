using System.IO;
using System.Text.Json;

namespace CodexResetTray.App.Services;

public sealed class JsonAlertSettingsService : IAlertSettingsService
{
    private const int DefaultThresholdPercent = 10;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private AppSettings _settings;

    public JsonAlertSettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexResetTray",
            "settings.json"))
    {
    }

    internal JsonAlertSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        _settings = Load(settingsPath);
    }

    public int? LowRemainingThresholdPercent
    {
        get => _settings.LowRemainingThresholdPercent;
        set
        {
            _settings = _settings with { LowRemainingThresholdPercent = Sanitize(value) };
            Save();
        }
    }

    private static int? Sanitize(int? value) =>
        value is { } percent ? Math.Clamp(percent, 1, 99) : (int?)null;

    private static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings(DefaultThresholdPercent);
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(DefaultThresholdPercent);
        }
        catch (IOException)
        {
            return new AppSettings(DefaultThresholdPercent);
        }
        catch (JsonException)
        {
            return new AppSettings(DefaultThresholdPercent);
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings, JsonOptions));
    }

    private sealed record AppSettings(int? LowRemainingThresholdPercent);
}
