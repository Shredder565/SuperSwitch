@echo off
rem Builds the SuperSwitch Video Converter and puts a shortcut on the desktop.
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%build-converter"

rem FFmpeg does the actual encoding (for this converter and SuperSwitch's recorder).
set "HAVE_FFMPEG="
where ffmpeg >nul 2>&1 && set "HAVE_FFMPEG=1"
if exist "%LOCALAPPDATA%\Microsoft\WinGet\Links\ffmpeg.exe" set "HAVE_FFMPEG=1"
if not defined HAVE_FFMPEG (
  echo Installing FFmpeg...
  winget install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements
  if errorlevel 1 (
    echo.
    echo FFmpeg could not be installed automatically. In PowerShell, run:
    echo     winget install --id Gyan.FFmpeg -e
    echo The converter will still be built; it needs FFmpeg before it can convert.
    echo.
  )
) else (
  echo FFmpeg is already installed.
)

echo Building SuperSwitch Video Converter...
dotnet publish "%ROOT%tools\VideoConverter\VideoConverter.csproj" -c Release -o "%OUT%"
if errorlevel 1 (
  echo.
  echo Build failed. Copy the error lines above and send them to Claude.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'),'SuperSwitch Video Converter.lnk'));" ^
  "$s.TargetPath='%OUT%\SuperSwitchVideoConverter.exe';" ^
  "$s.WorkingDirectory='%OUT%';" ^
  "$s.Description='Convert videos to high quality, small HEVC files';" ^
  "$s.Save()"

echo.
echo Done. "SuperSwitch Video Converter" is on your desktop.
echo Tip: you can also drag videos onto that desktop icon.
pause
