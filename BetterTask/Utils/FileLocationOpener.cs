using System.Diagnostics;
using System.IO;

namespace BetterTask.Utils;

public static class FileLocationOpener
{
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only GitHub HTTPS links can be opened.", nameof(url));
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }

    public static void OpenFolder(string path)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);

        string fileManager = OperatingSystem.IsWindows()
            ? "explorer.exe"
            : OperatingSystem.IsMacOS()
                ? "open"
                : "xdg-open";

        Process.Start(new ProcessStartInfo
        {
            FileName = fileManager,
            UseShellExecute = false,
            ArgumentList = { fullPath }
        });
    }
}
