#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif

[Setup]
AppId={{8D6B4A73-2D9E-4B18-9F5C-4B2C9E1E8A11}
AppName=Sapaio Digital
AppVersion={#AppVersion}
AppPublisher=Sapaio Digital
DefaultDirName={autopf}\Sapaio Digital
DefaultGroupName=Sapaio Digital
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=SapaioDigital-Setup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=yes
UninstallDisplayIcon={app}\SapaioDigital.App.exe

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar ícone na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
; Copia apenas os binários da aplicação.
; O banco SQLite e a logo ficam protegidos em %LocalAppData%\SapaioDigital e NUNCA são sobrescritos.
Source: "..\publish_out\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Sapaio Digital"; Filename: "{app}\SapaioDigital.App.exe"
Name: "{group}\Desinstalar Sapaio Digital"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Sapaio Digital"; Filename: "{app}\SapaioDigital.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SapaioDigital.App.exe"; Description: "Executar Sapaio Digital"; Flags: nowait postinstall skipifsilent
