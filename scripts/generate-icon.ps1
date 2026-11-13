$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$outDir = Join-Path $PSScriptRoot "..\assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)
$rect = New-Object System.Drawing.Rectangle(18, 30, 220, 190)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 32
$path.AddArc($rect.X,$rect.Y,$r,$r,180,90); $path.AddArc($rect.Right-$r,$rect.Y,$r,$r,270,90)
$path.AddArc($rect.Right-$r,$rect.Bottom-$r,$r,$r,0,90); $path.AddArc($rect.X,$rect.Bottom-$r,$r,$r,90,90); $path.CloseFigure()
$fill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,10,20,33))
$g.FillPath($fill,$path)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,40,190,235),8)
$g.DrawPath($pen,$path)
$g.DrawLine((New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,47,76,102),4)),28,82,228,82)
foreach ($item in @(@(53,56,244,83,95),@(80,56,255,195,93),@(107,56,36,214,142))) {
  $b = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($item[2],$item[3],$item[4]))
  $g.FillEllipse($b,$item[0],$item[1],16,16)
}
$font = New-Object System.Drawing.Font("Consolas",70,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,45,245,137))
$g.DrawString(">_", $font, $green, 45, 94)
# A compact cyan shield marks the protected Cloudflare route.
$shield = New-Object System.Drawing.Drawing2D.GraphicsPath
$shield.AddPolygon([System.Drawing.Point[]]@([System.Drawing.Point]::new(178,145),[System.Drawing.Point]::new(222,160),[System.Drawing.Point]::new(218,196),[System.Drawing.Point]::new(198,216),[System.Drawing.Point]::new(178,196)))
$g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,12,49,78))),$shield)
$g.DrawPath((New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,52,211,238),5)),$shield)
$g.Dispose()
$png = Join-Path $outDir "HattrickSSH.png"
$ico = Join-Path $outDir "HattrickSSH.ico"
$bmp.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$bytes = [System.IO.File]::ReadAllBytes($png)
$stream = [System.IO.File]::Create($ico)
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$bytes.Length); $writer.Write([UInt32]22)
$writer.Write($bytes); $writer.Dispose(); $stream.Dispose()
Write-Host "Generated transparent $ico"
