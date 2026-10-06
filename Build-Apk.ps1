#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds an Android APK from an export preset, and refuses to hand you a stale one.

.DESCRIPTION
    Godot's Android export SKIPS the C# build when its own output is newer than your sources, and an
    edit saved while an export is running lands inside exactly that window. Measured 2026-09-19:
    KinBoard.cs was saved at 00:55:14 with an export in flight, that export compiled at 00:55:15
    without the change, and every later export then saw a .dll one second newer than the .cs and
    skipped the rebuild — exit 0, 184 assemblies, plausible size, shipping pre-edit code
    indefinitely. (Measured on the KIN branch; the export pipeline is the same here.) `dotnet build` said nothing, because only the ExportRelease assembly was stale.

    So this script makes staleness impossible rather than asking anyone to remember a rule:

      1. Deletes the ExportRelease bin/obj, so the C# is always compiled fresh.
      2. Exports.
      3. Fails loudly if the APK carries no assemblies (the missing-solution trap: a SUCCESSFUL
         export with zero C# in it), or if any source file is newer than the assembly that was
         built — which is what an edit during the export looks like.

    Commands.md has the other quiet ways this export fails.

.EXAMPLE
    ./Build-Apk.ps1                                           # MTG -> build/mtg.apk
    ./Build-Apk.ps1 -Preset Android -Apk build/endling.apk    # another game's preset

    Presets live in SQGodotCommon/export_presets.cfg, which is gitignored and SHARED by every
    branch checked out in this directory — add a preset per game, never repurpose another's.
#>
[CmdletBinding()]
param(
    # The export preset to build, by name.
    [string] $Preset = 'Android MTG',

    # Where to write the APK. The http server and tunnel serve this directory.
    [string] $Apk = (Join-Path $PSScriptRoot 'build/mtg.apk'),

    # Assemblies a healthy build packs. Well under the ~184 real count: this is the zero-C# guard,
    # not an inventory, and pinning it exactly would fail on every legitimate dependency change.
    [int] $MinimumAssemblies = 100
)

$ErrorActionPreference = 'Stop'

# Absolute, or godot-mono resolves a relative path against the PROJECT folder, not the shell's.
$Apk = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Apk)

$project = Join-Path $PSScriptRoot 'SQGodotCommon'
$assembly = Join-Path $project '.godot/mono/temp/bin/ExportRelease/android-arm64/SQGodotCommon.dll'

# The sources whose staleness this is about: the Godot project and everything it references.
$sourceRoots = @(
    $project
    Join-Path $PSScriptRoot 'MtgCore'
    Join-Path $PSScriptRoot 'MtgSimulator'
    Join-Path $PSScriptRoot 'ImmutableGameObjects'
) | Where-Object { Test-Path $_ }

function Get-NewestSource {
    Get-ChildItem -Path $sourceRoots -Recurse -File -Filter *.cs |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin|\.godot)[\\/]' } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

# --- the SDKs, which Godot finds ONLY through the environment ----------------------------------
#
# `export/android/android_sdk_path` and `java_sdk_path` are both empty in this machine's editor
# settings, so Godot falls back to ANDROID_HOME and JAVA_HOME — and when neither is set it refuses
# the export with "A valid Android SDK path is required in Editor Settings", which sounds like a
# settings problem and is not one. Resolved here so the build does not depend on whose shell it
# was launched from.
$java = @($env:JAVA_HOME, (Join-Path $env:USERPROFILE 'scoop/apps/temurin17-jdk/current')) |
    Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
$android = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, (Join-Path $env:LOCALAPPDATA 'Android/Sdk')) |
    Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $java) { throw 'No JDK found. Godot needs JAVA_HOME (scoop install temurin17-jdk).' }
if (-not $android) { throw 'No Android SDK found. Godot needs ANDROID_HOME.' }

$env:JAVA_HOME = $java
$env:ANDROID_HOME = $android
Write-Host "==> jdk $java"
Write-Host "==> sdk $android"

# **And Godot 4.6 does NOT read those env vars.** It takes both paths from EDITOR SETTINGS only, and
# this machine's have come back empty twice — a headless run that saves settings on exit is enough
# to lose them. The symptom is "A valid Android SDK path is required in Editor Settings" on a
# machine where the SDK is plainly installed, and it is not recoverable from the command line
# because there is no flag for it. So the file gets patched, every build, before the export.
$settings = Get-ChildItem -Path (Join-Path $env:USERPROFILE 'scoop/persist/godot-mono/editor_data') `
    -Filter 'editor_settings-4.*.tres' -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -First 1

if ($settings) {
    $text = Get-Content -Raw $settings.FullName
    $patched = $text `
        -replace 'export/android/android_sdk_path = ".*?"', "export/android/android_sdk_path = `"$($android -replace '\\','/')`"" `
        -replace 'export/android/java_sdk_path = ".*?"', "export/android/java_sdk_path = `"$($java -replace '\\','/')`""

    if ($patched -ne $text) {
        Set-Content -NoNewline -Path $settings.FullName -Value $patched
        Write-Host "==> wrote both SDK paths into $($settings.Name)"
    }
} else {
    Write-Warning 'No editor settings found to patch; the export will fail if they are empty.'
}

Write-Host '==> clearing the ExportRelease output (this is the whole point of the script)'
foreach ($dir in @('bin/ExportRelease', 'obj/ExportRelease')) {
    $path = Join-Path $project ".godot/mono/temp/$dir"
    if (Test-Path $path) { Remove-Item -Recurse -Force $path }
}

New-Item -ItemType Directory -Force (Split-Path $Apk) | Out-Null

$startedAt = (Get-Date).ToUniversalTime()
Write-Host "==> exporting to $Apk"
godot-mono --headless --path $project --export-release $Preset $Apk
if ($LASTEXITCODE -ne 0) { throw "godot-mono exited $LASTEXITCODE" }

# --- the checks, none of which the export itself performs -------------------------------------

if (-not (Test-Path $assembly)) {
    throw "No ExportRelease assembly was produced. The export can still exit 0 in this state and " +
          "ship an APK with no C# in it at all — check the log for a solution-path error."
}

$builtAt = (Get-Item $assembly).LastWriteTimeUtc
$newest = Get-NewestSource

if ($newest -and $newest.LastWriteTimeUtc -gt $builtAt) {
    throw "$($newest.Name) changed at $($newest.LastWriteTimeUtc.ToString('HH:mm:ss')) but the " +
          "assembly was built at $($builtAt.ToString('HH:mm:ss')). The APK is missing that edit. " +
          "Do not edit sources while an export runs — rerun this script."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $Apk))
try {
    $assemblies = @($zip.Entries | Where-Object { $_.FullName.EndsWith('.dll') }).Count
} finally {
    $zip.Dispose()
}

if ($assemblies -lt $MinimumAssemblies) {
    throw "Only $assemblies assemblies in the APK. A missing solution path produces a successful " +
          "export with zero C# and a game that launches to nothing."
}

$size = [math]::Round((Get-Item $Apk).Length / 1MB, 1)
$took = [math]::Round(((Get-Date).ToUniversalTime() - $startedAt).TotalSeconds)

Write-Host ''
Write-Host "    APK         $Apk  ($size MB)"
Write-Host "    assemblies  $assemblies"
Write-Host "    built       $($builtAt.ToString('HH:mm:ss')) UTC, newest source $($newest.Name) at $($newest.LastWriteTimeUtc.ToString('HH:mm:ss'))"
Write-Host "    took        ${took}s"
Write-Host ''
Write-Host 'Serve it:  cd build; python -m http.server 8000 --bind 127.0.0.1'
Write-Host '           cloudflared tunnel --url http://localhost:8000 --no-autoupdate'
