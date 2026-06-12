$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$distDir = Join-Path $projectRoot "dist"
$assetsDir = Join-Path $projectRoot "assets"
$iconPath = Join-Path $assetsDir "switch.ico"
$versionFile = Join-Path $projectRoot "VERSION"
$objDir = Join-Path $projectRoot "obj"
$generatedAssemblyInfoPath = Join-Path $objDir "GeneratedAssemblyInfo.cs"
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $cscPath)) {
    throw "Missing csc.exe at $cscPath"
}

if (-not (Test-Path $versionFile)) {
    throw "Missing version file at $versionFile"
}

$appVersion = (Get-Content $versionFile -Raw).Trim()

if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "VERSION file is empty"
}

function New-RoundedRectanglePath {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.RectangleF]$Rect,
        [Parameter(Mandatory = $true)]
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath

    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    return $path
}

function New-SwitchBitmap {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $outerPadding = [float]($Size * 0.06)
        $backgroundRect = [System.Drawing.RectangleF]::new(
            [single]$outerPadding,
            [single]$outerPadding,
            [single]($Size - (2 * $outerPadding)),
            [single]($Size - (2 * $outerPadding)))
        $backgroundRadius = [float]($Size * 0.20)
        $backgroundPath = New-RoundedRectanglePath -Rect $backgroundRect -Radius $backgroundRadius
        $backgroundBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 22, 25, 33))
        $graphics.FillPath($backgroundBrush, $backgroundPath)

        $panelPadding = [float]($Size * 0.15)
        $panelWidth = [float]($Size * 0.24)
        $panelHeight = [float]($Size * 0.68)
        $panelTop = [float]($Size * 0.16)
        $leftPanelRect = [System.Drawing.RectangleF]::new(
            [single]$panelPadding,
            [single]$panelTop,
            [single]$panelWidth,
            [single]$panelHeight)
        $rightPanelRect = [System.Drawing.RectangleF]::new(
            [single]($Size - $panelPadding - $panelWidth),
            [single]$panelTop,
            [single]$panelWidth,
            [single]$panelHeight)
        $panelRadius = [float]($panelWidth / 2)
        $leftPanelPath = New-RoundedRectanglePath -Rect $leftPanelRect -Radius $panelRadius
        $rightPanelPath = New-RoundedRectanglePath -Rect $rightPanelRect -Radius $panelRadius

        $leftBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 231, 70, 78))
        $rightBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 65, 191, 255))
        $graphics.FillPath($leftBrush, $leftPanelPath)
        $graphics.FillPath($rightBrush, $rightPanelPath)

        $separatorBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 245, 248, 252))
        $separatorRect = [System.Drawing.RectangleF]::new(
            [single]($Size * 0.485),
            [single]($Size * 0.18),
            [single]($Size * 0.03),
            [single]($Size * 0.64))
        $graphics.FillRectangle($separatorBrush, $separatorRect)

        $smallCircleSize = [float]($Size * 0.10)
        $largeCircleSize = [float]($Size * 0.18)
        $leftCircleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $rightCircleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

        $graphics.FillEllipse(
            $leftCircleBrush,
            [float]($leftPanelRect.X + (($leftPanelRect.Width - $smallCircleSize) / 2)),
            [float]($Size * 0.30),
            $smallCircleSize,
            $smallCircleSize)
        $graphics.FillEllipse(
            $rightCircleBrush,
            [float]($rightPanelRect.X + (($rightPanelRect.Width - $largeCircleSize) / 2)),
            [float]($Size * 0.49),
            $largeCircleSize,
            $largeCircleSize)

        return $bitmap
    }
    finally {
        if ($null -ne $graphics) {
            $graphics.Dispose()
        }
    }
}

function New-IcoFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = @()
    $index = 0

    foreach ($size in $sizes) {
        $bitmap = New-SwitchBitmap -Size $size
        $memoryStream = New-Object System.IO.MemoryStream

        try {
            $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
            $images += [PSCustomObject]@{
                Size = $size
                Bytes = $memoryStream.ToArray()
            }
        }
        finally {
            $memoryStream.Dispose()
            $bitmap.Dispose()
        }
    }

    $fileStream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
    $writer = New-Object System.IO.BinaryWriter $fileStream

    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$images.Count)

        $offset = 6 + ($images.Count * 16)

        foreach ($image in $images) {
            $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }

            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$image.Bytes.Length)
            $writer.Write([UInt32]$offset)

            $offset += $image.Bytes.Length
        }

        foreach ($image in $images) {
            $writer.Write($image.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
    }
}

function Find-WebView2Assembly {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $searchRoots = @(
        "C:\Program Files",
        "C:\Program Files (x86)"
    ) | Where-Object { Test-Path $_ }

    $match = Get-ChildItem -Path $searchRoots -Recurse -Filter $FileName -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($null -eq $match) {
        throw "Missing WebView2 dependency: $FileName"
    }

    return $match.FullName
}

function Resolve-WebView2Artifacts {
    $winFormsPath = Find-WebView2Assembly -FileName "Microsoft.Web.WebView2.WinForms.dll"
    $corePath = Join-Path (Split-Path -Parent $winFormsPath) "Microsoft.Web.WebView2.Core.dll"

    if (-not (Test-Path $corePath)) {
        throw "Missing Core DLL beside WinForms DLL: $corePath"
    }

    $loaderCandidates = @(
        (Join-Path (Split-Path -Parent $winFormsPath) "runtimes\win-x64\native\WebView2Loader.dll"),
        (Join-Path (Split-Path -Parent $winFormsPath) "WebView2Loader.dll"),
        (Find-WebView2Assembly -FileName "WebView2Loader.dll")
    )

    $loaderPath = $loaderCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($loaderPath)) {
        throw "Missing WebView2Loader.dll"
    }

    [PSCustomObject]@{
        WinForms = $winFormsPath
        Core = $corePath
        Loader = $loaderPath
    }
}

New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
New-IcoFile -Path $iconPath

$artifacts = Resolve-WebView2Artifacts

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
Get-ChildItem -Path $distDir -Force | Remove-Item -Force -Recurse
New-Item -ItemType Directory -Path $objDir -Force | Out-Null

$generatedAssemblyInfo = @"
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Switch")]
[assembly: AssemblyDescription("Switch local web and command control center")]
[assembly: AssemblyCompany("AChttp")]
[assembly: AssemblyProduct("Switch")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("$appVersion.0")]
[assembly: AssemblyFileVersion("$appVersion.0")]
[assembly: AssemblyInformationalVersion("v$appVersion")]
"@

Set-Content -Path $generatedAssemblyInfoPath -Value $generatedAssemblyInfo -Encoding UTF8

$outputExe = Join-Path $distDir "Switch.exe"
$outputZip = Join-Path $distDir ("Switch-v{0}-win-x64.zip" -f $appVersion)
$sourceFiles = Get-ChildItem -Path (Join-Path $projectRoot "src") -Recurse -Filter *.cs |
    Sort-Object FullName |
    Select-Object -ExpandProperty FullName

if ($sourceFiles.Count -eq 0) {
    throw "No C# source files were found under $projectRoot\\src"
}

$compileArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/out:$outputExe",
    "/win32icon:$iconPath",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Runtime.Serialization.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:$($artifacts.Core)",
    "/reference:$($artifacts.WinForms)",
    "/resource:$($artifacts.Core),LocalWebTrayShell.Resources.Microsoft.Web.WebView2.Core.dll",
    "/resource:$($artifacts.WinForms),LocalWebTrayShell.Resources.Microsoft.Web.WebView2.WinForms.dll",
    "/resource:$($artifacts.Loader),LocalWebTrayShell.Resources.WebView2Loader.dll"
)

$compileArgs += $sourceFiles
$compileArgs += $generatedAssemblyInfoPath

& $cscPath $compileArgs

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

$releaseStageDir = Join-Path $distDir "release-stage"
New-Item -ItemType Directory -Path $releaseStageDir -Force | Out-Null
Copy-Item -Path $outputExe -Destination (Join-Path $releaseStageDir "Switch.exe") -Force
Copy-Item -Path (Join-Path $projectRoot "README.md") -Destination (Join-Path $releaseStageDir "README.md") -Force
Copy-Item -Path (Join-Path $projectRoot "LICENSE") -Destination (Join-Path $releaseStageDir "LICENSE") -Force
Compress-Archive -Path (Join-Path $releaseStageDir "*") -DestinationPath $outputZip -Force
Remove-Item -Path $releaseStageDir -Recurse -Force

Write-Host ""
Write-Host "Build complete:"
Write-Host "  EXE  : $outputExe"
Write-Host "  ZIP  : $outputZip"
Write-Host "  ICON : $iconPath"
Write-Host "  VER  : $appVersion"
