# Sapaio Digital — Sistema de Vendas (PDV) & Ordem de Serviço (WPF + SQLite)

Sistema desktop para Windows desenvolvido em **C# / WPF (.NET 8)** com persistência local em **SQLite**, projetado para lojas de informática e varejo.

---

## Funcionalidades Principais

1. **Configuração Inicial Obrigatória da Loja (Onboarding)**
   - Antes de operar o sistema, exige o preenchimento de **Razão Social**, **Telefone/WhatsApp**, **Foto/Logo da loja** e criação do **Usuário Administrador inicial**.
   - A logo e os dados da loja são exibidos automaticamente no cabeçalho do sistema, no **PDV** e nas **Ordens de Serviço (PDF)**.

2. **Controle de Usuários e Autenticação**
   - Cadastro de usuários (`Administrador` e `Operador`) com senhas protegidas por hash **PBKDF2 (SHA-256 + Salt)**.
   - Registro automático do usuário conectado em todas as vendas, movimentações de estoque e orçamentos/OS.

3. **Cadastro de Produtos e Movimentação de Estoque**
   - Cadastro com **Nome**, **Quantidade**, **Código de Barras (opcional)**, **Unidade**, **Preço de Venda** e **Preço de Custo**.
   - **Entrada e Saída de Estoque** com registro de motivo, saldo anterior, saldo novo, usuário responsável e data/hora.

4. **Cadastro de Clientes**
   - Cadastro com **Nome**, **Telefone / WhatsApp**, **Endereço** e observações, com busca rápida.

5. **Orçamentos / Ordem de Serviço (Informática) + PDF**
   - Vincula o cliente, equipamento/dispositivo, problema relatado, serviços previstos, valor estimado, status e data/hora.
   - O campo **Responsável** é preenchido automaticamente com o **usuário conectado**.
   - Gera **PDF profissional (A4)** via `QuestPDF` contendo logo, razão social e telefone da loja, dados do cliente, problema, quem fez a OS e horário.

6. **PDV (Ponto de Venda) + Cupom Não Fiscal**
   - Leitura por **código de barras** (`Enter`) ou busca por nome do produto.
   - Carrinho de compras, desconto, formas de pagamento (Dinheiro, PIX, Débito, Crédito) e cálculo automático de troco.
   - **Baixa transacional automática** no estoque ao finalizar a venda.
   - Impressão de **Cupom Não Fiscal** em layout de mercado (bobina térmica 80mm) com logo da loja, dados da venda, itens e totais.

7. **Relatórios**
   - Filtro por período com cards de resumo e abas para:
     - **Baixas e Movimentações de Estoque** (Entradas, Saídas e Baixas por Venda)
     - **Vendas Realizadas (PDV)** com reimpressão de cupom
     - **Orçamentos / Ordens de Serviço Feitos** com geração/reimpressão de PDF

8. **Versionamento, Instalador Windows e Atualização via GitHub**
   - **Banco de Dados Protegido**: O arquivo `sapaio_digital.db`, backups e logos ficam armazenados em `%LocalAppData%\SapaioDigital\`, fora da pasta de instalação (`Program Files`), garantindo que **nenhuma atualização ou reinstalação sobrescreva os dados**.
   - **Backup Preventivo**: Antes de qualquer migração de schema ou atualização, o sistema cria um backup atômico via `VACUUM INTO` em `%LocalAppData%\SapaioDigital\Backups\`.
   - **Instalador Windows (`SapaioDigital-Setup-vX.Y.Z.exe`)**: Gerado automaticamente via **Inno Setup** no GitHub Actions.
   - **Auto-Update Integrado**: Na tela **Loja & Atualização**, o sistema consulta a última release no GitHub (`digao13/sapaio-digital-pdv`), baixa o instalador `.exe` (ou pacote `.zip` portátil) e aplica a atualização preservando o banco SQLite.

---

## Como Gerar uma Nova Versão e Publicar Instalador no GitHub

Use o script de versionamento na raiz do projeto:

```powershell
# Incrementa patch (ex: 1.0.1 -> 1.0.2), faz commit, cria tag e envia ao GitHub:
.\bump-version.ps1 -Tipo patch -PushTag

# Outras opções: minor ou major
.\bump-version.ps1 -Tipo minor -PushTag
```

Ao enviar a tag `v*`, o workflow [release.yml](.github/workflows/release.yml) executa automaticamente os testes (`dotnet test`), publica os binários `win-x64`, compila o instalador `SapaioDigital-Setup-vX.Y.Z.exe` e cria a **GitHub Release**.

---

## Como Executar Localmente (Desenvolvimento)

```powershell
dotnet test SapaioDigital.slnx
dotnet run --project SapaioDigital.App/SapaioDigital.App.csproj
```
