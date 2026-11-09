@echo off
rem Creates a "SuperSwitch" shortcut on the desktop that builds (when needed) and starts SuperSwitch.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'),'SuperSwitch.lnk'));" ^
  "$s.TargetPath='%~dp0SuperSwitch.cmd';" ^
  "$s.WorkingDirectory='%~dp0';" ^
  "$s.IconLocation='%~dp0src\Ryujinx\Ryujinx.ico';" ^
  "$s.WindowStyle=1;" ^
  "$s.Description='Build (if needed) and start SuperSwitch';" ^
  "$s.Save()"
echo Desktop shortcut "SuperSwitch" created.
pause
