@echo off
cd /d "%~dp0"
dotnet clean SangriaSuite.sln
dotnet build SangriaSuite.sln -c Release
pause
