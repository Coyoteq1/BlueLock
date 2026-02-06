$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$asm = [System.Reflection.Assembly]::LoadFile((Join-Path $root 'VAutomationCore\\libs\\ProjectM.dll'))
$asm.GetTypes() | Where-Object { $_.Name -like '*UserName*' -or $_.Name -like '*Chat*' -or $_.Name -like '*Announcement*' } | Select-Object FullName
