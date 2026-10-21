; ReType installer (Inno Setup 6). Build with scripts/build-installer.ps1
#define MyAppName "ReType"
#ifndef MyAppVersion
  ; placeholder - build-installer.ps1 overrides via /DMyAppVersion from the git tag.
  ; The #ifndef guard matters: an unconditional #define would clobber the /D value.
  #define MyAppVersion "0.0.0"
#endif
#define MyAppPublisher "makomo-oss"

[Setup]
AppId={{B4D9F1D6-3C5E-4A2B-9E7A-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=https://github.com/makomo-oss/ReType
DefaultDirName={userpf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=NOTICE.txt
OutputDir=..\dist
OutputBaseFilename=ReType-Setup-{#MyAppVersion}
SetupIconFile=..\assets\app.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\ReType.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes

[Tasks]
Name: "autostart"; Description: "Windows の起動時に ReType を自動的に開始する"
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "*.pdb"

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\ReType.exe"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\ReType.exe"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "ReType"; ValueData: """{app}\ReType.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\ReType.exe"; Description: "ReType を起動する"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM ReType.exe /F"; Flags: runhidden; RunOnceId: "KillReType"
