using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetterTask.Utils;

public static class AutoUpdater
{
    private static int updating;
    public static bool CanInstall(UpdateCheckResult result) => result.Status == UpdateCheckStatus.UpdateAvailable
        && OperatingSystem.IsWindows()
        && result.Asset is not null
        && string.Equals(Path.GetFileName(Environment.ProcessPath), "BetterTask.exe", StringComparison.OrdinalIgnoreCase);

    public static async Task PrepareAndLaunchAsync(UpdateCheckResult result, IProgress<string> progress)
    {
        if (!CanInstall(result))
            throw new InvalidOperationException("Automatic updates require a published Windows build and a matching release ZIP.");
        if (Interlocked.Exchange(ref updating, 1) != 0)
            throw new InvalidOperationException("An update is already in progress.");
        try
        {
            UpdateAsset asset = result.Asset!;
            if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var url)
                || url.Scheme != "https" || url.Host != "github.com"
                || !url.AbsolutePath.StartsWith("/AirWolfShooter/BetterTask/releases/download/", StringComparison.Ordinal))
                throw new InvalidDataException("The update download is not from the BetterTask repository.");
            string target = Path.GetDirectoryName(Environment.ProcessPath!)!;
            // Check permission before downloading or closing the application.
            string probe = Path.Combine(target, $".bettertask-update-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe)) { }
            File.Delete(probe);
            string workspace = Path.Combine(Path.GetTempPath(), "BetterTask-Updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            string zipPath = Path.Combine(workspace, "update.zip");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"BetterTask/{GitHubUpdateChecker.CurrentVersion}");
            progress.Report("Downloading update...");
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(zipPath);
                await source.CopyToAsync(output);
            }
            if (asset.Size <= 0 || new FileInfo(zipPath).Length != asset.Size)
                throw new InvalidDataException("The update download is incomplete.");
            if (asset.Digest is { Length: > 0 })
            {
                await using var input = File.OpenRead(zipPath);
                string actual = "sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(input));
                if (!actual.Equals(asset.Digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The update checksum did not match. Your current installation was not changed.");
            }
            progress.Report("Preparing update...");
            string staged = Path.Combine(workspace, "staged");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, staged));
            if (!File.Exists(Path.Combine(staged, "BetterTask.exe")))
                throw new InvalidDataException("The release ZIP does not contain BetterTask.exe at its root.");
            // Reject links in the destination rather than writing outside the installation.
            var files = Directory.GetFiles(staged, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(staged, file)).ToArray();
            foreach (string file in files)
            {
                string? path = Path.Combine(target, file);
                while (path is not null)
                {
                    if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("The installation contains a symbolic link. Update this copy manually.");
                    path = Path.GetDirectoryName(path);
                }
            }
            string manifest = Path.Combine(workspace, "manifest.json");
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, Target = target, Staged = staged,
                Backup = Path.Combine(workspace, "backup"), Files = files,
                Executable = Environment.ProcessPath, Log = Path.Combine(workspace, "update-error.txt")
            }));
            string script = Path.Combine(workspace, "install.ps1");
            await File.WriteAllTextAsync(script, InstallScript, new UTF8Encoding(true));
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Manifest", manifest })
                start.ArgumentList.Add(arg);
            using Process installer = Process.Start(start) ?? throw new IOException("Could not start the update installer.");
            AppLogger.Info("Updates", $"Prepared update {result.LatestVersion}; installer workspace: {workspace}");
        }
        finally { Volatile.Write(ref updating, 0); }
    }

    internal const string InstallScript = """
        param([string]$Manifest)
        $ErrorActionPreference = 'Stop'
        $m = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
        $changed = [System.Collections.Generic.List[string]]::new()
        try {
            $running = Get-Process -Id $m.ProcessId -ErrorAction SilentlyContinue
            if ($running) { $running | Wait-Process -Timeout 120 }
            foreach ($relative in $m.Files) {
                $destination = Join-Path $m.Target $relative
                $backup = Join-Path $m.Backup $relative
                if (Test-Path -LiteralPath $destination) {
                    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
                    Copy-Item -LiteralPath $destination -Destination $backup -Force
                }
                $changed.Add($relative)
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
                Copy-Item -LiteralPath (Join-Path $m.Staged $relative) -Destination $destination -Force
            }
            Start-Process -FilePath $m.Executable -WorkingDirectory $m.Target
        } catch {
            $failure = $_ | Out-String
            foreach ($relative in $changed) {
                try {
                    $backup = Join-Path $m.Backup $relative
                    $destination = Join-Path $m.Target $relative
                    if (Test-Path -LiteralPath $backup) {
                        Copy-Item -LiteralPath $backup -Destination $destination -Force
                    } elseif (Test-Path -LiteralPath $destination) {
                        Remove-Item -LiteralPath $destination -Force
                    }
                } catch { $failure += "`r`nRollback: $_" }
            }
            $failure | Set-Content -LiteralPath $m.Log
            if ($changed.Count -gt 0) { Start-Process -FilePath $m.Executable -WorkingDirectory $m.Target }
            Start-Process notepad.exe -ArgumentList @($m.Log)
        }
        """;
}
