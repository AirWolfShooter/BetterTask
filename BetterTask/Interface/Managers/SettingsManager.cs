using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Interface;
using BetterTask.Interface.UserControlPages;
using BetterTask.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace BetterTask.Interface.Managers
{
    internal class SettingsManager
    {
        private static SettingsManager instance = new SettingsManager();
        public static SettingsManager Instance => instance;

        private string settingsFile;
        string folder;

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
                WriteIndented = true,
            });

            File.WriteAllText(settingsFile, json);
        }

        public void loadSettings()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                AppSettings? loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                if (loadedSettings != null)
                {
                    settings = loadedSettings;
                }
            }
        }
        public void loadSettingsFromSaved()
        {
            if (File.Exists(settingsFile))
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsFile)) ?? new AppSettings();
            }
        }
        public void exportSettings()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            saveFileDialog.DefaultExt = "json";
            saveFileDialog.FileName = "settings.json";

            if (saveFileDialog.ShowDialog() == true)
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(saveFileDialog.FileName, json);

            }

        }
    }
}
