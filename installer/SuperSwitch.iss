; SuperSwitch installer (Inno Setup 6). Built by Build-Setup.cmd, which compiles SuperSwitch and the Video Converter first.

#ifndef SourceRoot
  #define SourceRoot AddBackslash(SourcePath) + ".."
#endif
#define AppVersion GetDateTimeString('yyyy.mm.dd', '.', '')

[Setup]
AppId={{8B5F3C2A-6E1D-4C9B-9A57-3F2D1E0C7B44}
AppName=SuperSwitch
AppVersion={#AppVersion}
AppVerName=SuperSwitch {#AppVersion}
AppPublisher=SuperSwitch
DefaultDirName={sd}\SuperSwitch
DefaultGroupName=SuperSwitch
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#SourceRoot}
OutputBaseFilename=SuperSwitch-Setup
SetupIconFile={#SourceRoot}\src\Ryujinx\Ryujinx.ico
UninstallDisplayIcon={app}\SuperSwitch.exe
UninstallDisplayName=SuperSwitch
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut for SuperSwitch"
Name: "convertericon"; Description: "Create a desktop shortcut for the SuperSwitch Video Converter"
Name: "ffmpeg"; Description: "Install FFmpeg (needed for game recording and the Video Converter)"; Check: not FfmpegInstalled
Name: "copydata"; Description: "Copy my existing SuperSwitch data (saves, settings, keys, firmware, caches) to the new data folder"; Check: OldDataExists

[Files]
Source: "{#SourceRoot}\build\*"; DestDir: "{app}"; Excludes: "\Logs\*,*.nupkg,*.pdb,DataLocation.txt,\portable\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\build-converter\*"; DestDir: "{app}\Video Converter"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SuperSwitch"; Filename: "{app}\SuperSwitch.exe"; WorkingDir: "{app}"
Name: "{group}\SuperSwitch Video Converter"; Filename: "{app}\Video Converter\SuperSwitchVideoConverter.exe"; WorkingDir: "{app}\Video Converter"
Name: "{group}\Uninstall SuperSwitch"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SuperSwitch"; Filename: "{app}\SuperSwitch.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{autodesktop}\SuperSwitch Video Converter"; Filename: "{app}\Video Converter\SuperSwitchVideoConverter.exe"; WorkingDir: "{app}\Video Converter"; Tasks: convertericon

[Run]
Filename: "winget.exe"; Parameters: "install --id Microsoft.DotNet.DesktopRuntime.10 -e --silent --accept-source-agreements --accept-package-agreements"; StatusMsg: "Installing the .NET 10 runtime (Windows may ask for permission)..."; Flags: runhidden waituntilterminated; Check: not DotNetInstalled
Filename: "winget.exe"; Parameters: "install --id Gyan.FFmpeg -e --silent --accept-source-agreements --accept-package-agreements"; StatusMsg: "Installing FFmpeg..."; Flags: runhidden waituntilterminated; Tasks: ffmpeg
Filename: "{app}\SuperSwitch.exe"; Description: "Start SuperSwitch"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"

[Code]
var
  DataPage: TInputDirWizardPage;

function PointerFile: String;
begin
  Result := ExpandConstant('{localappdata}\SuperSwitch\DataLocation.txt');
end;

function OldDataDir: String;
var
  Lines: TArrayOfString;
  S: String;
begin
  { The folder SuperSwitch uses today: the one chosen earlier, or the original %AppData%\SuperSwitch. }
  Result := ExpandConstant('{userappdata}\SuperSwitch');
  { Read as text, not raw bytes: the file is UTF-8 and may start with a byte order mark. }
  if LoadStringsFromFile(PointerFile, Lines) and (GetArrayLength(Lines) > 0) then
  begin
    S := Trim(Lines[0]);
    { Remove a byte order mark that was not recognized as one (shows up as one or three odd characters). }
    if (Length(S) > 0) and (Ord(S[1]) = $FEFF) then
      Delete(S, 1, 1);
    if (Length(S) >= 3) and (Ord(S[1]) = $EF) and (Ord(S[2]) = $BB) and (Ord(S[3]) = $BF) then
      Delete(S, 1, 3);
    S := RemoveQuotes(Trim(S));
    if S <> '' then
      Result := S;
  end;
end;

function QuoteDirForCommandLine(Dir: String): String;
begin
  { A quoted path ending in a backslash (a drive root such as D:\) would swallow the closing quote; double the backslash. }
  if (Length(Dir) > 0) and (Dir[Length(Dir)] = '\') then
    Dir := Dir + '\';
  Result := '"' + Dir + '"';
end;

function SamePath(A, B: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(A), RemoveBackslashUnlessRoot(B)) = 0;
end;

function OldDataExists: Boolean;
begin
  Result := FileExists(AddBackslash(OldDataDir) + 'Config.json') and not SamePath(OldDataDir, DataPage.Values[0]);
end;

function FfmpegInstalled: Boolean;
begin
  Result := FileExists(ExpandConstant('{localappdata}\Microsoft\WinGet\Links\ffmpeg.exe')) or
            (FileSearch('ffmpeg.exe', GetEnv('PATH')) <> '');
end;

function DotNetInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

procedure InitializeWizard;
var
  DefaultDir: String;
begin
  DataPage := CreateInputDirPage(wpSelectDir,
    'Where should SuperSwitch keep your data?',
    'Saves, settings, keys, firmware, mods and shader caches go here.',
    'Pick any folder (another drive is fine). Uninstalling SuperSwitch never deletes this folder. ' +
    'You can change it later with File > Change Data Folder.',
    False, '');
  DataPage.Add('');

  DefaultDir := OldDataDir;
  if SamePath(DefaultDir, ExpandConstant('{userappdata}\SuperSwitch')) then
    DefaultDir := ExpandConstant('{sd}\SuperSwitch Data');
  DataPage.Values[0] := DefaultDir;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = DataPage.ID) and SamePath(DataPage.Values[0], WizardDirValue) then
  begin
    MsgBox('Please choose a data folder that is different from the program folder, so updates and uninstalling can never touch your saves.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DataDir: String;
  ResultCode: Integer;
  Lines: TArrayOfString;
begin
  if CurStep = ssPostInstall then
  begin
    DataDir := RemoveBackslashUnlessRoot(DataPage.Values[0]);
    ForceDirectories(DataDir);

    if WizardIsTaskSelected('copydata') then
    begin
      WizardForm.StatusLabel.Caption := 'Copying your saves, settings and caches (this can take a minute)...';
      Exec(ExpandConstant('{sys}\robocopy.exe'),
        QuoteDirForCommandLine(RemoveBackslashUnlessRoot(OldDataDir)) + ' ' + QuoteDirForCommandLine(DataDir) + ' /E /XO /R:1 /W:1 /XD Logs',
        '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      if ResultCode >= 8 then
        MsgBox('Some files could not be copied (robocopy code ' + IntToStr(ResultCode) + '). ' +
               'Your original data is untouched in ' + OldDataDir + '.', mbError, MB_OK);
    end;

    ForceDirectories(ExtractFileDir(PointerFile));
    SetArrayLength(Lines, 1);
    Lines[0] := DataDir;
    SaveStringsToUTF8File(PointerFile, Lines, False);
  end;
end;
