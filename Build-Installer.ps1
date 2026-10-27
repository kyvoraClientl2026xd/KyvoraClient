$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$launcherProject = Join-Path $projectRoot 'KyvoraPvp.csproj'
$installerProject = Join-Path $projectRoot 'KyvoraSetup\KyvoraSetup.csproj'
$publishDirectory = Join-Path $projectRoot 'bin\Release\net8.0-windows\win-x64\publish'
$packageDirectory = Join-Path $projectRoot 'obj\installer-package'
$payloadPath = Join-Path $packageDirectory 'KyvoraClientPayload.zip'
$outputDirectory = Join-Path $projectRoot 'dist'
$hudProject = Join-Path $projectRoot 'KyvoraHud'
$hudJar = Join-Path $hudProject 'build\libs\kyvora-hud-1.0.0.jar'
$hud1165Project = Join-Path $projectRoot 'KyvoraHud1165'
$hud1165Jar = Join-Path $hud1165Project 'build\libs\kyvorahud-1.16.5-1.0.0.jar'
$modDirectory = Join-Path $projectRoot 'mods'

$javaHome = $env:JAVA_HOME
if ([string]::IsNullOrWhiteSpace($javaHome)) {
    $javaHome = Join-Path $env:LOCALAPPDATA 'Programs\KyvoraTemurin8'
}
if (Test-Path -LiteralPath (Join-Path $javaHome 'bin\javac.exe') -PathType Leaf) {
    $javaExecutable = Join-Path $javaHome 'bin\java.exe'
    $javaDetails = & $env:ComSpec /c "`"$javaExecutable`" -XshowSettings:properties -version 2>&1"
    if ($LASTEXITCODE -ne 0 -or ($javaDetails -join "`n") -notmatch 'version "1\.8\.') {
        throw 'Kyvora HUD requiere JDK 8 x64.'
    }
    $javaArchitecture = $javaDetails | Select-String 'sun.arch.data.model' | Select-Object -First 1
    if ($null -eq $javaArchitecture -or $javaArchitecture.Line -notmatch '64') {
        throw 'Kyvora HUD requiere un JDK de 64 bits.'
    }

    $previousJavaHome = $env:JAVA_HOME
    $previousPath = $env:Path
    try {
        $env:JAVA_HOME = $javaHome
        $env:Path = (Join-Path $javaHome 'bin') + ';' + $env:Path
        Push-Location -LiteralPath $hudProject
        & (Join-Path $hudProject 'gradlew.bat') build --no-daemon
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar Kyvora HUD.' }
    }
    finally {
        Pop-Location
        $env:JAVA_HOME = $previousJavaHome
        $env:Path = $previousPath
    }
}
elseif (-not (Test-Path -LiteralPath $hudJar -PathType Leaf) -or -not (Test-Path -LiteralPath $hud1165Jar -PathType Leaf)) {
    throw 'Para compilar los HUD se requiere JDK 8 x64; no se encontraron los JAR ya compilados para reutilizarlos.'
}
else {
    Write-Host 'JDK 8 x64 no encontrado; se reutilizarán los dos HUD Kyvora ya compilados.' -ForegroundColor Yellow
}
if (-not (Test-Path -LiteralPath $hudJar -PathType Leaf)) { throw 'No se encontró el JAR de Kyvora HUD.' }
New-Item -ItemType Directory -Path $modDirectory -Force | Out-Null
Copy-Item -LiteralPath $hudJar -Destination (Join-Path $modDirectory 'KyvoraHud.jar') -Force

if (Test-Path -LiteralPath (Join-Path $javaHome 'bin\javac.exe') -PathType Leaf) {
    $previousJavaHome = $env:JAVA_HOME
    $previousPath = $env:Path
    try {
        $env:JAVA_HOME = $javaHome
        $env:Path = (Join-Path $javaHome 'bin') + ';' + $env:Path
        Push-Location -LiteralPath $hud1165Project
        & (Join-Path $hud1165Project 'gradlew.bat') build --no-daemon
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar Kyvora HUD para Minecraft 1.16.5.' }
    }
    finally {
        Pop-Location
        $env:JAVA_HOME = $previousJavaHome
        $env:Path = $previousPath
    }
}
if (-not (Test-Path -LiteralPath $hud1165Jar -PathType Leaf)) { throw 'No se encontró el JAR de Kyvora HUD 1.16.5.' }
Copy-Item -LiteralPath $hud1165Jar -Destination (Join-Path $modDirectory 'KyvoraHud-1.16.5.jar') -Force

dotnet publish $launcherProject -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar KyvoraClient.' }

$launcherExecutable = Join-Path $publishDirectory 'KyvoraClient.exe'
if (-not (Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
    throw 'No se generó KyvoraClient.exe en la carpeta publish.'
}

$runtimeArchive = Join-Path $packageDirectory 'Temurin8-jre-x64.zip'
$runtimeExtractDirectory = Join-Path $packageDirectory 'runtime-extract'
$runtimeDirectory = Join-Path $publishDirectory 'runtime'
New-Item -ItemType Directory -Path $runtimeExtractDirectory -Force | Out-Null
curl.exe -fL 'https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u504-b01/OpenJDK8U-jre_x64_windows_hotspot_8u504b01.zip' -o $runtimeArchive
if ($LASTEXITCODE -ne 0) { throw 'No se pudo descargar Java 8 x64 para empaquetarlo con KyvoraClient.' }
$runtimeHash = (Get-FileHash -LiteralPath $runtimeArchive -Algorithm SHA256).Hash
if ($runtimeHash -ne '82E2CDC6693737C5998445B31F69668FA0DA77C7705121053F6508AC84961123') {
    throw 'La verificación SHA-256 del runtime Temurin Java 8 no coincide.'
}
Expand-Archive -LiteralPath $runtimeArchive -DestinationPath $runtimeExtractDirectory -Force
$runtimeRoot = Get-ChildItem -LiteralPath $runtimeExtractDirectory -Directory | Select-Object -First 1
if ($null -eq $runtimeRoot -or
    -not (Test-Path -LiteralPath (Join-Path $runtimeRoot.FullName 'bin\javaw.exe') -PathType Leaf)) {
    throw 'El paquete de Java 8 x64 no contiene javaw.exe.'
}
New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $runtimeRoot.FullName -Force |
    Copy-Item -Destination $runtimeDirectory -Recurse -Force
Remove-Item -LiteralPath $runtimeArchive -Force
Remove-Item -LiteralPath $runtimeExtractDirectory -Recurse -Force

Remove-Item -LiteralPath (Join-Path $publishDirectory 'KyvoraPvp.exe') -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
if (Test-Path -LiteralPath $payloadPath) {
    Remove-Item -LiteralPath $payloadPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open(
    $payloadPath,
    [System.IO.Compression.ZipArchiveMode]::Create
)
try {
    Get-ChildItem -LiteralPath $publishDirectory -File -Recurse | ForEach-Object {
        if ($_.Name -in @('Install-KyvoraPvp.bat', 'Install-KyvoraPvp.ps1')) {
            return
        }
        $relativePath = $_.FullName.Substring($publishDirectory.Length).TrimStart('\').Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $_.FullName,
            $relativePath,
            [System.IO.Compression.CompressionLevel]::Fastest
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
dotnet publish $installerProject -c Release -r win-x64 --self-contained true `
    -p:PayloadArchive="$payloadPath" -o "$outputDirectory"
if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el instalador de KyvoraClient.' }

$installerExecutable = Join-Path $outputDirectory 'KyvoraSetup.exe'
$namedInstaller = Join-Path $outputDirectory 'KyvoraInstaller.exe'
if (-not (Test-Path -LiteralPath $installerExecutable -PathType Leaf)) {
    throw 'No se encontró el ejecutable publicado del instalador.'
}
if (Test-Path -LiteralPath $namedInstaller) {
    Remove-Item -LiteralPath $namedInstaller -Force
}
Move-Item -LiteralPath $installerExecutable -Destination $namedInstaller
Remove-Item -LiteralPath (Join-Path $outputDirectory 'KyvoraClient-Install.exe') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $outputDirectory 'KyvoraSetup.pdb') -Force -ErrorAction SilentlyContinue
Write-Host "Instalador listo: $namedInstaller" -ForegroundColor Green
