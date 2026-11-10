using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BetterTask.Core;
using BetterTask.Core.Input;
using BetterTask.Infrastructure;
using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Themes;

namespace BetterTask;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (!Design.IsDesignMode
            && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppLogger.Initialize();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            SettingsManager.Instance.loadSettingsFromSaved();
            ThemeManager.Instance.ApplyCurrentTheme();
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) =>
            {
                try
                {
                    GlobalHookManager.Instance.Stop();
                    KeybindsManager.Instance.stopListening();
                }
                catch (Exception ex)
                {
                    AppLogger.Error("App", "Error while stopping input hooks.", ex);
                }
                finally
                {
                    AppLogger.Info("App", "BetterTask is shutting down.");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
