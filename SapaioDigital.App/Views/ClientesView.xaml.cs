using System;
using System.Windows;
using System.Windows.Controls;
using SapaioDigital.App.Data;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Views;

public partial class ClientesView : UserControl
{
    private Cliente? _clienteEditando;

    public ClientesView()
    {
        InitializeComponent();
        CarregarClientes();
    }

    private void CarregarClientes() => GridClientes.ItemsSource = DatabaseService.ListarClientes(TxtBusca.Text);

    private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => CarregarClientes();

    private void GridClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridClientes.SelectedItem is not Cliente c) return;
        _clienteEditando = c;
        TxtTituloForm.Text = $"Editando Cliente #{c.Id}";
        TxtNome.Text = c.Nome;
        TxtTelefone.Text = c.TelefoneWhatsApp;
        TxtEndereco.Text = c.Endereco;
        TxtObs.Text = c.Observacoes;
    }

    private void BtnLimpar_Click(object sender, RoutedEventArgs e) => Limpar();

    private void Limpar()
    {
        _clienteEditando = null;
        GridClientes.SelectedItem = null;
        TxtTituloForm.Text = "Novo Cliente";
        TxtNome.Clear();
        TxtTelefone.Clear();
        TxtEndereco.Clear();
        TxtObs.Clear();
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNome.Text) || string.IsNullOrWhiteSpace(TxtTelefone.Text) || string.IsNullOrWhiteSpace(TxtEndereco.Text))
        {
            MessageBox.Show("Preencha Nome, Telefone/WhatsApp e Endereço.", "Campos obrigatórios", MessageBoxButton.OK, MessageBoxImage.Warning);
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
