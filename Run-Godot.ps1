#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs a Godot scene on the SECOND monitor, and optionally captures frames.

.DESCRIPTION
    Two things go wrong every time a scene is run by hand, and both are the kind nobody remembers:

      1. `godot` on PATH is the NON-.NET build and cannot load C# at all. It fails every script
         load with errors that look exactly like a broken project. This always uses `godot-mono`.

      2. The window opens on the PRIMARY monitor, over whatever you are working on.

    The monitor is DETECTED rather than hardcoded: the first non-primary screen, falling back to
    the primary when there is only one. Rearranging your displays cannot stale this.

    `--write-movie` also needs its output directory to already exist — it exits 0, prints "Done
    recording movie at path: ...", and writes nothing when it does not. This creates it, with a
    `.gdignore` inside: the folder lives in the Godot project, so without one every frame is
    imported and SHIPS in the APK (367 frames were 122 MB of one on the KIN branch).

.EXAMPLE
    ./Run-Godot.ps1                                         # the main scene (the menu)
    ./Run-Godot.ps1 MtgGame/Draft/DraftScene.tscn
    ./Run-Godot.ps1 -Capture shots/menu -Seconds 3
    ./Run-Godot.ps1 MtgGame/MtgGameScene.tscn -Headless -Seconds 5
#>
[CmdletBinding()]
param(
    # Scene to run, relative to the Godot project (SQGodotCommon/). Omit for the main scene.
    [Parameter(Position = 0)]
    [string] $Scene,

    # Capture frames into this directory (relative to the Godot project). Omit to just run it.
    [string] $Capture,

    # How long to run before quitting. Omit to run until closed.
    [double] $Seconds,

    # Arguments for the GAME rather than the engine — passed after `--`.
    [string[]] $GameArgs = @(),

    # Only resizes the window when project.godot has no window_*_override (it has 1280x720).
    [int] $Width = 1280,
    [int] $Height = 720,

    # No window at all. Ignores the monitor entirely; use for import and log-only checks.
    [switch] $Headless
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'SQGodotCommon'
$godot = Join-Path $env:USERPROFILE 'scoop/apps/godot-mono/current/godot-mono.exe'

if (-not (Test-Path $godot)) {
    throw "godot-mono not found at $godot. The plain ``godot`` on PATH cannot load C# — see Commands.md."
}

$engineArgs = @('--path', $project)

if ($Headless) {
    $engineArgs += '--headless'
}
else {
    # The second monitor, detected. --position takes virtual-desktop coordinates, so a screen whose
    # bounds start at X=1920 is the one to the right of a 1920-wide primary.
    Add-Type -AssemblyName System.Windows.Forms
    $screens = [System.Windows.Forms.Screen]::AllScreens
    $target = $screens | Where-Object { -not $_.Primary } | Select-Object -First 1
    if (-not $target) {
        Write-Host '==> only one monitor; using it'
        $target = $screens | Where-Object { $_.Primary } | Select-Object -First 1
    }

    $x = $target.Bounds.X + 40
    $y = $target.Bounds.Y + 40
    Write-Host "==> window at $x,$y on $($target.DeviceName)"
    $engineArgs += @('--position', "$x,$y", '--resolution', "${Width}x${Height}")
}

if ($Capture) {
    # The movie writer exits 0 and writes NOTHING when its directory is missing. The only tell is
    # `ERROR: Condition "f_wav.is_null()"` buried in the output.
    $dir = Join-Path $project $Capture
    New-Item -ItemType Directory -Force $dir | Out-Null
    New-Item -ItemType File -Force (Join-Path $dir '.gdignore') | Out-Null
    $engineArgs += @('--write-movie', "$Capture/frame.png", '--fixed-fps', '10')
    if (-not $Seconds) { $Seconds = 2 }
}

if ($Seconds) {
    # --quit-after counts FRAMES, and --fixed-fps pins them to 10 during a capture.
    $frames = if ($Capture) { [int]($Seconds * 10) } else { [int]($Seconds * 60) }
    $engineArgs += @('--quit-after', $frames)
}

if ($Scene) { $engineArgs += $Scene }
if ($GameArgs.Count) { $engineArgs += @('--') + $GameArgs }

Write-Host "==> godot-mono $($engineArgs -join ' ')"
& $godot @engineArgs 2>&1 | Where-Object {
    # The shader-sampler line and the Android editor-setting lookup are both pre-existing and both
    # say "Continuing". Everything else is worth seeing.
    $_ -notmatch 'custom_samplers|export/android|EditorSettings not instantiated'
}

if ($Capture) {
    $shots = Get-ChildItem (Join-Path $project $Capture) -Filter *.png -ErrorAction SilentlyContinue
    Write-Host "==> $($shots.Count) frames in $Capture"
    if ($shots.Count -eq 0) { throw "capture wrote nothing to $Capture" }
}
