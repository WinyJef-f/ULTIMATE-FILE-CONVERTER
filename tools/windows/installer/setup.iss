; Inno Setup script for the ULTIMATE-FILE-CONVERTER EXE installer.
; Values are supplied on the command line by tools/windows/build-installers.ps1:
;   ISCC.exe /DAppVersion=1.0.1 /DPublishDir=<publish> /DIconFile=<app.ico> /O<outdir> setup.iss
; Sensible defaults are provided so the script can also be opened directly in the Inno IDE.

#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\..\dist\windows\publish"
#endif
#ifndef IconFile
  #define IconFile "..\..\..\Windows\UltimateFileConverter.WinUI\Assets\app.ico"
#endif

#define MyAppName "ULTIMATE-FILE-CONVERTER"
#define MyAppExe "UltimateFileConverter.WinUI.exe"
#define MyAppPublisher "Jeffrey Heiler"
#define MyAppUrl "https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER"

[Setup]
AppId={{A5A3DA6A-1C67-4DA8-9E2D-62AE234E6B6D}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
SetupIconFile={#IconFile}
OutputBaseFilename=ULTIMATE-FILE-CONVERTER-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
LicenseFile=resources\license.rtf
InfoAfterFile=resources\readme.rtf

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to ULTIMATE-FILE-CONVERTER
WelcomeLabel2=ULTIMATE-FILE-CONVERTER converts files between 40+ formats — images, audio, video, documents, spreadsheets, presentations, subtitles, archives, e-books, and fonts — entirely on your device, with no network calls or uploads.%n%nOn first launch, the app will offer to install the required conversion tools (FFmpeg, ImageMagick, MuPDF, Pandoc, LibreOffice, 7-Zip, Calibre, FontForge) via winget. Windows may prompt for permission per package.%n%nClick Next to continue.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
; Right-click "Convert with UFC" context menu for all files
Root: HKCR; Subkey: "*\shell\ConvertWithUFC"; ValueType: string; ValueData: "Convert with UFC"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\ConvertWithUFC"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExe}"",0"
Root: HKCR; Subkey: "*\shell\ConvertWithUFC\command"; ValueType: string; ValueData: """{app}\{#MyAppExe}"" ""%1"""
; Add the install directory to the system PATH so ufc.exe is available in terminals
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueName: "Path"; ValueType: expandsz; ValueData: "{olddata};{app}"; Check: NeedsAddPath('{app}')

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
{ Returns True when {app} is not already present anywhere in the system PATH. }
function NeedsAddPath(AppDir: string): Boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(
      HKEY_LOCAL_MACHINE,
      'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
      'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(AppDir) + ';',
               ';' + Uppercase(OrigPath) + ';') = 0;
end;
