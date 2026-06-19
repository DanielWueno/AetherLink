# -----------------------------------------------------------------------------
# WinDiagnostic Helper - Live Launcher
# Este script descarga la aplicación compilada (.exe) de forma "Fileless"
# en el directorio temporal y la ejecuta sin requerir permisos de instalación.
# -----------------------------------------------------------------------------
$ErrorActionPreference = "Stop"

$tempDir = "$env:TEMP\WinDiagCache"
$appExe = "$tempDir\WinDiagnosticHelper.exe"

# Reemplaza esta URL por la liga directa a tu archivo .exe subido a GitHub (Raw) u otro servidor
$appUrl = "https://tu-servidor-interno.corp/Releases/WinDiagnosticHelper.exe"

Write-Host "Verificando herramientas de diagnóstico..." -ForegroundColor DarkGray

if (-not (Test-Path $appExe)) {
    Write-Host "Sincronizando binarios en la caché temporal. Por favor espera..." -ForegroundColor DarkGray
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    
    # Descargamos el binario a %TEMP%
    Invoke-WebRequest -Uri $appUrl -OutFile $appExe
}

Write-Host "Lanzando entorno gráfico en segundo plano..." -ForegroundColor DarkGray

# Ejecutamos el archivo y desconectamos la terminal para que la UI viva por su cuenta
Start-Process -FilePath $appExe

Write-Host "Listo. Ya puedes cerrar esta consola." -ForegroundColor Green
Start-Sleep -Seconds 2
