<#
.SYNOPSIS
    Draws the screenshots of the README and the user guide with the headless UI tests, or checks that they are current.

.DESCRIPTION
    The screenshots are pictures of the real application: the opt-in test class in
    test/DemaConsulting.CanvasPilot.UiTests (trait Category=Screenshots) builds a demonstration project, shows the
    real windows headless with Avalonia.Headless and Skia and the embedded Inter font, and saves each window as a
    PNG. The tests skip themselves unless the environment variable CANVASPILOT_SCREENSHOT_DIR names an output
    folder, so the normal test run (build.ps1) never writes pictures. This script sets that variable and runs only
    those tests.

    Without -Check it writes the pictures over the ones in docs/images. Review them, and commit the changed
    pictures with the change to the application that made them differ. Pictures with a name that starts with
    "smoke-" come from the smoke test of the tool itself and are never kept.

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

# Selects the screenshot tests, and when asked only those whose name contains the given text
$filter = 'Category=Screenshots'
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
