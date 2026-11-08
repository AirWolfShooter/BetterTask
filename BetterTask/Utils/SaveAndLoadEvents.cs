using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BetterTask.Core.RecordingAndPlayback;

namespace BetterTask.Utils;

public class SaveAndLoadEvents
{
    private static readonly SaveAndLoadEvents instance = new();
    private static readonly FilePickerFileType JsonFileType = new("JSON files")
    {
        Patterns = new[] { "*.json" }
    };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SaveAndLoadEvents Instance => instance;

    public string RecordingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterTask",
        "Recordings");

    public RecordingFile? CurrentRecording { get; private set; }

    private SaveAndLoadEvents()
    {
        Directory.CreateDirectory(RecordingsPath);
    }

    public string GetRecordingPath(string name)
    {
        return Path.Combine(RecordingsPath, $"{SanitizeName(name)}.json");
    }

    public RecordingFile? LoadRecording(string filePath)
    {
        string json = File.ReadAllText(filePath);
        RecordingFile? recording = JsonSerializer.Deserialize<RecordingFile>(json);
        if (recording is not null)
        {
            recording.Events ??= new List<RecordedEvent>();
            if (string.IsNullOrWhiteSpace(recording.Name))
            {
                recording.Name = Path.GetFileNameWithoutExtension(filePath);
            }
        }

        return recording;
    }

    public async Task ExportRecordingAsync(string? filePath, Window owner)
    {
        RecordingFile recording;
        if (filePath is not null)
        {
            recording = LoadRecording(filePath)
                ?? throw new InvalidDataException("The selected macro file is empty or invalid.");
        }
        else
        {
            recording = new RecordingFile
            {
                Name = "Recording",
                Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Events = Recording.RecordedEvents.ToList()
            };
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Macro",
            SuggestedFileName = $"{SanitizeName(recording.Name)}.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonFileType }
        });
        if (file is null)
        {
            return;
        }

        await using Stream destination = await file.OpenWriteAsync();
        await JsonSerializer.SerializeAsync(destination, recording, JsonOptions);
        AppLogger.Info("Macros", $"Exported '{recording.Name}' ({recording.Events.Count:N0} events).");
    }

    public string SaveRecordingToHome(
        string name,
        string version,
        IEnumerable<RecordedEvent>? events = null,
        bool overwrite = false)
    {
        string safeName = SanitizeName(name);
        string safeVersion = string.IsNullOrWhiteSpace(version) ? "1.0" : version.Trim();
        string filePath = Path.Combine(RecordingsPath, $"{safeName}.json");

        if (File.Exists(filePath) && !overwrite)
        {
            throw new IOException($"A macro named \"{safeName}\" already exists.");
        }

        CurrentRecording = new RecordingFile
        {
            Name = safeName,
            Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Version = safeVersion,
            Events = (events ?? Recording.RecordedEvents).ToList()
        };

        Directory.CreateDirectory(RecordingsPath);
        File.WriteAllText(filePath, JsonSerializer.Serialize(CurrentRecording, JsonOptions));
        AppLogger.Info(
            "Macros",
            $"Saved '{safeName}' ({CurrentRecording.Events.Count:N0} events, overwrite={overwrite}).");
        return filePath;
    }

    public async Task<RecordingFile?> ImportRecordingAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Macro",
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonFileType }
        });
        if (files.Count == 0)
        {
            return null;
        }

        await using Stream stream = await files[0].OpenReadAsync();
        RecordingFile? recording = await JsonSerializer.DeserializeAsync<RecordingFile>(stream);
        if (recording is null)
        {
            throw new InvalidDataException("The selected file does not contain a valid macro.");
        }

        recording.Events ??= new List<RecordedEvent>();
        if (string.IsNullOrWhiteSpace(recording.Name))
        {
            recording.Name = Path.GetFileNameWithoutExtension(files[0].Name);
        }

        if (string.IsNullOrWhiteSpace(recording.Version))
        {
            recording.Version = "1.0";
        }

        AppLogger.Info("Macros", $"Imported '{recording.Name}' ({recording.Events.Count:N0} events).");
        return recording;
    }

    private static string SanitizeName(string name)
    {
        string safeName = (name ?? string.Empty).Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidCharacter, '_');
        }

        safeName = safeName.Replace('/', '_').Replace('\\', '_').TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new ArgumentException("Enter a valid macro name.", nameof(name));
        }

        return safeName;
    }
}
