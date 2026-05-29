
#define MyAppName "mpv.net"
#define MyAppExeName "mpvnet.exe"
#define MyAppSourceDir "..\..\MpvNet.Windows\bin\Debug\win-x64\publish"
#define MyAppVersion GetFileVersion("..\..\MpvNet.Windows\bin\Debug\win-x64\publish\mpvnet.exe")
#define VideoAndPlaylistExtensions "mp4 m4v mkv webm avi mov qt wmv asf flv f4v mpg mpeg mpe m1v m2v vob ts mts m2ts 3gp 3g2 ogv ogg rm rmvb divx xvid dv nut nsv m3u m3u8 pls xspf"

[Setup]
AppId={{9AA2B100-BEF3-44D0-B819-D8FC3C4D557D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Frank Skare (stax76)
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
DefaultDirName={autopf}\{#MyAppName}
OutputBaseFilename=mpv.net-v{#MyAppVersion}-setup-x64
OutputDir=E:\Desktop
DefaultGroupName={#MyAppName}
SetupIconFile=..\..\MpvNet.Windows\mpv-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs;

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations video {#VideoAndPlaylistExtensions}"; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-file-associations unreg"; Flags: runhidden waituntilterminated
