# 从 assets/brand 下的品牌源文件生成各平台的应用图标。
#
#   pwsh scripts/build-icons.ps1
#
# 源文件:
#   assets/brand/AppIcon_Full_Color.png  —— 白底圆角方块 + 品牌粉图形,直接用作各平台的启动图标
#   assets/brand/AppIcon_Black.svg       —— 同图形的单色版,渲染后取出 alpha 通道作为
#                                           Android 自适应图标的前景层(再染成品牌粉)
#
# 产物:
#   LoliaFrpClient/Assets/icon.png                     Linux .desktop / AppImage 用
#   LoliaFrpClient.Desktop/Assets/app.ico              Windows 可执行文件图标(多尺寸)
#   LoliaFrpClient.Android/Resources/mipmap-*/         Android 启动图标(传统 + 自适应)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$brand = Join-Path $root 'assets/brand'
$fullColor = Join-Path $brand 'AppIcon_Full_Color.png'
$blackSvg = Join-Path $brand 'AppIcon_Black.svg'

# 品牌粉,取自 AppIcon_Full_Color.png 的实测值。
$brandPink = [System.Drawing.Color]::FromArgb(255, 255, 129, 172)

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) "lfc-icons-$([guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

function Resize-Png {
    param([string]$Source, [int]$Size, [string]$Destination)

    $src = [System.Drawing.Image]::FromFile($Source)
    try {
        $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $Size, $Size))
        }
        finally { $g.Dispose() }

        $dir = Split-Path -Parent $Destination
        if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        $bmp.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }
    finally { $src.Dispose() }
}

# 单色 SVG 由 Edge 无头渲染成位图(白底黑图形),再从亮度反推 alpha 并染成品牌粉。
# 之所以不指望 Edge 输出透明底:它的 --default-background-color 在这里不生效,
# 而黑图形在白底上的 alpha 恰好等于 1 - 亮度,反而更稳。
function New-Foreground {
    param([string]$SvgPath, [int]$Canvas, [string]$Destination)

    $edge = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $edge) { throw "找不到 msedge.exe,无法渲染 SVG" }

    # 自适应图标前景层。
    #
    # 尺寸按安全区反推,而不是凭观感:画布 108dp,但任何遮罩只保证中心 66dp 圆
    # 以内不被裁。图形是方的,要整个落进那个圆里,边长最多 66/√2 ≈ 46.7dp,
    # 即画布的 43%。之前取 66% 时对角线有 102dp,四个角被切掉一大块。
    $ratio = 0.43
    $glyph = [int][Math]::Round($Canvas * $ratio)
    $html = Join-Path $scratch "fg-$Canvas.html"
    $png = Join-Path $scratch "fg-$Canvas.png"
    $uri = ([System.Uri](Resolve-Path $SvgPath).Path).AbsoluteUri

    @"
<!doctype html><html><head><meta charset="utf-8"><style>
html,body{margin:0;padding:0;width:${Canvas}px;height:${Canvas}px;background:#fff;overflow:hidden}
body{display:flex;align-items:center;justify-content:center}
img{width:${glyph}px;height:${glyph}px;display:block}
</style></head><body><img src="$uri"></body></html>
"@ | Set-Content -Path $html -Encoding UTF8

    # 尺寸要拼成一个字符串再传:PowerShell 会把 --window-size=432,432 里的逗号
    # 当成数组分隔符,拆成两个参数,Edge 只收到一半就报 Invalid specification 并忽略。
    $size = "$Canvas,$Canvas"
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
        "--screenshot=$png" "--window-size=$size" ([System.Uri]$html).AbsoluteUri | Out-Null
    if (-not (Test-Path $png)) { throw "Edge 渲染失败:$SvgPath" }

    $src = [System.Drawing.Bitmap]::FromFile($png)
    try {
        $out = New-Object System.Drawing.Bitmap $Canvas, $Canvas, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt $Canvas; $y++) {
            for ($x = 0; $x -lt $Canvas; $x++) {
                $p = $src.GetPixel($x, $y)
                # 黑图形 / 白底:alpha = 255 - 亮度。取三通道最小值对纯灰阶等价,且对彩色抗锯齿边更宽容。
                $lum = [Math]::Min($p.R, [Math]::Min($p.G, $p.B))
                $a = 255 - $lum
                $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $brandPink.R, $brandPink.G, $brandPink.B))
            }
        }

        $dir = Split-Path -Parent $Destination
        if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        $out.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        $out.Dispose()
    }
    finally { $src.Dispose() }
}

# ICO 容器:Vista 起每帧可以直接嵌 PNG,所以拼一个头 + 目录 + 各尺寸 PNG 即可。
function Write-Ico {
    param([string[]]$Frames, [string]$Destination)

    $blobs = @($Frames | ForEach-Object { , [System.IO.File]::ReadAllBytes($_) })
    $sizes = @($Frames | ForEach-Object { [int][System.Drawing.Image]::FromFile($_).Width })

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$blobs.Count)

    $offset = 6 + 16 * $blobs.Count
    for ($i = 0; $i -lt $blobs.Count; $i++) {
        $s = $sizes[$i]
        # 256 在这两个字节里记作 0。
        $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
        $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$blobs[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $blobs[$i].Length
    }

    foreach ($b in $blobs) { $bw.Write($b) }
    $bw.Flush()

    $dir = Split-Path -Parent $Destination
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [System.IO.File]::WriteAllBytes($Destination, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose()
}

Write-Host '生成桌面端图标…'
Resize-Png -Source $fullColor -Size 256 -Destination (Join-Path $root 'LoliaFrpClient/Assets/icon.png')

$icoSizes = 16, 24, 32, 48, 64, 128, 256
$icoFrames = @()
foreach ($s in $icoSizes) {
    $f = Join-Path $scratch "ico-$s.png"
    Resize-Png -Source $fullColor -Size $s -Destination $f
    $icoFrames += $f
}
Write-Ico -Frames $icoFrames -Destination (Join-Path $root 'LoliaFrpClient.Desktop/Assets/app.ico')

Write-Host '生成 Android 图标…'
$androidRes = Join-Path $root 'LoliaFrpClient.Android/Resources'
# 每档密度:mipmap 尺寸:自适应前景 = 108dp。
$densities = @(
    @{ Name = 'mdpi';    Legacy = 48;  Adaptive = 108 },
    @{ Name = 'hdpi';    Legacy = 72;  Adaptive = 162 },
    @{ Name = 'xhdpi';   Legacy = 96;  Adaptive = 216 },
    @{ Name = 'xxhdpi';  Legacy = 144; Adaptive = 324 },
    @{ Name = 'xxxhdpi'; Legacy = 192; Adaptive = 432 }
)

foreach ($d in $densities) {
    Resize-Png -Source $fullColor -Size $d.Legacy `
        -Destination (Join-Path $androidRes "mipmap-$($d.Name)/ic_launcher.png")
    New-Foreground -SvgPath $blackSvg -Canvas $d.Adaptive `
        -Destination (Join-Path $androidRes "mipmap-$($d.Name)/ic_launcher_foreground.png")
}

New-Item -ItemType Directory -Force -Path (Join-Path $androidRes 'mipmap-anydpi-v26') | Out-Null
@'
<?xml version="1.0" encoding="utf-8"?>
<!-- 前景是单色图形 + 品牌粉,背景纯白,与 AppIcon_Full_Color 同观感。
     前景由 scripts/build-icons.ps1 从 AppIcon_Black.svg 渲染而来。 -->
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@color/ic_launcher_background" />
    <foreground android:drawable="@mipmap/ic_launcher_foreground" />
</adaptive-icon>
'@ | Set-Content -Path (Join-Path $androidRes 'mipmap-anydpi-v26/ic_launcher.xml') -Encoding UTF8

New-Item -ItemType Directory -Force -Path (Join-Path $androidRes 'values') | Out-Null
@'
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <!-- 与 AppIcon_Full_Color.png 的底色一致。 -->
    <color name="ic_launcher_background">#FFFFFF</color>
</resources>
'@ | Set-Content -Path (Join-Path $androidRes 'values/ic_launcher_background.xml') -Encoding UTF8

Remove-Item -Recurse -Force $scratch
Write-Host '完成。'
