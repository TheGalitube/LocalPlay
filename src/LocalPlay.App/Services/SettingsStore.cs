using System.Text.Json;
using LocalPlay.Models;

namespace LocalPlay.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalPlay");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            var settings = File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings()
                : new AppSettings();
            if (settings.StreamingDefaultsVersion < 1)
            {
                settings.Quality = "2K · 60 FPS (HEVC)";
                settings.PlaybackProfile = "EditingLowLatency";
                settings.StreamingDefaultsVersion = 1;
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.StreamingDefaultsVersion = 1;
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, _path, true);
    }

    public string PairingRegisterPath
    {
        get
        {
            var directory = Path.GetDirectoryName(_path)!;
            return Path.Combine(directory, "paired-devices.txt");
        }
    }
}

