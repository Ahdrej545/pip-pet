$ErrorActionPreference = 'Stop'
$pipInstall = Join-Path $env:LOCALAPPDATA 'PipDesktop'
New-Item -ItemType Directory -Force -Path $pipInstall | Out-Null
$pipSource = Join-Path $PSScriptRoot 'Pip.exe'
$pipTarget = Join-Path $pipInstall 'Pip.exe'
$pipHadInstall = Test-Path -LiteralPath $pipTarget
$pipRun = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$pipPreviousStartup = (Get-ItemProperty -Path $pipRun -Name PipDesktop -ErrorAction SilentlyContinue).PipDesktop
Get-Process -Name Pip -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $pipTarget } | ForEach-Object { Stop-Process -Id $_.Id; $_.WaitForExit() }
Copy-Item -LiteralPath $pipSource -Destination $pipTarget -Force
if (-not $pipHadInstall -or $null -ne $pipPreviousStartup) {
    New-ItemProperty -Path $pipRun -Name 'PipDesktop' -Value ('"' + $pipTarget + '"') -PropertyType String -Force | Out-Null
}
$pipShell = New-Object -ComObject WScript.Shell
$pipDesktop = [Environment]::GetFolderPath('Desktop')
$pipShortcut = $pipShell.CreateShortcut((Join-Path $pipDesktop 'Пип.lnk'))
$pipShortcut.TargetPath = $pipTarget
$pipShortcut.WorkingDirectory = $pipInstall
$pipShortcut.IconLocation = $pipTarget + ',0'
$pipShortcut.Description = 'Пип — питомец рабочего стола'
$pipShortcut.Save()
Start-Process -FilePath $pipTarget
Write-Output ('Installed Pip: ' + $pipTarget)
