using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SapaioDigital.App.Helpers;

namespace SapaioDigital.App.Services;

public class CloudOAuthAccount
{
    public string Provedor { get; set; } = ""; // GoogleDrive | OneDrive | Dropbox
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = ""; // Opcional (Google Desktop às vezes exige)
    public string RefreshToken { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public DateTime ExpiraEmUtc { get; set; }
    public bool Conectado => !string.IsNullOrWhiteSpace(RefreshToken) || !string.IsNullOrWhiteSpace(AccessToken);
}

/// <summary>
/// Autenticação OAuth 2.0 PKCE no navegador (sem servidor externo) com armazenamento
/// protegido por DPAPI do Windows e upload direto para Google Drive, OneDrive ou Dropbox.
/// </summary>
public static class CloudOAuthService
{
    private const int CallbackPort = 54821;
    private static readonly string RedirectUri = $"http://127.0.0.1:{CallbackPort}/callback";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    private static string TokenFilePath => Path.Combine(AppPaths.DataRoot, "cloud_oauth.dat");

    public static CloudOAuthAccount? CarregarConta()
    {
        try
        {
            if (!File.Exists(TokenFilePath)) return null;
            byte[] encrypted = File.ReadAllBytes(TokenFilePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<CloudOAuthAccount>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void SalvarConta(CloudOAuthAccount conta)
    {
        AppPaths.EnsureDirectories();
        string json = JsonSerializer.Serialize(conta);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(TokenFilePath, encrypted);
    }

    public static void DesconectarConta()
    {
        if (File.Exists(TokenFilePath))
            File.Delete(TokenFilePath);
    }

    public static async Task<CloudOAuthAccount> ConectarViaNavegadorAsync(string provedor, string clientId, string clientSecret = "")
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Informe o Client ID do aplicativo para conectar a conta.");

        string codeVerifier = GerarCodeVerifier();
        string codeChallenge = GerarCodeChallenge(codeVerifier);
        string state = Guid.NewGuid().ToString("N");

        (string authUrl, string tokenUrl, string scope) = provedor switch
        {
            "GoogleDrive" => (
                "https://accounts.google.com/o/oauth2/v2/auth",
                "https://oauth2.googleapis.com/token",
                "https://www.googleapis.com/auth/drive.file"
            ),
            "OneDrive" => (
                "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                "Files.ReadWrite offline_access"
            ),
            "Dropbox" => (
                "https://www.dropbox.com/oauth2/authorize",
                "https://api.dropboxapi.com/oauth2/token",
                "files.content.write files.content.read"
            ),
            _ => throw new ArgumentException("Provedor inválido.")
        };

        var query = new StringBuilder();
        query.Append($"?client_id={Uri.EscapeDataString(clientId.Trim())}");
        query.Append($"&redirect_uri={Uri.EscapeDataString(RedirectUri)}");
        query.Append("&response_type=code");
        query.Append($"&scope={Uri.EscapeDataString(scope)}");
        query.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
        query.Append("&code_challenge_method=S256");
        query.Append($"&state={state}");

        if (provedor == "GoogleDrive")
            query.Append("&access_type=offline&prompt=consent");
        else if (provedor == "Dropbox")
            query.Append("&token_access_type=offline");

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{CallbackPort}/");
        listener.Start();

        Process.Start(new ProcessStartInfo(authUrl + query) { UseShellExecute = true });

        var context = await listener.GetContextAsync();
        string? code = context.Request.QueryString["code"];
        string? recvState = context.Request.QueryString["state"];
        string? error = context.Request.QueryString["error"];

        string htmlResposta = string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(code)
            ? "<html><body style='font-family:Segoe UI;text-align:center;padding:40px;'><h2>Conta conectada ao Sampaio Digital!</h2><p>Você já pode fechar esta aba e voltar ao sistema.</p></body></html>"
            : $"<html><body style='font-family:Segoe UI;text-align:center;padding:40px;'><h2>Falha na autorização</h2><p>{WebUtility.HtmlEncode(error ?? "Código não recebido")}</p></body></html>";

        byte[] buffer = Encoding.UTF8.GetBytes(htmlResposta);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.OutputStream.Close();
        listener.Stop();

        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException($"Autorização cancelada ou recusada: {error ?? "sem código"}");
        if (recvState != state)
            throw new InvalidOperationException("Estado OAuth inválido.");

        // Troca code por token
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId.Trim(),
            ["code_verifier"] = codeVerifier
        };
        if (!string.IsNullOrWhiteSpace(clientSecret))
            form["client_secret"] = clientSecret.Trim();

        using var resp = await Http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        string json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro ao obter token ({resp.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string accessToken = root.GetProperty("access_token").GetString() ?? "";
        string refreshToken = root.TryGetProperty("refresh_token", out var rt) ? (rt.GetString() ?? "") : "";
        int expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        var conta = new CloudOAuthAccount
        {
            Provedor = provedor,
            ClientId = clientId.Trim(),
            ClientSecret = clientSecret.Trim(),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiraEmUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60))
        };

        SalvarConta(conta);
        return conta;
    }

    public static async Task EnviarArquivoParaNuvemAsync(string filePath)
    {
        var conta = CarregarConta();
        if (conta == null || !conta.Conectado) return;

        await GarantirTokenValidoAsync(conta);
        string fileName = Path.GetFileName(filePath);
        byte[] bytes = await File.ReadAllBytesAsync(filePath);

        switch (conta.Provedor)
        {
            case "Dropbox":
                await UploadDropboxAsync(conta.AccessToken, fileName, bytes);
                break;
            case "OneDrive":
                await UploadOneDriveAsync(conta.AccessToken, fileName, bytes);
                break;
            case "GoogleDrive":
                await UploadGoogleDriveAsync(conta.AccessToken, fileName, bytes);
                break;
        }
    }

    private static async Task GarantirTokenValidoAsync(CloudOAuthAccount conta)
    {
        if (DateTime.UtcNow < conta.ExpiraEmUtc && !string.IsNullOrWhiteSpace(conta.AccessToken))
            return;
        if (string.IsNullOrWhiteSpace(conta.RefreshToken))
            throw new InvalidOperationException("Sessão da nuvem expirada. Conecte novamente sua conta.");

        string tokenUrl = conta.Provedor switch
        {
            "GoogleDrive" => "https://oauth2.googleapis.com/token",
            "OneDrive" => "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            "Dropbox" => "https://api.dropboxapi.com/oauth2/token",
            _ => throw new InvalidOperationException("Provedor inválido.")
        };

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = conta.RefreshToken,
            ["client_id"] = conta.ClientId
        };
        if (!string.IsNullOrWhiteSpace(conta.ClientSecret))
            form["client_secret"] = conta.ClientSecret;

        using var resp = await Http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        string json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao renovar acesso na nuvem: {json}");

        using var doc = JsonDocument.Parse(json);
        conta.AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        conta.ExpiraEmUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        SalvarConta(conta);
    }

    private static async Task UploadDropboxAsync(string token, string fileName, byte[] bytes)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://content.dropboxapi.com/2/files/upload");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new
        {
            path = $"/SapaioDigital_Backups/{fileName}",
            mode = "overwrite",
            autorename = false,
            mute = true
        }));
        req.Content = new ByteArrayContent(bytes);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task UploadOneDriveAsync(string token, string fileName, byte[] bytes)
    {
        string url = $"https://graph.microsoft.com/v1.0/me/drive/root:/SapaioDigital_Backups/{Uri.EscapeDataString(fileName)}:/content";
        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new ByteArrayContent(bytes);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task UploadGoogleDriveAsync(string token, string fileName, byte[] bytes)
    {
        string boundary = "sapaio_" + Guid.NewGuid().ToString("N");
        string metadataJson = JsonSerializer.Serialize(new { name = fileName });

        using var multipart = new MultipartContent("related", boundary);
        multipart.Add(new StringContent(metadataJson, Encoding.UTF8, "application/json"));
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-sqlite3");
        multipart.Add(fileContent);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = multipart;
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    private static string GerarCodeVerifier()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GerarCodeChallenge(string verifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
