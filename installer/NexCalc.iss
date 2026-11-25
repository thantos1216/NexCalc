; NexCalc installer script - Inno Setup 6
; Builds NexCalc-Setup-1.0.0.exe from the self-contained publish output in release\app

#define MyVersion "1.1.0"
#ifndef AppSource
  #define AppSource "..\release\app"
#endif

[Setup]
AppId={{8F3A1C97-7B4D-4E2A-9C58-1A2B3C4D5E6F}
AppName=NexCalc
AppVersion={#MyVersion}
AppVerName=NexCalc {#MyVersion}
AppPublisher=NexCalc
AppPublisherURL=https://github.com/thantos1216/NexCalc
DefaultDirName={autopf}\NexCalc
DefaultGroupName=NexCalc
UninstallDisplayIcon={app}\NexCalc.exe
UninstallDisplayName=NexCalc {#MyVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
OutputDir=..\installer
OutputBaseFilename=NexCalc-Setup-{#MyVersion}
SetupIconFile=..\src\NexCalc\Assets\nexcalc.ico
LicenseFile=..\LICENSE.txt

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NexCalc"; Filename: "{app}\NexCalc.exe"; IconFilename: "{app}\NexCalc.exe"
Name: "{group}\Uninstall NexCalc"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NexCalc"; Filename: "{app}\NexCalc.exe"; IconFilename: "{app}\NexCalc.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\NexCalc.exe"; Description: "Launch NexCalc"; Flags: nowait postinstall skipifsilent
