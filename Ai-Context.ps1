# Collect-AllProjects.ps1
# Runs the global Collect-CSharpFiles command in each project directory.
# Usage: .\Collect-AllProjects.ps1

param(
    [string]$Command = "Collect-CSharpFiles"
)

$projects = @(
    "ImmutableGameObjects",
    "MtgCore",
    "MtgCore.Tests",
    "MtgConsole",
    "MtgSimulator"
)

$solutionRoot = $PSScriptRoot
$succeeded = @()
$failed = @()

foreach ($project in $projects) {
    $projectPath = Join-Path $solutionRoot $project

    if (-not (Test-Path $projectPath)) {
        Write-Warning "[$project] Directory not found - skipping."
        $failed += $project
        continue
    }

    Write-Host "[$project] Running..." -ForegroundColor Cyan
    Push-Location $projectPath
    try {
        & $Command -OutputFile "ai_context_$project.txt"
        $succeeded += $project
        Write-Host "[$project] Done." -ForegroundColor Green
    }
    catch {
        Write-Warning "[$project] Failed: $_"
        $failed += $project
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Yellow
Write-Host "Succeeded: $($succeeded -join ', ')" -ForegroundColor Green
if ($failed.Count -gt 0) {
    Write-Host "Skipped/Failed: $($failed -join ', ')" -ForegroundColor Red
}