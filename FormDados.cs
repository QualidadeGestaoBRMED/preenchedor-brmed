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
        readonly RotuloQuebra titulo;
        readonly RotuloQuebra erro;
        ModeloFormulario modelo;

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
            var topo = new Panel { Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(24, 20, 24, 16) };
            topo.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Tema.Linha), 0, topo.Height - 1, topo.Width, topo.Height - 1);
            titulo = new RotuloQuebra
            {
                ForeColor = Tema.Marinho, Font = Tema.Titulo(15f), Dock = DockStyle.Top
            };
            topo.Controls.Add(titulo);
            titulo.Resize += (s, e) => topo.Height = titulo.Height + topo.Padding.Vertical;
            topo.Height = 64;

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
                foreach (var c in caixas.Values) c.Clear();
                MostrarErro("");
                var primeira = caixas.Values.FirstOrDefault();
                if (primeira != null) primeira.Focus();
                Aviso.Mostrar("Dados apagados.");
            };

            var salvar = new BotaoMarca("Salvar dados", EstiloBotao.Primario) { Dock = DockStyle.Right, Width = 150 };
            salvar.Click += (s, e) => Salvar();

            botoes.Controls.Add(salvar);
            botoes.Controls.Add(limpar);
            rodape.Controls.Add(botoes);
            rodape.Controls.Add(erro);
            AcceptButton = salvar;

            // A ordem importa: o controle que preenche vem primeiro, as bordas depois.
            Controls.Add(campos);
            Controls.Add(rodape);
            Controls.Add(topo);

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
            rascunhos[modelo.Id] = caixas.ToDictionary(par => par.Key, par => par.Value.Text);
        }

        void Montar(ModeloFormulario m)
        {
            modelo = m;
            Dados.FormularioAtivo = m.Id;
            titulo.Text = m.Nome;
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
                Name = "txt_" + campo.Chave, Font = Tema.Texto(11f), BorderStyle = BorderStyle.None,
                BackColor = Color.White, ForeColor = Tema.TextoCorpo, Dock = DockStyle.Fill, AccessibleName = campo.Rotulo
            };
            // Borda cinza do guia (#BFC3C6), petróleo quando o campo está em foco.
            var moldura = new Panel { Dock = DockStyle.Bottom, BackColor = Color.White, Padding = new Padding(10, 9, 10, 7) };
            moldura.Height = caixa.PreferredHeight + moldura.Padding.Vertical;
            moldura.Paint += (s, e) =>
            {
                using (var caneta = new Pen(caixa.Focused ? Tema.Petroleo : Tema.LinhaForte))
                    e.Graphics.DrawRectangle(caneta, 0, 0, moldura.Width - 1, moldura.Height - 1);
            };
            caixa.Enter += (s, e) => moldura.Invalidate();
            caixa.Leave += (s, e) => moldura.Invalidate();
            moldura.Click += (s, e) => caixa.Focus();
            moldura.Controls.Add(caixa);
            if (campo.EhCpf)
            {
                caixa.MaxLength = 14;
                caixa.TextChanged += (s, e) =>
                {
                    var formatado = Dados.FormatarCpf(caixa.Text);
                    if (formatado != caixa.Text) { caixa.Text = formatado; caixa.SelectionStart = formatado.Length; }
                };
            }
            if (campo.EhNumero)
                caixa.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            if (campo.EhRg || campo.EhPassaporte) caixa.MaxLength = 20;
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
                else if (campo.EhPassaporte) texto = texto.ToUpperInvariant();
                novo[campo.Chave] = texto;

                if (texto == "")
                {
                    if (campo.Obrigatorio) marcar(campo, "Informe o campo \"" + campo.Rotulo + "\".");
                }
                else if (campo.EhEmail && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um e-mail válido.");
                else if (campo.EhCpf && !Dados.CpfValido(texto))
                    marcar(campo, "\"" + campo.Rotulo + "\" não é um CPF válido. Confira os dígitos.");
                else if (campo.EhNumero && Dados.SomenteDigitos(texto) != texto)
                    marcar(campo, "\"" + campo.Rotulo + "\" deve ter apenas números.");
                else if (campo.EhRg && Dados.SomenteAlfanumerico(texto).Length < 5)
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um RG válido.");
                else if (campo.EhPassaporte && !System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[A-Za-z0-9]{5,12}$"))
                    marcar(campo, "\"" + campo.Rotulo + "\" não parece um passaporte válido (letras e números, de 5 a 12 caracteres).");
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
            MostrarErro("");
            Aviso.Mostrar("Dados de \"" + modelo.Nome + "\" salvos. Agora vá a cada sistema e use " + Atalhos.Texto(Acao.Colar) + ".");
        }

        /// <summary>Coloca nos campos o que foi capturado de outro sistema (Ctrl+Alt+C), sem salvar.</summary>
        public void Receber(string formularioId, Dictionary<string, string> valores)
        {
            var alvo = RepositorioFormularios.Obter(formularioId);
            if (modelo == null || modelo.Id != alvo.Id) Selecionar(alvo.Id);
            foreach (var par in valores)
            {
                TextBox caixa;
                if (!caixas.TryGetValue(par.Key, out caixa)) continue;
                var campo = modelo.Campo(par.Key);
                caixa.Text = campo != null && campo.EhCpf ? Dados.FormatarCpf(par.Value) : par.Value;
            }
            MostrarErro("");
            var primeira = caixas.Values.FirstOrDefault();
            if (primeira != null) primeira.Focus();
        }
    }
}
