using System;
using System.Diagnostics;
using System.Linq;
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
        /// <param name="paraCaptura">true = o campo será lido (Ctrl+Alt+K), o que a posição na tela não permite.</param>
        public static Captura Capturar(int x, int y, bool paraCaptura = false)
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

                string id = campo.Current.AutomationId;
                string nome = campo.Current.Name;
                string classe = campo.Current.ClassName;

                // Programas em Qt não têm janelas nativas nos campos: a automação é lenta e sem identificadores. Eles (e as
                // opções, como Masculino/Feminino) são mapeados pela posição dentro da tela e preenchidos com clique e digitação.
                bool semJanelasNativas = (janela.Current.ClassName ?? "").StartsWith("Qt", StringComparison.OrdinalIgnoreCase);
                if (!paraCaptura && (semJanelasNativas || tipo == ControlType.RadioButton) && MarcarPosicao(janela, campo, x, y, mapeado))
                {
                    mapeado.Descricao = DescreverPosicao(campo, mapeado);
                    c.Campo = mapeado;
                    return c;
                }

                // Uma única leitura da janela (com cache) serve para todas as verificações abaixo.
                var inst = Instantaneo.Tirar(janela);
                var item = inst.De(campo);
                if (item == null)
                {
                    // Tipo que o instantâneo não lê (ex.: controle personalizado): cai nas buscas diretas.
                    if (!MapearDireto(janela, campo, tipo, mapeado, id, nome, classe)) { c.Erro = SemIdentificacao; return c; }
                }
                else
                {
                    var doTipo = inst.DoTipo(tipo);
                    // Um AutomationId que é só o número da janela do campo (Delphi) muda a cada abertura do formulário: não serve.
                    if (!string.IsNullOrWhiteSpace(id) && !IdVolatil(campo, id) && inst.Campos.Count(i => i.Id == id) == 1)
                        mapeado.AutomationId = id;
                    else if (!string.IsNullOrWhiteSpace(nome) && doTipo.Count(i => i.Nome == nome) == 1)
                        mapeado.NomeControle = nome;
                    else
                    {
                        // Sem id nem nome (Qt, Delphi): o texto do rótulo vizinho, desde que leve de volta a este mesmo campo.
                        string posicao;
                        var rotulo = inst.RotuloDe(item, out posicao);
                        if (rotulo != null && ReferenceEquals(inst.PorRotulo(doTipo, rotulo, posicao), item))
                        {
                            mapeado.Rotulo = rotulo;
                            mapeado.PosRotulo = posicao;
                        }
                        else if (ClasseEstavel(classe) && inst.Campos.Count(i => i.Classe == classe) > 0)
                        {
                            // A classe e a posição entre os controles dessa classe (ex.: o 3º TEdit).
                            mapeado.ClassName = classe;
                            mapeado.IndiceClasse = inst.Campos.Where(i => i.Classe == classe).ToList().IndexOf(item);
                        }
                        else
                        {
                            // Último recurso: posição entre os campos do mesmo tipo.
                            mapeado.Indice = doTipo.IndexOf(item);
                        }
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

        /// <summary>Guarda onde o campo está dentro da tela: a fração da área interna da janela (vale em qualquer posição ou escala).</summary>
        static bool MarcarPosicao(AutomationElement janela, AutomationElement campo, int x, int y, CampoMapeado mapeado)
        {
            var hwnd = new IntPtr(janela.Current.NativeWindowHandle);
            int ox, oy, largura, altura;
            if (hwnd == IntPtr.Zero || !Nativo.AreaCliente(hwnd, out ox, out oy, out largura, out altura)) return false;

            // O centro do controle vale mais que o ponto do mouse; sem área conhecida, usa o mouse.
            double cx = x, cy = y;
            var area = campo.Current.BoundingRectangle;
            if (!area.IsEmpty && area.Width > 1 && area.Height > 1) { cx = area.X + area.Width / 2; cy = area.Y + area.Height / 2; }

            double fx = (cx - ox) / largura, fy = (cy - oy) / altura;
            if (fx < 0 || fx > 1 || fy < 0 || fy > 1) return false;
            mapeado.Posicao = true;
            mapeado.PosX = Math.Round(fx, 4);
            mapeado.PosY = Math.Round(fy, 4);
            mapeado.JanelaTitulo = janela.Current.Name ?? "";
            return true;
        }

        static string DescreverPosicao(AutomationElement campo, CampoMapeado m)
        {
            try
            {
                var rotulo = campo.GetCurrentPropertyValue(AutomationElement.LabeledByProperty) as AutomationElement;
                if (rotulo != null && !string.IsNullOrWhiteSpace(rotulo.Current.Name)) return rotulo.Current.Name.Trim();
            }
            catch (ElementNotAvailableException) { }
            if (!string.IsNullOrWhiteSpace(campo.Current.Name)) return campo.Current.Name.Trim();
            return "Campo em " + (int)Math.Round(m.PosX * 100) + "% · " + (int)Math.Round(m.PosY * 100) + "%";
        }

        const string SemIdentificacao = "Este campo não tem identificação que a automação consiga reencontrar. Para este sistema, use um perfil por teclado.";

        /// <summary>Caminho antigo (busca direta na árvore), para controles que o instantâneo não lê.</summary>
        static bool MapearDireto(AutomationElement janela, AutomationElement campo, ControlType tipo, CampoMapeado mapeado, string id, string nome, string classe)
        {
            var porTipo = new PropertyCondition(AutomationElement.ControlTypeProperty, tipo);
            if (!string.IsNullOrWhiteSpace(id) && !IdVolatil(campo, id) && Unico(janela, new PropertyCondition(AutomationElement.AutomationIdProperty, id)))
                mapeado.AutomationId = id;
            else if (!string.IsNullOrWhiteSpace(nome) && Unico(janela, new AndCondition(porTipo, new PropertyCondition(AutomationElement.NameProperty, nome))))
                mapeado.NomeControle = nome;
            else if (ClasseEstavel(classe) && PosicaoNaClasse(janela, campo, classe) >= 0)
            {
                mapeado.ClassName = classe;
                mapeado.IndiceClasse = PosicaoNaClasse(janela, campo, classe);
            }
            else
            {
                var todos = janela.FindAll(TreeScope.Descendants, porTipo);
                for (int i = 0; i < todos.Count; i++)
                    if (Automation.Compare(todos[i], campo)) { mapeado.Indice = i; break; }
                if (mapeado.Indice < 0) return false;
            }
            return true;
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
                if (t == ControlType.Edit || t == ControlType.ComboBox || t == ControlType.Document || t == ControlType.Spinner || t == ControlType.RadioButton)
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
            if (!string.IsNullOrWhiteSpace(m.Rotulo)) return m.Rotulo;
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
                case "RadioButton": return ControlType.RadioButton;
                case "CheckBox": return ControlType.CheckBox;
                default: return ControlType.Edit;
            }
        }
    }
}
