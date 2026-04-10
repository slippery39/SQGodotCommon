# Collect-CSharpFiles.ps1
# Recursively finds all .cs files in the current directory and writes
# their contents into a single output file for AI context sharing.

param(
    [string]$OutputFile = "ai_context.txt"
)

$files = Get-ChildItem -Path . -Recurse -Filter "*.cs" | Sort-Object FullName

if ($files.Count -eq 0) {
    Write-Host "No .cs files found." -ForegroundColor Yellow
    exit
}

$writer = [System.IO.StreamWriter]::new($OutputFile, $false, [System.Text.Encoding]::UTF8)
$writer.WriteLine("========================================")
$writer.WriteLine("PROJECT C# SOURCE FILES")
$writer.WriteLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$writer.WriteLine("Total files: $($files.Count)")
$writer.WriteLine("========================================")
$writer.WriteLine()

foreach ($file in $files) {
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "")

    $writer.WriteLine("----------------------------------------")
    $writer.WriteLine("FILE: $relativePath")
    $writer.WriteLine("----------------------------------------")
    $writer.WriteLine()
    $writer.WriteLine((Get-Content $file.FullName -Raw))
    $writer.WriteLine()
}

$writer.Close()
Write-Host "Done! $($files.Count) file(s) written to '$OutputFile'." -ForegroundColor Green