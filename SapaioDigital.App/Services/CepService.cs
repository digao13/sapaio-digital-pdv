using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SapaioDigital.App.Services;

public record CepResultado(bool Sucesso, string Rua, string Bairro, string Cidade, string Uf, string Erro);

public static class CepService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static string LimparCep(string? cep) =>
        new((cep ?? "").Where(char.IsDigit).ToArray());

    public static async Task<CepResultado> BuscarAsync(string? cep)
    {
        string limpo = LimparCep(cep);
        if (limpo.Length != 8)
            return new CepResultado(false, "", "", "", "", "Informe um CEP válido com 8 dígitos.");

        try
        {
            using var resp = await Http.GetAsync($"https://viacep.com.br/ws/{limpo}/json/");
            if (!resp.IsSuccessStatusCode)
                return new CepResultado(false, "", "", "", "", $"Erro ao consultar CEP ({(int)resp.StatusCode}).");

            string json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<ViaCepResponse>(json);
            if (data == null || data.Erro)
                return new CepResultado(false, "", "", "", "", "CEP não encontrado.");

            return new CepResultado(
                true,
                (data.Logradouro ?? "").Trim(),
                (data.Bairro ?? "").Trim(),
                (data.Localidade ?? "").Trim(),
                (data.Uf ?? "").Trim().ToUpperInvariant(),
                "");
        }
        catch (Exception ex)
        {
            return new CepResultado(false, "", "", "", "", $"Falha de rede ao consultar CEP: {ex.Message}");
        }
    }

    public static string FormatarEndereco(string? rua, string? numero, string? bairro, string? cidade, string? uf, string? cep)
    {
        string r = (rua ?? "").Trim();
        string n = (numero ?? "").Trim();
        string b = (bairro ?? "").Trim();
        string c = (cidade ?? "").Trim();
        string u = (uf ?? "").Trim().ToUpperInvariant();
        string cepLimpo = LimparCep(cep);

        string linha1 = string.IsNullOrEmpty(r) ? "" : (string.IsNullOrEmpty(n) ? r : $"{r}, {n}");
        string cidUf = string.IsNullOrEmpty(c) ? u : (string.IsNullOrEmpty(u) ? c : $"{c}/{u}");

        var partes = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(linha1)) partes.Add(linha1);
        if (!string.IsNullOrEmpty(b)) partes.Add(b);
        if (!string.IsNullOrEmpty(cidUf)) partes.Add(cidUf);

        string texto = string.Join(" — ", partes);
        if (cepLimpo.Length == 8)
        {
            string cepFmt = $"{cepLimpo[..5]}-{cepLimpo[5..]}";
            texto = string.IsNullOrEmpty(texto) ? $"CEP {cepFmt}" : $"{texto} (CEP {cepFmt})";
        }
        return texto;
    }

    private sealed class ViaCepResponse
    {
        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("localidade")]
        public string? Localidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("erro")]
        public bool Erro { get; set; }
    }
}