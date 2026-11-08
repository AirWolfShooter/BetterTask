using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterTask.Utils;

public static class AppLogger
{
    private static readonly object sync = new();
    private static bool initialized;

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterTask",
        "Logs");

    public static void Initialize()
    {
        lock (sync)
        {
            if (initialized)
                return;

            try
            {
                Directory.CreateDirectory(LogDirectory);
                PruneOldLogs();
                initialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not initialize file logging: {ex}");
            }
        }

        Info(
            "App",
            $"Started BetterTask {Assembly.GetExecutingAssembly().GetName().Version}; " +
            $"{RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}.");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Error("App", "Unhandled exception.", args.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Error("App", "Unobserved background task exception.", args.Exception);
        };
    }

    public static void Info(string category, string message) => Write("INFO", category, message);

    public static void Warning(string category, string message, Exception? exception = null) =>
        Write("WARN", category, message, exception);

    public static void Error(string category, string message, Exception? exception = null) =>
        Write("ERROR", category, message, exception);

    private static void Write(string level, string category, string message, Exception? exception = null)
    {
        string entry = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] [{category}] {message}";
        if (exception is not null)
            entry += Environment.NewLine + exception;

        lock (sync)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                string filePath = Path.Combine(LogDirectory, $"BetterTask-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(filePath, entry + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception loggingException)
            {
                Debug.WriteLine($"Could not write application log: {loggingException.Message}");
            }
        }

        Debug.WriteLine(entry);
    }

    private static void PruneOldLogs()
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-14);
        foreach (string file in Directory.EnumerateFiles(LogDirectory, "BetterTask-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not remove old log file: {ex.Message}");
            }
        }
    }
}
