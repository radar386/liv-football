; Inno Setup script for LIV Football Mobile (Windows build)

#define MyAppName "LIV Football Mobile"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Name or Studio"
; Must match the exe Unity produces (your Product Name in Player Settings)
#define MyAppExeName "LIV Football Mobile.exe"
; Folder where you built the game from Unity
#define BuildDir "C:\Builds\LIVFootballMobile"

[Setup]
; Generate your own GUID in the IDE: Tools > Generate GUID
AppId={{7D7BF011-EB2F-402D-B145-4CEEF9784EF4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=LIVFootballMobile_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
; Uncomment after you create icon.ico from your game icon (256x256 PNG -> ICO):
; SetupIconFile=icon.ico
; Shows the license page (license.txt must be next to this script):
LicenseFile=license.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Ships the whole Unity build: exe, *_Data folder, UnityPlayer.dll, MonoBleedingEdge, etc.
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
