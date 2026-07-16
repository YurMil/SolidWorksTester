@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ===================================================
echo   Building standalone SolidWorksTester.exe
echo   (self-contained, single-file, win-x64)
echo ===================================================
echo.

REM Explicit Configuration/RID so profile settings are not skipped by SDK defaults.
REM Platform=x64 matches csproj Platforms and SOLIDWORKS x64 COM.
dotnet publish SolidWorksTester.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishProfile=StandaloneWin64 ^
  -p:Platform=x64
if errorlevel 1 (
    echo.
    echo Publish FAILED.
    exit /b 1
)

REM Pubxml drops WebView2 XML docs next to the EXE — remove clutter.
del /q "%~dp0Microsoft.Web.WebView2*.xml" 2>nul

set "EXE=%~dp0SolidWorksTester.exe"
if not exist "%EXE%" (
    echo.
    echo Publish reported success but EXE is missing:
    echo   %EXE%
    exit /b 1
)

for %%A in ("%EXE%") do (
    echo.
    echo ===================================================
    echo   Done: %%~fA
    echo   Size: %%~zA bytes
    echo   Time: %%~tA
    echo   No .NET runtime install required on target PC.
    echo   SOLIDWORKS must still be installed ^(COM API^).
    echo ===================================================
)

endlocal
exit /b 0
