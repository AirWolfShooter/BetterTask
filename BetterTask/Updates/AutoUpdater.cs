using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BetterTask.Infrastructure;

namespace BetterTask.Updates;

public static class AutoUpdater
{
    private static int updating;
    public static bool CanInstall(UpdateCheckResult result) => result.Status == UpdateCheckStatus.UpdateAvailable
        && OperatingSystem.IsWindows()
        && result.Asset is { Name: var name }
        && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetExtension(Environment.ProcessPath), ".exe", StringComparison.OrdinalIgnoreCase);

    public static async Task PrepareAndLaunchAsync(UpdateCheckResult result, IProgress<string> progress)
    {
        if (!CanInstall(result))
            throw new InvalidOperationException("Automatic updates require a published Windows executable.");
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
            string executableName = Path.GetFileName(Environment.ProcessPath!);
            string downloadPath = Path.Combine(workspace, "update.exe");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"BetterTask/{GitHubUpdateChecker.CurrentVersion}");
            progress.Report("Downloading update...");
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(downloadPath);
                await source.CopyToAsync(output);
            }
            if (asset.Size <= 0 || new FileInfo(downloadPath).Length != asset.Size)
                throw new InvalidDataException("The update download is incomplete.");
            if (asset.Digest is { Length: > 0 })
            {
                await using var input = File.OpenRead(downloadPath);
                string actual = "sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(input));
                if (!actual.Equals(asset.Digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The update checksum did not match. Your current installation was not changed.");
            }
            progress.Report("Preparing update...");
            string staged = Path.Combine(workspace, "staged");
            Directory.CreateDirectory(staged);
            File.Copy(downloadPath, Path.Combine(staged, executableName));
            if (!File.Exists(Path.Combine(staged, executableName)))
                throw new InvalidDataException("The release did not contain a valid BetterTask executable.");
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
                ProcessId = Environment.ProcessId, ProcessName = executableName, Target = target, Staged = staged,
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
        function Wait-UntilExecutableIsFree {
            $deadline = [DateTime]::UtcNow.AddSeconds(120)
            do {
                $running = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -ieq $m.ProcessName -and $_.ExecutablePath -and [string]::Equals([IO.Path]::GetFullPath($_.ExecutablePath), [IO.Path]::GetFullPath($m.Executable), [StringComparison]::OrdinalIgnoreCase) }
                if (!$running) {
                    $probe = $null
                    try {
                        $probe = [IO.File]::Open($m.Executable, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                        return
                    } catch {
                        Start-Sleep -Milliseconds 400
                    } finally {
                        if ($probe) { $probe.Dispose() }
                    }
                } else {
                    Start-Sleep -Milliseconds 400
                }
            } while ([DateTime]::UtcNow -lt $deadline)
            throw "$($m.ProcessName) is still in use. Close every BetterTask window and retry the update."
        }
        function Copy-WithRetry([string]$Source, [string]$Destination) {
            $lastError = $null
            for ($attempt = 0; $attempt -lt 20; $attempt++) {
                try {
                    Copy-Item -LiteralPath $Source -Destination $Destination -Force
                    return
                } catch {
                    $lastError = $_
                    Start-Sleep -Milliseconds 500
                }
            }
            throw $lastError
        }
        try {
            Wait-UntilExecutableIsFree
            foreach ($relative in $m.Files) {
                $destination = Join-Path $m.Target $relative
                $backup = Join-Path $m.Backup $relative
                if (Test-Path -LiteralPath $destination) {
                    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
                    Copy-WithRetry $destination $backup
                }
                $changed.Add($relative)
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
                Copy-WithRetry (Join-Path $m.Staged $relative) $destination
            }
            Start-Process -FilePath $m.Executable -WorkingDirectory $m.Target
        } catch {
            $failure = $_ | Out-String
            foreach ($relative in $changed) {
                try {
                    $backup = Join-Path $m.Backup $relative
                    $destination = Join-Path $m.Target $relative
                    if (Test-Path -LiteralPath $backup) {
                        Copy-WithRetry $backup $destination
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
