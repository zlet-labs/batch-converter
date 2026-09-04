#ifndef AppVersion
  #error AppVersion must be supplied by build-installer.ps1
#endif
#ifndef RuntimeIdentifier
  #error RuntimeIdentifier must be supplied by build-installer.ps1
#endif
#ifndef PackageName
  #error PackageName must be supplied by build-installer.ps1
#endif
#ifndef AppExeName
  #error AppExeName must be supplied by build-installer.ps1
#endif
#ifndef SourceDir
  #error SourceDir must be supplied by build-installer.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by build-installer.ps1
#endif
#ifndef IconPath
  #error IconPath must be supplied by build-installer.ps1
#endif

#define AppName "Zlet Converter"
#define AppPublisher "Zlet Labs"
#define AppUrl "https://github.com/zlet-labs/zlet-converter"
#define SetupBaseName "ZletConverter-v" + AppVersion + "-Setup-" + RuntimeIdentifier

[Setup]
AppId={{B124EC99-C473-496E-B293-3FCA72E7CACD}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Zlet Converter
DefaultGroupName=Zlet Converter
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename={#SetupBaseName}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; ZL-057: remove only exact app-owned files/shortcuts from the previous public name.
Type: files; Name: "{app}\ZletBatchConverter.exe"
Type: files; Name: "{app}\ZletBatchConverter.dll"
Type: files; Name: "{app}\ZletBatchConverter.deps.json"
Type: files; Name: "{app}\ZletBatchConverter.runtimeconfig.json"
Type: files; Name: "{app}\ZletBatchConverter.pdb"
Type: files; Name: "{autoprograms}\Zlet Batch Converter.lnk"
Type: files; Name: "{autodesktop}\Zlet Batch Converter.lnk"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Zlet Converter"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Zlet Converter"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Bootstrap through the application's single settings store. It writes only when no
; valid saved language exists, so upgrades never overwrite a user's later choice.
Filename: "{app}\{#AppExeName}"; Parameters: "--bootstrap-language={code:GetInitialAppLanguage}"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,Zlet Converter}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
function GetInitialAppLanguage(Param: String): String;
begin
  if ActiveLanguage = 'russian' then
    Result := 'ru-RU'
  else
    Result := 'en-US';
end;
