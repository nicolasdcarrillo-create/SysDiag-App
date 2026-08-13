[Setup]
AppName=SysDiag
AppVersion=1.0.0
AppPublisher=SysDiag
AppPublisherURL=https://github.com/nicolasdcarrillo-create/SysDiag-App
AppSupportURL=https://github.com/nicolasdcarrillo-create/SysDiag-App/issues
AppUpdatesURL=https://github.com/nicolasdcarrillo-create/SysDiag-App/releases
DefaultDirName={autopf}\SysDiag
DefaultGroupName=SysDiag
Compression=lzma
SolidCompression=yes
OutputBaseFilename=sysdiag-1.0.0
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SysDiag"; Filename: "{app}\SysDiag.exe"
Name: "{group}\Desinstalar SysDiag"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\SysDiag.exe"; Description: "Abrir SysDiag"; Flags: nowait postinstall skipifsilent
