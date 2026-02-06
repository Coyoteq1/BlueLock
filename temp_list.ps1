$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$asm = [System.Reflection.Assembly]::LoadFile((Join-Path $root 'VAutomationCore\\libs\\ProjectM.dll'))
$asm2 = [System.Reflection.Assembly]::LoadFile((Join-Path $root 'VAutomationCore\\libs\\ProjectM.Gameplay.Scripting.dll'))
try {
    $types = $asm2.GetTypes()
} catch {
    $_.Exception.LoaderExceptions | ForEach-Object { Write-Host "Loader error: $($_.Message)" }
    return
}
$types | Where-Object { $_.FullName -like '*Chat*' -or $_.FullName -like '*Announcement*' } | Select-Object FullName
