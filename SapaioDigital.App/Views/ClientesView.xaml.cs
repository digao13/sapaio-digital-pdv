using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;

namespace SapaioDigital.App.Views;

public partial class ClientesView : UserControl
{
    private Cliente? _clienteEditando;
    private bool _atualizandoCampos;
    private string _bairroAtual = "";

    public ClientesView()
    {
        InitializeComponent();
        InputValidator.AplicarSomenteTelefone(TxtTelefone);
        CarregarClientes();
    }

    private void CarregarClientes() => GridClientes.ItemsSource = DatabaseService.ListarClientes(TxtBusca.Text);

    private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => CarregarClientes();

    private void GridClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridClientes.SelectedItem is not Cliente c) return;
        _clienteEditando = c;
        _atualizandoCampos = true;
        _bairroAtual = "";
        TxtTituloForm.Text = $"Editando Cliente #{c.Id}";
        TxtNome.Text = c.Nome;
        TxtTelefone.Text = c.TelefoneWhatsApp;
        TxtCep.Clear();
        TxtRua.Clear();
        TxtNumero.Clear();
        TxtCidade.Clear();
        TxtUf.Clear();
        TxtEndereco.Text = c.Endereco;
        TxtObs.Text = c.Observacoes;
        _atualizandoCampos = false;
    }

    private void BtnLimpar_Click(object sender, RoutedEventArgs e) => Limpar();

    private void Limpar()
    {
        _clienteEditando = null;
        _atualizandoCampos = true;
        _bairroAtual = "";
        GridClientes.SelectedItem = null;
        TxtTituloForm.Text = "Novo Cliente";
        TxtNome.Clear();
        TxtTelefone.Clear();
        TxtCep.Clear();
        TxtRua.Clear();
        TxtNumero.Clear();
        TxtCidade.Clear();
        TxtUf.Clear();
        TxtEndereco.Clear();
        TxtObs.Clear();
        _atualizandoCampos = false;
    }

    private async void TxtCep_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_atualizandoCampos) return;
        if (CepService.LimparCep(TxtCep.Text).Length == 8)
            await BuscarCepAsync(mostrarErro: false);
    }

    private async void BtnBuscarCep_Click(object sender, RoutedEventArgs e) =>
        await BuscarCepAsync(mostrarErro: true);

    private async Task BuscarCepAsync(bool mostrarErro)
    {
        BtnBuscarCep.IsEnabled = false;
        try
        {
            var res = await CepService.BuscarAsync(TxtCep.Text);
            if (!res.Sucesso)
            {
                if (mostrarErro)
                    MessageBox.Show(res.Erro, "CEP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _atualizandoCampos = true;
            _bairroAtual = res.Bairro;
            TxtRua.Text = res.Rua;
            TxtCidade.Text = res.Cidade;
            TxtUf.Text = res.Uf;
            _atualizandoCampos = false;

            AtualizarEnderecoMontado();
            TxtNumero.Focus();
        }
        finally
        {
            BtnBuscarCep.IsEnabled = true;
        }
    }

    private void CampoEndereco_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_atualizandoCampos) return;
        AtualizarEnderecoMontado();
    }

    private void AtualizarEnderecoMontado()
    {
        string montado = CepService.FormatarEndereco(
            TxtRua.Text, TxtNumero.Text, _bairroAtual, TxtCidade.Text, TxtUf.Text, TxtCep.Text);
        if (!string.IsNullOrWhiteSpace(montado))
            TxtEndereco.Text = montado;
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNome.Text) || string.IsNullOrWhiteSpace(TxtEndereco.Text))
        {
            MessageBox.Show("Preencha Nome e Endereço.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!InputValidator.TelefoneValido(TxtTelefone.Text))
        {
            MessageBox.Show("Informe um Telefone / WhatsApp válido (mínimo 8 dígitos).", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var c = _clienteEditando ?? new Cliente();
            c.Nome = TxtNome.Text.Trim();
            c.TelefoneWhatsApp = TxtTelefone.Text.Trim();
            c.Endereco = TxtEndereco.Text.Trim();
            c.Observacoes = TxtObs.Text.Trim();

            DatabaseService.SalvarCliente(c);
            CarregarClientes();
            Limpar();
            MessageBox.Show("Cliente salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar cliente: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}