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
        readonly Dictionary<string, TextBox> caixas = new Dictionary<string, TextBox>();
        // O que foi digitado e ainda não salvo, para não perder ao trocar de formulário.
        readonly Dictionary<string, Dictionary<string, string>> rascunhos = new Dictionary<string, Dictionary<string, string>>();
        readonly Panel campos;
        readonly Panel rodape;
        readonly ComboBox seletor;
        readonly RotuloQuebra erro;
        ModeloFormulario modelo;
        bool montando;

        public FormDados()
        {
            Text = "Dados para preencher · BR MED";
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

            // ----- Cabeçalho -----
            var topo = new Panel { Dock = DockStyle.Top, BackColor = Tema.Marinho, Padding = new Padding(24, 16, 24, 14) };
            var titulo = new RotuloQuebra
            {
                Text = "Dados para preencher", ForeColor = Color.White, Font = Tema.Titulo(14f),
                Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 4)
            };
            var subtitulo = new RotuloQuebra
            {
                Text = "Preencha uma vez. Depois, em cada sistema, use Ctrl+Alt+V.",
                ForeColor = ColorTranslator.FromHtml("#C9DCE4"), Font = Tema.Texto(9.5f), Dock = DockStyle.Top
            };
            topo.Controls.Add(subtitulo);
            topo.Controls.Add(titulo);
            Action ajustarTopo = () => topo.Height = titulo.Height + subtitulo.Height + topo.Padding.Vertical;
            titulo.Resize += (s, e) => ajustarTopo();
            subtitulo.Resize += (s, e) => ajustarTopo();
            topo.Height = 84;

            // ----- Escolha do formulário -----
            var seletorPainel = new Panel { Dock = DockStyle.Top, Height = 52 };
            var esquerda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 8, 0) };
            seletor = new ComboBox
            {
                Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Texto(10.5f),
                DisplayMember = "Nome", AccessibleName = "Formulário"
            };
            seletor.DropDownWidth = 320;
            esquerda.Controls.Add(seletor);
            var direita = new Panel { Dock = DockStyle.Right, Width = 140, Padding = new Padding(0, 10, 24, 8) };
            var gerenciar = new Button
            {
                Text = "Gerenciar…", Dock = DockStyle.Fill, Font = Tema.Texto(9.5f), FlatStyle = FlatStyle.Flat,
                ForeColor = Tema.Petroleo, BackColor = Color.White, UseMnemonic = false
            };
            gerenciar.FlatAppearance.BorderColor = Tema.Petroleo;
            gerenciar.Click += (s, e) => FormModelos.Abrir();
            direita.Controls.Add(gerenciar);
            seletorPainel.Controls.Add(esquerda);
            seletorPainel.Controls.Add(direita);
            seletor.SelectedIndexChanged += (s, e) =>
            {
                if (montando) return;
                GuardarRascunho();
                Montar((ModeloFormulario)seletor.SelectedItem);
            };

            // ----- Campos (rolagem automática quando a janela é pequena) -----
            campos = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 8, 24, 0) };

            // ----- Rodapé -----
            rodape = new Panel { Dock = DockStyle.Bottom, Height = 64 };
            erro = new RotuloQuebra
            {
                ForeColor = Tema.Cobre, Font = Tema.Texto(9.5f), Dock = DockStyle.Top,
                Padding = new Padding(24, 0, 24, 6), Visible = false
            };
            erro.Resize += (s, e) => AjustarRodape();
            var botoes = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(24, 12, 24, 12) };

            var limpar = new Button
            {
                Text = "Limpar dados", Font = Tema.Texto(10f), FlatStyle = FlatStyle.Flat, Dock = DockStyle.Left,
                ForeColor = Tema.Suave, BackColor = Color.White, Width = 130
            };
            limpar.FlatAppearance.BorderColor = Tema.Linha;
            limpar.Click += (s, e) =>
            {
                Dados.Limpar(modelo.Id);
                rascunhos.Remove(modelo.Id);
                foreach (var c in caixas.Values) c.Clear();
                MostrarErro("");
                var primeira = caixas.Values.FirstOrDefault();
                if (primeira != null) primeira.Focus();
                Aviso.Mostrar("Dados apagados.");
            };

            var salvar = new Button
            {
                Text = "Salvar dados", Font = Tema.TextoForte(10f), FlatStyle = FlatStyle.Flat, Dock = DockStyle.Right,
                ForeColor = Color.White, BackColor = Tema.Petroleo, Width = 150
            };
            salvar.FlatAppearance.BorderSize = 0;
            salvar.Click += (s, e) => Salvar();

            botoes.Controls.Add(salvar);
            botoes.Controls.Add(limpar);
            rodape.Controls.Add(botoes);
            rodape.Controls.Add(erro);
            AcceptButton = salvar;

            // A ordem importa: o controle que preenche vem primeiro, as bordas depois.
            Controls.Add(campos);
            Controls.Add(seletorPainel);
            Controls.Add(rodape);
            Controls.Add(topo);

            CarregarLista(Dados.FormularioAtivo);
            RepositorioFormularios.Alterado += Recarregar;
        }

        // ---------- Formulários ----------

        void CarregarLista(string idPreferido)
        {
            montando = true;
            var todos = RepositorioFormularios.Todos();
            seletor.DataSource = todos;
            var escolhido = todos.FirstOrDefault(m => m.Id == idPreferido) ?? todos[0];
            seletor.SelectedItem = escolhido;
            montando = false;
            Montar(escolhido);
        }

        /// <summary>Chamado quando o editor de formulários salva alguma mudança.</summary>
        void Recarregar()
        {
            GuardarRascunho();
            CarregarLista(modelo == null ? Dados.FormularioAtivo : modelo.Id);
        }

        void GuardarRascunho()
        {
            if (modelo == null) return;
            rascunhos[modelo.Id] = caixas.ToDictionary(par => par.Key, par => par.Value.Text);
        }

        void Montar(ModeloFormulario m)
        {
            modelo = m;
            Dados.FormularioAtivo = m.Id;
            MostrarErro("");

            campos.SuspendLayout();
            foreach (var antigo in campos.Controls.Cast<Control>().ToList()) { campos.Controls.Remove(antigo); antigo.Dispose(); }
            caixas.Clear();

            var blocos = m.Campos.Select(c => NovoBloco(m, c)).ToList();
            // Dock=Top empilha de baixo para cima na ordem de inclusão; por isso o primeiro campo entra por último.
            for (int i = blocos.Count - 1; i >= 0; i--) campos.Controls.Add(blocos[i]);

            var valores = Dados.Copia(m.Id);
            Dictionary<string, string> rascunho;
            if (rascunhos.TryGetValue(m.Id, out rascunho)) valores = rascunho;
            foreach (var campo in m.Campos)
            {
                string v;
                if (valores.TryGetValue(campo.Chave, out v)) caixas[campo.Chave].Text = campo.EhCpf ? Dados.FormatarCpf(v) : v;
            }

            campos.AutoScrollPosition = Point.Empty;
            campos.ResumeLayout(true);
        }

        Control NovoBloco(ModeloFormulario m, CampoForm campo)
        {
            var caixa = new TextBox
            {
                Name = "txt_" + campo.Chave, Font = Tema.Texto(11f), BorderStyle = BorderStyle.FixedSingle,
                BackColor = Tema.Fundo, ForeColor = Tema.Marinho, Dock = DockStyle.Bottom, AccessibleName = campo.Rotulo
            };
            if (campo.EhCpf)
            {
                caixa.MaxLength = 14;
                caixa.TextChanged += (s, e) =>
                {
                    var formatado = Dados.FormatarCpf(caixa.Text);
                    if (formatado != caixa.Text) { caixa.Text = formatado; caixa.SelectionStart = formatado.Length; }
                };
            }
            caixas[campo.Chave] = caixa;

            // O rótulo quebra linha conforme a largura da janela; a bolinha fica na margem esquerda dele.
            var rotulo = new RotuloQuebra
            {
                Text = campo.Rotulo + (campo.Obrigatorio ? " (obrigatório)" : ""),
                Font = Tema.TextoForte(9.5f), ForeColor = Tema.Marinho,
                Dock = DockStyle.Top, Padding = new Padding(16, 0, 0, 6)
            };
            rotulo.Controls.Add(new PictureBox { Image = Tema.Bolinha(Dados.CorDoCampo(m, campo)), Size = new Size(10, 10), Location = new Point(0, 4) });

            var bloco = new Panel { Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 14) };
            bloco.Controls.Add(caixa);
            bloco.Controls.Add(rotulo);
            Action ajustar = () => bloco.Height = rotulo.Height + caixa.Height + bloco.Padding.Vertical;
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

        void Salvar()
        {
            var novo = new Dictionary<string, string>();
            var problemas = new List<string>();
            TextBox primeiro = null;
            Action<CampoForm, string> marcar = (campo, texto) => { problemas.Add(texto); if (primeiro == null) primeiro = caixas[campo.Chave]; };

            foreach (var campo in modelo.Campos)
            {
                var texto = caixas[campo.Chave].Text.Trim();
                if (campo.EhCpf) texto = Dados.SomenteDigitos(texto);
                novo[campo.Chave] = texto;

                if (texto == "")
                {
                    if (campo.Obrigatorio) marcar(campo, "Informe o campo \"" + campo.Rotulo + "\".");
                }
                else if (campo.EhEmail && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um e-mail válido.");
                else if (campo.EhCpf && !Dados.CpfValido(texto))
                    marcar(campo, "\"" + campo.Rotulo + "\" não é um CPF válido. Confira os dígitos.");
            }

            if (problemas.Count == 0 && modelo.Campos.Count > 0 && novo.Values.All(v => v == ""))
                marcar(modelo.Campos.First(), "Preencha pelo menos um campo.");

            if (problemas.Count > 0)
            {
                MostrarErro(string.Join(" ", problemas));
                if (primeiro != null) primeiro.Focus();
                return;
            }

            Dados.Salvar(modelo.Id, novo);
            rascunhos.Remove(modelo.Id);
            Close();
            Aviso.Mostrar("Dados de \"" + modelo.Nome + "\" salvos. Agora vá a cada sistema e use Ctrl+Alt+V.");
        }
    }
}
