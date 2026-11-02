<#
.SYNOPSIS
    Builds the release packages: a self-contained publish packed by Velopack into Setup.exe and a portable zip.

.DESCRIPTION
    Output goes to artifacts\release\<rid>\:
      BtBatteryTray-<rid>-Setup.exe     per-user installer (no admin), Start menu shortcut, clean uninstall
      BtBatteryTray-<rid>-Portable.zip  unzip and run, nothing installed
    plus the .nupkg / releases.*.json files Velopack needs to serve updates later.

    The .NET runtime and the Windows App SDK runtime are both bundled, so target machines need nothing preinstalled.

.EXAMPLE
    .\build\release.ps1
    .\build\release.ps1 -Version 0.2.0 -Runtime win-x64, win-arm64
    .\build\release.ps1 -Force   # rebuild a version that was already packed
#>
param(
    # Defaults to <Version> in the csproj.
    [string] $Version,
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $Runtime = @('win-x64'),
    # Velopack refuses to pack a version that is not newer than what the output folder already holds.
    # -Force clears that folder first, which also drops older releases (only matters once updates use deltas).
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$root = Split-Path $PSScriptRoot -Parent
$projectDir = Join-Path $root 'src\BtBatteryTray'
$project = Join-Path $projectDir 'BtBatteryTray.csproj'

if (-not $Version) {
    $Version = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not SemVer (e.g. 1.2.3 or 1.2.3-beta.1); Velopack requires it."
}

dotnet tool restore --tool-manifest (Join-Path $root 'dotnet-tools.json') | Out-Null

foreach ($rid in $Runtime) {
    $platform = if ($rid -eq 'win-arm64') { 'ARM64' } else { 'x64' }
    $publishDir = Join-Path $root "artifacts\publish\$rid"
    $releaseDir = Join-Path $root "artifacts\release\$rid"

    if (Test-Path (Join-Path $releaseDir "BtBatteryTray-$Version-$rid-full.nupkg")) {
        if (-not $Force) {
            throw "$Version for $rid was already packed into $releaseDir. Bump <Version> in the csproj, or pass -Force to rebuild it."
        }
        Remove-Item -Recurse -Force $releaseDir
    }

    Write-Host "==> Publishing $Version for $rid" -ForegroundColor Cyan
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
    dotnet publish $project -c Release -r $rid --self-contained "-p:Platform=$platform" "-p:Version=$Version" -o $publishDir -nologo -v q

    Write-Host "==> Packing $rid" -ForegroundColor Cyan
    # A channel per architecture keeps their update feeds apart once updates are turned on.
    dotnet vpk pack `
        --packId BtBatteryTray `
        --packVersion $Version `
        --packTitle '蓝牙电量监控' `
        --packDir $publishDir `
        --mainExe BtBatteryTray.exe `
        --icon (Join-Path $projectDir 'Assets\AppIcon.ico') `
        --shortcuts StartMenuRoot `
        --runtime $rid `
        --channel $rid `
        --outputDir $releaseDir

    Write-Host "==> $rid done:" -ForegroundColor Green
    Get-ChildItem $releaseDir -File | Where-Object Extension -in '.exe', '.zip' |
        ForEach-Object { '    {0}  ({1:N1} MB)' -f $_.Name, ($_.Length / 1MB) }
}
