using System;
using System.Diagnostics;
using System.Windows.Automation;

namespace PreenchedorBRMED
{
    class Captura
    {
        public string Erro;
        public string Processo;
        public string TituloJanela;
        public CampoMapeado Campo;
    }

    /// <summary>Descobre qual campo está sob o mouse e como reencontrá-lo depois.</summary>
    static class Mapeador
    {
        public static Captura Capturar(int x, int y)
        {
            var c = new Captura();
            try
            {
                var el = AutomationElement.FromPoint(new System.Windows.Point(x, y));
                var campo = SubirAteCampo(el);
                if (campo == null)
                {
                    c.Erro = "Não encontrei um campo de texto sob o mouse. Aponte para dentro da caixa onde se digita.";
                    return c;
                }

                var janela = JanelaRaiz(campo);
                c.TituloJanela = janela.Current.Name;
                c.Processo = Process.GetProcessById(campo.Current.ProcessId).ProcessName;

                var tipo = campo.Current.ControlType;
                var mapeado = new CampoMapeado { Tipo = NomeDoTipo(tipo), Indice = -1 };
                var porTipo = new PropertyCondition(AutomationElement.ControlTypeProperty, tipo);

                string id = campo.Current.AutomationId;
                string nome = campo.Current.Name;
                string classe = campo.Current.ClassName;

                // Um AutomationId que é só o número da janela do campo (Delphi) muda a cada abertura do formulário: não serve.
                if (!string.IsNullOrWhiteSpace(id) && !IdVolatil(campo, id) && Unico(janela, new PropertyCondition(AutomationElement.AutomationIdProperty, id)))
                    mapeado.AutomationId = id;
                else if (!string.IsNullOrWhiteSpace(nome) && Unico(janela, new AndCondition(porTipo, new PropertyCondition(AutomationElement.NameProperty, nome))))
                    mapeado.NomeControle = nome;
                else if (ClasseEstavel(classe) && PosicaoNaClasse(janela, campo, classe) >= 0)
                {
                    // Sem identificador estável: guarda a classe e a posição entre os controles dessa classe (ex.: o 3º TEdit).
                    mapeado.ClassName = classe;
                    mapeado.IndiceClasse = PosicaoNaClasse(janela, campo, classe);
                }
                else
                {
                    // Último recurso: posição entre os campos do mesmo tipo.
                    var todos = janela.FindAll(TreeScope.Descendants, porTipo);
                    for (int i = 0; i < todos.Count; i++)
                        if (Automation.Compare(todos[i], campo)) { mapeado.Indice = i; break; }
                    if (mapeado.Indice < 0)
                    {
                        c.Erro = "Este campo não tem identificação que a automação consiga reencontrar. Para este sistema, use um perfil por teclado.";
                        return c;
                    }
                }

                mapeado.Descricao = Descrever(campo, mapeado);
                c.Campo = mapeado;
            }
            catch (Exception erro)
            {
                c.Erro = "Não consegui ler este campo: " + erro.Message;
            }
            return c;
        }

        /// <summary>Número de janela (HWND) vira AutomationId em programas Delphi: passa de 65535 ou é o próprio handle do campo.</summary>
        static bool IdVolatil(AutomationElement campo, string id)
        {
            long n;
            if (!long.TryParse(id, out n)) return false;
            return n > 65535 || n == campo.Current.NativeWindowHandle;
        }

        /// <summary>Classes como TEdit ou Edit são fixas; nomes com sufixo gerado (WindowsForms10.EDIT.app.0.xxx) mudam a cada execução.</summary>
        static bool ClasseEstavel(string classe)
        {
            return !string.IsNullOrWhiteSpace(classe) && classe.Length < 40 && System.Text.RegularExpressions.Regex.IsMatch(classe, @"^[A-Za-z_][A-Za-z0-9_]*$");
        }

        static int PosicaoNaClasse(AutomationElement janela, AutomationElement campo, string classe)
        {
            var todos = janela.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, classe));
            for (int i = 0; i < todos.Count; i++)
                if (Automation.Compare(todos[i], campo)) return i;
            return -1;
        }

        static AutomationElement SubirAteCampo(AutomationElement el)
        {
            var walker = TreeWalker.ControlViewWalker;
            for (int nivel = 0; el != null && nivel < 3; nivel++)
            {
                var t = el.Current.ControlType;
                if (t == ControlType.Edit || t == ControlType.ComboBox || t == ControlType.Document || t == ControlType.Spinner)
                    return el;
                object p;
                if (t == ControlType.Custom && el.TryGetCurrentPattern(ValuePattern.Pattern, out p)) return el;
                el = walker.GetParent(el);
            }
            return null;
        }

        static AutomationElement JanelaRaiz(AutomationElement el)
        {
            var walker = TreeWalker.ControlViewWalker;
            var atual = el;
            while (true)
            {
                var pai = walker.GetParent(atual);
                if (pai == null || pai == AutomationElement.RootElement) return atual;
                atual = pai;
            }
        }

        static bool Unico(AutomationElement janela, Condition condicao)
        {
            return janela.FindAll(TreeScope.Descendants, condicao).Count == 1;
        }

        static string Descrever(AutomationElement campo, CampoMapeado m)
        {
            try
            {
                var rotulo = campo.GetCurrentPropertyValue(AutomationElement.LabeledByProperty) as AutomationElement;
                if (rotulo != null && !string.IsNullOrWhiteSpace(rotulo.Current.Name)) return rotulo.Current.Name.Trim();
            }
            catch (ElementNotAvailableException) { }
            if (!string.IsNullOrWhiteSpace(campo.Current.Name)) return campo.Current.Name.Trim();
            if (!string.IsNullOrWhiteSpace(campo.Current.AutomationId)) return campo.Current.AutomationId;
            return m.Tipo + " nº " + (m.Indice + 1);
        }

        public static string NomeDoTipo(ControlType tipo)
        {
            // "ControlType.Edit" -> "Edit"
            var nome = tipo.ProgrammaticName ?? "";
            int ponto = nome.LastIndexOf('.');
            return ponto >= 0 ? nome.Substring(ponto + 1) : nome;
        }

        public static ControlType TipoPorNome(string nome)
        {
            switch (nome)
            {
                case "ComboBox": return ControlType.ComboBox;
                case "Document": return ControlType.Document;
                case "Spinner": return ControlType.Spinner;
                case "Custom": return ControlType.Custom;
                default: return ControlType.Edit;
            }
        }
    }
}
