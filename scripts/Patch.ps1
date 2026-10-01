$repoDir = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $repoDir "src"
$libDir = Join-Path $srcDir "lib"
$cecil = Join-Path $libDir "Mono.Cecil.dll"
$cecilRocks = Join-Path $libDir "Mono.Cecil.Rocks.dll"
$fullPatcherCs = Join-Path $srcDir "FullPatcher.cs"
$outExe = Join-Path $libDir "FullPatcher.exe"

$cscCandidates = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $csc) {
    Write-Host "[ERROR] .NET Framework compiler (csc.exe) not found." -ForegroundColor Red
    exit 1
}

Write-Host "Compiling patcher..." -ForegroundColor Cyan
& $csc /nologo /target:exe /out:"$outExe" /r:"$cecil" /r:"$cecilRocks" "$fullPatcherCs"

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Compilation failed." -ForegroundColor Red
    exit 1
}

Write-Host "Running patcher..." -ForegroundColor Cyan
& "$outExe"

# Cleanup compiled binary after running
if (Test-Path "$outExe") {
    Remove-Item "$outExe" -Force -ErrorAction SilentlyContinue
}
