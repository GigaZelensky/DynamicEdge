@echo off
setlocal
set "APP_VERSION=1.0.1"
set "OUT_NAME=DynamicEdge-%APP_VERSION%.exe"
set "SRC_DIR=%~dp0src"
set "MANIFEST=%~dp0app.manifest"
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
echo Compiling...
"%csc%" /target:winexe /unsafe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /out:%OUT_NAME% /win32manifest:"%MANIFEST%" "%SRC_DIR%\\*.cs"

if exist %OUT_NAME% (
    echo.
    echo Success! Run %OUT_NAME%
) else (
    echo Build Failed.
)
pause
