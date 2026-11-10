using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using BetterTask.Interface.UserControlPages;
using BetterTask.Themes;

namespace BetterTask.Interface;

public partial class Settings : Window
{
    private readonly GeneralSettings generalSettings = new();
    private readonly KeybindsSetting keybindsSetting = new();
    private readonly ThemesSettings themesSettings = new();
    private readonly LogsSettings logsSettings = new();

    public Settings()
    {
        InitializeComponent();
        ThemeManager.Instance.RegisterBackgroundImage(ThemeBackgroundImage);
        ShowGeneralSettings();
    }

    private void GeneralSettingsClick(object? sender, RoutedEventArgs e)
    {
        ShowGeneralSettings();
    }

    private void KeybindsSettingsClick(object? sender, RoutedEventArgs e)
    {
        GeneralButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        KeybindsButton.Theme = GetButtonTheme("RoundedButtonStyle");
        ThemesButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        LogsButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        ShowContent(keybindsSetting);
    }

    private void ThemesSettingsClick(object? sender, RoutedEventArgs e)
    {
        GeneralButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        KeybindsButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        ThemesButton.Theme = GetButtonTheme("RoundedButtonStyle");
        LogsButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        ShowContent(themesSettings);
    }

    private void LogsSettingsClick(object? sender, RoutedEventArgs e)
    {
        GeneralButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        KeybindsButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        ThemesButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        LogsButton.Theme = GetButtonTheme("RoundedButtonStyle");
        ShowContent(logsSettings);
    }

    private void ShowGeneralSettings()
    {
        GeneralButton.Theme = GetButtonTheme("RoundedButtonStyle");
        KeybindsButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        ThemesButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        LogsButton.Theme = GetButtonTheme("SecondaryButtonStyle");
        ShowContent(generalSettings);
    }

    private void ShowContent(Control content)
    {
        ShownContent.Content = content;
        ContentScrollViewer.Offset = Vector.Zero;
    }

    private static ControlTheme? GetButtonTheme(string key)
    {
        var app = Application.Current;
        return app is not null
            && app.TryGetResource(key, ThemeVariant.Default, out object? theme)
                ? theme as ControlTheme
                : null;
    }
}
