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

#define AppName "Zlet Batch Converter"
#define AppPublisher "Zlet Labs"
#define AppUrl "https://github.com/zlet-labs/folder-converter"
#define SetupBaseName "ZletBatchConverter-v" + AppVersion + "-Setup-" + RuntimeIdentifier

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
DefaultDirName={localappdata}\Programs\Zlet Batch Converter
DefaultGroupName=Zlet Batch Converter
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

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Zlet Batch Converter"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Zlet Batch Converter"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,Zlet Batch Converter}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
