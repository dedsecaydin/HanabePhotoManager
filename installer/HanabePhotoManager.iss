#ifndef PublishDir
  #error PublishDir must point to the self-contained publish directory.
#endif

#ifndef OutputDir
  #define OutputDir "."
#endif

#define AppName "Hanabe Photo Manager"
#define AppVersion "0.1.0-alpha"
#define AppExeName "HanabePhotoManager.App.exe"

[Setup]
AppId={{F3569E4D-6636-4D98-A1A8-D40F540C6B56}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=HanabePhotoManager
DefaultDirName={localappdata}\Programs\HanabePhotoManager
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=HanabePhotoManager-0.1.0-alpha-win-x64-Setup
SetupIconFile=..\src\HanabePhotoManager.App\Assets\HanabeApp.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent
