<#
.SYNOPSIS
    Draws the screenshots of the README and the user guide with the headless UI tests, or checks that they are current.

.DESCRIPTION
    The screenshots are pictures of the real application: the opt-in test class ScreenshotTests in
    test/DemaConsulting.CanvasPilot.UiTests (trait Category=Screenshots) builds a demonstration project in a temporary
    folder, shows the real windows headless with Avalonia.Headless and Skia and the embedded Inter font, and saves each
    window as a PNG. The tests skip themselves unless the environment variable CANVASPILOT_SCREENSHOT_DIR names an output
    folder, so the normal test run (build.ps1) never writes pictures. This script sets that variable and runs only
    those tests. It writes these pictures:

      main-window.png        the main window, dark, in a conversation with a picture, two marks, and tool chips (1600 x 1000)
      main-window-light.png  the same window in the light theme
      welcome.png            the welcome screen with four recent projects and their small pictures (1600 x 1000)
      style-kits.png         the Style Kits manager with the kits of the program and of the user (1180 x 1000)

    The pictures do not depend on the computer: the content is made by the tests, the culture is US English, and the
    fonts are embedded. The demonstration projects are made in C:\CanvasPilot (or the same place at the root of the
    drive on another system) when that folder does not exist, so that the paths on the cards are neutral, and in a
    temporary folder otherwise; everything is removed at the end. A drawing is repeatable except for the render time
    that the status bar of the main window tells.

    Without -Check it writes the pictures over the ones in docs/images. Review them (the whole window, the text, no
    clipping), and commit the changed pictures with the change to the application that made them differ. Pictures with
    a name that starts with "smoke-" come from the smoke test of the tool itself, which this script does not run.

    With -Check it draws the pictures into a temporary folder and compares each with the committed picture of the
    same name using "canvasnet compare ... --threshold rmse=2 --threshold diff-ratio=0.01". It changes nothing in
    the repository and exits 1 when a committed picture is missing, has another size, or differs by more than the
    tolerance. The tolerance is not zero on purpose: text hinting and anti-aliasing are not bit-for-bit the same on
    every operating system, but a change of text, color, or layout is far above it. The check is a tool for the
    person who changes the user interface; the continuous integration does not run it, for the same reason.

    Run it from anywhere; paths are taken from the repository the script is in.

.PARAMETER Check
    Compare fresh pictures with the committed ones instead of replacing them.

.PARAMETER Only
    Draw only the tests whose name contains this text (for example "Welcome"). The default draws every screenshot.

.PARAMETER OutputFolder
    The folder to write to. The default is docs/images. It is ignored with -Check, which uses a temporary folder.
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [string]$Only = '',
    [string]$OutputFolder = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = 'test/DemaConsulting.CanvasPilot.UiTests'
$committedFolder = Join-Path $repo 'docs/images'

# Selects the screenshot tests (not the smoke test of the tool itself), and when asked only those whose name contains the given text
$filter = 'Category=Screenshots&FullyQualifiedName!~Smoke'
if ($Only) { $filter += "&FullyQualifiedName~$Only" }

# Runs the screenshot tests so that they write to the given folder
function Invoke-Screenshots([string]$folder) {
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $env:CANVASPILOT_SCREENSHOT_DIR = $folder
    try {
        dotnet test $project --filter $filter
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Remove-Item Env:\CANVASPILOT_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    }
}

Push-Location $repo
try {
    if (-not $Check) {
        $target = if ($OutputFolder) { $OutputFolder } else { $committedFolder }
        Invoke-Screenshots (Resolve-Path -LiteralPath (New-Item -ItemType Directory -Force -Path $target)).Path
        exit 0
    }

    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE"
    }

    $work = Join-Path ([System.IO.Path]::GetTempPath()) ('render-screenshots-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $failed = $false
    try {
        Invoke-Screenshots $work

        # Compare every fresh picture, except the smoke pictures of the tool itself, with the committed one
        $fresh = Get-ChildItem -LiteralPath $work -Filter '*.png' | Where-Object { $_.Name -notlike 'smoke-*' }
        foreach ($picture in $fresh) {
            $committed = Join-Path $committedFolder $picture.Name
            $label = "docs/images/$($picture.Name)"
            if (-not (Test-Path -LiteralPath $committed)) {
                Write-Host "FAIL  ${label}: the committed picture is missing (run docs/images/render-screenshots.ps1)"
                $failed = $true
                continue
            }

            $json = dotnet canvasnet compare $committed $picture.FullName --threshold 'rmse=2' --threshold 'diff-ratio=0.01' `
                --side-by-side (Join-Path $work ($picture.BaseName + '-compare.png')) --json 2>$null
            $exit = $LASTEXITCODE
            $result = $null
            try { $result = ($json | Out-String) | ConvertFrom-Json } catch { $result = $null }

            if ($exit -eq 0) {
                Write-Host ("ok    {0}: rmse {1:N3}, differing pixels {2:P3}" -f `
                        $label, $result.metrics.rmse, $result.metrics.diffPixelRatio)
            }
            else {
                $failed = $true
                Write-Host "FAIL  ${label}: it differs from a fresh drawing (canvasnet compare exited with code $exit)"
            }
        }

        if ($failed) {
            Write-Host '      If the change is intended, run: pwsh docs/images/render-screenshots.ps1 and commit the pictures.'
            Write-Host "      The fresh pictures and side-by-side comparisons are kept in $work"
        }
        else {
            Write-Host 'The committed screenshots are current.'
        }
    }
    finally {
        if (-not $failed) {
            Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    exit ($failed ? 1 : 0)
}
finally {
    Pop-Location
}
