#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\src\HattrickSSH\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{A3D7F31F-6A80-4B0A-A167-0C37E1F2D9B4}
AppName=Hattrick SSH
AppVersion={#AppVersion}
AppPublisher=Hattrick SSH
AppPublisherURL=https://github.com/dincer552/deskopssh
DefaultDirName={localappdata}\Programs\HattrickSSH
DefaultGroupName=Hattrick SSH
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=HattrickSSH-Setup
SetupIconFile=..\assets\HattrickSSH.ico
UninstallDisplayIcon={app}\HattrickSSH.exe
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
CloseApplications=yes
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UsePreviousAppDir=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Hattrick SSH"; Filename: "{app}\HattrickSSH.exe"
Name: "{autodesktop}\Hattrick SSH"; Filename: "{app}\HattrickSSH.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek kısayollar:"

[Run]
Filename: "{app}\HattrickSSH.exe"; Description: "Hattrick SSH uygulamasını başlat"; Flags: nowait postinstall skipifsilent
