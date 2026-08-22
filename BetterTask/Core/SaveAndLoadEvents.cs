using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Interface.Utils;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;

namespace BetterTask.Core
{
    public class SaveAndLoadEvents
    {
        private static readonly SaveAndLoadEvents instance = new SaveAndLoadEvents();
        public static SaveAndLoadEvents Instance => instance;

        public RecordingFile? CurrentRecording { get; set; }

        private SaveAndLoadEvents() { }

        public void SaveRecording()
        {
            SaveFileDialog saveFileDialog = new();

            saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            saveFileDialog.FileName = "Recording.json";

            if (saveFileDialog.ShowDialog() == true)
            {
                CurrentRecording = new RecordingFile
                {
                    Name = Path.GetFileNameWithoutExtension(saveFileDialog.FileName),
                    Created = DateTime.Now.ToString(),
                    Version = "1.0",
                    Application = "BetterTask",
                    Events = Recording.RecordedEvents
                };

                string json = JsonSerializer.Serialize(
                    CurrentRecording,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(saveFileDialog.FileName, json);
            }
        }
        public void LoadRecording()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            
            if (openFileDialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                CurrentRecording = JsonSerializer.Deserialize<RecordingFile>(json);
                if (CurrentRecording != null)
                {
                    Recording.RecordedEvents = CurrentRecording.Events;
                }
            }
        }
    }
}