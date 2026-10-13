using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Automation;

namespace PreenchedorBRMED
{
    class Resultado
    {
        public int Preenchidos;
        public List<string> NaoEncontrados = new List<string>();
        public List<string> SemValor = new List<string>();
        public string Erro;

        public string Resumo()
        {
            if (Erro != null) return Erro;
            var partes = new List<string> { Preenchidos == 1 ? "1 campo preenchido." : Preenchidos + " campos preenchidos." };
            if (NaoEncontrados.Count > 0) partes.Add("Não encontrei: " + string.Join(", ", NaoEncontrados) + ".");
            if (SemValor.Count > 0) partes.Add("Sem dado no formulário: " + string.Join(", ", SemValor) + ".");
            return string.Join(" ", partes);
        }

        public bool Problema { get { return Erro != null || NaoEncontrados.Count > 0; } }
    }

    /// <summary>Cola os dados nos campos de uma janela. Sempre chamado fora da thread da interface.</summary>
    static class Preenchedor
    {
        public static Resultado Colar(Perfil perfil, IntPtr janela)
        {
            var r = new Resultado();
            try
            {
                Teclado.EsperarSoltarModificadores();
                if (perfil.PorTeclado) ColarPorTeclado(perfil, r);
                else ColarPorAutomacao(perfil, janela, r);
            }
            catch (Exception erro)
            {
                r.Erro = "Não consegui preencher: " + erro.Message;
            }
            return r;
        }

        // ---------- Método "uia" ----------
        static void ColarPorAutomacao(Perfil perfil, IntPtr janela, Resultado r)
        {
            if (perfil.Campos == null || perfil.Campos.Count == 0)
            {
                r.Erro = "O perfil \"" + perfil.Nome + "\" ainda não tem campos. Aponte o mouse para um campo e use Ctrl+Alt+M.";
                return;
            }
            var raiz = AutomationElement.FromHandle(janela);
            foreach (var campo in perfil.Campos)
            {
                string rotulo = Dados.Rotulo(perfil.FormularioId, campo.Dado);
                string valor = Dados.Valor(perfil.FormularioId, campo.Dado);
                if (string.IsNullOrEmpty(valor)) { r.SemValor.Add(rotulo); continue; }

                var elemento = Localizar(raiz, campo);
                if (elemento == null || !Escrever(elemento, valor)) { r.NaoEncontrados.Add(rotulo); continue; }
                r.Preenchidos++;
            }
        }

        public static AutomationElement Localizar(AutomationElement raiz, CampoMapeado campo)
        {
            var tipo = Mapeador.TipoPorNome(campo.Tipo);
            var porTipo = new PropertyCondition(AutomationElement.ControlTypeProperty, tipo);

            if (!string.IsNullOrEmpty(campo.AutomationId))
                return raiz.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, campo.AutomationId));

            if (!string.IsNullOrEmpty(campo.NomeControle))
                return raiz.FindFirst(TreeScope.Descendants, new AndCondition(porTipo,
                    new PropertyCondition(AutomationElement.NameProperty, campo.NomeControle)));

            if (campo.Indice >= 0)
            {
                var todos = raiz.FindAll(TreeScope.Descendants, porTipo);
                return campo.Indice < todos.Count ? todos[campo.Indice] : null;
            }
            return null;
        }

        static bool Escrever(AutomationElement el, string valor)
        {
            if (!el.Current.IsEnabled) return false;

            if (el.Current.ControlType == ControlType.ComboBox && SelecionarNaLista(el, valor)) return true;

            object padrao;
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out padrao))
            {
                var vp = (ValuePattern)padrao;
                if (!vp.Current.IsReadOnly)
                {
                    try { el.SetFocus(); } catch (InvalidOperationException) { }
                    vp.SetValue(valor);
                    return true;
                }
            }

            // Plano B para campos sem ValuePattern: foca e digita por cima.
            try { el.SetFocus(); } catch (InvalidOperationException) { return false; }
            Thread.Sleep(50);
            Teclado.SelecionarTudo();
            Teclado.Digitar(valor);
            return true;
        }

        static bool SelecionarNaLista(AutomationElement combo, string valor)
        {
            object padrao;
            ExpandCollapsePattern expandir = null;
            if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out padrao))
            {
                expandir = (ExpandCollapsePattern)padrao;
                try { expandir.Expand(); Thread.Sleep(150); } catch (InvalidOperationException) { }
            }

            var item = combo.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.NameProperty, valor, PropertyConditionFlags.IgnoreCase)));

            bool ok = false;
            if (item != null && item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out padrao))
            {
                ((SelectionItemPattern)padrao).Select();
                ok = true;
            }
            if (expandir != null) { try { expandir.Collapse(); } catch (InvalidOperationException) { } }
            return ok;
        }

        // ---------- Método "teclado" ----------
        static void ColarPorTeclado(Perfil perfil, Resultado r)
        {
            if (perfil.Sequencia == null || perfil.Sequencia.Count == 0)
            {
                r.Erro = "O perfil \"" + perfil.Nome + "\" não tem sequência de digitação.";
                return;
            }
            foreach (var passo in perfil.Sequencia)
            {
                var p = (passo ?? "").Trim();
                switch (p.ToUpperInvariant())
                {
                    case "{TAB}": Teclado.Tecla(Teclado.VK_TAB); continue;
                    case "{ENTER}": Teclado.Tecla(Teclado.VK_RETURN); continue;
                    case "{ESPERA}": Thread.Sleep(400); continue;
                }
                if (p.StartsWith("=")) { Teclado.Digitar(p.Substring(1)); continue; } // texto fixo

                var valor = Dados.Valor(perfil.FormularioId, p);
                if (string.IsNullOrEmpty(valor)) { r.SemValor.Add(Dados.Rotulo(perfil.FormularioId, p)); continue; }
                Teclado.Digitar(valor);
                r.Preenchidos++;
            }
        }
    }
}
