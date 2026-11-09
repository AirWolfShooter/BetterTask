# Exercises the actual installer script in isolated folders. Process launches are stubbed.
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../BetterTask/Utils/AutoUpdater.cs') -Raw
$match = [regex]::Match($source, '(?s)internal const string InstallScript = """\r?\n(.*?)\r?\n        """;')
if (!$match.Success) { throw 'Installer script not found' }
$script = [regex]::Replace($match.Groups[1].Value, '(?m)^        ', '')
$root = Join-Path ([IO.Path]::GetTempPath()) ('BetterTask-updater-test-' + [guid]::NewGuid().ToString('N'))
function Assert($condition, $message) { if (!$condition) { throw $message } }
foreach ($scenario in @('success', 'rollback')) {
    $folder = Join-Path $root $scenario
    $target = Join-Path $folder 'installation with spaces'
    $staged = Join-Path $folder 'staged'
    [IO.Directory]::CreateDirectory($target) | Out-Null
    [IO.Directory]::CreateDirectory($staged) | Out-Null
    Set-Content -LiteralPath (Join-Path $target 'BetterTask.exe') -Value 'old version'
    Set-Content -LiteralPath (Join-Path $target 'settings.json') -Value 'keep settings'
    Set-Content -LiteralPath (Join-Path $staged 'BetterTask.exe') -Value 'new version'
    Set-Content -LiteralPath (Join-Path $staged 'new.dll') -Value 'new dependency'
    $files = @('BetterTask.exe', 'new.dll')
    if ($scenario -eq 'rollback') { $files += 'missing.dll' }
    $manifest = Join-Path $folder 'manifest.json'
    @{
        ProcessId = 2147483647; Target = $target; Staged = $staged
        Backup = (Join-Path $folder 'backup'); Files = $files
        Executable = (Join-Path $target 'BetterTask.exe'); Log = (Join-Path $folder 'error.txt')
    } | ConvertTo-Json | Set-Content -LiteralPath $manifest
    $runner = Join-Path $folder 'install.ps1'
    # Stub only process launches; all file copying and rollback use the production script.
    $stub = 'function Start-Process { param($FilePath, $WorkingDirectory, $ArgumentList) }'
    $script.Replace('$ErrorActionPreference = ''Stop''', ('$ErrorActionPreference = ''Stop''' + "`r`n" + $stub)) | Set-Content -LiteralPath $runner
    & (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe') -NoProfile -NonInteractive -File $runner -Manifest $manifest
    Assert ($LASTEXITCODE -eq 0) "$scenario installer failed"
    Assert ((Get-Content -LiteralPath (Join-Path $target 'settings.json') -Raw).Trim() -eq 'keep settings') 'Settings were modified'
    if ($scenario -eq 'success') {
        Assert ((Get-Content -LiteralPath (Join-Path $target 'BetterTask.exe') -Raw).Trim() -eq 'new version') 'Update not installed'
        Assert (Test-Path -LiteralPath (Join-Path $target 'new.dll')) 'Dependency missing'
        Assert (!(Test-Path -LiteralPath (Join-Path $folder 'error.txt'))) 'Unexpected installer error'
    } else {
        Assert ((Get-Content -LiteralPath (Join-Path $target 'BetterTask.exe') -Raw).Trim() -eq 'old version') 'Original version not restored'
        Assert (!(Test-Path -LiteralPath (Join-Path $target 'new.dll'))) 'New dependency not rolled back'
        Assert (Test-Path -LiteralPath (Join-Path $folder 'error.txt')) 'Missing failure log'
    }
    Write-Output "$scenario passed"
}
