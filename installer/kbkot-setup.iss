; KKBOT Framework Installer Script
; InnoSetup 6.2+

#define MyAppName "KKBOT Framework"
#define MyAppVersion "24.1.0"
#define MyAppPublisher "KKBOT Team"
#define MyAppURL "https://kkbot.io"
#define MyAppExeName "KKBOT.Host.exe"
#define MyAppGuid "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId={{#MyAppGuid}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=KKBOT-Setup-{#MyAppVersion}
SetupIconFile=..\src\KKBOT.Host\Resources\app.ico
Compression=lzma/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

; Silent install support
SilentInstallMode=silent
SilentUninstallMode=silent

; Restart behavior
RestartIfNeededByRun=ask

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "vietnamese"; MessagesFile: "compiler:Vietnamese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start KKBOT on Windows startup"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Core runtime
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; License file
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

; Configuration templates
Source: "..\config\*"; DestDir: "{app}\config"; Flags: ignoreversion recursesubdirs createallsubdirs; OnlyBelowVersion: 0.0.0

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\data"

[Registry]
; Application registration for Windows
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: string; ValueName: "DisplayVersion"; ValueData: "{#MyAppVersion}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: string; ValueName: "Publisher"; ValueData: "{#MyAppPublisher}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: string; ValueName: "URLInfoAbout"; ValueData: "{#MyAppURL}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: dword; ValueName: "NoModify"; ValueData: 1
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppGuid}"; ValueType: dword; ValueName: "NoRepair"; ValueData: 1

; Startup registration
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KKBOT"; ValueData: "\"{app}\{#MyAppExeName}\" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[Code]
var
  InstallPath: string;

function InitializeSetup(): Boolean;
begin
  // Check for .NET 10 runtime
  if not IsDotNetInstalled('10.0.0') then
  begin
    MsgBox('KKBOT requires .NET 10 Runtime. Please install it from https://dotnet.microsoft.com/download/dotnet/10.0', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  Result := True;
end;

function IsDotNetInstalled(version: string): Boolean;
var
  key: string;
  value: string;
begin
  key := 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full';
  if RegKeyExists(HKLM, key) then
  begin
    if RegQueryStringValue(HKLM, key, 'Release', value) then
    begin
      // .NET 10 release number is >= 535000 (approximate)
      Result := StrToInt(value) >= 535000;
    end;
  end
  else
    Result := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create necessary directories
    ForceDirectories(ExpandConstant('{app}\logs'));
    ForceDirectories(ExpandConstant('{app}\data'));
    ForceDirectories(ExpandConstant('{app}\config'));
    
    // Set permissions for data/logs folders
    Exec('icacls.exe', ExpandConstant('"{app}\logs" /grant "Users:(OI)(CI)M"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('icacls.exe', ExpandConstant('"{app}\data" /grant "Users:(OI)(CI)M"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpReady then
  begin
    InstallPath := ExpandConstant('{app}');
  end;
end;

procedure DeinitializeSetup();
begin
  // Cleanup on uninstall
end;

[Messages]
; Custom messages for silent install
english.installing=Installing KKBOT Framework...
english.uninstalling=Uninstalling KKBOT Framework...
vietnamese.installing=Đang cài đặt KKBOT Framework...
vietnamese.uninstalling=Đang gỡ cài đặt KKBOT Framework...