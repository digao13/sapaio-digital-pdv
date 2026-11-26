# Sampaio Digital â€” Sistema de Vendas (PDV) & Ordem de ServiÃ§o (WPF + SQLite)

Sistema desktop para Windows desenvolvido em **C# / WPF (.NET 8)** com persistÃªncia local em **SQLite**, projetado para lojas de informÃ¡tica e varejo.

---

## Funcionalidades Principais

1. **ConfiguraÃ§Ã£o Inicial ObrigatÃ³ria da Loja (Onboarding)**
   - Antes de operar o sistema, exige o preenchimento de **RazÃ£o Social**, **Telefone/WhatsApp**, **Foto/Logo da loja** e criaÃ§Ã£o do **UsuÃ¡rio Administrador inicial**.
   - A logo e os dados da loja sÃ£o exibidos automaticamente no cabeÃ§alho do sistema, no **PDV** e nas **Ordens de ServiÃ§o (PDF)**.

2. **Controle de UsuÃ¡rios e AutenticaÃ§Ã£o**
   - Cadastro de usuÃ¡rios (`Administrador` e `Operador`) com senhas protegidas por hash **PBKDF2 (SHA-256 + Salt)**.
   - Registro automÃ¡tico do usuÃ¡rio conectado em todas as vendas, movimentaÃ§Ãµes de estoque e orÃ§amentos/OS.

3. **Cadastro de Produtos e MovimentaÃ§Ã£o de Estoque**
   - Cadastro com **Nome**, **Quantidade**, **CÃ³digo de Barras (opcional)**, **Unidade**, **PreÃ§o de Venda** e **PreÃ§o de Custo**.
   - **Entrada e SaÃ­da de Estoque** com registro de motivo, saldo anterior, saldo novo, usuÃ¡rio responsÃ¡vel e data/hora.

4. **Cadastro de Clientes**
   - Cadastro com **Nome**, **Telefone / WhatsApp**, **EndereÃ§o** e observaÃ§Ãµes, com busca rÃ¡pida.

5. **OrÃ§amentos / Ordem de ServiÃ§o (InformÃ¡tica) + PDF**
   - Vincula o cliente, equipamento/dispositivo, problema relatado, serviÃ§os previstos, valor estimado, status e data/hora.
   - O campo **ResponsÃ¡vel** Ã© preenchido automaticamente com o **usuÃ¡rio conectado**.
   - Gera **PDF profissional (A4)** via `QuestPDF` contendo logo, razÃ£o social e telefone da loja, dados do cliente, problema, quem fez a OS e horÃ¡rio.

6. **PDV (Ponto de Venda) + Cupom NÃ£o Fiscal**
   - Leitura por **cÃ³digo de barras** (`Enter`) ou busca por nome do produto.
   - Carrinho de compras, desconto, formas de pagamento (Dinheiro, PIX, DÃ©bito, CrÃ©dito) e cÃ¡lculo automÃ¡tico de troco.
   - **Baixa transacional automÃ¡tica** no estoque ao finalizar a venda.
   - ImpressÃ£o de **Cupom NÃ£o Fiscal** em layout de mercado (bobina tÃ©rmica 80mm) com logo da loja, dados da venda, itens e totais.

7. **RelatÃ³rios**
   - Filtro por perÃ­odo com cards de resumo e abas para:
     - **Baixas e MovimentaÃ§Ãµes de Estoque** (Entradas, SaÃ­das e Baixas por Venda)
     - **Vendas Realizadas (PDV)** com reimpressÃ£o de cupom
     - **OrÃ§amentos / Ordens de ServiÃ§o Feitos** com geraÃ§Ã£o/reimpressÃ£o de PDF

8. **Versionamento, Instalador Windows e AtualizaÃ§Ã£o via GitHub**
   - **Banco de Dados Protegido**: O arquivo `sapaio_digital.db`, backups e logos ficam armazenados em `%LocalAppData%\SapaioDigital\`, fora da pasta de instalaÃ§Ã£o (`Program Files`), garantindo que **nenhuma atualizaÃ§Ã£o ou reinstalaÃ§Ã£o sobrescreva os dados**.
   - **Backup Preventivo**: Antes de qualquer migraÃ§Ã£o de schema ou atualizaÃ§Ã£o, o sistema cria um backup atÃ´mico via `VACUUM INTO` em `%LocalAppData%\SapaioDigital\Backups\`.
   - **Instalador Windows (`SapaioDigital-Setup-vX.Y.Z.exe`)**: Gerado automaticamente via **Inno Setup** no GitHub Actions.
   - **Auto-Update Integrado**: Na tela **Loja & AtualizaÃ§Ã£o**, o sistema consulta a Ãºltima release no GitHub (`digao13/sapaio-digital-pdv`), baixa o instalador `.exe` (ou pacote `.zip` portÃ¡til) e aplica a atualizaÃ§Ã£o preservando o banco SQLite.

---

## Como Gerar uma Nova VersÃ£o e Publicar Instalador no GitHub

Use o script de versionamento na raiz do projeto:

```powershell
# Incrementa patch (ex: 1.0.1 -> 1.0.2), faz commit, cria tag e envia ao GitHub:
.\bump-version.ps1 -Tipo patch -PushTag

# Outras opÃ§Ãµes: minor ou major
.\bump-version.ps1 -Tipo minor -PushTag
```

Ao enviar a tag `v*`, o workflow [release.yml](.github/workflows/release.yml) executa automaticamente os testes (`dotnet test`), publica os binÃ¡rios `win-x64`, compila o instalador `SapaioDigital-Setup-vX.Y.Z.exe` e cria a **GitHub Release**.

---

## Como Executar Localmente (Desenvolvimento)

```powershell
dotnet test SapaioDigital.slnx
dotnet run --project SapaioDigital.App/SapaioDigital.App.csproj
```

---

## Tema Claro e Tema Escuro

- Alterne instantaneamente entre **Tema Claro** e **Tema Escuro** pelo botão no topo da janela principal (🌙 Tema Escuro / ☀️ Tema Claro).
- A preferência é salva automaticamente em %LocalAppData%\SapaioDigital\theme.txt.

## Dependências Embutidas no Instalador Windows

- O aplicativo é publicado como **Self-Contained (.NET 8 Windows x64)**, não exigindo instalação separada do .NET Runtime.
- O instalador SapaioDigital-Setup-vX.Y.Z.exe inclui automaticamente o **Microsoft Visual C++ Redistributable 2015–2022 (x64)** (c_redist.x64.exe), necessário para os componentes nativos do **SQLite** e **QuestPDF (SkiaSharp)** em computadores Windows recém-formatados. Caso o PC já possua o pacote, a etapa é ignorada automaticamente.