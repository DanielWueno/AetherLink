param (
    [int]$LocalPort = 1080,
    [int]$DevicePort = 8080
)

$ErrorActionPreference = "Stop"

Write-Host "WinDiagnostic Helper initialized." -ForegroundColor DarkGray

# 1. Preparar directorio temporal invisible
$tempDir = "$env:TEMP\WinDiagHelper"
$adbExe = "$tempDir\adb.exe"

if (-not (Test-Path $adbExe)) {
    Write-Host "Fetching diagnostic dependencies..." -ForegroundColor DarkGray
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    
    # Descargar ADB oficial de Google de forma silenciosa
    $url = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
    $zipPath = "$tempDir\tools.zip"
    Invoke-WebRequest -Uri $url -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force
    
    # Extraer solo lo necesario
    Move-Item -Path "$tempDir\platform-tools\adb.exe" -Destination $adbExe -Force
    Move-Item -Path "$tempDir\platform-tools\AdbWinApi.dll" -Destination "$tempDir\AdbWinApi.dll" -Force
    Move-Item -Path "$tempDir\platform-tools\AdbWinUsbApi.dll" -Destination "$tempDir\AdbWinUsbApi.dll" -Force
    Remove-Item -Path "$tempDir\platform-tools" -Recurse -Force
    Remove-Item -Path $zipPath -Force
}

# 2. Iniciar el túnel USB
Write-Host "Attaching to local hardware bridge..." -ForegroundColor DarkGray
& $adbExe start-server | Out-Null
& $adbExe forward tcp:$LocalPort tcp:$DevicePort

# 3. Configurar entorno proxy local para esta sesión
$proxyUrl = "http://127.0.0.1:$LocalPort"
$env:HTTP_PROXY = $proxyUrl
$env:HTTPS_PROXY = $proxyUrl
$env:NO_PROXY = "localhost,127.0.0.1,::1"

Write-Host "Connection established. CLI traffic is now routed through hardware bridge." -ForegroundColor DarkGray
Write-Host "Type 'exit' to terminate session and clean up." -ForegroundColor DarkGray

# 4. Iniciar una consola interactiva "hija" que hereda este entorno
powershell.exe -NoLogo

# 5. Limpieza automática al cerrar la consola
Write-Host "Cleaning up diagnostics..." -ForegroundColor DarkGray
& $adbExe forward --remove tcp:$LocalPort
& $adbExe kill-server
