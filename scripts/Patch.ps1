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

# The embedded actools assembly references ImageMagick only through a field
# constant. A tiny build-time stub lets Mono.Cecil rewrite the assembly without
# bundling or installing anything into Assetto Corsa.
$magickStub = Join-Path $env:TEMP "Magick.NET-Q8-x86.dll"
if (-not (Test-Path $magickStub)) {
    $stubSource = Join-Path $env:TEMP "TrackdayTimer.MagickStub.cs"
    @'
using System.Reflection;
[assembly: AssemblyVersion("7.0.0.0")]
namespace ImageMagick { public enum MagickFormat { Unknown = 0 } }
'@ | Set-Content -LiteralPath $stubSource -Encoding ASCII
    & $csc /nologo /target:library /out:"$magickStub" "$stubSource"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Could not create the build-time assembly resolver stub." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Running patcher..." -ForegroundColor Cyan
& "$outExe"

# Cleanup compiled binary after running
if (Test-Path "$outExe") {
    Remove-Item "$outExe" -Force -ErrorAction SilentlyContinue
}
