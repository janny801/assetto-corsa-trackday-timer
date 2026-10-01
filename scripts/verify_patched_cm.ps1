Add-Type -Path "$env:TEMP\cm_inspect\Mono.Cecil.dll"
Add-Type -Path "$env:TEMP\cm_inspect\Mono.Cecil.Rocks.dll"

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly("C:\Users\jred8\OneDrive\Desktop\Content Manager.exe")
$qd = $asm.MainModule.Types | Where-Object { $_.Name -eq "QuickDrive_Trackday" }
$vm = $qd.NestedTypes | Where-Object { $_.Name -eq "ViewModel" }

function DumpMethod($m) {
    Write-Output "=== $($m.DeclaringType.Name).$($m.Name) ==="
    foreach ($i in $m.Body.Instructions) {
        Write-Output ("{0:X4}: {1,-12} {2}" -f $i.Offset, $i.OpCode.Name, $i.Operand)
    }
}

DumpMethod ($vm.Methods | Where-Object { $_.Name -eq "get_TrackdayDuration" })
DumpMethod ($vm.Methods | Where-Object { $_.Name -eq "set_TrackdayDuration" })
DumpMethod ($vm.Methods | Where-Object { $_.Name -eq "Save" })
DumpMethod ($vm.Methods | Where-Object { $_.Name -eq "Load" -and $_.Parameters.Count -eq 1 })
DumpMethod ($vm.Methods | Where-Object { $_.Name -eq "GetModeProperties" })
DumpMethod ($qd.Methods | Where-Object { $_.Name -eq "OnLoaded" })
DumpMethod ($vm.Methods | Where-Object { $_.Name -eq ".ctor" })

Write-Output "=== Fields on ViewModel ==="
$vm.Fields | ForEach-Object { Write-Output "  $($_.Name): $($_.FieldType.FullName)" }

Write-Output "=== Fields on SaveableData ==="
$saveable = $vm.NestedTypes | Where-Object { $_.Name -eq "SaveableData" }
$saveable.Fields | ForEach-Object { Write-Output "  $($_.Name): $($_.FieldType.FullName)" }

Write-Output "=== Assembly References ==="
$asm.MainModule.AssemblyReferences | ForEach-Object { Write-Output "  $($_.Name)" }
