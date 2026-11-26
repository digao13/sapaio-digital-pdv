using System;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Helpers;

public static class SessaoAtual
{
    public static Usuario? UsuarioLogado { get; set; }
    public static LojaConfig? Loja { get; set; }

    public static event Action? LojaAtualizada;

    public static void NotificarLojaAtualizada() => LojaAtualizada?.Invoke();
}