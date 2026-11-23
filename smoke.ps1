# smoke.ps1
#
# PURPOSE:
#   Starts the published CanvasPilot program and checks that it runs: the process is still alive after
#   -Seconds seconds, it has a main window with a title, and it wrote no error to its log or to the
#   Windows Application event log. Then it closes the program. Exits 1 on failure.
#   The headless UI tests cannot see a crash that only happens on the real platform (for example while
#   the window loads), so this script is the launch gate that CI runs on the published win-x64 build.
#
# USAGE:
#   dotnet publish src/DemaConsulting.CanvasPilot.Desktop/DemaConsulting.CanvasPilot.Desktop.csproj `
#     --configuration Release --runtime win-x64 --self-contained true --property:PublishSingleFile=true `
#     --output publish/win-x64
#   pwsh ./smoke.ps1 [-Exe publish/win-x64/CanvasPilot.exe] [-Seconds 10]
#
# NOTES:
#   Windows only (it prints a skip on other systems). It needs an interactive desktop session, as a
#   main window needs one. The program keeps its settings and log in the real per-user folder
#   (%LOCALAPPDATA%\CanvasPilot), which cannot be redirected, so only the log entries written after the
#   start are checked, and the program is closed the normal way (as a user does) and stopped by its
#   process ID only when it does not close.
#   It is not part of build.ps1 because build.ps1 does not publish and agents run it with no arguments.

param(
    [string]$Exe = "publish/win-x64/CanvasPilot.exe",
    [int]$Seconds = 10
)

if (-not $IsWindows) {
    Write-Host "SKIPPED: the launch smoke test runs on Windows only."
    exit 0
}

if (-not (Test-Path $Exe)) {
    Write-Host "FAILED: '$Exe' was not found. Publish the win-x64 build first (see USAGE)."
    exit 1
}

$exePath = (Resolve-Path $Exe).Path
$logDir = Join-Path $env:LOCALAPPDATA "CanvasPilot/logs"
$start = Get-Date
$failures = @()

Write-Host "Starting $exePath ..."
$process = Start-Process -FilePath $exePath -PassThru

try {
    # Wait the full time, but stop early when the process ends (a crash)
    $deadline = $start.AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline -and -not $process.HasExited) {
        Start-Sleep -Milliseconds 250
    }

    $process.Refresh()
    if ($process.HasExited) {
        $failures += "The process ended after less than $Seconds seconds (exit code $($process.ExitCode))."
    } else {
        if ($process.MainWindowHandle -eq 0) {
            $failures += "The process has no main window (a crashed process held by Windows Error Reporting looks like this)."
        }
        if ([string]::IsNullOrWhiteSpace($process.MainWindowTitle)) {
            $failures += "The main window has no title."
        } else {
            Write-Host "Main window: '$($process.MainWindowTitle)'"
        }
    }

    # Close the program the way a user does, and stop it by its ID only when it does not close
    if (-not $process.HasExited) {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(10000)) {
            $failures += "The program did not close within 10 seconds; it was stopped."
            Stop-Process -Id $process.Id -Force
        }
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}

# Errors the program logged after the start (each entry starts with an ISO timestamp and the level)
$startUtc = $start.ToUniversalTime()
if (Test-Path $logDir) {
    $logFiles = Get-ChildItem $logDir -Filter *.log | Where-Object { $_.LastWriteTime -ge $start.AddSeconds(-1) }
    foreach ($file in $logFiles) {
        foreach ($line in Get-Content $file.FullName) {
            if ($line -match '^(\S+) \[Error\] (.*)$') {
                $when = [DateTimeOffset]::MinValue
                if ([DateTimeOffset]::TryParse($Matches[1], [ref]$when) -and $when.UtcDateTime -ge $startUtc) {
                    $failures += "Log error: $($Matches[2])"
                }
            }
        }
    }
}

# Errors that Windows recorded for this program
try {
    $events = Get-WinEvent -FilterHashtable @{ LogName = "Application"; Level = 2; StartTime = $start } -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match [regex]::Escape((Split-Path $exePath -Leaf)) }
    foreach ($event in $events) {
        $failures += "Application event log ($($event.ProviderName)): $($event.Message.Split("`n")[0])"
    }
} catch {
    Write-Host "NOTE: the Application event log could not be read: $($_.Exception.Message)"
}

if ($failures.Count -gt 0) {
    Write-Host "FAILED: the launch smoke test found $($failures.Count) problem(s):"
    $failures | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host "PASSED: the program ran for $Seconds seconds with a main window and no errors."
exit 0
