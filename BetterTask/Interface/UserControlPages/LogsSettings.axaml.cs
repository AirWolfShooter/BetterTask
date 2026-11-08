using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BetterTask.Utils;

namespace BetterTask.Interface.UserControlPages;

public partial class LogsSettings : UserControl
{
    private const int MaximumDisplayedLines = 3000;

    public LogsSettings()
    {
        InitializeComponent();
        LoadLogs();
    }

    private void refreshLogsClicked(object? sender, RoutedEventArgs e)
    {
        LoadLogs();
    }

    private void openLogsFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            FileLocationOpener.OpenFolder(AppLogger.LogDirectory);
            LogStatus.Text = string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Logs", "Could not open the logs folder.", ex);
            LogStatus.Text = $"Could not open logs folder: {ex.Message}";
        }
    }

    private void LoadLogs()
    {
        try
        {
            if (!Directory.Exists(AppLogger.LogDirectory))
            {
                LogOutput.Text = string.Empty;
                LogStatus.Text = "No logs have been created yet.";
                return;
            }

            string[] logFiles = Directory
                .EnumerateFiles(AppLogger.LogDirectory, "BetterTask-*.log")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string[] lines = logFiles
                .SelectMany(File.ReadLines)
                .TakeLast(MaximumDisplayedLines)
                .ToArray();

            LogOutput.Text = string.Join(Environment.NewLine, lines);
            LogStatus.Text = lines.Length == 0
                ? "No log entries found."
                : $"Showing the latest {lines.Length:N0} lines from {logFiles.Length:N0} log file(s).";
        }
        catch (Exception ex)
        {
            LogOutput.Text = string.Empty;
            LogStatus.Text = $"Could not read application logs: {ex.Message}";
        }
    }
}
