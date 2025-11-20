@echo off
setlocal
echo ==========================================
echo      Dynamic Edge Debug Builder
echo ==========================================

:: Find the latest .NET Framework 64-bit compiler
for /d %%i in (%windir%\Microsoft.NET\Framework64\v4*) do set "csc=%%i\csc.exe"
if not exist "%csc%" (
    echo Compiler not found.
    pause
    exit
)

echo Creating Manifest...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1"^>
echo   ^<assemblyIdentity version="1.0.0.0" name="DynamicEdge"/^>
echo   ^<application xmlns="urn:schemas-microsoft-com:asm.v3"^>
echo     ^<windowsSettings^>
echo       ^<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings"^>PerMonitorV2^</dpiAwareness^>
echo       ^<dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings"^>true^</dpiAware^>
echo     ^</windowsSettings^>
echo   ^</application^>
echo ^</assembly^>
) > app.manifest

echo Compiling...
:: Added /unsafe for performance optimizations
:: Added /r references to ensure UI libraries are found
"%csc%" /target:winexe /unsafe /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:DynamicEdge.exe /win32manifest:app.manifest Program.cs

:: Cleanup
del app.manifest

if exist DynamicEdge.exe (
    echo.
    echo Success! Run DynamicEdge.exe
) else (
    echo Build Failed.
)
pause