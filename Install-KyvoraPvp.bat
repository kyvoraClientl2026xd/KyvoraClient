@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-KyvoraPvp.ps1"
if errorlevel 1 (
    echo.
    echo No se pudo instalar KyvoraClient.
    pause
    exit /b 1
)
echo.
pause
