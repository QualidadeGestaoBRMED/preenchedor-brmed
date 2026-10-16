using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;

namespace PreenchedorBRMED
{
    class ResultadoCaptura
    {
        public Dictionary<string, string> Valores = new Dictionary<string, string>();
        public int Encontrados;
        public string Erro;
        /// <summary>true = veio da captura padrão do BRNET, não de campos mapeados.</summary>
        public bool Brnet;

        public string Resumo()
        {
            if (Erro != null) return Erro;
            if (Encontrados == 0) return "Não encontrei nesta tela os campos mapeados para captura.";
            if (Valores.Count == 0) return "Os campos mapeados estão vazios.";
            return (Valores.Count == 1 ? "1 campo capturado." : Valores.Count + " campos capturados.") + " Complete o formulário e salve.";
        }
    }

    /// <summary>Lê os campos mapeados para captura. Sempre chamado fora da thread da interface.</summary>
    static class Capturador
    {
        public static ResultadoCaptura Capturar(Perfil perfil, IntPtr janela)
        {
            var r = new ResultadoCaptura();
            try
            {
                Teclado.EsperarSoltarModificadores();
                var raiz = AutomationElement.FromHandle(janela);
                var inst = Instantaneo.Tirar(raiz);
                foreach (var campo in perfil.Campos.Where(c => c.Captura))
                {
                    var el = Preenchedor.Localizar(inst, raiz, campo);
                    // Campos de outras telas do mesmo programa (ex.: outro site no navegador) são ignorados em silêncio.
                    if (el == null) continue;
                    r.Encontrados++;
                    var valor = Ler(el);
                    if (!string.IsNullOrWhiteSpace(valor)) r.Valores[campo.Dado] = valor.Trim();
                }
            }
            catch (Exception erro)
            {
                r.Erro = "Não consegui capturar: " + erro.Message;
            }
            return r;
        }

        static string Ler(AutomationElement el)
        {
            object padrao;
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out padrao))
            {
                var v = ((ValuePattern)padrao).Current.Value;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            if (el.TryGetCurrentPattern(TextPattern.Pattern, out padrao))
            {
                var t = ((TextPattern)padrao).DocumentRange.GetText(2000);
                if (!string.IsNullOrEmpty(t)) return t;
            }
            if (el.TryGetCurrentPattern(SelectionPattern.Pattern, out padrao))
            {
                var itens = ((SelectionPattern)padrao).Current.GetSelection();
                if (itens.Length > 0) return itens[0].Current.Name;
            }
            return null;
        }
    }
}
