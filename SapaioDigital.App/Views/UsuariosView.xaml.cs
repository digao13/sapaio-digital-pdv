using System;
using System.Windows;
using System.Windows.Controls;
using SapaioDigital.App.Data;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Views;

public partial class UsuariosView : UserControl
{
    private Usuario? _usuarioEditando;

    public UsuariosView()
    {
        InitializeComponent();
        CarregarUsuarios();
    }

    private void CarregarUsuarios() => GridUsuarios.ItemsSource = DatabaseService.ListarUsuarios();

    private void GridUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridUsuarios.SelectedItem is not Usuario u) return;
        _usuarioEditando = u;
        TxtTituloForm.Text = $"Editando Usuário #{u.Id}";
        TxtNome.Text = u.Nome;
        TxtLogin.Text = u.Login;
        TxtSenha.Clear();
        CmbPerfil.SelectedIndex = u.Perfil == "Administrador" ? 1 : 0;
        ChkAtivo.IsChecked = u.Ativo;
    }

    private void BtnLimpar_Click(object sender, RoutedEventArgs e) => Limpar();

    private void Limpar()
    {
        _usuarioEditando = null;
        GridUsuarios.SelectedItem = null;
        TxtTituloForm.Text = "Novo Usuário";
        TxtNome.Clear();
        TxtLogin.Clear();
        TxtSenha.Clear();
        CmbPerfil.SelectedIndex = 0;
        ChkAtivo.IsChecked = true;
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNome.Text) || string.IsNullOrWhiteSpace(TxtLogin.Text))
        {
            MessageBox.Show("Informe Nome e Login.", "Campos obrigatórios", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var u = _usuarioEditando ?? new Usuario();
            u.Nome = TxtNome.Text.Trim();
            u.Login = TxtLogin.Text.Trim();
            u.Perfil = (CmbPerfil.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Operador";
            u.Ativo = ChkAtivo.IsChecked == true;

            DatabaseService.SalvarUsuario(u, TxtSenha.Password);
            CarregarUsuarios();
            Limpar();
            MessageBox.Show("Usuário salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar usuário: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
