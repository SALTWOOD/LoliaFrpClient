param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$props = Join-Path $root 'Directory.Build.props'
$android = Join-Path $root 'LoliaFrpClient.Android/LoliaFrpClient.Android.csproj'
$manifest = Join-Path $root 'LoliaFrpClient.Desktop/app.manifest'

function Invoke-Git {
    param([string[]]$Arguments)

    $out = git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') exited with $LASTEXITCODE" }
    return $out
}

function Set-FileText {
    param([string]$Path, [string]$Pattern, [string]$Replacement)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($bom) { 3 } else { 0 }

    $text = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    if ($text -notmatch $Pattern) { throw "$Pattern not found in $Path" }

    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    if ($updated -eq $text) { return }

    # Log first: if anything in here blows up under Windows PowerShell, the file is
    # still untouched. Path.GetFileName rather than GetRelativePath, which is .NET Core
    # only and throws on the .NET Framework that powershell.exe runs.
    Write-Host "  $([System.IO.Path]::GetFileName($Path)) -> $Replacement"
    [System.IO.File]::WriteAllText($Path, $updated, [System.Text.UTF8Encoding]::new($bom))
}

if ($Version -notmatch '^v?\d+(\.\d+){0,3}$') {
    throw "wrong version format: '$Version'"
}

$parts = @(($Version -replace '^v', '').Split('.') | ForEach-Object { [int]$_ })
while ($parts.Count -lt 4) { $parts += 0 }

$display = $parts -join '.'
$tag = "v$display"

$current = [regex]::Match(
    [System.IO.File]::ReadAllText($android), '<ApplicationVersion>(\d+)</ApplicationVersion>')
if (-not $current.Success) { throw "<ApplicationVersion> not found in $android" }

$code = [long]$current.Groups[1].Value + 1
if ($code -gt 2000000000) { throw "versionCode out of range: $code" }

Push-Location $root
try {
    if (Invoke-Git @('status', '--porcelain')) {
        throw 'workspace dirty'
    }

    if (Invoke-Git @('tag', '--list', $tag)) { throw "$tag already exists" }

    Write-Host "write ${tag}:"
    Set-FileText $props '<Version>[^<]*</Version>' "<Version>$display</Version>"
    Set-FileText $android '<ApplicationVersion>\d+</ApplicationVersion>' "<ApplicationVersion>$code</ApplicationVersion>"
    Set-FileText $manifest '(?<=<assemblyIdentity version=")[^"]*' $display

    Invoke-Git @('add', '--', $props, $android, $manifest) | Out-Null
    Invoke-Git @('commit', '-q', '-m', "chore(release): $tag") | Out-Null
    Invoke-Git @('tag', '-a', $tag, '-m', "Release $tag") | Out-Null

    Write-Host ''
    Write-Host "versionCode = $code"
    Write-Host 'Push changes with: git push --follow-tags'
}
finally {
    Pop-Location
}
