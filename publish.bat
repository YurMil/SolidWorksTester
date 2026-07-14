@echo off
setlocal
cd /d "%~dp0"

echo ===================================================
echo   Building standalone SolidWorksTester.exe
echo   (self-contained, single-file, win-x64)
echo ===================================================
echo.

dotnet publish -p:PublishProfile=StandaloneWin64
if errorlevel 1 (
    echo.
    echo Publish FAILED.
    exit /b 1
)

echo.
echo ===================================================
echo   Done: %~dp0SolidWorksTester.exe
echo   No .NET runtime install required on target PC.
echo   SOLIDWORKS must still be installed (COM API).
echo ===================================================
echo.

endlocal
