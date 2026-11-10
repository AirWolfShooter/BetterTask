using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Core.Sharing;
using BetterTask.Infrastructure;
using BetterTask.Themes;

namespace BetterTask.Interface;

public partial class MacroWindow : Window
{
    private SaveAndLoadEvents EventStore => SaveAndLoadEvents.Instance;

    public MacroWindow()
    {
        InitializeComponent();
        ThemeManager.Instance.RegisterBackgroundImage(ThemeBackgroundImage);
        int eventCount = Recording.RecordedEvents.Count;
        CurrentRecordingSummary.Text = Recording.isRecording
            ? "Recording in progress"
            : eventCount == 0 ? "No events recorded" : $"{eventCount:N0} recorded events";
        bool hasCurrentRecording = eventCount > 0 && !Recording.isRecording && !Playback.playbackStarted;
        ExportCurrentButton.IsEnabled = hasCurrentRecording;
        SaveButton.IsEnabled = hasCurrentRecording;
        RefreshRecordingButtons();
    }

    private void RefreshRecordingButtons()
    {
        RecordingContainer.Children.Clear();

        foreach (string filePath in Directory.GetFiles(EventStore.RecordingsPath, "*.json")
                     .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                RecordingFile? recording = EventStore.LoadRecording(filePath);
                if (recording is null)
                {
                    RecordingContainer.Children.Add(CreateRecordingRow(
                        filePath,
                        null,
                        "This file does not contain a valid macro."));
                    continue;
                }

                RecordingContainer.Children.Add(CreateRecordingRow(filePath, recording));
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Macros", $"Could not load macro '{Path.GetFileName(filePath)}'.", ex);
                RecordingContainer.Children.Add(CreateRecordingRow(filePath, null, ex.Message));
            }
        }

        EmptyStateText.IsVisible = RecordingContainer.Children.Count == 0;
    }

    private async void openRecordingsFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            FileLocationOpener.OpenFolder(EventStore.RecordingsPath);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macros", "Could not open the recordings folder.", ex);
            await ShowErrorAsync($"Could not open the macro folder: {ex.Message}");
        }
    }

    private Control CreateRecordingRow(string filePath, RecordingFile? recording, string? loadError = null)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string displayName = recording?.Name ?? fileName;
        var details = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 3,
            Children =
            {
                new TextBlock
                {
                    Text = displayName,
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = loadError is null
                        ? $"{recording!.Events.Count} events  ·  {recording.Created}"
                        : $"Could not load: {loadError}",
                    FontSize = 11,
                    Foreground = GetAppBrush(loadError is null ? "TextSecondaryBrush" : "DangerBrush",
                        loadError is null ? "#9BA9B9" : "#E35E71"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };

        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 6
        };

        var playButton = CreateActionButton("Play", 66, "SecondaryButtonStyle");
        playButton.IsEnabled = recording is not null
            && recording.Events.Count > 0
            && !Recording.isRecording
            && !Playback.playbackStarted;
        playButton.Click += async (_, _) =>
        {
            if (recording is not null)
            {
                await PlayMacroAsync(recording);
            }
        };

        var equipButton = CreateActionButton("Set active", 104, "RoundedButtonStyle");
        equipButton.IsEnabled = recording is not null && recording.Events.Count > 0
            && !Recording.isRecording && !Playback.playbackStarted;
        equipButton.Click += async (_, _) =>
        {
            if (recording is not null)
                await EquipMacroAsync(filePath, displayName);
        };

        var exportButton = CreateActionButton("Export", 72, "SecondaryButtonStyle");
        exportButton.IsEnabled = recording is not null;
        exportButton.Click += async (_, _) =>
        {
            try
            {
                await EventStore.ExportRecordingAsync(filePath, this);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Macros", $"Could not export '{displayName}'.", ex);
                await ShowErrorAsync($"Could not export \"{displayName}\": {ex.Message}");
            }
        };

        var shareButton = CreateActionButton("Share", 72, "SecondaryButtonStyle");
        shareButton.IsEnabled = recording is not null && recording.Events.Count > 0;
        shareButton.Click += async (_, _) =>
        {
            if (recording is not null)
                await ShareMacroAsync(recording, shareButton);
        };

        var deleteButton = CreateActionButton("Delete", 72, "DangerButtonStyle");
        deleteButton.Click += async (_, _) => await DeleteRecordingAsync(filePath, displayName);

        actionButtons.Children.Add(equipButton);
        actionButtons.Children.Add(playButton);
        actionButtons.Children.Add(exportButton);
        actionButtons.Children.Add(shareButton);
        actionButtons.Children.Add(deleteButton);

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10
        };
        content.Children.Add(details);
        Grid.SetColumn(actionButtons, 1);
        content.Children.Add(actionButtons);

        bool isActive = recording is not null
            && string.Equals(EventStore.ActiveRecordingPath, Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase);
        var card = new Border
        {
            Background = isActive
                ? GetAppBrush("AccentSoftBrush", "#332D83F6")
                : GetAppBrush("CardBrush", "#172230"),
            BorderBrush = isActive
                ? GetAppBrush("AccentBrush", "#7569E8")
                : GetAppBrush("OutlineBrush", "#29384A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 7),
            Child = content
        };
        card.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        card.PointerEntered += (_, _) =>
        {
            card.Background = GetAppBrush("CardHoverBrush", "#26364A");
            card.BorderBrush = GetAppBrush(isActive ? "AccentHoverBrush" : "OutlineHoverBrush",
                isActive ? "#8A80F5" : "#44536A");
        };
        card.PointerExited += (_, _) =>
        {
            card.Background = isActive
                ? GetAppBrush("AccentSoftBrush", "#332D83F6")
                : GetAppBrush("CardBrush", "#172230");
            card.BorderBrush = isActive
                ? GetAppBrush("AccentBrush", "#7569E8")
                : GetAppBrush("OutlineBrush", "#29384A");
        };
        if (recording is not null && recording.Events.Count > 0)
        {
            card.Tapped += async (_, args) =>
            {
                if (ReferenceEquals(args.Source, card))
                    await EquipMacroAsync(filePath, displayName);
            };
        }

        return card;
    }

    private Button CreateActionButton(string label, double width, string themeKey = "SecondaryButtonStyle")
    {
        return new Button
        {
            Content = label,
            Width = width,
            Height = 36,
            Theme = GetButtonTheme(themeKey)
        };
    }

    private async Task EquipMacroAsync(string filePath, string displayName)
    {
        if (Recording.isRecording || Playback.playbackStarted)
        {
            await ShowErrorAsync("Stop recording or playback before changing the active macro.");
            return;
        }

        try
        {
            RecordingFile loaded = EventStore.LoadRecording(filePath)
                ?? throw new InvalidDataException("This file does not contain a valid macro.");
            Recording.RecordedEvents.Clear();
            Recording.RecordedEvents.AddRange(loaded.Events);
            MainWindow.Instance.SetActiveMacro(loaded.Name, filePath, loaded.Events.Count);
            AppLogger.Info("Macros", $"Loaded '{displayName}' as the active macro ({loaded.Events.Count:N0} events).");
            Close();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macros", $"Could not load '{displayName}' as the active macro.", ex);
            await ShowErrorAsync($"Could not load the macro: {ex.Message}");
        }
    }

    private async Task PlayMacroAsync(RecordingFile recording)
    {
        if (Recording.isRecording)
        {
            await ShowErrorAsync("Stop recording before playing a macro.");
            return;
        }

        if (Playback.playbackStarted)
        {
            await ShowErrorAsync("Stop the current playback before starting another macro.");
            return;
        }

        if (recording.Events.Count == 0)
        {
            await ShowErrorAsync("This macro does not contain any recorded events.");
            return;
        }

        List<RecordedEvent> events = recording.Events.ToList();
        AppLogger.Info("Macros", $"Starting playback of '{recording.Name}' ({events.Count:N0} events).");
        Close();
        await MainWindow.Instance.PlayRecordingAsync(events);
    }

    private async void importRecordingClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            RecordingFile? recording = await EventStore.ImportRecordingAsync(this);
            if (recording is null)
            {
                return;
            }

            string path = EventStore.GetRecordingPath(recording.Name);
            bool overwrite = false;
            if (File.Exists(path))
            {
                overwrite = await ConfirmAsync(
                    "Replace Macro",
                    $"A macro named \"{Path.GetFileNameWithoutExtension(path)}\" already exists. Replace it?",
                    "Replace");
                if (!overwrite)
                {
                    return;
                }
            }

            EventStore.SaveRecordingToHome(recording.Name, recording.Version, recording.Events, overwrite);
            RefreshRecordingButtons();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macros", "Macro import failed.", ex);
            await ShowErrorAsync($"Could not import the macro: {ex.Message}");
        }
    }

    private async void importSharedCodeClicked(object? sender, RoutedEventArgs e)
    {
        string? code = await PromptForCodeAsync();
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            RecordingFile recording = await MacroShareClient.DownloadAsync(code);
            if (!await ConfirmAsync("Import shared macro",
                    $"Import \"{recording.Name}\" ({recording.Events.Count:N0} recorded keyboard/mouse inputs)? It will be saved locally and will not run unless you choose Play.",
                    "Import"))
                return;
            await SaveImportedRecordingAsync(recording);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macro sharing", "Could not import the shared macro.", ex);
            await ShowErrorAsync($"Could not import the shared macro: {ex.Message}");
        }
    }

    private async Task ShareMacroAsync(RecordingFile recording, Button shareButton)
    {
        Window? progressDialog = null;
        try
        {
            if (!await ConfirmAsync("Share macro",
                    $"Upload \"{recording.Name}\" ({recording.Events.Count:N0} recorded keyboard/mouse inputs)? Anyone with the code can download it until it expires.",
                    "Share"))
                return;

            shareButton.IsEnabled = false;
            shareButton.Content = "Sharing…";
            progressDialog = CreateSharingProgressDialog();
            progressDialog.Show(this);
            MacroShareClient.MacroShareLink share = await MacroShareClient.ShareAsync(recording);
            progressDialog.Close();
            progressDialog = null;
            await ShowShareCodeAsync(share);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macro sharing", $"Could not share '{recording.Name}'.", ex);
            await ShowErrorAsync($"Could not share the macro: {ex.Message}");
        }
        finally
        {
            if (progressDialog?.IsVisible == true)
                progressDialog.Close();
            shareButton.Content = "Share";
            shareButton.IsEnabled = true;
        }
    }

    private Window CreateSharingProgressDialog()
    {
        var dialog = new Window
        {
            Title = "Sharing macro",
            Width = 370,
            Height = 170,
            CanResize = false,
            ShowInTaskbar = false,
            Topmost = true,
            Background = GetAppBrush("SurfaceBrush", "#121A25"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Connecting to the sharing service…",
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9"),
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new ProgressBar { IsIndeterminate = true, Height = 6 },
                new TextBlock
                {
                    Text = "If the service has been idle, its first response can take a little longer.",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Foreground = GetAppBrush("TextSecondaryBrush", "#AAB5C3")
                }
            }
        };
        return dialog;
    }

    private async Task SaveImportedRecordingAsync(RecordingFile recording)
    {
        string path = EventStore.GetRecordingPath(recording.Name);
        bool overwrite = false;
        if (File.Exists(path))
        {
            overwrite = await ConfirmAsync("Replace Macro",
                $"A macro named \"{Path.GetFileNameWithoutExtension(path)}\" already exists. Replace it?", "Replace");
            if (!overwrite) return;
        }
        EventStore.SaveRecordingToHome(recording.Name, recording.Version, recording.Events, overwrite);
        RefreshRecordingButtons();
    }

    private async Task<string?> PromptForCodeAsync()
    {
        var dialog = new Window
        {
            Title = "Import shared macro", Width = 400, Height = 190, CanResize = false,
            Topmost = true, Background = GetAppBrush("SurfaceBrush", "#121A25"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var box = new TextBox { PlaceholderText = "XXXX-XXXX-XXXX" };
        var cancel = CreateActionButton("Cancel", 90);
        var load = CreateActionButton("Load code", 100, "RoundedButtonStyle");
        cancel.Click += (_, _) => dialog.Close((string?)null);
        load.Click += (_, _) => dialog.Close(box.Text?.Trim());
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Enter a macro sharing code.", Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9") },
                box,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { cancel, load } }
            }
        };
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task ShowShareCodeAsync(MacroShareClient.MacroShareLink share)
    {
        var dialog = new Window
        {
            Title = "Macro sharing code", Width = 440, Height = 280, CanResize = false, Topmost = true,
            Background = GetAppBrush("SurfaceBrush", "#121A25"), WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var status = new TextBlock
        {
            Text = share.Reused ? "This macro already had a code; no duplicate share was created." : "Anyone with the code can download this macro.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetAppBrush("TextSecondaryBrush", "#AAB5C3"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        var codeText = new TextBlock
        {
            Text = share.Code,
            FontSize = 25,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9")
        };
        var expiresText = new TextBlock
        {
            Text = $"Expires {share.ExpiresAt.ToLocalTime():MMMM d, yyyy 'at' h:mm tt} (your local time)",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = GetAppBrush("TextSecondaryBrush", "#AAB5C3")
        };
        var copy = CreateActionButton("Copy to clipboard", 155, "RoundedButtonStyle");
        copy.Click += async (_, _) =>
        {
            try
            {
                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null)
                    throw new InvalidOperationException("Clipboard is unavailable.");
                await clipboard.SetTextAsync(share.Code);
                status.Text = "Code copied to clipboard.";
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Macro sharing", "Could not copy the share code to the clipboard.", ex);
                status.Text = "Could not copy the code. You can select and copy it manually.";
            }
        };
        var close = CreateActionButton("Close", 90, "SecondaryButtonStyle");
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 14, Children =
        {
            status,
            codeText,
            expiresText,
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8, Children = { copy, close } }
        }};
        await dialog.ShowDialog(this);
    }

    private async void exportCurrentRecordingClicked(object? sender, RoutedEventArgs e)
    {
        if (Recording.isRecording || Playback.playbackStarted)
        {
            await ShowErrorAsync("Stop recording or playback before exporting the current recording.");
            return;
        }

        if (Recording.RecordedEvents.Count == 0)
        {
            await ShowErrorAsync("There is no current recording to export.");
            return;
        }

        try
        {
            await EventStore.ExportRecordingAsync(null, this);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macros", "Current macro export failed.", ex);
            await ShowErrorAsync($"Could not export the current recording: {ex.Message}");
        }
    }

    private async void saveCurrentRecordingClicked(object? sender, RoutedEventArgs e)
    {
        if (Recording.isRecording || Playback.playbackStarted)
        {
            await ShowErrorAsync("Stop recording or playback before saving the current recording.");
            return;
        }

        List<RecordedEvent> events = Recording.RecordedEvents.ToList();
        if (events.Count == 0)
        {
            await ShowErrorAsync("There is no current recording to save.");
            return;
        }

        string? name = await PromptForNameAsync();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            string path = EventStore.GetRecordingPath(name);
            bool overwrite = false;
            if (File.Exists(path))
            {
                overwrite = await ConfirmAsync(
                    "Replace Macro",
                    $"A macro named \"{Path.GetFileNameWithoutExtension(path)}\" already exists. Replace it?",
                    "Replace");
                if (!overwrite)
                {
                    return;
                }
            }

            EventStore.SaveRecordingToHome(name, "1.0", events, overwrite);
            RefreshRecordingButtons();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macros", "Saving the current recording failed.", ex);
            await ShowErrorAsync($"Could not save the macro: {ex.Message}");
        }
    }

    private async Task DeleteRecordingAsync(string filePath, string recordingName)
    {
        try
        {
            if (!await ConfirmAsync(
                    "Delete Macro",
                    $"Are you sure you want to delete \"{recordingName}\"?",
                    "Delete"))
            {
                return;
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                AppLogger.Info("Macros", $"Deleted '{recordingName}'.");
            }

            RefreshRecordingButtons();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Macros", $"Could not delete '{recordingName}'.", ex);
            await ShowErrorAsync($"Could not delete the macro: {ex.Message}");
        }
    }

    private async Task<string?> PromptForNameAsync()
    {
        var dialog = new Window
        {
            Title = "Save Current Macro",
            Width = 380,
            Height = 190,
            CanResize = false,
            Topmost = true,
            Background = GetAppBrush("SurfaceBrush", "#121A25"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var nameBox = new TextBox
        {
            PlaceholderText = "Macro name",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = GetAppBrush("CardBrush", "#172230"),
            Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9"),
            BorderBrush = GetAppBrush("OutlineBrush", "#29384A")
        };
        var cancelButton = CreateActionButton("Cancel", 90, "SecondaryButtonStyle");
        var saveButton = CreateActionButton("Save", 90, "RoundedButtonStyle");
        cancelButton.Click += (_, _) => dialog.Close((string?)null);
        saveButton.Click += (_, _) => dialog.Close(nameBox.Text?.Trim());

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Choose a name for this macro.",
                    Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9")
                },
                nameBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, saveButton }
                }
            }
        };

        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 190,
            CanResize = false,
            Topmost = true,
            Background = GetAppBrush("SurfaceBrush", "#121A25"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = CreateActionButton("Cancel", 90, "SecondaryButtonStyle");
        var confirmButton = CreateActionButton(
            confirmLabel,
            90,
            confirmLabel == "Delete" ? "DangerButtonStyle" : "RoundedButtonStyle");
        cancelButton.Click += (_, _) => dialog.Close(false);
        confirmButton.Click += (_, _) => dialog.Close(true);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9")
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, confirmButton }
                }
            }
        };

        return await dialog.ShowDialog<bool>(this);
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Macro Manager",
            Width = 420,
            Height = 190,
            CanResize = false,
            Topmost = true,
            Background = GetAppBrush("SurfaceBrush", "#121A25"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var closeButton = CreateActionButton("OK", 90, "RoundedButtonStyle");
        closeButton.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = GetAppBrush("TextPrimaryBrush", "#F1F4F9")
                },
                closeButton
            }
        };
        await dialog.ShowDialog(this);
    }

    private static ControlTheme? GetButtonTheme(string key)
    {
        var app = Application.Current;
        return app is not null
            && app.TryGetResource(key, ThemeVariant.Default, out object? theme)
            ? theme as ControlTheme
            : null;
    }

    private static IBrush GetAppBrush(string key, string fallback)
    {
        var app = Application.Current;
        return app is not null
            && app.TryGetResource(key, ThemeVariant.Default, out object? brush)
            && brush is IBrush appBrush
                ? appBrush
                : new SolidColorBrush(Color.Parse(fallback));
    }
}
