using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SapaioDigital.App.Helpers;

/// <summary>
/// Validação de entrada em tempo real (digitação e Ctrl+V) para campos WPF.
/// </summary>
public static class InputValidator
{
    private static readonly Regex RegexInteiro = new(@"^\d+$");
    private static readonly Regex RegexDecimal = new(@"^[0-9]+([,\.][0-9]{0,2})?$");
    private static readonly Regex RegexTelefone = new(@"^[0-9\(\)\-\+\s]+$");

    public static void AplicarSomenteInteiro(TextBox tb)
    {
        tb.PreviewTextInput += (_, e) => e.Handled = !RegexInteiro.IsMatch(e.Text);
        DataObject.AddPastingHandler(tb, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string txt = (string)e.DataObject.GetData(typeof(string))!;
                if (!RegexInteiro.IsMatch(txt)) e.CancelCommand();
            }
            else e.CancelCommand();
        });
    }

    public static void AplicarSomenteDecimal(TextBox tb)
    {
        tb.PreviewTextInput += (_, e) =>
        {
            string novo = SimularTexto(tb, e.Text);
            e.Handled = !RegexDecimal.IsMatch(novo);
        };
        DataObject.AddPastingHandler(tb, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string txt = (string)e.DataObject.GetData(typeof(string))!;
                if (!RegexDecimal.IsMatch(txt)) e.CancelCommand();
            }
            else e.CancelCommand();
        });
    }

    public static void AplicarSomenteTelefone(TextBox tb)
    {
        tb.PreviewTextInput += (_, e) => e.Handled = !RegexTelefone.IsMatch(e.Text);
        DataObject.AddPastingHandler(tb, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string txt = (string)e.DataObject.GetData(typeof(string))!;
                if (!RegexTelefone.IsMatch(txt)) e.CancelCommand();
            }
            else e.CancelCommand();
        });
    }

    public static bool TryParseDecimalPtBr(string texto, out decimal valor)
    {
        string norm = (texto ?? "").Trim().Replace('.', ',');
        return decimal.TryParse(norm, NumberStyles.Number, new CultureInfo("pt-BR"), out valor);
    }

    public static bool TelefoneValido(string texto)
    {
        int digitos = (texto ?? "").Count(char.IsDigit);
        return digitos >= 8;
    }

    private static string SimularTexto(TextBox tb, string entrada)
    {
        string atual = tb.Text ?? "";
        int start = tb.SelectionStart;
        int len = tb.SelectionLength;
        return atual.Remove(start, len).Insert(start, entrada);
    }
}
