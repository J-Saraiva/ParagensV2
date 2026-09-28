@echo off
title Moovit Live Proxy - ParagensV2
echo ========================================================
echo   Iniciando Moovit Live Proxy para ParagensV2
echo ========================================================
cd /d "%~dp0"

echo Verificando dependencias...
python -m pip install playwright >nul 2>&1
python -m playwright install chromium >nul 2>&1

echo Iniciando servidor proxy em http://localhost:5000 ...
python server.py
pause
