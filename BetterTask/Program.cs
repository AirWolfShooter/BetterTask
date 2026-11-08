using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;

namespace BetterTask;

internal static class Program
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }

    [STAThread]
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
    }
}
