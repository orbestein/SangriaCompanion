@echo off
chcp 65001 >nul
title Sangria Companion v0.5.0
cd /d "%~dp0"
echo Limpando compilacao anterior...
dotnet clean
echo.
echo Compilando Sangria Companion v0.5.0...
dotnet build -c Release
echo.
pause
