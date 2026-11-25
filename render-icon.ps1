<#
.SYNOPSIS
    Draws the application icon, Icon.png and Icon.ico, from Icon.svg, or checks that they are current.

.DESCRIPTION
    Icon.svg at the root of the repository is the single source of the CanvasPilot application icon: the brand mark
    of the title bar of the main window (a rounded tile with the accent gradient and a white sparkle). This script
    draws the two pictures that the builds use from it, with "dotnet canvasnet" from the tool manifest:

    - Icon.png, 256 by 256 with transparent corners: the icon as a single picture, for places that need a PNG;
    - Icon.ico, with frames of 16, 24, 32, 48, 64, and 256 px: the icon of CanvasPilot.exe (Explorer, the taskbar,
      Alt-Tab, the Start Menu shortcut), the window icon of every window (an Avalonia resource of the application
      library, so title bars use the 16 and 24 px frames), and the icon of the Add/Remove Programs entry of the
      installer.

    Every frame is drawn from the vector source at its own size, so no frame is a scaled-down picture. At 16 and 24 px
    the outline of the sparkle is thinner than a pixel and blurs, so those two frames draw a solid variant: the script
    keeps only the outer outlines of the two sparkles (the first and the third of the four subpaths of the path with
    the id "sparkle"), which fills them. The frames of 16 to 64 px are 32-bit bitmaps (with an AND mask, as Windows
    expects of an icon) and the 256 px frame is the PNG, as Windows Vista and later read it.

    Without -Check it writes Icon.png and Icon.ico at the root of the repository. Commit them with the change to
    Icon.svg that made them differ.

    With -Check it draws the icon into a temporary folder and compares it with the committed Icon.png and every frame
    of the committed Icon.ico using "canvasnet compare ... --threshold rmse=1 --threshold diff-ratio=0.001". It
    changes nothing in the repository and exits 1 when a committed file is missing, has another layout (other frame
    sizes, another bit depth), or differs by more than the tolerance. The tolerance is not zero on purpose: a drawing
    is not bit-for-bit the same on every operating system (a last-bit difference in floating point may move a few
    anti-aliased pixels), but any change of shape or color is far above it.

    Run it from anywhere; paths are taken from the repository the script is in.

.PARAMETER Check
    Compare a fresh drawing with the committed Icon.png and Icon.ico instead of replacing them.
#>
[CmdletBinding()]
param(
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = $PSScriptRoot
$source = 'Icon.svg'
$committedPng = 'Icon.png'
$committedIco = 'Icon.ico'

# The frames of Icon.ico, in the order they are written: the sizes Windows asks for (16 for a title bar and small
# icons, 24 and 32 for the taskbar and lists, 48 and 64 for Explorer's medium and large views at higher scales)
# and 256 for Explorer's extra-large view.
$frameSizes = @(16, 24, 32, 48, 64, 256)

# The sizes that draw the solid variant of the sparkle (see the description).
$solidSizes = @(16, 24)

# The size of Icon.png and of the PNG frame of Icon.ico. Windows reads a frame this large only as a PNG.
$pngSize = 256

<#
.SYNOPSIS
    Writes the solid variant of the icon: the source with the holes of the two sparkles removed.

.DESCRIPTION
    The icon must stay legible at 16 and 24 px, where the outline of the sparkle is about one pixel wide or less. The
    path with the id "sparkle" in the source has four subpaths: the outer and the inner outline of the large sparkle,
    then the outer and the inner outline of the small sparkle. Keeping the first and the third fills both sparkles.
    The variant is derived here rather than kept as a second source so that the two can never disagree.

.PARAMETER Source
    The path of the source SVG (Icon.svg).

.PARAMETER Destination
    The path of the SVG to write; it is overwritten if it exists.

.NOTES
    Throws when the source has no path with the id "sparkle" or when that path does not have exactly four subpaths,
    because the variant would then be wrong; fix the source (see its header comment). Writes only the destination.
#>
function New-SolidVariant {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    $xml = [xml](Get-Content -LiteralPath $Source -Raw)
    $sparkle = @($xml.GetElementsByTagName('path') | Where-Object { $_.GetAttribute('id') -eq 'sparkle' })
    if ($sparkle.Count -ne 1) {
        throw "${Source}: expected one path with id=""sparkle"", found $($sparkle.Count)"
    }

    # Every subpath starts with an absolute move (M), as in the Fluent glyph the path is copied from.
    $subpaths = @($sparkle[0].GetAttribute('d') -split '(?=M)' |
            Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() })
    if ($subpaths.Count -ne 4) {
        throw "${Source}: the sparkle path must have 4 subpaths (outer and inner outline of each sparkle), " +
        "found $($subpaths.Count)"
    }

    $sparkle[0].SetAttribute('d', $subpaths[0] + ' ' + $subpaths[2])
    $xml.Save($Destination)
}

<#
.SYNOPSIS
    Draws an SVG at a square pixel size with CanvasNet.

.DESCRIPTION
    The one place that runs "canvasnet render", so every frame is drawn the same way: from the vector source at the
    size of the frame (sharp, not scaled from a larger picture), with a transparent background so the rounded corners
    of the tile stay transparent, and with --strict so an unsupported SVG feature fails instead of being skipped.
    The output format follows the extension: .bmp gives a 32-bit bottom-up BGRA bitmap, .png an RGBA PNG.

.PARAMETER Source
    The path of the SVG to draw.

.PARAMETER Size
    The width and height in pixels.

.PARAMETER Destination
    The path of the picture to write; it is overwritten if it exists.

.NOTES
    Throws when CanvasNet exits with an error. Writes only the destination.
#>
function Invoke-CanvasNetRender {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][int]$Size,
        [Parameter(Mandatory)][string]$Destination
    )

    dotnet canvasnet render $Source --size "${Size}x${Size}" --background transparent --strict -o $Destination |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "canvasnet render of $Source at $Size px failed with exit code $LASTEXITCODE"
    }
}

<#
.SYNOPSIS
    Reads the pixels of a 32-bit bitmap file that CanvasNet wrote.

.DESCRIPTION
    An icon frame of 16 to 64 px holds the same pixels as a 32-bit BMP file: bottom-up rows of blue, green, red, and
    alpha bytes, with straight (not premultiplied) alpha. CanvasNet writes exactly that, so the pixels are taken as
    they are. The header is checked first, because packing a bitmap of another layout would give a broken icon.

.PARAMETER Path
    The path of the BMP file.

.PARAMETER Size
    The width and height the bitmap must have.

.OUTPUTS
    The pixel bytes, Size * Size * 4 of them, bottom row first.

.NOTES
    Throws when the file is not a BMP with a 40-byte header, the expected size, a positive (bottom-up) height,
    32 bits per pixel, and no compression.
#>
function Read-BitmapPixel {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$Size
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $pixelLength = $Size * $Size * 4
    if ($bytes.Length -lt 54 -or $bytes[0] -ne 0x42 -or $bytes[1] -ne 0x4D) {
        throw "${Path}: not a BMP file"
    }

    $pixelOffset = [BitConverter]::ToUInt32($bytes, 10)
    $headerSize = [BitConverter]::ToUInt32($bytes, 14)
    $width = [BitConverter]::ToInt32($bytes, 18)
    $height = [BitConverter]::ToInt32($bytes, 22)
    $bitCount = [BitConverter]::ToUInt16($bytes, 28)
    $compression = [BitConverter]::ToUInt32($bytes, 30)
    if ($headerSize -ne 40 -or $width -ne $Size -or $height -ne $Size -or $bitCount -ne 32 -or $compression -ne 0 -or
        $bytes.Length -lt $pixelOffset + $pixelLength) {
        throw "${Path}: expected a ${Size}x${Size} bottom-up 32-bit uncompressed BMP, found header $headerSize, " +
        "${width}x${height}, $bitCount bits, compression $compression"
    }

    return , [byte[]]$bytes[$pixelOffset..($pixelOffset + $pixelLength - 1)]
}

<#
.SYNOPSIS
    Makes the AND mask of an icon frame from the alpha of its pixels.

.DESCRIPTION
    An icon bitmap is followed by a 1-bit AND mask that tells programs that ignore alpha which pixels are transparent.
    A bit is set where the pixel is fully transparent (alpha 0). The rows are bottom-up like the pixels, the bits of a
    byte run from the most significant bit leftmost, and every row is padded to a multiple of 4 bytes.

.PARAMETER Pixel
    The bottom-up BGRA pixel bytes of the frame (Size * Size * 4).

.PARAMETER Size
    The width and height of the frame in pixels.

.OUTPUTS
    The mask bytes.
#>
function New-IconMask {
    param(
        [Parameter(Mandatory)][byte[]]$Pixel,
        [Parameter(Mandatory)][int]$Size
    )

    $stride = [int]([Math]::Ceiling($Size / 32.0) * 4)
    $mask = [byte[]]::new($stride * $Size)
    for ($y = 0; $y -lt $Size; $y++) {
        for ($x = 0; $x -lt $Size; $x++) {
            if ($Pixel[($y * $Size + $x) * 4 + 3] -eq 0) {
                $index = $y * $stride + [Math]::Floor($x / 8)
                $mask[$index] = $mask[$index] -bor (0x80 -shr ($x % 8))
            }
        }
    }

    return , $mask
}

<#
.SYNOPSIS
    Packs the pixels of a frame into the bitmap form an icon file stores.

.DESCRIPTION
    A bitmap frame of an icon file is a BITMAPINFOHEADER, the pixels, and the AND mask. The header gives twice the
    height of the frame, because by the icon format the height covers the pixels and the mask stacked together.

.PARAMETER Pixel
    The bottom-up BGRA pixel bytes of the frame (Size * Size * 4).

.PARAMETER Size
    The width and height of the frame in pixels.

.OUTPUTS
    The bytes of the frame, as they go into the icon file.
#>
function ConvertTo-IconFrame {
    param(
        [Parameter(Mandatory)][byte[]]$Pixel,
        [Parameter(Mandatory)][int]$Size
    )

    $mask = New-IconMask -Pixel $Pixel -Size $Size
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint32]40)                               # biSize
        $writer.Write([int32]$Size)                             # biWidth
        $writer.Write([int32]($Size * 2))                       # biHeight: pixels and mask together
        $writer.Write([uint16]1)                                # biPlanes
        $writer.Write([uint16]32)                               # biBitCount
        $writer.Write([uint32]0)                                # biCompression: BI_RGB
        $writer.Write([uint32]($Pixel.Length + $mask.Length))   # biSizeImage
        $writer.Write([int32]0)                                 # biXPelsPerMeter
        $writer.Write([int32]0)                                 # biYPelsPerMeter
        $writer.Write([uint32]0)                                # biClrUsed
        $writer.Write([uint32]0)                                # biClrImportant
        $writer.Write($Pixel)
        $writer.Write($mask)
        $writer.Flush()
        return , $stream.ToArray()
    }
    finally {
        $writer.Dispose()
    }
}

<#
.SYNOPSIS
    Writes an icon file from its frames.

.DESCRIPTION
    An icon file is an ICONDIR (reserved 0, type 1 for an icon, the number of frames), one 16-byte ICONDIRENTRY per
    frame, and then the frames. An entry gives the width and the height as one byte each, 0 standing for 256, the
    bit depth, the length of the frame, and where it starts in the file.

.PARAMETER Path
    The path of the icon file; it is overwritten if it exists.

.PARAMETER Frame
    The frames in the order to write them, each a hashtable with Size (pixels) and Bytes (a packed bitmap frame or
    the bytes of a PNG file).
#>
function Write-IconFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable[]]$Frame
    )

    $stream = [System.IO.File]::Create($Path)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)               # reserved
        $writer.Write([uint16]1)               # type: icon
        $writer.Write([uint16]$Frame.Count)

        $offset = 6 + 16 * $Frame.Count
        foreach ($item in $Frame) {
            $dimension = if ($item.Size -ge 256) { 0 } else { $item.Size }
            $writer.Write([byte]$dimension)    # width
            $writer.Write([byte]$dimension)    # height
            $writer.Write([byte]0)             # colors in the palette: none
            $writer.Write([byte]0)             # reserved
            $writer.Write([uint16]1)           # color planes
            $writer.Write([uint16]32)          # bits per pixel
            $writer.Write([uint32]$item.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $item.Bytes.Length
        }

        foreach ($item in $Frame) {
            $writer.Write([byte[]]$item.Bytes)
        }
    }
    finally {
        $writer.Dispose()
    }
}

<#
.SYNOPSIS
    Reads the frames of an icon file.

.DESCRIPTION
    Used by -Check to compare a committed Icon.ico frame by frame with a fresh drawing. It reads the directory and
    returns every frame with its size and whether it is a PNG or a bitmap.

.PARAMETER Path
    The path of the icon file.

.OUTPUTS
    One object per frame, in file order: Size (pixels; 256 for an entry of 0), BitCount, IsPng, and Bytes.

.NOTES
    Throws when the file is not an icon file or an entry points outside the file.
#>
function Read-IconFile {
    param(
        [Parameter(Mandatory)][string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 6 -or [BitConverter]::ToUInt16($bytes, 0) -ne 0 -or
        [BitConverter]::ToUInt16($bytes, 2) -ne 1) {
        throw "${Path}: not an icon file"
    }

    $count = [BitConverter]::ToUInt16($bytes, 4)
    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + 16 * $index
        $width = if ($bytes[$entry] -eq 0) { 256 } else { [int]$bytes[$entry] }
        $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
        $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
        if ($length -lt 8 -or $offset + $length -gt $bytes.Length) {
            throw "${Path}: frame $index points outside the file"
        }

        $data = [byte[]]$bytes[$offset..($offset + $length - 1)]
        [pscustomobject]@{
            Size     = $width
            BitCount = [BitConverter]::ToUInt16($bytes, $entry + 6)
            IsPng    = ($data[0] -eq 0x89 -and $data[1] -eq 0x50 -and $data[2] -eq 0x4E -and $data[3] -eq 0x47)
            Bytes    = $data
        }
    }
}

<#
.SYNOPSIS
    Turns a bitmap frame of an icon file back into a BMP file.

.DESCRIPTION
    CanvasNet compares pictures, not icon frames, so -Check writes each committed bitmap frame as a BMP file (a
    14-byte file header and the frame's header with the true height) to compare it with the fresh drawing. The frame
    is checked on the way: its header must be the 32-bit layout this script writes, and its AND mask must agree with
    the alpha of its pixels.

.PARAMETER Frame
    The bytes of the bitmap frame, as stored in the icon file.

.PARAMETER Size
    The width and height of the frame in pixels.

.PARAMETER Destination
    The path of the BMP file to write.

.OUTPUTS
    $null when the frame is well formed; otherwise the reason it is not (and nothing is written).
#>
function ConvertFrom-IconFrame {
    param(
        [Parameter(Mandatory)][byte[]]$Frame,
        [Parameter(Mandatory)][int]$Size,
        [Parameter(Mandatory)][string]$Destination
    )

    $pixelLength = $Size * $Size * 4
    if ($Frame.Length -lt 40 + $pixelLength -or [BitConverter]::ToUInt32($Frame, 0) -ne 40 -or
        [BitConverter]::ToInt32($Frame, 4) -ne $Size -or [BitConverter]::ToInt32($Frame, 8) -ne $Size * 2 -or
        [BitConverter]::ToUInt16($Frame, 14) -ne 32 -or [BitConverter]::ToUInt32($Frame, 16) -ne 0) {
        return "the frame is not a ${Size}x${Size} 32-bit bitmap"
    }

    $pixel = [byte[]]$Frame[40..(40 + $pixelLength - 1)]
    $mask = New-IconMask -Pixel $pixel -Size $Size
    if ($Frame.Length -ne 40 + $pixelLength + $mask.Length -or
        [Convert]::ToBase64String($Frame, 40 + $pixelLength, $mask.Length) -ne [Convert]::ToBase64String($mask)) {
        return 'the AND mask does not match the transparent pixels'
    }

    $stream = [System.IO.File]::Create($Destination)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([byte]0x42)                       # "BM"
        $writer.Write([byte]0x4D)
        $writer.Write([uint32](54 + $pixelLength))      # file size
        $writer.Write([uint32]0)                        # reserved
        $writer.Write([uint32]54)                       # offset of the pixels
        $writer.Write([uint32]40)                       # the info header, with the true height
        $writer.Write([int32]$Size)
        $writer.Write([int32]$Size)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]0)
        $writer.Write([uint32]$pixelLength)
        $writer.Write([int32]0)
        $writer.Write([int32]0)
        $writer.Write([uint32]0)
        $writer.Write([uint32]0)
        $writer.Write($pixel)
    }
    finally {
        $writer.Dispose()
    }

    return $null
}

<#
.SYNOPSIS
    Draws every frame of the icon into a work folder.

.DESCRIPTION
    Shared by both modes, so -Check compares against exactly what a normal run writes. Draws the solid variant for
    the small sizes, the source for the others, and the PNG frame.

.PARAMETER Work
    The folder to draw into.

.OUTPUTS
    A hashtable with Frames (the frames for Write-IconFile, in the order of $frameSizes), Png (the path of the fresh
    256 px PNG), and Bitmaps (the path of the fresh BMP per bitmap frame size).
#>
function New-IconDrawing {
    param(
        [Parameter(Mandatory)][string]$Work
    )

    $solid = Join-Path $Work 'Icon-solid.svg'
    New-SolidVariant -Source (Join-Path $repo $source) -Destination $solid

    $frames = @()
    $bitmaps = @{}
    $png = Join-Path $Work "icon-$pngSize.png"
    foreach ($size in $frameSizes) {
        $from = if ($solidSizes -contains $size) { $solid } else { Join-Path $repo $source }
        if ($size -eq $pngSize) {
            Invoke-CanvasNetRender -Source $from -Size $size -Destination $png
            $frames += @{ Size = $size; Bytes = [System.IO.File]::ReadAllBytes($png) }
        }
        else {
            $bitmap = Join-Path $Work "icon-$size.bmp"
            Invoke-CanvasNetRender -Source $from -Size $size -Destination $bitmap
            $pixel = Read-BitmapPixel -Path $bitmap -Size $size
            $frames += @{ Size = $size; Bytes = (ConvertTo-IconFrame -Pixel $pixel -Size $size) }
            $bitmaps[$size] = $bitmap
        }
    }

    return @{ Frames = $frames; Png = $png; Bitmaps = $bitmaps }
}

<#
.SYNOPSIS
    Compares a committed picture with a fresh drawing and reports the result.

.DESCRIPTION
    Runs "canvasnet compare" with the tolerance of the script (see the description of the script) and prints an
    "ok" or a "FAIL" line for the picture.

.PARAMETER Label
    What is compared, as printed (for example "Icon.ico 16 px").

.PARAMETER Committed
    The path of the committed picture (or of a frame written out of the committed icon).

.PARAMETER Fresh
    The path of the fresh drawing.

.OUTPUTS
    $true when the two agree within the tolerance.
#>
function Compare-Picture {
    param(
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Committed,
        [Parameter(Mandatory)][string]$Fresh
    )

    $json = dotnet canvasnet compare $Committed $Fresh --threshold 'rmse=1' --threshold 'diff-ratio=0.001' --json `
        2>$null
    $exit = $LASTEXITCODE
    $result = $null
    try { $result = ($json | Out-String) | ConvertFrom-Json } catch { $result = $null }

    if ($exit -eq 0) {
        Write-Host ("ok    {0}: rmse {1:N3}, differing pixels {2:P3}" -f `
                $Label, $result.metrics.rmse, $result.metrics.diffPixelRatio)
        return $true
    }

    $reason = if ($result -and $result.PSObject.Properties['error'] -and $result.error) {
        $result.error.message
    }
    else {
        "it differs from a fresh drawing of $source (canvasnet compare exited with code $exit)"
    }
    Write-Host "FAIL  ${Label}: $reason"
    if ($result -and $result.PSObject.Properties['metrics']) {
        Write-Host ("      rmse {0:N3}, differing pixels {1:P3}" -f `
                $result.metrics.rmse, $result.metrics.diffPixelRatio)
    }
    return $false
}

<#
.SYNOPSIS
    Checks the committed Icon.png and Icon.ico against a fresh drawing.

.DESCRIPTION
    The body of -Check: Icon.png must match the fresh 256 px drawing, and Icon.ico must have exactly the frames of
    $frameSizes, bitmaps of 32 bits except the PNG frame, each matching its fresh drawing.

.PARAMETER Drawing
    The fresh drawing from New-IconDrawing.

.PARAMETER Work
    The work folder, for the frames written out of the committed icon.

.OUTPUTS
    $true when everything is current.
#>
function Test-CommittedIcon {
    param(
        [Parameter(Mandatory)][hashtable]$Drawing,
        [Parameter(Mandatory)][string]$Work
    )

    $current = $true

    # Icon.png
    if (-not (Test-Path -LiteralPath (Join-Path $repo $committedPng))) {
        Write-Host "FAIL  ${committedPng}: the committed picture is missing"
        $current = $false
    }
    elseif (-not (Compare-Picture -Label $committedPng -Committed (Join-Path $repo $committedPng) `
                -Fresh $Drawing.Png)) {
        $current = $false
    }

    # Icon.ico: the layout first, then every frame
    if (-not (Test-Path -LiteralPath (Join-Path $repo $committedIco))) {
        Write-Host "FAIL  ${committedIco}: the committed icon is missing"
        return $false
    }

    $frames = @(Read-IconFile -Path (Join-Path $repo $committedIco))
    $sizes = ($frames | ForEach-Object { $_.Size }) -join ', '
    if ($sizes -ne ($frameSizes -join ', ')) {
        Write-Host "FAIL  ${committedIco}: has frames of $sizes px, expected $($frameSizes -join ', ')"
        return $false
    }

    foreach ($frame in $frames) {
        $label = "$committedIco $($frame.Size) px"
        $committedFrame = Join-Path $Work "committed-$($frame.Size)"
        if ($frame.Size -eq $pngSize) {
            if (-not $frame.IsPng) {
                Write-Host "FAIL  ${label}: the frame is not a PNG"
                $current = $false
                continue
            }
            [System.IO.File]::WriteAllBytes("$committedFrame.png", $frame.Bytes)
            if (-not (Compare-Picture -Label $label -Committed "$committedFrame.png" -Fresh $Drawing.Png)) {
                $current = $false
            }
            continue
        }

        $problem = if ($frame.IsPng -or $frame.BitCount -ne 32) {
            'the frame is not a 32-bit bitmap'
        }
        else {
            ConvertFrom-IconFrame -Frame $frame.Bytes -Size $frame.Size -Destination "$committedFrame.bmp"
        }
        if ($problem) {
            Write-Host "FAIL  ${label}: $problem"
            $current = $false
        }
        elseif (-not (Compare-Picture -Label $label -Committed "$committedFrame.bmp" `
                    -Fresh $Drawing.Bitmaps[$frame.Size])) {
            $current = $false
        }
    }

    return $current
}

Push-Location $repo
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE"
    }

    $work = Join-Path ([System.IO.Path]::GetTempPath()) `
        ('render-icon-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    $failed = $false
    try {
        $drawing = New-IconDrawing -Work $work

        if (-not $Check) {
            # Icon.png and the PNG frame of Icon.ico are the same file.
            Copy-Item -LiteralPath $drawing.Png -Destination (Join-Path $repo $committedPng) -Force
            Write-IconFile -Path (Join-Path $repo $committedIco) -Frame $drawing.Frames
            Write-Host "wrote $committedPng ($pngSize px) and $committedIco ($($frameSizes -join ', ') px) from $source"
        }
        elseif (Test-CommittedIcon -Drawing $drawing -Work $work) {
            Write-Host 'The committed icon is current.'
        }
        else {
            $failed = $true
            Write-Host '      If the change is intended, run: pwsh ./render-icon.ps1 and commit Icon.png and Icon.ico.'
            Write-Host "      The fresh drawing is kept in $work"
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
