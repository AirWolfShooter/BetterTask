using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Utils;

namespace BetterTask.Interface.UserControlPages;

public partial class GeneralSettings : UserControl
{
    private readonly Regex numberRegex = new("[^0-9.]");
    private bool isLoadingSettings;
    private string? latestReleaseUrl;
    private UpdateCheckResult? availableUpdate;

    public GeneralSettings()
    {
        InitializeComponent();
        loadSettingsText();
    }

    public void loadSettingsText()
    {
        isLoadingSettings = true;
        stayOnTop.IsChecked = SettingsManager.Instance.settings.StayOnTop ?? false;
        checkForUpdates.IsChecked = SettingsManager.Instance.settings.CheckForUpdates ?? false;
        saveAutomatically.IsChecked = SettingsManager.Instance.settings.SaveAutomatically ?? false;
        compactMode.IsChecked = SettingsManager.Instance.settings.CompactMode ?? false;
        compactLayoutStyle.SelectedIndex = SettingsManager.Instance.settings.CompactStyle;
        showAppendRecordingButton.IsChecked = SettingsManager.Instance.settings.ShowAppendRecordingButton;
        startDelay.Text = SettingsManager.Instance.settings.StartDelay?.ToString() ?? "0";
        isLoadingSettings = false;
        MainWindow.Instance.SetCompactMode(compactMode.IsChecked ?? false, persist: false);
        MainWindow.Instance.SetAppendRecordingButtonVisibility(showAppendRecordingButton.IsChecked ?? false);
        checkSettings();
    }

    public void stayOnTopClicked(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.settings.StayOnTop = stayOnTop.IsChecked;
        checkSettings();
    }

    public void checkForUpdatesClicked(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.settings.CheckForUpdates = checkForUpdates.IsChecked;
        checkSettings();
        if (checkForUpdates.IsChecked == true)
            _ = checkForUpdatesAsync();
    }

    private async void checkForUpdatesNowClicked(object? sender, RoutedEventArgs e)
    {
        await checkForUpdatesAsync();
    }

    private async void openUpdateReleaseClicked(object? sender, RoutedEventArgs e)
    {
        if (availableUpdate is not null)
            await MainWindow.Instance.ShowUpdateAvailableDialogAsync(availableUpdate);
    }

    public void saveAutoClicked(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.settings.SaveAutomatically = saveAutomatically.IsChecked;
        checkSettings();
    }

    private void compactModeClicked(object? sender, RoutedEventArgs e)
    {
        bool enabled = compactMode.IsChecked ?? false;
        SettingsManager.Instance.settings.CompactMode = enabled;
        MainWindow.Instance.SetCompactMode(enabled, persist: false);
        SettingsManager.Instance.saveSettings();
    }

    private void compactLayoutStyleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isLoadingSettings || compactLayoutStyle.SelectedIndex < 0)
            return;

        SettingsManager.Instance.settings.CompactStyle = compactLayoutStyle.SelectedIndex == 1 ? 1 : 0;
        MainWindow.Instance.SetCompactMode(compactMode.IsChecked ?? false, persist: false);
        SettingsManager.Instance.saveSettings();
    }

    private void appendRecordingButtonClicked(object? sender, RoutedEventArgs e)
    {
        bool enabled = showAppendRecordingButton.IsChecked ?? false;
        SettingsManager.Instance.settings.ShowAppendRecordingButton = enabled;
        MainWindow.Instance.SetAppendRecordingButtonVisibility(enabled);
        SettingsManager.Instance.saveSettings();
    }

    public void saveSettingsClicked(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.saveSettings();
    }

    public async void exportSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await SettingsManager.Instance.exportSettingsAsync(owner);
        }
    }

    private void openSettingsFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            FileLocationOpener.OpenFolder(SettingsManager.Instance.SettingsDirectory);
            SettingsFolderStatus.Text = string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Settings", "Could not open the settings folder.", ex);
            SettingsFolderStatus.Text = $"Could not open settings folder: {ex.Message}";
        }
    }

    private void openLogsFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            FileLocationOpener.OpenFolder(AppLogger.LogDirectory);
            SettingsFolderStatus.Text = string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Settings", "Could not open the logs folder.", ex);
            SettingsFolderStatus.Text = $"Could not open logs folder: {ex.Message}";
        }
    }

    public async void loadSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        await SettingsManager.Instance.loadSettingsAsync(owner);
        loadSettingsText();
    }

    public void checkSettings()
    {
        keepOnTop();
        if (SettingsManager.Instance.settings.SaveAutomatically ?? false)
        {
            SettingsManager.Instance.saveSettings();
        }
    }

    public void keepOnTop()
    {
        MainWindow.Instance.Topmost = SettingsManager.Instance.settings.StayOnTop ?? false;
    }

    public async Task checkForUpdatesAsync()
    {
        checkUpdatesButton.IsEnabled = false;
        openUpdateReleaseButton.IsVisible = false;
        latestReleaseUrl = null;
        availableUpdate = null;
        UpdateStatus.Text = "Checking GitHub for updates...";

        try
        {
            UpdateCheckResult result = await GitHubUpdateChecker.CheckAsync();
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    UpdateStatus.Text = $"Version {result.LatestVersion} is available (you have {result.CurrentVersion}).";
                    availableUpdate = result;
                    latestReleaseUrl = result.ReleaseUrl;
                    openUpdateReleaseButton.IsVisible = latestReleaseUrl is not null;
                    break;
                case UpdateCheckStatus.UpToDate:
                    UpdateStatus.Text = $"You're up to date (version {result.CurrentVersion}).";
                    break;
                case UpdateCheckStatus.NoRelease:
                    UpdateStatus.Text = "No GitHub release has been published yet.";
                    break;
                default:
                    UpdateStatus.Text = $"Could not check for updates: {result.ErrorMessage}";
                    break;
            }
        }
        finally
        {
            checkUpdatesButton.IsEnabled = true;
        }
    }

    private void StartDelayTextInput(object? sender, TextInputEventArgs e)
    {
        e.Handled = numberRegex.IsMatch(e.Text ?? string.Empty);
    }

    private void startDelay_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (int.TryParse(startDelay.Text, out int delay))
        {
            SettingsManager.Instance.settings.StartDelay = delay;
            if (SettingsManager.Instance.settings.SaveAutomatically ?? false)
            {
                SettingsManager.Instance.saveSettings();
            }
        }
    }
}
