@echo off
rem Builds SuperSwitch-Setup.exe: an installer with SuperSwitch, the Video Converter and all SuperSwitch changes.
setlocal
set "ROOT=%~dp0"

echo [1/4] Building SuperSwitch...
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\SuperSwitch.ps1" -BuildOnly
if errorlevel 1 goto fail

echo.
echo [2/4] Building the SuperSwitch Video Converter...
dotnet publish "%ROOT%tools\VideoConverter\VideoConverter.csproj" -c Release -o "%ROOT%build-converter"
if errorlevel 1 goto fail

echo.
echo [3/4] Looking for Inno Setup (makes the installer)...
call :findiscc
if not defined ISCC (
  echo Installing Inno Setup...
  winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements
  call :findiscc
)
if not defined ISCC (
  echo Inno Setup could not be found or installed. In PowerShell, run:
  echo     winget install --id JRSoftware.InnoSetup -e
  echo then run this file again.
  goto fail
)

echo.
echo [4/4] Creating SuperSwitch-Setup.exe...
"%ISCC%" /Q "/DSourceRoot=%ROOT:~0,-1%" "%ROOT%installer\SuperSwitch.iss"
if errorlevel 1 goto fail

echo.
echo Done: %ROOT%SuperSwitch-Setup.exe
explorer /select,"%ROOT%SuperSwitch-Setup.exe"
pause
exit /b 0

:findiscc
set "ISCC="
for %%P in ("%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%ProgramFiles%\Inno Setup 6\ISCC.exe") do (
  if exist "%%~P" set "ISCC=%%~P"
)
exit /b 0

:fail
echo.
echo Creating the installer failed. Copy the error lines above and send them to Claude.
pause
exit /b 1
