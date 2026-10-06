; LATCHI Browser — Inno Setup script
; Per-user install (NO admin rights), x64 only — same pattern as LATCHI xCLOUD.
; User data lives under %LOCALAPPDATA%\LATCHI\Browser and is NEVER touched on uninstall.
;
; Build:  ISCC /DMyAppVersion=1.0.0 installer\latchi-browser.iss
; Expects the self-contained publish output in ..\publish\

#define MyAppName "LATCHI Browser"
#define MyAppExeName "LATCHI-BROWSER.exe"
#ifndef MyAppVersion
#define MyAppVersion "1.1.1"
#endif

[Setup]
AppId={{7C3F4A21-9E5B-4D87-B2A4-LATCHIBRW1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=LATCHI
AppPublisherURL=https://github.com/latchidz/LATCHI-BROWSER
DefaultDirName={localappdata}\LATCHI Browser
DefaultGroupName=LATCHI Browser
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=LATCHI-BROWSER-{#MyAppVersion}-x64-Setup
SetupIconFile=..\assets\appicon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

; NOTE (deliberate): no [UninstallDelete] — profiles, sessions, bookmarks, history
; and the DPAPI-encrypted key under %LOCALAPPDATA%\LATCHI\Browser must survive an
; uninstall (they belong to the user, not to the app).
