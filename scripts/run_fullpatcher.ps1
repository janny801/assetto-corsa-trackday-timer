$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$bin = "$env:TEMP\cm_inspect"
$cecil = "$bin\Mono.Cecil.dll"
$cecilRocks = "$bin\Mono.Cecil.Rocks.dll"

& $csc /target:exe /out:"$bin\FullPatcher.exe" /r:"$cecil" /r:"$cecilRocks" "$bin\FullPatcher.cs"
Write-Output "Compilation exit code: $LASTEXITCODE"
if ($LASTEXITCODE -eq 0) {
    & "$bin\FullPatcher.exe"
    Write-Output "Patcher exit code: $LASTEXITCODE"
}
