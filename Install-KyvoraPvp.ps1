$ErrorActionPreference = 'Stop'

$sourceDirectory = $PSScriptRoot
$executable = Join-Path $sourceDirectory 'KyvoraClient.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'No encuentro KyvoraClient.exe. Ejecuta este instalador desde la carpeta publish completa.'
}

$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\KyvoraClient'
$startMenuDirectory = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\KyvoraClient'
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'KyvoraClient.lnk'
$startMenuShortcutPath = Join-Path $startMenuDirectory 'KyvoraClient.lnk'

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

Get-ChildItem -LiteralPath $sourceDirectory -Directory | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse -Force
}

Get-ChildItem -LiteralPath $sourceDirectory -File |
    Where-Object { $_.Name -notin @('appsettings.json', 'Install-KyvoraPvp.ps1', 'Install-KyvoraPvp.bat') } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Force
    }

$sourceSettings = Join-Path $sourceDirectory 'appsettings.json'
$installedSettings = Join-Path $installDirectory 'appsettings.json'
if (-not (Test-Path -LiteralPath $installedSettings -PathType Leaf) -and
    (Test-Path -LiteralPath $sourceSettings -PathType Leaf)) {
    Copy-Item -LiteralPath $sourceSettings -Destination $installedSettings
}

New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($startMenuShortcutPath, $desktopShortcutPath)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installDirectory 'KyvoraClient.exe'
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.Description = 'Launcher de Minecraft Java 1.8.9'
    $shortcut.IconLocation = (Join-Path $installDirectory 'KyvoraClient.exe') + ',0'
    $shortcut.Save()
}

Write-Host ''
Write-Host 'KyvoraClient is installed.' -ForegroundColor Green
Write-Host 'Open KyvoraClient from the Start menu or the desktop shortcut.'
Write-Host "Installed files: $installDirectory"
