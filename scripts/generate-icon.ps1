$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$outDir = Join-Path $PSScriptRoot "..\assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$bmp = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

$rect = [System.Drawing.Rectangle]::new(18, 30, 220, 190)
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$r = 32
$path.AddArc($rect.X, $rect.Y, $r, $r, 180, 90)
$path.AddArc($rect.Right - $r, $rect.Y, $r, $r, 270, 90)
$path.AddArc($rect.Right - $r, $rect.Bottom - $r, $r, $r, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $r, $r, $r, 90, 90)
$path.CloseFigure()
$fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 10, 20, 33))
$g.FillPath($fill, $path)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 40, 190, 235), 8)
$g.DrawPath($pen, $path)
$divider = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 47, 76, 102), 4)
$g.DrawLine($divider, 28, 82, 228, 82)

$circles = @(
  @{X=53; Color=[System.Drawing.Color]::FromArgb(255,244,83,95)},
  @{X=80; Color=[System.Drawing.Color]::FromArgb(255,255,195,93)},
  @{X=107; Color=[System.Drawing.Color]::FromArgb(255,36,214,142)}
)
foreach ($circle in $circles) {
  $brush = [System.Drawing.SolidBrush]::new($circle.Color)
  $g.FillEllipse($brush, $circle.X, 56, 16, 16)
  $brush.Dispose()
}

$font = [System.Drawing.Font]::new("Consolas", 70, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$green = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,45,245,137))
$g.DrawString(">_", $font, $green, 45, 94)
$shield = [System.Drawing.Drawing2D.GraphicsPath]::new()
$points = [System.Drawing.Point[]]@(
  [System.Drawing.Point]::new(178,145), [System.Drawing.Point]::new(222,160),
  [System.Drawing.Point]::new(218,196), [System.Drawing.Point]::new(198,216),
  [System.Drawing.Point]::new(178,196)
)
$shield.AddPolygon($points)
$shieldFill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,12,49,78))
$shieldPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255,52,211,238), 5)
$g.FillPath($shieldFill, $shield)
$g.DrawPath($shieldPen, $shield)

$png = Join-Path $outDir "HattrickSSH.png"
$ico = Join-Path $outDir "HattrickSSH.ico"
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$bytes = [System.IO.File]::ReadAllBytes($png)
$stream = [System.IO.File]::Create($ico)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$bytes.Length); $writer.Write([UInt32]22)
$writer.Write($bytes)
$writer.Dispose(); $stream.Dispose()
Write-Host "Generated transparent $ico"
