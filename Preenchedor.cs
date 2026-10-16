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
        public List<string> Falhas = new List<string>();
        /// <summary>Títulos das telas a que pertencem campos mapeados por posição que foram ignorados porque outra tela está aberta.</summary>
        public List<string> OutrasTelas = new List<string>();
        public string Erro;

        public string Resumo()
        {
            if (Erro != null) return Erro;
            var partes = new List<string> { Preenchidos == 1 ? "1 campo preenchido." : Preenchidos + " campos preenchidos." };
            if (NaoEncontrados.Count > 0) partes.Add("Não encontrei: " + string.Join(", ", NaoEncontrados) + ".");
            if (Falhas.Count > 0) partes.Add("O programa recusou: " + string.Join("; ", Falhas) + ".");
            if (SemValor.Count > 0) partes.Add("Sem dado no formulário: " + string.Join(", ", SemValor) + ".");
            if (Preenchidos == 0 && OutrasTelas.Count > 0) partes.Add("Estes campos são da tela \"" + string.Join("\" ou \"", OutrasTelas) + "\": abra-a e cole de novo.");
            return string.Join(" ", partes);
        }

        public bool Problema { get { return Erro != null || NaoEncontrados.Count > 0 || Falhas.Count > 0; } }
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
            if (perfil.Campos == null || !perfil.Campos.Any(c => !c.Captura))
            {
                r.Erro = "O perfil \"" + perfil.Nome + "\" ainda não tem campos. Aponte o mouse para um campo e use " + Atalhos.Texto(Acao.Mapear) + ".";
                return;
            }
            var raiz = AutomationElement.FromHandle(janela);
            // A leitura da janela (com cache) é a parte lenta: só é feita se algum campo precisar da automação.
            Instantaneo inst = null;
            bool estruturaAnotada = false;
            string tituloAtual = Nativo.TituloDaJanela(janela);
            foreach (var campo in perfil.Campos.Where(c => !c.Captura))
            {
                string rotulo = Dados.Rotulo(perfil.FormularioId, campo.Dado);

                // Opção de lista ("sexo|Feminino"): só é clicada quando o valor do campo é aquela opção.
                string campoDado, opcao;
                bool ehOpcao = Dados.EhOpcao(campo.Dado, out campoDado, out opcao);
                string valor = Dados.Valor(perfil.FormularioId, ehOpcao ? campoDado : campo.Dado);
                if (string.IsNullOrEmpty(valor))
                {
                    if (!ehOpcao) r.SemValor.Add(rotulo);
                    continue;
                }
                if (ehOpcao && !Instantaneo.CasaOpcao(opcao, valor)) continue;

                if (campo.Posicao)
                {
                    if (!string.Equals(campo.JanelaTitulo ?? "", tituloAtual ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!r.OutrasTelas.Contains(campo.JanelaTitulo)) r.OutrasTelas.Add(campo.JanelaTitulo);
                        continue;
                    }
                    if (PreencherPorPosicao(janela, campo, ehOpcao ? null : valor)) r.Preenchidos++;
                    else { Diagnostico.Nota("não preencheu \"" + rotulo + "\" · área da janela indisponível"); r.NaoEncontrados.Add(rotulo); }
                    continue;
                }

                AutomationElement elemento = null;
                try
                {
                    if (inst == null) inst = Instantaneo.Tirar(raiz);
                    elemento = Localizar(inst, raiz, campo);
                    if (elemento == null || !Escrever(elemento, valor))
                    {
                        if (!estruturaAnotada && Diagnostico.Ativo)
                        {
                            estruturaAnotada = true;
                            Diagnostico.Nota("controles que a janela mostra para a automação:" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", inst.Descrever().ToArray()));
                        }
                        Diagnostico.Nota("não preencheu \"" + rotulo + "\" · " + (elemento == null ? "controle não localizado" : "controle desabilitado ou recusou a digitação")
                            + " · mapeado como " + campo.Tipo + " id=" + campo.AutomationId + " nome=" + campo.NomeControle + " índice=" + campo.Indice);
                        r.NaoEncontrados.Add(rotulo); continue;
                    }
                    r.Preenchidos++;
                }
                catch (Exception erro)
                {
                    // A UI Automation recusa a ação com mensagens genéricas; um campo recusado não deve parar os demais.
                    r.Falhas.Add(rotulo + " (" + erro.GetType().Name + ": " + erro.Message + ")");
                    Diagnostico.Falha("preenchimento", rotulo, campo, elemento, erro);
                }
            }
        }

        /// <summary>
        /// Clica na posição guardada (fração da área interna da janela) e, para campos de texto, digita o valor por cima.
        /// Sem consulta à árvore de acessibilidade, então é rápido mesmo em programas Qt. valor null = só clicar (opção).
        /// </summary>
        static bool PreencherPorPosicao(IntPtr janela, CampoMapeado campo, string valor)
        {
            int ox, oy, largura, altura;
            if (!Nativo.AreaCliente(janela, out ox, out oy, out largura, out altura)) return false;
            Teclado.Clicar(ox + (int)Math.Round(campo.PosX * largura), oy + (int)Math.Round(campo.PosY * altura));
            if (valor != null)
            {
                Thread.Sleep(30);
                Teclado.SelecionarTudo();
                Teclado.Digitar(valor);
            }
            return true;
        }

        /// <summary>Resolve o campo no instantâneo da janela (rápido); só controles personalizados usam a busca direta.</summary>
        public static AutomationElement Localizar(Instantaneo inst, AutomationElement raiz, CampoMapeado campo)
        {
            if (campo.Tipo == "Custom") return LocalizarDireto(raiz, campo);
            var item = inst.Achar(campo);
            return item == null ? null : item.El;
        }

        static AutomationElement LocalizarDireto(AutomationElement raiz, CampoMapeado campo)
        {
            var tipo = Mapeador.TipoPorNome(campo.Tipo);
            var porTipo = new PropertyCondition(AutomationElement.ControlTypeProperty, tipo);

            // Do mais firme para o mais frágil; cada um só vale se encontrar o campo.
            AutomationElement achado;
            if (!string.IsNullOrEmpty(campo.AutomationId))
            {
                achado = raiz.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, campo.AutomationId));
                if (achado != null) return achado;
            }

            if (!string.IsNullOrEmpty(campo.NomeControle))
            {
                achado = raiz.FindFirst(TreeScope.Descendants, new AndCondition(porTipo,
                    new PropertyCondition(AutomationElement.NameProperty, campo.NomeControle)));
                if (achado != null) return achado;
            }

            if (!string.IsNullOrEmpty(campo.ClassName))
            {
                var daClasse = raiz.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, campo.ClassName));
                if (campo.IndiceClasse >= 0 && campo.IndiceClasse < daClasse.Count) return daClasse[campo.IndiceClasse];
            }

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
                    try
                    {
                        vp.SetValue(valor);
                        // Alguns programas (Qt) aceitam o SetValue sem erro e não mudam o campo: confere e, se preciso, digita.
                        if (Instantaneo.Normalizar(vp.Current.Value) == Instantaneo.Normalizar(valor))
                        {
                            Diagnostico.Nota("preenchido por ValuePattern · " + el.Current.ControlType.ProgrammaticName);
                            return true;
                        }
                        Diagnostico.Nota("ValuePattern aceitou mas o campo não mudou; tentando digitar");
                    }
                    catch (InvalidOperationException erro) // o programa recusou SetValue; tenta digitando
                    {
                        Diagnostico.Nota("ValuePattern.SetValue recusado (" + erro.Message + "); tentando digitar");
                    }
                }
            }

            // Plano B para campos sem ValuePattern: foca e digita por cima.
            try { el.SetFocus(); }
            catch (InvalidOperationException)
            {
                // Controles personalizados recusam SetFocus: foca com um clique no centro do campo.
                var area = el.Current.BoundingRectangle;
                if (area.IsEmpty || area.Width < 1 || area.Height < 1) return false;
                Diagnostico.Nota("SetFocus recusado; focando com clique em " + (int)(area.X + area.Width / 2) + "," + (int)(area.Y + area.Height / 2));
                Teclado.Clicar((int)(area.X + area.Width / 2), (int)(area.Y + area.Height / 2));
            }
            Thread.Sleep(50);
            Teclado.SelecionarTudo();
            Teclado.Digitar(valor);
            Diagnostico.Nota("preenchido digitando · " + el.Current.ControlType.ProgrammaticName);
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
