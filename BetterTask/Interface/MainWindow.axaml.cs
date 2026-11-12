using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using BetterTask.Core;
using BetterTask.Core.Input;
using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Infrastructure;
using BetterTask.Interface.Managers;
using BetterTask.Interface.UserControlPages;
using BetterTask.Themes;
using BetterTask.Updates;

namespace BetterTask.Interface;

public partial class MainWindow : Window
{
    private static MainWindow? instance;
    public static MainWindow Instance => instance!;
    public static MainWindow? Current => instance;

    private readonly Recording recorder = Recording.Instance;
    private readonly Playback playback = Playback.Instance;
    private readonly DispatcherTimer uiTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public static Stopwatch displayTimer = new();
    public static bool loop;
    private bool compactMode;
    private bool appendRecordingActive;
    private List<RecordedEvent>? appendUndoSnapshot;
    private Settings? settingsWindow;

    public MainWindow()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
            return;

        instance = this;
        ThemeManager.Instance.RegisterBackgroundImage(ThemeBackgroundImage);
        uiTimer.Tick += UpdateTimer;
        uiTimer.Start();

        SettingsManager.Instance.loadSettingsFromSaved();
        SetCompactMode(SettingsManager.Instance.settings.CompactMode ?? false, persist: false);
        SetAppendRecordingButtonVisibility(SettingsManager.Instance.settings.ShowAppendRecordingButton);
        Topmost = SettingsManager.Instance.settings.StayOnTop ?? false;
        GlobalHookManager.Instance.Start();
        KeybindsManager.Instance.startListening();

        Opened += async (_, _) =>
        {
            if (SettingsManager.Instance.settings.CheckForUpdates ?? false)
                await CheckForUpdatesAtStartupAsync();
        };
    }

    private async Task CheckForUpdatesAtStartupAsync()
    {
        UpdateCheckResult result = await GitHubUpdateChecker.CheckAsync();
        if (result.Status != UpdateCheckStatus.UpdateAvailable)
            return;

        StatusTextBlock.Text = $"Update available: version {result.LatestVersion}.";
        await ShowUpdateAvailableDialogAsync(result);
    }

    public async Task ShowUpdateAvailableDialogAsync(UpdateCheckResult result)
    {
        var dialog = new Window
        {
            Title = "BetterTask update available",
            Topmost = Topmost,
            Width = 500,
            Height = 490,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ThemeManager.Instance.GetBrush("AppBackgroundBrush", "#FF171821")
        };

        bool installing = false;
        dialog.Closing += (_, e) => { if (installing) e.Cancel = true; };
        var updateStatus = new TextBlock
        {
            Text = AutoUpdater.CanInstall(result)
                ? "The update will download, install, and restart BetterTask. Save your recording first."
                : "Automatic installation is unavailable for this build. Use the GitHub download below.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = ThemeManager.Instance.GetBrush("TextPrimaryBrush", "#FFFFFFFF"),
            FontSize = 12
        };
        var updateButton = new Button
        {
            Content = "Update and restart",
            Height = 38,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            IsEnabled = AutoUpdater.CanInstall(result)
        };
        updateButton.Click += async (_, _) =>
        {
            if (Recording.isRecording || Playback.playbackStarted)
            {
                updateStatus.Text = "Stop recording or playback before updating.";
                return;
            }
            installing = true;
            updateButton.IsEnabled = false;
            try
            {
                await AutoUpdater.PrepareAndLaunchAsync(result,
                    new Progress<string>(message => updateStatus.Text = message));
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    installing = false;
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Updates", "Could not install the update.", ex);
                updateStatus.Text = $"Update failed: {ex.Message} You can retry or download from GitHub.";
                updateButton.IsEnabled = true;
            }
            finally { installing = false; }
        };

        var openReleaseButton = new Button
        {
            Content = "Open GitHub changelog",
            Height = 38,
            Padding = new Thickness(10, 4),
            Theme = Application.Current?.TryGetResource("SecondaryButtonStyle", ThemeVariant.Default, out object? theme) == true
                ? theme as ControlTheme
                : null
        };
        openReleaseButton.Click += (_, _) =>
        {
            if (installing) return;
            try
            {
                if (result.ReleaseUrl is not null)
                    FileLocationOpener.OpenUrl(result.ReleaseUrl);
                dialog.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Updates", "Could not open the GitHub release page.", ex);
                StatusTextBlock.Text = $"Could not open the update page: {ex.Message}";
                dialog.Close();
            }
        };

        var laterButton = new Button
        {
            Content = "Later",
            Height = 38,
            Padding = new Thickness(10, 4),
            Theme = Application.Current?.TryGetResource("SecondaryButtonStyle", ThemeVariant.Default, out object? laterTheme) == true
                ? laterTheme as ControlTheme
                : null
        };
        laterButton.Click += (_, _) => { if (!installing) dialog.Close(); };

        var releaseNotes = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                ? "No release notes were provided. Open the GitHub release for details and downloads."
                : result.ReleaseNotes.Trim(),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = ThemeManager.Instance.GetBrush("TextPrimaryBrush", "#FFFFFFFF"),
            FontSize = 12
        };

        var releaseNotesPanel = new Border
        {
            Background = ThemeManager.Instance.GetBrush("CardBrush", "#FF20232E"),
            BorderBrush = ThemeManager.Instance.GetBrush("OutlineBrush", "#FF343847"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                MaxHeight = 180,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = releaseNotes
            }
        };

        var buttons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 10,
            Children =
            {
                openReleaseButton,
                laterButton
            }
        };
        Grid.SetColumn(laterButton, 1);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = $"BetterTask {result.LatestVersion} is available. You are using {result.CurrentVersion}.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = ThemeManager.Instance.GetBrush("TextPrimaryBrush", "#FFFFFFFF"),
                    FontSize = 14
                },
                new TextBlock
                {
                    Text = "Changelog",
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Foreground = ThemeManager.Instance.GetBrush("TextPrimaryBrush", "#FFFFFFFF"),
                    FontSize = 13
                },
                releaseNotesPanel,
                updateStatus,
                updateButton,
                buttons
            }
        };

        await dialog.ShowDialog(this);
    }

    public void UpdateTimer(object? sender, EventArgs e)
    {
        TimeSpan time = displayTimer.Elapsed;
        Timer.Text = $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
    }

    public void resetTimer()
    {
        displayTimer.Restart();
        Timer.Text = "00:00";
    }

    private void ToggleCompactModeClicked(object? sender, RoutedEventArgs e)
    {
        SetCompactMode(!compactMode);
    }

    public void SetCompactMode(bool enabled, bool persist = true)
    {
        compactMode = enabled;
        bool extraCompact = compactMode && SettingsManager.Instance.settings.CompactStyle == 1;
        BrandBadge.IsVisible = !compactMode;
        HeaderSubtitle.IsVisible = !compactMode;
        HeaderTitle.FontSize = extraCompact ? 14 : compactMode ? 19 : 23;
        CompactToggleButton.Content = compactMode ? "Expand" : "Compact";
        CompactToggleButton.Width = extraCompact ? 56 : compactMode ? 72 : 86;
        CompactToggleButton.Height = extraCompact ? 28 : 36;
        CompactToggleButton.FontSize = extraCompact ? 10 : 12;
        CompactToggleButton.Padding = extraCompact ? new Thickness(5, 3) : new Thickness(10, 6);

        QuickActionsLabel.IsVisible = !compactMode;
        SecondaryActions.IsVisible = !compactMode;
        Footer.IsVisible = !compactMode;

        RootGrid.Margin = extraCompact ? new Thickness(8) : compactMode ? new Thickness(12) : new Thickness(24);
        RootGrid.RowSpacing = extraCompact ? 4 : compactMode ? 8 : 18;
        SessionCard.Padding = extraCompact ? new Thickness(8) : compactMode ? new Thickness(12) : new Thickness(18);
        StatusTextBlock.FontSize = extraCompact ? 12 : compactMode ? 15 : 18;
        Timer.FontSize = extraCompact ? 17 : compactMode ? 22 : 30;
        QuickActionsPanel.Spacing = compactMode ? 0 : 12;
        ButtonStackPanel.Spacing = compactMode ? 0 : 10;
        RecordButton.Height = extraCompact ? 38 : compactMode ? 48 : 62;
        PlayBackButton.Height = extraCompact ? 38 : compactMode ? 48 : 62;
        RecordButton.FontSize = extraCompact ? 11 : compactMode ? 13 : 16;
        PlayBackButton.FontSize = extraCompact ? 11 : compactMode ? 13 : 16;
        RecordButton.Padding = extraCompact ? new Thickness(6, 4) : compactMode ? new Thickness(8, 6) : new Thickness(16, 10);
        PlayBackButton.Padding = extraCompact ? new Thickness(6, 4) : compactMode ? new Thickness(8, 6) : new Thickness(14, 9);
        AppendRecordingButton.Height = extraCompact ? 28 : 32;
        AppendRecordingButton.Width = extraCompact ? 98 : 136;
        AppendRecordingButton.FontSize = extraCompact ? 10 : 11;
        AppendRecordingButton.Padding = extraCompact ? new Thickness(4, 3) : new Thickness(8, 5);

        UpdateWindowDimensions();

        if (persist)
        {
            SettingsManager.Instance.settings.CompactMode = compactMode;
            SettingsManager.Instance.saveSettings();
        }
    }

    public void RefreshThemedControls()
    {
        if (Recording.isRecording)
        {
            RecordButton.Background = ThemeManager.Instance.GetBrush("DangerBrush", "#FFE35E71");
        }

        if (Playback.playbackStarted)
        {
            PlayBackButton.Background = ThemeManager.Instance.GetBrush(
                "PrimaryButtonBackgroundBrush",
                "#FF7569E8");
        }
    }

    public void recordingClicked(object? sender, RoutedEventArgs? e)
    {
        if (!Recording.isRecording)
        {
            if (Playback.playbackStarted)
                return;

            appendRecordingActive = false;
            SaveAndLoadEvents.Instance.ActiveRecordingPath = null;
            appendUndoSnapshot = null;
            UndoAppendButton.IsVisible = false;
            RecordButton.Background = ThemeManager.Instance.GetBrush("DangerBrush", "#FFE35E71");
            RecordButton.Content = "Recording";
            StatusTextBlock.Text = "Recording...";
            recorder.clearRecording();
            recorder.Subscribe();
            PauseRecordingButton.IsVisible = true;
            ResumeRecordingButton.IsVisible = false;
            UpdateAppendRecordingButtonState();
        }
        else
        {
            StopRecording("Recording stopped.");
        }
    }

    public void appendRecordingClicked(object? sender, RoutedEventArgs? e)
    {
        if (!SettingsManager.Instance.settings.ShowAppendRecordingButton || Playback.playbackStarted)
            return;

        if (appendRecordingActive)
        {
            StopRecording("Recording segment added.");
            return;
        }

        if (Recording.isRecording || Recording.RecordedEvents.Count == 0)
            return;

        appendUndoSnapshot = Recording.RecordedEvents.ToList();
        UndoAppendButton.IsVisible = false;
        appendRecordingActive = true;
        RecordButton.Background = ThemeManager.Instance.GetBrush("DangerBrush", "#FFE35E71");
        RecordButton.Content = "Recording";
        PauseRecordingButton.IsVisible = true;
        ResumeRecordingButton.IsVisible = false;
        AppendRecordingButton.Content = "Finish segment";
        StatusTextBlock.Text = "Appending to recording...";
        recorder.Subscribe(appendToExisting: true);
        UpdateAppendRecordingButtonState();
    }

    public void SetAppendRecordingButtonVisibility(bool visible)
    {
        AppendRecordingButton.IsVisible = visible;
        UpdateWindowDimensions();
        UpdateAppendRecordingButtonState();
    }

    private void UpdateWindowDimensions()
    {
        bool extraCompact = compactMode && SettingsManager.Instance.settings.CompactStyle == 1;
        Width = extraCompact ? 250 : compactMode ? 320 : 420;
        Height = extraCompact
            ? (AppendRecordingButton.IsVisible ? 202 : 172)
            : compactMode
                ? (AppendRecordingButton.IsVisible ? 268 : 230)
                : 600;
    }

    private void StopRecording(string status)
    {
        recorder.Unsubscribe();
        bool appendedSegment = appendRecordingActive;
        appendRecordingActive = false;
        UndoAppendButton.IsVisible = appendedSegment && appendUndoSnapshot is not null
            && Recording.RecordedEvents.Count > appendUndoSnapshot.Count;
        RecordButton.ClearValue(Button.BackgroundProperty);
        RecordButton.Content = "Record";
        PauseRecordingButton.IsVisible = false;
        ResumeRecordingButton.IsVisible = false;
        AppendRecordingButton.Content = "Append segment";
        StatusTextBlock.Text = status;
        UpdateAppendRecordingButtonState();
    }

    public void pauseRecordingClicked(object? sender, RoutedEventArgs? e) => PauseRecording();

    public void resumeRecordingClicked(object? sender, RoutedEventArgs? e) => ResumeRecording();

    public void PauseRecording()
    {
        if (!Recording.isRecording || Recording.isPaused)
            return;
        recorder.Pause();
        PauseRecordingButton.IsVisible = false;
        ResumeRecordingButton.IsVisible = true;
        StatusTextBlock.Text = "Recording paused.";
    }

    public void ResumeRecording()
    {
        if (!Recording.isRecording || !Recording.isPaused)
            return;
        recorder.Resume();
        PauseRecordingButton.IsVisible = true;
        ResumeRecordingButton.IsVisible = false;
        StatusTextBlock.Text = appendRecordingActive ? "Appending to recording..." : "Recording...";
    }

    public void undoAppendClicked(object? sender, RoutedEventArgs? e)
    {
        if (Recording.isRecording || Playback.playbackStarted || appendUndoSnapshot is null)
            return;

        Recording.RecordedEvents.Clear();
        Recording.RecordedEvents.AddRange(appendUndoSnapshot);
        appendUndoSnapshot = null;
        UndoAppendButton.IsVisible = false;
        StatusTextBlock.Text = "Appended segment undone.";
        UpdateAppendRecordingButtonState();
        AppLogger.Info("Recording", "Undid the latest appended segment.");
    }

    private void UpdateAppendRecordingButtonState()
    {
        bool settingEnabled = SettingsManager.Instance.settings.ShowAppendRecordingButton;
        AppendRecordingButton.IsVisible = settingEnabled;
        AppendRecordingButton.Content = appendRecordingActive ? "Finish segment" : "Append segment";
        UndoAppendButton.IsVisible = SettingsManager.Instance.settings.ShowAppendRecordingButton
            && !appendRecordingActive && appendUndoSnapshot is not null;
        UndoAppendButton.IsEnabled = !Recording.isRecording && !Playback.playbackStarted;
        AppendRecordingButton.IsEnabled = settingEnabled
            && (appendRecordingActive
                || (!Recording.isRecording
                    && !Playback.playbackStarted
                    && Recording.RecordedEvents.Count > 0));
    }

    public async void playbackClicked(object? sender, RoutedEventArgs? e)
    {
        if (Playback.playbackStarted)
        {
            playback.stopPlayback();
            StatusTextBlock.Text = "Playback stopped.";
            PlayBackButton.ClearValue(Button.BackgroundProperty);
            PlayBackButton.Content = "Play";
            ButtonStackPanel.IsEnabled = true;
            return;
        }

        if (Recording.RecordedEvents.Count > 0 && !Recording.isRecording)
        {
            if (sender is Button)
            {
                loop = false;
            }

            await PlayRecordingAsync(Recording.RecordedEvents, loop);
        }
    }

    public async Task PlayRecordingAsync(IReadOnlyList<RecordedEvent> events, bool shouldLoop = false)
    {
        if (events.Count == 0 || Recording.isRecording || Playback.playbackStarted)
        {
            return;
        }

        StatusTextBlock.Text = "Playing...";
        PlayBackButton.Content = "Playing";
        ButtonStackPanel.IsEnabled = false;
        PlayBackButton.Background = ThemeManager.Instance.GetBrush(
            "PrimaryButtonBackgroundBrush",
            "#FF7569E8");

        try
        {
            await playback.StartPlayback(events, shouldLoop);
            StatusTextBlock.Text = "Playback finished.";
        }
        finally
        {
            PlayBackButton.ClearValue(Button.BackgroundProperty);
            PlayBackButton.Content = "Play";
            ButtonStackPanel.IsEnabled = true;
        }
    }

    public void SetActiveMacro(string name, string filePath, int eventCount)
    {
        if (Recording.isRecording || Playback.playbackStarted || eventCount <= 0)
            return;

        appendUndoSnapshot = null;
        SaveAndLoadEvents.Instance.ActiveRecordingPath = Path.GetFullPath(filePath);
        UndoAppendButton.IsVisible = false;
        StatusTextBlock.Text = $"Loaded macro: {name}.";
        MainWindow.displayTimer.Reset();
        uiTimer.Stop();
        uiTimer.Start();
    }

    public async void macroManagerClicked(object? sender, RoutedEventArgs? e)
    {
        await new MacroWindow().ShowDialog(this);
    }

    public void settingsButtonClicked(object? sender, RoutedEventArgs? e)
    {
        if (settingsWindow is not null)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new Settings();
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show(this);
    }
}

