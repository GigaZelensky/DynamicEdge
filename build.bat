@echo off
setlocal
set "APP_VERSION=1.0.2"
set "OUT_NAME=DynamicEdge-%APP_VERSION%.exe"
set "SRC_DIR=%~dp0src"
set "MANIFEST=%~dp0app.manifest"
set "CONFIG=%SRC_DIR%\app.config"
echo ==========================================
echo      Dynamic Edge Debug Builder
echo ==========================================

for /d %%i in (%windir%\Microsoft.NET\Framework64\v4*) do set "csc=%%i\csc.exe"
if not exist "%csc%" (
    echo Compiler not found.
    pause
    exit
)

if not exist "%MANIFEST%" (
    echo Manifest not found: %MANIFEST%
    pause
    exit
)

echo Using manifest: %MANIFEST%
echo Using config:   %CONFIG%
echo Compiling...
"%csc%" /target:winexe /unsafe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /out:%OUT_NAME% /win32manifest:"%MANIFEST%" "%SRC_DIR%\\*.cs"

if exist "%CONFIG%" (
    copy /Y "%CONFIG%" "%OUT_NAME%.config" >nul
    echo Configuration file copied.
)

if exist "%OUT_NAME%" (
    echo.
    echo Success!
    echo 1. %OUT_NAME%
    if exist "%OUT_NAME%.config" echo 2. %OUT_NAME%.config
    echo.
    echo Keep these files together
) else (
    echo Build Failed.
)
pause
