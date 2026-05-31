
#define MyAppName "MPV.NET Media Player Community Edition"
#define MyAppExeName "mpvnet.exe"
#define MyAppPublisher "Wanderson Estanislau de Souza Rodrigues"
#define MyAppURL "https://github.com/WandersondeSouza/mpv.net"
#define MyAppSourceDir "..\..\MpvNet.Windows\bin\Debug\win-x64\publish"
#define MyAppVersion GetFileVersion("..\..\MpvNet.Windows\bin\Debug\win-x64\publish\mpvnet.exe")

[Setup]
AppId={{9AA2B100-BEF3-44D0-B819-D8FC3C4D557D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
DefaultDirName={autopf}\{#MyAppName}
OutputBaseFilename=MPV.NET-Media-Player-Community-Edition-v{#MyAppVersion}-setup-x64
OutputDir=E:\Desktop
DefaultGroupName={#MyAppName}
SetupIconFile=..\..\MpvNet.Windows\mpv-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright=Copyright (C) {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs;

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations video"; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations audio"; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations unreg"; Flags: runhidden waituntilterminated
