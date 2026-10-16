using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Formulário principal: o colaborador escolhe um formulário e digita os dados uma única vez.</summary>
    class FormDados : Form
    {
        readonly Dictionary<string, Control> caixas = new Dictionary<string, Control>(); // TextBox, ou ComboBox nos campos de lista
        // O que foi digitado e ainda não salvo, para não perder ao trocar de formulário.
        readonly Dictionary<string, Dictionary<string, string>> rascunhos = new Dictionary<string, Dictionary<string, string>>();
        readonly Panel campos;
        readonly Panel rodape;
        readonly RotuloQuebra erro;
        ModeloFormulario modelo;

        /// <summary>O usuário clicou em "Colar no sistema" (os dados já foram validados e salvos).</summary>
        public event Action PedirColar;
        /// <summary>O usuário clicou em "Copiar do BRNET".</summary>
        public event Action PedirCapturar;

        // O botão do rodapé é "Salvar dados" enquanto há algo não salvo e vira "Colar no sistema" depois de salvar.
        readonly BotaoMarca principal;
        bool salvo;
        bool montando;
        bool brnetAtivo;
        // Campos que o "Copiar do BRNET" não preencheu: ficam em destaque até serem preenchidos.
        readonly HashSet<string> pendentes = new HashSet<string>();
        readonly Dictionary<string, Panel> molduras = new Dictionary<string, Panel>();
        static readonly Color FundoPendente = Color.FromArgb(0xFF, 0xF3, 0xDC);

        public FormDados()
        {
            Text = "Cadastro BRNET";
            Icon = Tema.CriarIcone();
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            ClientSize = new Size(440, 600);
            MinimumSize = new Size(340, 420);
            KeyPreview = true;

            // ----- Campos (rolagem automática quando a janela é pequena) -----
            campos = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 20, 24, 0) };

            // ----- Rodapé -----
            rodape = new Panel { Dock = DockStyle.Bottom, Height = 64 };
            erro = new RotuloQuebra
            {
                ForeColor = Tema.Perigo, Font = Tema.Texto(9.5f), Dock = DockStyle.Top,
                Padding = new Padding(24, 0, 24, 6), Visible = false
            };
            erro.Resize += (s, e) => AjustarRodape();
            var botoes = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(24, 12, 24, 12) };

            var limpar = new BotaoMarca("Limpar dados", EstiloBotao.Contorno) { Dock = DockStyle.Left, Width = 130 };
            limpar.Click += (s, e) =>
            {
                Dados.Limpar(modelo.Id);
                rascunhos.Remove(modelo.Id);
                foreach (var c in modelo.Campos.Where(Visivel)) Definir(caixas[c.Chave], "");
                MostrarErro("");
                LimparDestaques();
                salvo = false;
                AtualizarBotao();
                FocarPrimeira();
                Aviso.Mostrar("Dados apagados.");
            };

            principal = new BotaoMarca("Salvar dados", EstiloBotao.Primario) { Dock = DockStyle.Right, Width = 170 };
            principal.Click += (s, e) =>
            {
                if (CopiarDisponivel) Disparar(PedirCapturar);
                else if (!salvo) { if (Salvar(true)) { salvo = true; AtualizarBotao(); } }
                else Disparar(PedirColar);
            };


            botoes.Controls.Add(principal);
            botoes.Controls.Add(limpar);
            rodape.Controls.Add(botoes);
            rodape.Controls.Add(erro);
            AcceptButton = principal;

            // A ordem importa: o controle que preenche vem primeiro, as bordas depois.
            Controls.Add(campos);
            Controls.Add(rodape);

            Selecionar(RepositorioFormularios.IdPrincipal);
            RepositorioFormularios.Alterado += Recarregar;
        }

        // ---------- Formulários ----------

        /// <summary>Mostra o formulário pedido (o principal, se ele não existir mais), guardando o que estava digitado.</summary>
        public void Selecionar(string id)
        {
            var alvo = RepositorioFormularios.Obter(id);
            if (modelo != null) GuardarRascunho();
            Montar(alvo);
        }

        /// <summary>Chamado quando o editor de formulários salva alguma mudança.</summary>
        void Recarregar()
        {
            GuardarRascunho();
            Selecionar(modelo == null ? RepositorioFormularios.IdPrincipal : modelo.Id);
        }

        void GuardarRascunho()
        {
            if (modelo == null) return;
            if (salvo) { rascunhos.Remove(modelo.Id); return; } // nada a guardar: o que está na tela já foi salvo
            rascunhos[modelo.Id] = caixas.ToDictionary(par => par.Key, par => par.Value.Text);
        }

        void Montar(ModeloFormulario m)
        {
            montando = true;
            pendentes.Clear();
            molduras.Clear();
            modelo = m;
            Dados.FormularioAtivo = m.Id;
            Text = m.Nome; // o nome do formulário fica na barra de título da janela
            MostrarErro("");

            campos.SuspendLayout();
            foreach (var antigo in campos.Controls.Cast<Control>().ToList()) { campos.Controls.Remove(antigo); antigo.Dispose(); }
            caixas.Clear();

            // Campos "ao lado" dividem a linha com o anterior.
            var grupos = new List<List<Control>>();
            foreach (var c in m.Campos.Where(Visivel))
            {
                var bloco = NovoBloco(m, c);
                if (c.AoLado && grupos.Count > 0) grupos[grupos.Count - 1].Add(bloco);
                else grupos.Add(new List<Control> { bloco });
            }
            var blocos = grupos.Select(g => g.Count == 1 ? g[0] : NovaLinha(g)).ToList();
            // Dock=Top empilha de baixo para cima na ordem de inclusão; por isso o primeiro campo entra por último.
            for (int i = blocos.Count - 1; i >= 0; i--) campos.Controls.Add(blocos[i]);

            var valores = Dados.Copia(m.Id);
            Dictionary<string, string> rascunho;
            if (rascunhos.TryGetValue(m.Id, out rascunho)) valores = rascunho;
            foreach (var campo in m.Campos.Where(Visivel))
            {
                string v;
                if (valores.TryGetValue(campo.Chave, out v) && v != "") Definir(caixas[campo.Chave], Exibir(campo, v));
            }

            campos.AutoScrollPosition = Point.Empty;
            campos.ResumeLayout(true);

            // Sem rascunho, o que aparece é exatamente o que foi salvo.
            salvo = !rascunhos.ContainsKey(m.Id) && Dados.TemDados(m.Id);
            montando = false;
            AtualizarBotao();
        }

        /// <summary>Campos preenchidos automaticamente com a data de hoje não aparecem no formulário.</summary>
        static bool Visivel(CampoForm campo) { return !campo.Hoje; }

        /// <summary>Nenhum campo visível tem texto: nada para salvar nem colar ainda.</summary>
        public bool EmBranco
        {
            get { return modelo != null && modelo.Campos.Where(Visivel).All(c => caixas[c.Chave].Text == ""); }
        }

        /// <summary>Com o formulário em branco e a aba do BRNET ativa, o botão do rodapé copia os dados do BRNET.</summary>
        bool CopiarDisponivel { get { return brnetAtivo && EmBranco; } }

        /// <summary>Chamado pelo rastreio da janela ativa: a aba do BRNET está aberta no navegador?</summary>
        public void DefinirBrnetAtivo(bool ativo)
        {
            if (brnetAtivo == ativo) return;
            brnetAtivo = ativo;
            AtualizarBotao();
        }

        void AtualizarBotao()
        {
            principal.Text = CopiarDisponivel ? "Copiar do BRNET" : salvo ? "Colar no sistema" : "Salvar dados";
        }

        /// <summary>Qualquer mudança nos campos pede um novo "Salvar dados".</summary>
        void MarcarAlterado()
        {
            if (montando) return;
            salvo = false;
            AtualizarBotao();
        }

        /// <summary>Coloca vários campos curtos lado a lado, com a mesma largura.</summary>
        static Control NovaLinha(List<Control> itens)
        {
            const int folga = 12;
            var linha = new Panel { Dock = DockStyle.Top };
            bool ajustando = false;
            Action ajustar = () =>
            {
                if (ajustando) return;
                ajustando = true;
                int largura = Math.Max(40, (linha.ClientSize.Width - folga * (itens.Count - 1)) / itens.Count);
                int altura = 0;
                for (int i = 0; i < itens.Count; i++)
                {
                    itens[i].SetBounds(i * (largura + folga), 0, largura, itens[i].Height);
                    altura = Math.Max(altura, itens[i].Height);
                }
                if (linha.Height != altura) linha.Height = altura;
                ajustando = false;
            };
            foreach (var item in itens)
            {
                item.Dock = DockStyle.None;
                linha.Controls.Add(item);
                item.SizeChanged += (s, e) => ajustar();
            }
            linha.Resize += (s, e) => ajustar();
            ajustar();
            return linha;
        }

        static int alturaCampo;

        static int AlturaCampo()
        {
            if (alturaCampo == 0)
                using (var texto = new TextBox { Font = Tema.Texto(11f), BorderStyle = BorderStyle.None })
                using (var lista = new ComboBox { Font = Tema.Texto(11f), FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList })
                    alturaCampo = Math.Max(texto.PreferredHeight, lista.PreferredHeight);
            return alturaCampo;
        }

        Control NovoBloco(ModeloFormulario m, CampoForm campo)
        {
            Control caixa;
            int alturaCaixa;
            if (campo.EhLista)
            {
                var lista = new ComboBox
                {
                    Name = "cbo_" + campo.Chave, Font = Tema.Texto(11f), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White, ForeColor = Tema.TextoCorpo, Dock = DockStyle.Fill, AccessibleName = campo.Rotulo
                };
                lista.Items.AddRange((campo.Opcoes ?? new List<string>()).Cast<object>().ToArray());
                caixa = lista;
                alturaCaixa = lista.PreferredHeight;
            }
            else
            {
                var texto = new TextBox
                {
                    Name = "txt_" + campo.Chave, Font = Tema.Texto(11f), BorderStyle = BorderStyle.None,
                    BackColor = Color.White, ForeColor = Tema.TextoCorpo, Dock = DockStyle.Fill, AccessibleName = campo.Rotulo
                };
                if (campo.EhCpf)
                {
                    texto.MaxLength = 14;
                    texto.TextChanged += (s, e) =>
                    {
                        var formatado = Dados.FormatarCpf(texto.Text);
                        if (formatado != texto.Text) { texto.Text = formatado; texto.SelectionStart = formatado.Length; }
                    };
                }
                if (campo.EhData)
                {
                    texto.MaxLength = 10;
                    texto.TextChanged += (s, e) =>
                    {
                        var formatado = Dados.FormatarData(texto.Text);
                        if (formatado != texto.Text) { texto.Text = formatado; texto.SelectionStart = formatado.Length; }
                    };
                }
                if (campo.EhNumero)
                    texto.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
                if (campo.EhDecimal)
                {
                    texto.MaxLength = 7;
                    texto.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.') e.Handled = true; };
                }
                if (campo.EhRg || campo.EhPassaporte || campo.EhDocumento) texto.MaxLength = 20;
                caixa = texto;
                alturaCaixa = texto.PreferredHeight;
            }
            // Borda cinza do guia (#BFC3C6), petróleo quando o campo está em foco.
            // Lista e caixa de texto têm alturas próprias diferentes: todas as molduras usam a maior, para as linhas alinharem.
            int sobra = AlturaCampo() - alturaCaixa;
            var moldura = new Panel
            {
                Dock = DockStyle.Bottom, BackColor = Color.White,
                Padding = new Padding(10, 9 + sobra / 2, 10, 7 + sobra - sobra / 2)
            };
            moldura.Height = alturaCaixa + moldura.Padding.Vertical;
            moldura.Paint += (s, e) =>
            {
                bool falta = pendentes.Contains(campo.Chave);
                using (var caneta = new Pen(falta ? Tema.Ambar : caixa.Focused ? Tema.Petroleo : Tema.LinhaForte, falta ? 2f : 1f))
                    e.Graphics.DrawRectangle(caneta, falta ? 1 : 0, falta ? 1 : 0, moldura.Width - (falta ? 3 : 1), moldura.Height - (falta ? 3 : 1));
            };
            molduras[campo.Chave] = moldura;
            caixa.TextChanged += (s, e) =>
            {
                MarcarAlterado();
                if (caixa.Text != "" && pendentes.Remove(campo.Chave)) Estilizar(campo.Chave);
            };
            caixa.Enter += (s, e) => moldura.Invalidate();
            caixa.Leave += (s, e) => moldura.Invalidate();
            moldura.Click += (s, e) => caixa.Focus();
            moldura.Controls.Add(caixa);
            caixas[campo.Chave] = caixa;

            // O rótulo quebra linha conforme a largura da janela.
            var rotulo = new RotuloQuebra
            {
                Text = campo.Rotulo + (campo.Obrigatorio ? " (obrigatório)" : ""),
                Font = Tema.TextoForte(9.5f), ForeColor = Tema.Marinho,
                Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 6)
            };

            var bloco = new Panel { Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 14) };
            bloco.Controls.Add(moldura);
            bloco.Controls.Add(rotulo);
            Action ajustar = () => bloco.Height = rotulo.Height + moldura.Height + bloco.Padding.Vertical;
            rotulo.Resize += (s, e) => ajustar();
            ajustar();
            return bloco;
        }

        // ---------- Mensagem de erro ----------

        void MostrarErro(string texto)
        {
            erro.Text = texto;
            erro.Visible = texto != "";
            AjustarRodape();
        }

        void AjustarRodape()
        {
            rodape.Height = 64 + (erro.Visible ? erro.Height : 0);
        }

        // ---------- Eventos ----------

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            var primeira = caixas.Values.FirstOrDefault();
            if (primeira != null) primeira.Focus();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RepositorioFormularios.Alterado -= Recarregar;
            base.OnFormClosed(e);
        }

        static void Disparar(Action acao)
        {
            if (acao != null) acao();
        }

        /// <summary>Valida e salva. Devolve false (mostrando o erro) se algum campo estiver inválido.</summary>
        bool Salvar(bool avisar)
        {
            var novo = new Dictionary<string, string>();
            var problemas = new List<string>();
            Control primeiro = null;
            Action<CampoForm, string> marcar = (campo, texto) => { problemas.Add(texto); if (primeiro == null) primeiro = caixas[campo.Chave]; };

            foreach (var campo in modelo.Campos)
            {
                // "Data de hoje" não aparece na tela: vai sempre com o dia em que os dados foram salvos.
                if (!Visivel(campo)) { novo[campo.Chave] = Dados.DataDeHoje(); continue; }
                var texto = caixas[campo.Chave].Text.Trim();
                bool documentoCpf = campo.EhDocumento && Dados.ParecaCpf(texto);
                if (campo.EhCpf || documentoCpf) texto = Dados.SomenteDigitos(texto);
                else if (campo.EhPassaporte || campo.EhDocumento) texto = texto.ToUpperInvariant();
                else if (campo.EhDecimal) texto = texto.Replace('.', ',');
                novo[campo.Chave] = texto;

                if (texto == "")
                {
                    if (campo.Obrigatorio) marcar(campo, "Informe o campo \"" + campo.Rotulo + "\".");
                }
                else if (campo.EhEmail && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um e-mail válido.");
                else if (documentoCpf && !Dados.CpfValido(texto))
                    marcar(campo, "\"" + campo.Rotulo + "\" parece um CPF, mas não é válido. Confira os dígitos (passaporte tem letras e números).");
                else if (campo.EhDocumento && !documentoCpf && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[A-Za-z0-9]{5,12}$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um CPF nem um passaporte válido.");
                else if (campo.EhData && !Dados.DataValida(texto))
                    marcar(campo, "\"" + campo.Rotulo + "\" deve ser uma data no formato dd/mm/aaaa.");
                else if (campo.EhCpf && !Dados.CpfValido(texto))
                    marcar(campo, "\"" + campo.Rotulo + "\" não é um CPF válido. Confira os dígitos.");
                else if (campo.EhDecimal && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^\d{1,4}([.,]\d{1,2})?$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" deve ser um número, com vírgula se precisar (ex.: 70,5).");
                else if (campo.EhNumero && Dados.SomenteDigitos(texto) != texto)
                    marcar(campo, "\"" + campo.Rotulo + "\" deve ter apenas números.");
                else if (campo.EhRg && Dados.SomenteAlfanumerico(texto).Length < 5)
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um RG válido.");
                else if (campo.EhPassaporte && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[A-Za-z0-9]{5,12}$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um passaporte válido (letras e números, de 5 a 12 caracteres).");
            }

            if (problemas.Count == 0 && modelo.Campos.Count > 0 && novo.Where(par => !modelo.Campo(par.Key).Hoje).All(par => par.Value == ""))
                marcar(modelo.Campos.First(), "Preencha pelo menos um campo.");

            if (problemas.Count > 0)
            {
                MostrarErro(string.Join(" ", problemas));
                if (primeiro != null) primeiro.Focus();
                return false;
            }

            Dados.Salvar(modelo.Id, novo);
            rascunhos.Remove(modelo.Id);
            LimparDestaques();
            MostrarErro("");
            if (avisar)
                Aviso.Mostrar("Dados de \"" + modelo.Nome + "\" salvos. Agora vá ao sistema e clique em \"Colar no sistema\" (ou use " + Atalhos.Texto(Acao.Colar) + ").");
            return true;
        }

        /// <summary>Coloca nos campos o que foi capturado de outro sistema (botão Copiar do BRNET ou atalho), sem salvar.</summary>
        public void Receber(string formularioId, Dictionary<string, string> valores)
        {
            var alvo = RepositorioFormularios.Obter(formularioId);
            if (modelo == null || modelo.Id != alvo.Id) Selecionar(alvo.Id);
            foreach (var par in valores)
            {
                Control caixa;
                if (!caixas.TryGetValue(par.Key, out caixa)) continue;
                var campo = modelo.Campo(par.Key);
                Definir(caixa, campo == null ? par.Value : Exibir(campo, par.Value));
            }
            MostrarErro("");
            FocarPrimeira();
        }

        /// <summary>Captura do BRNET: preenche o formulário aberto (ou o Cadastro BRNET, se o aberto não tem esses campos).</summary>
        public int ReceberBrnet(Dictionary<string, string> valores)
        {
            bool serve = modelo != null && valores.Keys.Any(k => modelo.Campo(k) != null);
            Receber(serve ? modelo.Id : RepositorioFormularios.IdBrnet, valores);

            // O que o BRNET não trouxe (ou veio vazio) fica em destaque para ser preenchido à mão.
            LimparDestaques();
            foreach (var campo in modelo.Campos.Where(Visivel))
                if (caixas[campo.Chave].Text == "") pendentes.Add(campo.Chave);
            foreach (var chave in pendentes) Estilizar(chave);
            FocarPrimeira();
            return pendentes.Count;
        }

        /// <summary>Pinta de âmbar a moldura dos campos pendentes e devolve ao normal os demais.</summary>
        void Estilizar(string chave)
        {
            Panel moldura;
            Control caixa;
            if (!molduras.TryGetValue(chave, out moldura) || !caixas.TryGetValue(chave, out caixa)) return;
            var fundo = pendentes.Contains(chave) ? FundoPendente : Color.White;
            moldura.BackColor = fundo;
            caixa.BackColor = fundo;
            moldura.Invalidate();
        }

        void LimparDestaques()
        {
            var chaves = pendentes.ToList();
            pendentes.Clear();
            foreach (var chave in chaves) Estilizar(chave);
        }

        void FocarPrimeira()
        {
            var visiveis = modelo.Campos.Where(Visivel).ToList();
            var primeira = visiveis.Where(c => pendentes.Contains(c.Chave)).Concat(visiveis).Select(c => caixas[c.Chave]).FirstOrDefault();
            if (primeira != null) primeira.Focus();
        }

        // ---------- Valores dos campos ----------

        /// <summary>Como o valor guardado aparece na tela (CPF com pontos).</summary>
        static string Exibir(CampoForm campo, string valor)
        {
            if (campo.EhCpf || (campo.EhDocumento && Dados.ParecaCpf(valor))) return Dados.FormatarCpf(valor);
            return valor;
        }

        /// <summary>Escreve em uma caixa de texto ou escolhe a opção da lista (sem diferenciar maiúsculas); opção desconhecida deixa a lista vazia.</summary>
        static void Definir(Control caixa, string valor)
        {
            var lista = caixa as ComboBox;
            if (lista == null) { caixa.Text = valor ?? ""; return; }
            lista.SelectedIndex = -1;
            for (int i = 0; i < lista.Items.Count; i++)
                if (string.Equals((string)lista.Items[i], valor, StringComparison.OrdinalIgnoreCase)) { lista.SelectedIndex = i; break; }
        }
    }
}
