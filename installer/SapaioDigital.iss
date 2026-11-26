#ifndef AppVersion
  #define AppVersion "1.0.2"
#endif

[Setup]
AppId={{8D6B4A73-2D9E-4B18-9F5C-4B2C9E1E8A11}
AppName=Sampaio Digital
AppVersion={#AppVersion}
AppPublisher=Sampaio Digital
DefaultDirName={autopf}\Sampaio Digital
DefaultGroupName=Sampaio Digital
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=SapaioDigital-Setup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=yes
UninstallDisplayIcon={app}\SapaioDigital.App.exe
SetupIconFile=..\SapaioDigital.App\Assets\app_icon.ico

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar ícone na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
; Binários da aplicação (Self-Contained .NET 8 Desktop Runtime já incluído).
; O banco SQLite e a logo ficam protegidos em %LocalAppData%\SapaioDigital e NUNCA são sobrescritos.
Source: "..\publish_out\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Dependência nativa obrigatória (SQLite e_sqlite3.dll / QuestPDF SkiaSharp) em PCs Windows limpos
Source: "redist\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NecessitaVcRedist

[Icons]
Name: "{group}\Sampaio Digital"; Filename: "{app}\SapaioDigital.App.exe"
Name: "{group}\Desinstalar Sampaio Digital"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Sampaio Digital"; Filename: "{app}\SapaioDigital.App.exe"; Tasks: desktopicon

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /passive /norestart"; StatusMsg: "Instalando Microsoft Visual C++ Redistributable (x64)..."; Check: NecessitaVcRedist
Filename: "{app}\SapaioDigital.App.exe"; Description: "Executar Sampaio Digital"; Flags: nowait postinstall skipifsilent

[Code]
function NecessitaVcRedist: Boolean;
var
  Installed: Cardinal;
begin
  Result := True;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
  begin
    if Installed = 1 then
      Result := False;
  end;
end;
