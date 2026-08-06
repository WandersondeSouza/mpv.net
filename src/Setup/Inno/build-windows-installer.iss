
#define MyAppName "MPV.NET Media Player"
#define MyAppExeName "mpvnet.exe"
#define MyAppPublisher "Wanderson Estanislau de Souza Rodrigues"
#define MyAppURL "https://github.com/WandersondeSouza/mpv.net"
#define MyAppSourceDir "..\..\MpvNet.Windows\bin\Release\win-x64\publish"
#define MyAppVersion GetVersionNumbersString("..\..\MpvNet.Windows\bin\Release\win-x64\publish\mpvnet.exe")

[Setup]
AppId={{9AA2B100-BEF3-44D0-B819-D8FC3C4D557D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
DefaultDirName={autopf}\{#MyAppName}
OutputBaseFilename=MPV.NET-Media-Player-v{#MyAppVersion}-setup-x64
OutputDir=E:\Desktop
DefaultGroupName={#MyAppName}
SetupIconFile=..\..\MpvNet.Windows\mpv-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright=Copyright (C) {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
#ifdef TestInstall
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
#else
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
#endif
ChangesEnvironment=yes

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\Scripts\osc.lua"; DestDir: "{app}\Scripts"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\libmpv-2.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\libmpv-2-v3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Excludes: "Scripts\osc.lua,libmpv-2.dll,libmpv-2-v3.dll"; Flags: ignoreversion recursesubdirs createallsubdirs;

#ifndef SkipFileAssociations
[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations audio"; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations video"; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations playlist"; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations unreg"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterFileAssociations"
#endif

#ifndef TestInstall
[Code]
const
  MachineEnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  UserEnvironmentKey = 'Environment';

function GetEnvironmentRootKey(): Integer;
begin
  if IsAdminInstallMode() then
    Result := HKEY_LOCAL_MACHINE
  else
    Result := HKEY_CURRENT_USER;
end;

function GetEnvironmentKeyName(): String;
begin
  if IsAdminInstallMode() then
    Result := MachineEnvironmentKey
  else
    Result := UserEnvironmentKey;
end;

function NormalizePathEntry(Value: String): String;
begin
  Result := RemoveQuotes(Trim(Value));
  while (Length(Result) > 0) and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

function PathContainsEntry(PathValue, Entry: String): Boolean;
var
  Index: Integer;
  CurrentEntry: String;
  NormalizedEntry: String;
begin
  Result := False;
  NormalizedEntry := NormalizePathEntry(Entry);

  while PathValue <> '' do
  begin
    Index := Pos(';', PathValue);
    if Index = 0 then
    begin
      CurrentEntry := PathValue;
      PathValue := '';
    end
    else
    begin
      CurrentEntry := Copy(PathValue, 1, Index - 1);
      Delete(PathValue, 1, Index);
    end;

    if CompareText(NormalizePathEntry(CurrentEntry), NormalizedEntry) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function RemovePathEntry(PathValue, Entry: String): String;
var
  Index: Integer;
  CurrentEntry: String;
  NormalizedEntry: String;
begin
  Result := '';
  NormalizedEntry := NormalizePathEntry(Entry);

  while PathValue <> '' do
  begin
    Index := Pos(';', PathValue);
    if Index = 0 then
    begin
      CurrentEntry := PathValue;
      PathValue := '';
    end
    else
    begin
      CurrentEntry := Copy(PathValue, 1, Index - 1);
      Delete(PathValue, 1, Index);
    end;

    if (NormalizePathEntry(CurrentEntry) <> '') and
       (CompareText(NormalizePathEntry(CurrentEntry), NormalizedEntry) <> 0) then
    begin
      if Result <> '' then
        Result := Result + ';';

      Result := Result + Trim(CurrentEntry);
    end;
  end;
end;

procedure AddInstallDirToPath();
var
  PathValue: String;
  InstallDir: String;
begin
  InstallDir := ExpandConstant('{app}');

  if not RegQueryStringValue(GetEnvironmentRootKey(), GetEnvironmentKeyName(), 'Path', PathValue) then
    PathValue := '';

  if PathContainsEntry(PathValue, InstallDir) then
    Exit;

  if PathValue = '' then
    PathValue := InstallDir
  else
    PathValue := PathValue + ';' + InstallDir;

  RegWriteExpandStringValue(GetEnvironmentRootKey(), GetEnvironmentKeyName(), 'Path', PathValue);
end;

procedure RemoveInstallDirFromPath();
var
  PathValue: String;
  UpdatedPathValue: String;
begin
  if not RegQueryStringValue(GetEnvironmentRootKey(), GetEnvironmentKeyName(), 'Path', PathValue) then
    Exit;

  UpdatedPathValue := RemovePathEntry(PathValue, ExpandConstant('{app}'));

  if UpdatedPathValue <> PathValue then
    RegWriteExpandStringValue(GetEnvironmentRootKey(), GetEnvironmentKeyName(), 'Path', UpdatedPathValue);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    AddInstallDirToPath();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveInstallDirFromPath();
end;
#endif
