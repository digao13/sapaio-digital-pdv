using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Data;

public static class DatabaseService
{
    private const int CurrentSchemaVersion = 4;

    private static string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = AppPaths.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = true
    }.ConnectionString;

    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public static void Initialize()
    {
        AppPaths.EnsureDirectories();
        bool dbExisted = File.Exists(AppPaths.DatabasePath);

        using var conn = OpenConnection();

        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersion';";
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (!exists)
            {
                CreateBaseSchema(conn);
                SetSchemaVersion(conn, CurrentSchemaVersion);
                return;
            }
        }

        int version = GetSchemaVersion(conn);
        if (version < CurrentSchemaVersion && dbExisted)
        {
            CriarBackupSeguro($"pre_migracao_v{version}_para_v{CurrentSchemaVersion}");
            if (version < 2)
            {
                using var migCmd = conn.CreateCommand();
                migCmd.CommandText = "ALTER TABLE LojaConfig ADD COLUMN Cnpj TEXT NOT NULL DEFAULT '';";
                migCmd.ExecuteNonQuery();
                SetSchemaVersion(conn, 2);
            }
            if (version < 3)
            {
                using var m3 = conn.CreateCommand();
                m3.CommandText = @"ALTER TABLE Orcamentos ADD COLUMN ValorServico REAL NOT NULL DEFAULT 0;
                    CREATE TABLE IF NOT EXISTS OrcamentoItens (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OrcamentoId INTEGER NOT NULL,
                        ProdutoId INTEGER NOT NULL,
                        ProdutoNome TEXT NOT NULL,
                        Quantidade INTEGER NOT NULL CHECK (Quantidade > 0),
                        PrecoUnitario REAL NOT NULL,
                        Baixado INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (OrcamentoId) REFERENCES Orcamentos(Id) ON DELETE CASCADE,
                        FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
                    );";
                m3.ExecuteNonQuery();
                SetSchemaVersion(conn, 3);
            }
            if (version < 4)
            {
                using var m4 = conn.CreateCommand();
                m4.CommandText = @"ALTER TABLE LojaConfig ADD COLUMN Endereco TEXT NOT NULL DEFAULT '';
                    UPDATE LojaConfig SET Endereco = '' WHERE Endereco IS NULL;";
                m4.ExecuteNonQuery();
                SetSchemaVersion(conn, 4);
            }
        }
    }

    public static string CriarBackupSeguro(string prefixo = "manual")
    {
        AppPaths.EnsureDirectories();
        string fileName = $"sapaio_{prefixo}_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        string destPath = Path.Combine(AppPaths.BackupsDir, fileName);

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "VACUUM INTO @dest;";
        cmd.Parameters.AddWithValue("@dest", destPath);
        cmd.ExecuteNonQuery();
        if (!File.Exists(destPath) || new FileInfo(destPath).Length == 0)
            throw new IOException("Falha ao gerar backup válido do banco de dados.");
        return destPath;
    }

    private static int GetSchemaVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetSchemaVersion(SqliteConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO SchemaVersion (Version, AppliedAt) VALUES (@v, @dt);";
        cmd.Parameters.AddWithValue("@v", version);
        cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private static void CreateBaseSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version INTEGER PRIMARY KEY,
                AppliedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS LojaConfig (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                RazaoSocial TEXT NOT NULL,
                Telefone TEXT NOT NULL,
                Cnpj TEXT NOT NULL DEFAULT '',
                Endereco TEXT NOT NULL DEFAULT '',
                LogoPath TEXT NOT NULL,
                Configurada INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Usuarios (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                Login TEXT NOT NULL UNIQUE,
                SenhaHash TEXT NOT NULL,
                Perfil TEXT NOT NULL DEFAULT 'Operador',
                Ativo INTEGER NOT NULL DEFAULT 1,
                CriadoEm TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Produtos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                CodigoBarras TEXT UNIQUE,
                Quantidade INTEGER NOT NULL DEFAULT 0,
                PrecoVenda REAL NOT NULL DEFAULT 0,
                PrecoCusto REAL NOT NULL DEFAULT 0,
                Unidade TEXT NOT NULL DEFAULT 'UN',
                Ativo INTEGER NOT NULL DEFAULT 1,
                CriadoEm TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MovimentacoesEstoque (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProdutoId INTEGER NOT NULL,
                ProdutoNome TEXT NOT NULL,
                Tipo TEXT NOT NULL CHECK (Tipo IN ('ENTRADA', 'SAIDA', 'VENDA')),
                Quantidade INTEGER NOT NULL,
                SaldoAnterior INTEGER NOT NULL,
                SaldoNovo INTEGER NOT NULL,
                Motivo TEXT NOT NULL DEFAULT '',
                UsuarioId INTEGER,
                UsuarioNome TEXT NOT NULL DEFAULT '',
                DataHora TEXT NOT NULL,
                FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE CASCADE,
                FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS Clientes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                TelefoneWhatsApp TEXT NOT NULL,
                Endereco TEXT NOT NULL,
                Observacoes TEXT NOT NULL DEFAULT '',
                CriadoEm TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Orcamentos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClienteId INTEGER NOT NULL,
                ClienteNome TEXT NOT NULL,
                ClienteTelefone TEXT NOT NULL,
                ClienteEndereco TEXT NOT NULL,
                Equipamento TEXT NOT NULL DEFAULT '',
                ProblemaRelatado TEXT NOT NULL,
                ServicosObservacoes TEXT NOT NULL DEFAULT '',
                ValorServico REAL NOT NULL DEFAULT 0,
                ValorEstimado REAL NOT NULL DEFAULT 0,
                DataHora TEXT NOT NULL,
                UsuarioId INTEGER,
                UsuarioNome TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Aberto',
                FOREIGN KEY (ClienteId) REFERENCES Clientes(Id) ON DELETE RESTRICT,
                FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS OrcamentoItens (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrcamentoId INTEGER NOT NULL,
                ProdutoId INTEGER NOT NULL,
                ProdutoNome TEXT NOT NULL,
                Quantidade INTEGER NOT NULL CHECK (Quantidade > 0),
                PrecoUnitario REAL NOT NULL,
                Baixado INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (OrcamentoId) REFERENCES Orcamentos(Id) ON DELETE CASCADE,
                FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS Vendas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClienteId INTEGER,
                ClienteNome TEXT NOT NULL DEFAULT 'Consumidor Final',
                UsuarioId INTEGER,
                UsuarioNome TEXT NOT NULL,
                DataHora TEXT NOT NULL,
                FormaPagamento TEXT NOT NULL,
                Subtotal REAL NOT NULL,
                Desconto REAL NOT NULL DEFAULT 0,
                Total REAL NOT NULL,
                ValorRecebido REAL NOT NULL DEFAULT 0,
                Troco REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (ClienteId) REFERENCES Clientes(Id) ON DELETE SET NULL,
                FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS VendaItens (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VendaId INTEGER NOT NULL,
                ProdutoId INTEGER NOT NULL,
                ProdutoNome TEXT NOT NULL,
                CodigoBarras TEXT,
                Quantidade INTEGER NOT NULL CHECK (Quantidade > 0),
                PrecoUnitario REAL NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY (VendaId) REFERENCES Vendas(Id) ON DELETE CASCADE,
                FOREIGN KEY (ProdutoId) REFERENCES Produtos(Id) ON DELETE RESTRICT
            );
        ";
        cmd.ExecuteNonQuery();
    }

    // ==================== LOJA CONFIG ====================
    public static LojaConfig? ObterLojaConfig()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, RazaoSocial, Telefone, Cnpj, Endereco, LogoPath, Configurada FROM LojaConfig WHERE Id = 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new LojaConfig
        {
            Id = reader.GetInt32(0),
            RazaoSocial = reader.GetString(1),
            Telefone = reader.GetString(2),
            Cnpj = reader.GetString(3),
            Endereco = reader.GetString(4),
            LogoPath = reader.GetString(5),
            Configurada = reader.GetInt32(6) == 1
        };
    }

    public static void SalvarLojaConfig(LojaConfig config)
    {
        AppPaths.EnsureDirectories();
        string finalLogoPath = config.LogoPath;

        if (!string.IsNullOrWhiteSpace(config.LogoPath) && File.Exists(config.LogoPath))
        {
            string ext = Path.GetExtension(config.LogoPath);
            string destLogo = Path.Combine(AppPaths.LogosDir, $"logo_loja{ext}");
            if (!string.Equals(Path.GetFullPath(config.LogoPath), Path.GetFullPath(destLogo), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(config.LogoPath, destLogo, overwrite: true);
            }
            finalLogoPath = destLogo;
        }

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO LojaConfig (Id, RazaoSocial, Telefone, Cnpj, Endereco, LogoPath, Configurada)
            VALUES (1, @razao, @tel, @cnpj, @end, @logo, @conf)
            ON CONFLICT(Id) DO UPDATE SET
                RazaoSocial = excluded.RazaoSocial,
                Telefone = excluded.Telefone,
                Cnpj = excluded.Cnpj,
                Endereco = excluded.Endereco,
                LogoPath = excluded.LogoPath,
                Configurada = excluded.Configurada;
        ";
        cmd.Parameters.AddWithValue("@razao", config.RazaoSocial.Trim());
        cmd.Parameters.AddWithValue("@tel", config.Telefone.Trim());
        cmd.Parameters.AddWithValue("@cnpj", (config.Cnpj ?? "").Trim());
        cmd.Parameters.AddWithValue("@end", (config.Endereco ?? "").Trim());
        cmd.Parameters.AddWithValue("@logo", finalLogoPath);
        cmd.Parameters.AddWithValue("@conf", config.Configurada ? 1 : 0);
        cmd.ExecuteNonQuery();

        config.LogoPath = finalLogoPath;
        SessaoAtual.Loja = config;
        SessaoAtual.NotificarLojaAtualizada();
    }

    // ==================== USUARIOS ====================
    public static bool ExisteAlgumUsuario()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE Ativo = 1;";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public static Usuario? Autenticar(string login, string senha)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nome, Login, SenhaHash, Perfil, Ativo, CriadoEm FROM Usuarios WHERE LOWER(Login) = LOWER(@login) AND Ativo = 1;";
        cmd.Parameters.AddWithValue("@login", login.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        string hash = reader.GetString(3);
        if (!PasswordHasher.Verify(senha, hash)) return null;

        return new Usuario
        {
            Id = reader.GetInt32(0),
            Nome = reader.GetString(1),
            Login = reader.GetString(2),
            SenhaHash = hash,
            Perfil = reader.GetString(4),
            Ativo = reader.GetInt32(5) == 1,
            CriadoEm = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture)
        };
    }

    public static List<Usuario> ListarUsuarios()
    {
        var lista = new List<Usuario>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nome, Login, SenhaHash, Perfil, Ativo, CriadoEm FROM Usuarios ORDER BY Nome;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lista.Add(new Usuario
            {
                Id = reader.GetInt32(0),
                Nome = reader.GetString(1),
                Login = reader.GetString(2),
                SenhaHash = reader.GetString(3),
                Perfil = reader.GetString(4),
                Ativo = reader.GetInt32(5) == 1,
                CriadoEm = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture)
            });
        }
        return lista;
    }

    public static void SalvarUsuario(Usuario usuario, string? senhaPlana)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        if (usuario.Id == 0)
        {
            if (string.IsNullOrWhiteSpace(senhaPlana))
                throw new InvalidOperationException("Informe uma senha para o novo usuário.");

            cmd.CommandText = @"
                INSERT INTO Usuarios (Nome, Login, SenhaHash, Perfil, Ativo, CriadoEm)
                VALUES (@nome, @login, @hash, @perfil, @ativo, @criado);";
            cmd.Parameters.AddWithValue("@hash", PasswordHasher.Hash(senhaPlana));
            cmd.Parameters.AddWithValue("@criado", DateTime.Now.ToString("o"));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(senhaPlana))
            {
                cmd.CommandText = @"
                    UPDATE Usuarios SET Nome = @nome, Login = @login, SenhaHash = @hash, Perfil = @perfil, Ativo = @ativo
                    WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@hash", PasswordHasher.Hash(senhaPlana));
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE Usuarios SET Nome = @nome, Login = @login, Perfil = @perfil, Ativo = @ativo
                    WHERE Id = @id;";
            }
            cmd.Parameters.AddWithValue("@id", usuario.Id);
        }

        cmd.Parameters.AddWithValue("@nome", usuario.Nome.Trim());
        cmd.Parameters.AddWithValue("@login", usuario.Login.Trim());
        cmd.Parameters.AddWithValue("@perfil", usuario.Perfil);
        cmd.Parameters.AddWithValue("@ativo", usuario.Ativo ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    // ==================== PRODUTOS & ESTOQUE ====================
    public static List<Produto> ListarProdutos(string filtro = "")
    {
        var lista = new List<Produto>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        if (string.IsNullOrWhiteSpace(filtro))
        {
            cmd.CommandText = "SELECT Id, Nome, CodigoBarras, Quantidade, PrecoVenda, PrecoCusto, Unidade, Ativo, CriadoEm FROM Produtos WHERE Ativo = 1 ORDER BY Nome;";
        }
        else
        {
            cmd.CommandText = @"
                SELECT Id, Nome, CodigoBarras, Quantidade, PrecoVenda, PrecoCusto, Unidade, Ativo, CriadoEm
                FROM Produtos
                WHERE Ativo = 1 AND (LOWER(Nome) LIKE LOWER(@f) OR COALESCE(CodigoBarras, '') LIKE @f)
                ORDER BY Nome;";
            cmd.Parameters.AddWithValue("@f", $"%{filtro.Trim()}%");
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lista.Add(MapProduto(reader));
        }
        return lista;
    }

    public static Produto? ObterProdutoPorCodigoBarras(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nome, CodigoBarras, Quantidade, PrecoVenda, PrecoCusto, Unidade, Ativo, CriadoEm FROM Produtos WHERE Ativo = 1 AND CodigoBarras = @cod;";
        cmd.Parameters.AddWithValue("@cod", codigo.Trim());
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapProduto(reader) : null;
    }

    private static Produto MapProduto(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Nome = reader.GetString(1),
        CodigoBarras = reader.IsDBNull(2) ? null : reader.GetString(2),
        Quantidade = reader.GetInt32(3),
        PrecoVenda = Convert.ToDecimal(reader.GetDouble(4)),
        PrecoCusto = Convert.ToDecimal(reader.GetDouble(5)),
        Unidade = reader.GetString(6),
        Ativo = reader.GetInt32(7) == 1,
        CriadoEm = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture)
    };

    public static void SalvarProduto(Produto produto, int? usuarioId, string usuarioNome)
    {
        string? codigoLimpo = string.IsNullOrWhiteSpace(produto.CodigoBarras) ? null : produto.CodigoBarras.Trim();

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        if (produto.Id == 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Produtos (Nome, CodigoBarras, Quantidade, PrecoVenda, PrecoCusto, Unidade, Ativo, CriadoEm)
                VALUES (@nome, @cod, @qtd, @pv, @pc, @un, 1, @dt);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@nome", produto.Nome.Trim());
            cmd.Parameters.AddWithValue("@cod", (object?)codigoLimpo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@qtd", produto.Quantidade);
            cmd.Parameters.AddWithValue("@pv", (double)produto.PrecoVenda);
            cmd.Parameters.AddWithValue("@pc", (double)produto.PrecoCusto);
            cmd.Parameters.AddWithValue("@un", string.IsNullOrWhiteSpace(produto.Unidade) ? "UN" : produto.Unidade.Trim());
            cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("o"));
            produto.Id = Convert.ToInt32(cmd.ExecuteScalar());

            if (produto.Quantidade > 0)
            {
                InserirMovimentacao(conn, tx, produto.Id, produto.Nome.Trim(), "ENTRADA", produto.Quantidade, 0, produto.Quantidade, "Estoque inicial no cadastro", usuarioId, usuarioNome);
            }
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                UPDATE Produtos
                SET Nome = @nome, CodigoBarras = @cod, PrecoVenda = @pv, PrecoCusto = @pc, Unidade = @un
                WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", produto.Id);
            cmd.Parameters.AddWithValue("@nome", produto.Nome.Trim());
            cmd.Parameters.AddWithValue("@cod", (object?)codigoLimpo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pv", (double)produto.PrecoVenda);
            cmd.Parameters.AddWithValue("@pc", (double)produto.PrecoCusto);
            cmd.Parameters.AddWithValue("@un", string.IsNullOrWhiteSpace(produto.Unidade) ? "UN" : produto.Unidade.Trim());
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public static void RegistrarMovimentacaoEstoque(int produtoId, string tipo, int quantidade, string motivo, int? usuarioId, string usuarioNome)
    {
        if (quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        string nomeProd;
        int saldoAtual;
        using (var getCmd = conn.CreateCommand())
        {
            getCmd.Transaction = tx;
            getCmd.CommandText = "SELECT Nome, Quantidade FROM Produtos WHERE Id = @id;";
            getCmd.Parameters.AddWithValue("@id", produtoId);
            using var r = getCmd.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("Produto não encontrado.");
            nomeProd = r.GetString(0);
            saldoAtual = r.GetInt32(1);
        }

        int novoSaldo = tipo == "ENTRADA" ? saldoAtual + quantidade : saldoAtual - quantidade;
        if (novoSaldo < 0)
            throw new InvalidOperationException($"Estoque insuficiente para '{nomeProd}'. Saldo atual: {saldoAtual}.");

        using (var updCmd = conn.CreateCommand())
        {
            updCmd.Transaction = tx;
            updCmd.CommandText = "UPDATE Produtos SET Quantidade = @novo WHERE Id = @id;";
            updCmd.Parameters.AddWithValue("@novo", novoSaldo);
            updCmd.Parameters.AddWithValue("@id", produtoId);
            updCmd.ExecuteNonQuery();
        }

        InserirMovimentacao(conn, tx, produtoId, nomeProd, tipo, quantidade, saldoAtual, novoSaldo, motivo, usuarioId, usuarioNome);
        tx.Commit();
    }

    private static void InserirMovimentacao(
        SqliteConnection conn, SqliteTransaction tx,
        int produtoId, string produtoNome, string tipo, int quantidade,
        int saldoAnterior, int saldoNovo, string motivo, int? usuarioId, string usuarioNome)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            INSERT INTO MovimentacoesEstoque
                (ProdutoId, ProdutoNome, Tipo, Quantidade, SaldoAnterior, SaldoNovo, Motivo, UsuarioId, UsuarioNome, DataHora)
            VALUES
                (@pid, @pnome, @tipo, @qtd, @ant, @novo, @motivo, @uid, @unome, @dt);";
        cmd.Parameters.AddWithValue("@pid", produtoId);
        cmd.Parameters.AddWithValue("@pnome", produtoNome);
        cmd.Parameters.AddWithValue("@tipo", tipo);
        cmd.Parameters.AddWithValue("@qtd", quantidade);
        cmd.Parameters.AddWithValue("@ant", saldoAnterior);
        cmd.Parameters.AddWithValue("@novo", saldoNovo);
        cmd.Parameters.AddWithValue("@motivo", motivo ?? "");
        cmd.Parameters.AddWithValue("@uid", (object?)usuarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@unome", usuarioNome ?? "");
        cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    // ==================== CLIENTES ====================
    public static List<Cliente> ListarClientes(string filtro = "")
    {
        var lista = new List<Cliente>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        if (string.IsNullOrWhiteSpace(filtro))
        {
            cmd.CommandText = "SELECT Id, Nome, TelefoneWhatsApp, Endereco, Observacoes, CriadoEm FROM Clientes ORDER BY Nome;";
        }
        else
        {
            cmd.CommandText = @"
                SELECT Id, Nome, TelefoneWhatsApp, Endereco, Observacoes, CriadoEm
                FROM Clientes
                WHERE LOWER(Nome) LIKE LOWER(@f) OR TelefoneWhatsApp LIKE @f
                ORDER BY Nome;";
            cmd.Parameters.AddWithValue("@f", $"%{filtro.Trim()}%");
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lista.Add(new Cliente
            {
                Id = reader.GetInt32(0),
                Nome = reader.GetString(1),
                TelefoneWhatsApp = reader.GetString(2),
                Endereco = reader.GetString(3),
                Observacoes = reader.GetString(4),
                CriadoEm = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture)
            });
        }
        return lista;
    }

    public static void SalvarCliente(Cliente cliente)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        if (cliente.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Clientes (Nome, TelefoneWhatsApp, Endereco, Observacoes, CriadoEm)
                VALUES (@nome, @tel, @end, @obs, @dt);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("o"));
        }
        else
        {
            cmd.CommandText = @"
                UPDATE Clientes SET Nome = @nome, TelefoneWhatsApp = @tel, Endereco = @end, Observacoes = @obs
                WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", cliente.Id);
        }

        cmd.Parameters.AddWithValue("@nome", cliente.Nome.Trim());
        cmd.Parameters.AddWithValue("@tel", cliente.TelefoneWhatsApp.Trim());
        cmd.Parameters.AddWithValue("@end", cliente.Endereco.Trim());
        cmd.Parameters.AddWithValue("@obs", cliente.Observacoes?.Trim() ?? "");

        if (cliente.Id == 0)
            cliente.Id = Convert.ToInt32(cmd.ExecuteScalar());
        else
            cmd.ExecuteNonQuery();
    }

    // ==================== ORÇAMENTOS / OS ====================
    public static int SalvarOrcamento(Orcamento os)
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            if (os.Id == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO Orcamentos
                        (ClienteId, ClienteNome, ClienteTelefone, ClienteEndereco, Equipamento, ProblemaRelatado, ServicosObservacoes, ValorServico, ValorEstimado, DataHora, UsuarioId, UsuarioNome, Status)
                    VALUES
                        (@cid, @cnome, @ctel, @cend, @equip, @prob, @serv, @vs, @val, @dt, @uid, @unome, @status);
                    SELECT last_insert_rowid();";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE Orcamentos
                    SET ClienteId = @cid, ClienteNome = @cnome, ClienteTelefone = @ctel, ClienteEndereco = @cend,
                        Equipamento = @equip, ProblemaRelatado = @prob, ServicosObservacoes = @serv, ValorServico = @vs, ValorEstimado = @val,
                        DataHora = @dt, Status = @status
                    WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@id", os.Id);
            }

            cmd.Parameters.AddWithValue("@cid", os.ClienteId);
            cmd.Parameters.AddWithValue("@cnome", os.ClienteNome);
            cmd.Parameters.AddWithValue("@ctel", os.ClienteTelefone);
            cmd.Parameters.AddWithValue("@cend", os.ClienteEndereco);
            cmd.Parameters.AddWithValue("@equip", os.Equipamento?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@prob", os.ProblemaRelatado.Trim());
            cmd.Parameters.AddWithValue("@serv", os.ServicosObservacoes?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@vs", (double)os.ValorServico);
            cmd.Parameters.AddWithValue("@val", (double)os.ValorEstimado);
            cmd.Parameters.AddWithValue("@dt", os.DataHora.ToString("o"));
            cmd.Parameters.AddWithValue("@uid", (object?)os.UsuarioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@unome", os.UsuarioNome);
            cmd.Parameters.AddWithValue("@status", os.Status);

            if (os.Id == 0)
                os.Id = Convert.ToInt32(cmd.ExecuteScalar());
            else
                cmd.ExecuteNonQuery();
        }

        foreach (var item in os.Itens)
        {
            if (item.Baixado) continue;

            int saldoAtual;
            string nomeProd;
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT Nome, Quantidade FROM Produtos WHERE Id = @pid;";
                checkCmd.Parameters.AddWithValue("@pid", item.ProdutoId);
                using var r = checkCmd.ExecuteReader();
                if (!r.Read())
                    throw new InvalidOperationException($"Produto '{item.ProdutoNome}' não encontrado.");
                nomeProd = r.GetString(0);
                saldoAtual = r.GetInt32(1);
            }

            if (saldoAtual < item.Quantidade)
                throw new InvalidOperationException($"Estoque insuficiente para '{nomeProd}'. Disponível: {saldoAtual}, Solicitado: {item.Quantidade}.");

            int novoSaldo = saldoAtual - item.Quantidade;
            using (var updCmd = conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandText = "UPDATE Produtos SET Quantidade = @novo WHERE Id = @pid;";
                updCmd.Parameters.AddWithValue("@novo", novoSaldo);
                updCmd.Parameters.AddWithValue("@pid", item.ProdutoId);
                updCmd.ExecuteNonQuery();
            }

            InserirMovimentacao(
                conn, tx, item.ProdutoId, nomeProd, "SAIDA", item.Quantidade,
                saldoAtual, novoSaldo, $"Peça usada na OS #{os.Id:D5}", os.UsuarioId, os.UsuarioNome);

            using (var insItem = conn.CreateCommand())
            {
                insItem.Transaction = tx;
                insItem.CommandText = @"
                    INSERT INTO OrcamentoItens (OrcamentoId, ProdutoId, ProdutoNome, Quantidade, PrecoUnitario, Baixado)
                    VALUES (@oid, @pid, @pnome, @qtd, @pu, 1);
                    SELECT last_insert_rowid();";
                insItem.Parameters.AddWithValue("@oid", os.Id);
                insItem.Parameters.AddWithValue("@pid", item.ProdutoId);
                insItem.Parameters.AddWithValue("@pnome", nomeProd);
                insItem.Parameters.AddWithValue("@qtd", item.Quantidade);
                insItem.Parameters.AddWithValue("@pu", (double)item.PrecoUnitario);
                item.Id = Convert.ToInt32(insItem.ExecuteScalar());
                item.OrcamentoId = os.Id;
                item.Baixado = true;
            }
        }

        tx.Commit();
        return os.Id;
    }

    public static List<Orcamento> ListarOrcamentos(DateTime? inicio = null, DateTime? fim = null, string filtro = "")
    {
        var lista = new List<Orcamento>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = @"
            SELECT Id, ClienteId, ClienteNome, ClienteTelefone, ClienteEndereco, Equipamento,
                   ProblemaRelatado, ServicosObservacoes, ValorServico, ValorEstimado, DataHora, UsuarioId, UsuarioNome, Status
            FROM Orcamentos
            WHERE 1=1";

        if (inicio.HasValue)
        {
            sql += " AND DataHora >= @ini";
            cmd.Parameters.AddWithValue("@ini", inicio.Value.Date.ToString("o"));
        }
        if (fim.HasValue)
        {
            sql += " AND DataHora <= @fim";
            cmd.Parameters.AddWithValue("@fim", fim.Value.Date.AddDays(1).AddTicks(-1).ToString("o"));
        }
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            sql += " AND (LOWER(ClienteNome) LIKE LOWER(@f) OR LOWER(ProblemaRelatado) LIKE LOWER(@f) OR LOWER(Equipamento) LIKE LOWER(@f))";
            cmd.Parameters.AddWithValue("@f", $"%{filtro.Trim()}%");
        }

        sql += " ORDER BY Id DESC;";
        cmd.CommandText = sql;

        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                lista.Add(new Orcamento
                {
                    Id = reader.GetInt32(0),
                    ClienteId = reader.GetInt32(1),
                    ClienteNome = reader.GetString(2),
                    ClienteTelefone = reader.GetString(3),
                    ClienteEndereco = reader.GetString(4),
                    Equipamento = reader.GetString(5),
                    ProblemaRelatado = reader.GetString(6),
                    ServicosObservacoes = reader.GetString(7),
                    ValorServico = Convert.ToDecimal(reader.GetDouble(8)),
                    ValorEstimado = Convert.ToDecimal(reader.GetDouble(9)),
                    DataHora = DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture),
                    UsuarioId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    UsuarioNome = reader.GetString(12),
                    Status = reader.GetString(13)
                });
            }
        }

        foreach (var os in lista)
        {
            using var iCmd = conn.CreateCommand();
            iCmd.CommandText = "SELECT Id, OrcamentoId, ProdutoId, ProdutoNome, Quantidade, PrecoUnitario, Baixado FROM OrcamentoItens WHERE OrcamentoId = @oid;";
            iCmd.Parameters.AddWithValue("@oid", os.Id);
            using var r = iCmd.ExecuteReader();
            while (r.Read())
            {
                os.Itens.Add(new OrcamentoItem
                {
                    Id = r.GetInt32(0),
                    OrcamentoId = r.GetInt32(1),
                    ProdutoId = r.GetInt32(2),
                    ProdutoNome = r.GetString(3),
                    Quantidade = r.GetInt32(4),
                    PrecoUnitario = Convert.ToDecimal(r.GetDouble(5)),
                    Baixado = r.GetInt32(6) == 1
                });
            }
        }

        return lista;
    }

    // ==================== PDV / VENDAS ====================
    public static int FinalizarVenda(Venda venda)
    {
        if (venda.Itens.Count == 0)
            throw new InvalidOperationException("Adicione pelo menos um produto ao carrinho.");

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        foreach (var item in venda.Itens)
        {
            int saldoAtual;
            string nomeProd;
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT Nome, Quantidade FROM Produtos WHERE Id = @pid;";
                checkCmd.Parameters.AddWithValue("@pid", item.ProdutoId);
                using var r = checkCmd.ExecuteReader();
                if (!r.Read())
                    throw new InvalidOperationException($"Produto '{item.ProdutoNome}' não encontrado.");
                nomeProd = r.GetString(0);
                saldoAtual = r.GetInt32(1);
            }

            if (saldoAtual < item.Quantidade)
                throw new InvalidOperationException($"Estoque insuficiente para '{nomeProd}'. Disponível: {saldoAtual}, Solicitado: {item.Quantidade}.");

            int novoSaldo = saldoAtual - item.Quantidade;
            using (var updCmd = conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandText = "UPDATE Produtos SET Quantidade = @novo WHERE Id = @pid;";
                updCmd.Parameters.AddWithValue("@novo", novoSaldo);
                updCmd.Parameters.AddWithValue("@pid", item.ProdutoId);
                updCmd.ExecuteNonQuery();
            }

            InserirMovimentacao(
                conn, tx, item.ProdutoId, nomeProd, "VENDA", item.Quantidade,
                saldoAtual, novoSaldo, "Venda PDV", venda.UsuarioId, venda.UsuarioNome);
        }

        using (var vCmd = conn.CreateCommand())
        {
            vCmd.Transaction = tx;
            vCmd.CommandText = @"
                INSERT INTO Vendas (ClienteId, ClienteNome, UsuarioId, UsuarioNome, DataHora, FormaPagamento, Subtotal, Desconto, Total, ValorRecebido, Troco)
                VALUES (@cid, @cnome, @uid, @unome, @dt, @fp, @sub, @desc, @tot, @rec, @troco);
                SELECT last_insert_rowid();";
            vCmd.Parameters.AddWithValue("@cid", (object?)venda.ClienteId ?? DBNull.Value);
            vCmd.Parameters.AddWithValue("@cnome", string.IsNullOrWhiteSpace(venda.ClienteNome) ? "Consumidor Final" : venda.ClienteNome);
            vCmd.Parameters.AddWithValue("@uid", (object?)venda.UsuarioId ?? DBNull.Value);
            vCmd.Parameters.AddWithValue("@unome", venda.UsuarioNome);
            vCmd.Parameters.AddWithValue("@dt", venda.DataHora.ToString("o"));
            vCmd.Parameters.AddWithValue("@fp", venda.FormaPagamento);
            vCmd.Parameters.AddWithValue("@sub", (double)venda.Subtotal);
            vCmd.Parameters.AddWithValue("@desc", (double)venda.Desconto);
            vCmd.Parameters.AddWithValue("@tot", (double)venda.Total);
            vCmd.Parameters.AddWithValue("@rec", (double)venda.ValorRecebido);
            vCmd.Parameters.AddWithValue("@troco", (double)venda.Troco);
            venda.Id = Convert.ToInt32(vCmd.ExecuteScalar());
        }

        foreach (var item in venda.Itens)
        {
            using var iCmd = conn.CreateCommand();
            iCmd.Transaction = tx;
            iCmd.CommandText = @"
                INSERT INTO VendaItens (VendaId, ProdutoId, ProdutoNome, CodigoBarras, Quantidade, PrecoUnitario, Subtotal)
                VALUES (@vid, @pid, @pnome, @cod, @qtd, @pu, @sub);";
            iCmd.Parameters.AddWithValue("@vid", venda.Id);
            iCmd.Parameters.AddWithValue("@pid", item.ProdutoId);
            iCmd.Parameters.AddWithValue("@pnome", item.ProdutoNome);
            iCmd.Parameters.AddWithValue("@cod", (object?)item.CodigoBarras ?? DBNull.Value);
            iCmd.Parameters.AddWithValue("@qtd", item.Quantidade);
            iCmd.Parameters.AddWithValue("@pu", (double)item.PrecoUnitario);
            iCmd.Parameters.AddWithValue("@sub", (double)item.Subtotal);
            iCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return venda.Id;
    }

    public static List<Venda> ListarVendas(DateTime? inicio = null, DateTime? fim = null)
    {
        var lista = new List<Venda>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT Id, ClienteId, ClienteNome, UsuarioId, UsuarioNome, DataHora, FormaPagamento, Subtotal, Desconto, Total, ValorRecebido, Troco FROM Vendas WHERE 1=1";
        if (inicio.HasValue)
        {
            sql += " AND DataHora >= @ini";
            cmd.Parameters.AddWithValue("@ini", inicio.Value.Date.ToString("o"));
        }
        if (fim.HasValue)
        {
            sql += " AND DataHora <= @fim";
            cmd.Parameters.AddWithValue("@fim", fim.Value.Date.AddDays(1).AddTicks(-1).ToString("o"));
        }
        sql += " ORDER BY Id DESC;";
        cmd.CommandText = sql;

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new Venda
            {
                Id = r.GetInt32(0),
                ClienteId = r.IsDBNull(1) ? null : r.GetInt32(1),
                ClienteNome = r.GetString(2),
                UsuarioId = r.IsDBNull(3) ? null : r.GetInt32(3),
                UsuarioNome = r.GetString(4),
                DataHora = DateTime.Parse(r.GetString(5), CultureInfo.InvariantCulture),
                FormaPagamento = r.GetString(6),
                Subtotal = Convert.ToDecimal(r.GetDouble(7)),
                Desconto = Convert.ToDecimal(r.GetDouble(8)),
                Total = Convert.ToDecimal(r.GetDouble(9)),
                ValorRecebido = Convert.ToDecimal(r.GetDouble(10)),
                Troco = Convert.ToDecimal(r.GetDouble(11))
            });
        }
        return lista;
    }

    public static Venda? ObterVendaCompleta(int vendaId)
    {
        using var conn = OpenConnection();
        Venda? venda = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, ClienteId, ClienteNome, UsuarioId, UsuarioNome, DataHora, FormaPagamento, Subtotal, Desconto, Total, ValorRecebido, Troco FROM Vendas WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", vendaId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                venda = new Venda
                {
                    Id = r.GetInt32(0),
                    ClienteId = r.IsDBNull(1) ? null : r.GetInt32(1),
                    ClienteNome = r.GetString(2),
                    UsuarioId = r.IsDBNull(3) ? null : r.GetInt32(3),
                    UsuarioNome = r.GetString(4),
                    DataHora = DateTime.Parse(r.GetString(5), CultureInfo.InvariantCulture),
                    FormaPagamento = r.GetString(6),
                    Subtotal = Convert.ToDecimal(r.GetDouble(7)),
                    Desconto = Convert.ToDecimal(r.GetDouble(8)),
                    Total = Convert.ToDecimal(r.GetDouble(9)),
                    ValorRecebido = Convert.ToDecimal(r.GetDouble(10)),
                    Troco = Convert.ToDecimal(r.GetDouble(11))
                };
            }
        }

        if (venda == null) return null;

        using (var iCmd = conn.CreateCommand())
        {
            iCmd.CommandText = "SELECT Id, VendaId, ProdutoId, ProdutoNome, CodigoBarras, Quantidade, PrecoUnitario FROM VendaItens WHERE VendaId = @id;";
            iCmd.Parameters.AddWithValue("@id", vendaId);
            using var r = iCmd.ExecuteReader();
            while (r.Read())
            {
                venda.Itens.Add(new VendaItem
                {
                    Id = r.GetInt32(0),
                    VendaId = r.GetInt32(1),
                    ProdutoId = r.GetInt32(2),
                    ProdutoNome = r.GetString(3),
                    CodigoBarras = r.IsDBNull(4) ? null : r.GetString(4),
                    Quantidade = r.GetInt32(5),
                    PrecoUnitario = Convert.ToDecimal(r.GetDouble(6))
                });
            }
        }

        return venda;
    }

    // ==================== RELATÓRIOS ====================
    public static List<MovimentacaoEstoque> ListarMovimentacoes(DateTime? inicio = null, DateTime? fim = null, string? tipo = null)
    {
        var lista = new List<MovimentacaoEstoque>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT Id, ProdutoId, ProdutoNome, Tipo, Quantidade, SaldoAnterior, SaldoNovo, Motivo, UsuarioId, UsuarioNome, DataHora FROM MovimentacoesEstoque WHERE 1=1";

        if (inicio.HasValue)
        {
            sql += " AND DataHora >= @ini";
            cmd.Parameters.AddWithValue("@ini", inicio.Value.Date.ToString("o"));
        }
        if (fim.HasValue)
        {
            sql += " AND DataHora <= @fim";
            cmd.Parameters.AddWithValue("@fim", fim.Value.Date.AddDays(1).AddTicks(-1).ToString("o"));
        }
        if (!string.IsNullOrWhiteSpace(tipo) && tipo != "TODOS")
        {
            sql += " AND Tipo = @tipo";
            cmd.Parameters.AddWithValue("@tipo", tipo);
        }

        sql += " ORDER BY Id DESC;";
        cmd.CommandText = sql;

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new MovimentacaoEstoque
            {
                Id = r.GetInt32(0),
                ProdutoId = r.GetInt32(1),
                ProdutoNome = r.GetString(2),
                Tipo = r.GetString(3),
                Quantidade = r.GetInt32(4),
                SaldoAnterior = r.GetInt32(5),
                SaldoNovo = r.GetInt32(6),
                Motivo = r.GetString(7),
                UsuarioId = r.IsDBNull(8) ? null : r.GetInt32(8),
                UsuarioNome = r.GetString(9),
                DataHora = DateTime.Parse(r.GetString(10), CultureInfo.InvariantCulture)
            });
        }
        return lista;
    }
}
