using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BetterTask.Utils;

namespace BetterTask.Interface.Managers;

internal class SettingsManager
{
    private static readonly SettingsManager instance = new();
    public static SettingsManager Instance => instance;

    private static readonly FilePickerFileType JsonFileType = new("JSON files")
    {
        Patterns = new[] { "*.json" }
    };

    private readonly string settingsFile;
    private readonly string folder;
    public string SettingsDirectory => folder;

    public AppSettings settings { get; private set; } = new();

    private SettingsManager()
    {
        folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterTask");
        Directory.CreateDirectory(folder);
        settingsFile = Path.Combine(folder, "settings.json");
        loadSettingsFromSaved();
    }

    public void saveSettings()
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(settingsFile, json);
    }

    public async Task loadSettingsAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Settings",
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonFileType }
        });
        if (files.Count == 0)
        {
            return;
        }

        await using Stream stream = await files[0].OpenReadAsync();
        AppSettings? loadedSettings = await JsonSerializer.DeserializeAsync<AppSettings>(stream);
        if (loadedSettings is not null)
        {
            settings = loadedSettings;
            normalizeSettings();
            AppLogger.Info("Settings", "Loaded settings from a selected file.");
        }
    }

    public void loadSettingsFromSaved()
    {
        if (File.Exists(settingsFile))
        {
            settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsFile)) ?? new AppSettings();
            normalizeSettings();
            AppLogger.Info("Settings", "Loaded saved application settings.");
        }
        else
        {
            AppLogger.Info("Settings", "No saved settings file was found; using defaults.");
        }
    }

    private void normalizeSettings()
    {
        settings.Theme ??= new ThemeSettings();
        settings.Theme.Normalize();
        settings.CompactStyle = Math.Clamp(settings.CompactStyle, 0, 1);
        settings.ThemePresets ??= new List<ThemePreset>();
        foreach (ThemePreset preset in settings.ThemePresets)
        {
            preset.Theme ??= new ThemeSettings();
            preset.Theme.Normalize();
        }
    }

    public async Task exportSettingsAsync(Window owner)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Settings",
            SuggestedFileName = "settings.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonFileType }
        });
        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        AppLogger.Info("Settings", "Exported application settings.");
    }
}
