# Publish-Index.ps1
# Packs exact-version patch files (main.js / renderer.js) from the community repo
# into zips with manifest.json, computes SHA-256 + size, and writes an index.
#
# IMPORTANT: patch zips are NEVER committed to this repo.
#   - Zips always go to artifacts/patches/ (gitignored).
#   - Default mode writes the COMMITTED index (resources/index.json) using -PublicBaseUrl,
#     i.e. the remote location where you will upload the zips (e.g. GitHub Releases).
#   - -LocalIndex mode writes artifacts/index.local.json with file:/// urls for offline testing;
#     point the app's "资源仓库 URL" setting at that file while developing.
#
# Usage:
#   ./tools/Publish-Index.ps1 -Versions 3.6.4 -ListOnly
#   ./tools/Publish-Index.ps1 -Versions 3.6.4 -LocalIndex
#   ./tools/Publish-Index.ps1 -Versions 3.5.0 -PublicBaseUrl https://github.com/OWNER/REPO/releases/download/patches-v1/

param(
    [string[]]$Versions = @("3.6.4"),
    [string]$RepoOwner = "743859910",
    [string]$RepoName = "GitHub_Desktop_Simplified_Chinese",
    [string]$Branch = "master",
    [string]$PatchDir = "artifacts/patches",
    [string]$IndexPath = "resources/index.json",
    [string]$LocalIndexPath = "artifacts/index.local.json",
    [string]$PublicBaseUrl = "",
    [switch]$LocalIndex,
    [switch]$ListOnly
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$RawBase = "https://raw.githubusercontent.com/$RepoOwner/$RepoName/$Branch/Version"

function Test-PatchFile([string]$Version, [string]$Name) {
    $url = "$RawBase/$Version/Windows/$Name"
    try {
        $req = [Net.HttpWebRequest]::Create($url)
        $req.Method = 'HEAD'
        $req.Timeout = 30000
        $resp = $req.GetResponse()
        $len = $resp.ContentLength
        $resp.Close()
        return ($len -gt 0)
    } catch { return $false }
}

function Save-PatchFile([string]$Version, [string]$Name, [string]$DestPath) {
    $url = "$RawBase/$Version/Windows/$Name"
    try {
        $client = New-Object System.Net.WebClient
        $client.DownloadFile($url, $DestPath)
        return ((Get-Item $DestPath).Length -gt 0)
    } catch { return $false }
}

if (-not $ListOnly -and -not $LocalIndex -and [string]::IsNullOrWhiteSpace($PublicBaseUrl)) {
    throw "Provide -PublicBaseUrl (remote location of the zips) or use -LocalIndex for offline dev/test."
}

$Versions = $Versions | Sort-Object { [version]$_ } -Descending
Write-Host "Versions to process: $($Versions -join ', ')"

if (-not (Test-Path $PatchDir)) { New-Item -ItemType Directory -Path $PatchDir -Force | Out-Null }

$newEntries = @()
foreach ($ver in $Versions) {
    Write-Host "--- $ver ---"
    $staging = Join-Path ([IO.Path]::GetFullPath("$PSScriptRoot/../obj")) "publish-index/$ver"
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    $files = @()
    $ok = $true
    if ($ListOnly) {
        foreach ($name in @('main.js', 'renderer.js')) {
            if (Test-PatchFile -Version $ver -Name $name) { $files += $name } else { $ok = $false }
        }
        if ($ok) { Write-Host "  OK: $($files -join ', ')" } else { Write-Host "  MISSING files" }
        continue
    }

    foreach ($name in @('main.js', 'renderer.js')) {
        $dest = Join-Path $staging $name
        if (-not (Save-PatchFile -Version $ver -Name $name -DestPath $dest)) { $ok = $false; Write-Host "  FAIL download $name"; break }
        $files += $name
        Write-Host ("  downloaded {0} ({1:N0} bytes)" -f $name, (Get-Item $dest).Length)
    }
    if (-not $ok) { continue }

    # manifest.json
    $manifest = @{
        version   = $ver
        files     = $files
        allowlist = $files
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText((Join-Path $staging 'manifest.json'), $manifestJson)

    # zip -> artifacts/patches (never committed)
    $zipName = "GitHubDesktop-$ver-zh.zip"
    $zipPath = Join-Path $PatchDir $zipName
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    # hash + size
    $sha = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item $zipPath).Length

    if ($LocalIndex) {
        $url = ([Uri][IO.Path]::GetFullPath($zipPath)).AbsoluteUri
    } else {
        $base = if ($PublicBaseUrl.EndsWith('/')) { $PublicBaseUrl } else { "$PublicBaseUrl/" }
        $url = "$base$zipName"
    }

    $newEntries += [ordered]@{
        version = $ver
        url     = $url
        sha256  = $sha
        size    = $size
    }
    Write-Host ("  packed {0} ({1:N0} bytes, sha256={2}...)" -f $zipName, $size, $sha.Substring(0, 12))
}

Remove-Item -Recurse -Force (Join-Path ([IO.Path]::GetFullPath("$PSScriptRoot/../obj")) "publish-index") -ErrorAction SilentlyContinue

if ($ListOnly) { return }
if ($newEntries.Count -eq 0) { throw "No patches were generated." }

$outPath = if ($LocalIndex) { $LocalIndexPath } else { $IndexPath }

# Merge into existing index (preserve entries not regenerated)
$index = @{ patches = @() }
if (Test-Path $outPath) {
    try { $existing = Get-Content $outPath -Raw | ConvertFrom-Json } catch { $existing = $null }
    if ($existing -and $existing.patches) {
        foreach ($p in $existing.patches) {
            if ($newEntries.version -notcontains $p.version) {
                $index.patches += [ordered]@{
                    version = $p.version
                    url     = $p.url
                    sha256  = $p.sha256
                    size    = $p.size
                }
                if ($p.compat) { $index.patches[-1]['compat'] = @($p.compat) }
            }
        }
    }
}
foreach ($e in $newEntries) { $index.patches += $e }
$index.patches = @($index.patches | Sort-Object { [version]$_.version } -Descending)

$json = $index | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText([IO.Path]::GetFullPath($outPath), $json)
Write-Host "`nWrote $outPath with $($index.patches.Count) entries."
