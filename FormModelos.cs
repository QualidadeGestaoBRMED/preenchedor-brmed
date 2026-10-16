using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Tela "Meus formulários": o colaborador cria, renomeia e exclui formulários e define os campos de cada um.</summary>
    class FormModelos : Form
    {
        static readonly string[] NomesTipo = { "Texto", "E-mail", "Número", "Documento: CPF", "Documento: RG", "Documento: Passaporte", "Documento: CPF ou Passaporte", "Data (dd/mm/aaaa)", "Lista de opções", "Número decimal" };
        static readonly string[] ChavesTipo = { CampoForm.TipoTexto, CampoForm.TipoEmail, CampoForm.TipoNumero, CampoForm.TipoCpf, CampoForm.TipoRg, CampoForm.TipoPassaporte, CampoForm.TipoDocumento, CampoForm.TipoData, CampoForm.TipoLista, CampoForm.TipoDecimal };

        static FormModelos aberto;

        /// <summary>Abre o editor (ou traz o que já está aberto para frente).</summary>
        public static void Abrir()
        {
            if (aberto == null || aberto.IsDisposed) aberto = new FormModelos();
            if (!aberto.Visible) aberto.Show();
            aberto.WindowState = FormWindowState.Normal;
            aberto.Activate();
        }

        readonly List<ModeloFormulario> modelos;
        readonly ListBox lista = new ListBox();
        readonly TextBox nome = new TextBox();
        readonly DataGridView grade = new DataGridView();
        readonly Button excluir;
        ModeloFormulario atual;
        bool carregando;
        bool alterado;

        FormModelos()
        {
            modelos = RepositorioFormularios.Todos().Select(m => m.Clonar()).ToList();

            Text = "Meus formulários · Preenchedor de Cadastro";
            Icon = Tema.CriarIcone();
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true; // o formulário principal também fica por cima; este não pode ficar escondido atrás dele
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Tema.Fundo;
            ClientSize = new Size(780, 540);
            MinimumSize = new Size(620, 420);
            Font = Tema.Texto(9.5f);

            // ----- Lado direito: nome e campos do formulário escolhido -----
            var direita = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 12, 12, 4) };

            ConfigurarGrade();

            var subir = Botao("Subir", false);
            var descer = Botao("Descer", false);
            var removerCampo = Botao("Remover campo", false);
            subir.Click += (s, e) => Mover(-1);
            descer.Click += (s, e) => Mover(1);
            removerCampo.Click += (s, e) => RemoverCampo();
            var acoesCampo = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 6, 0, 0) };
            acoesCampo.Controls.AddRange(new Control[] { subir, descer, removerCampo });

            var dicaCampos = new RotuloQuebra
            {
                Text = "Campos do formulário. Escreva o rótulo na última linha vazia para criar um campo novo. "
                    + "Os tipos E-mail, Número, Data e Documento (CPF, RG, Passaporte, CPF ou Passaporte) validam o que for digitado; \"Lista de opções\" usa a coluna das opções e \"Data de hoje\" não aparece no formulário e vai sempre com o dia em que os dados foram salvos; \"Na linha do anterior\" põe campos curtos lado a lado; CPF e RG podem ir com ou sem pontos para os outros sistemas, escolhendo ao mapear; os campos obrigatórios precisam estar preenchidos para salvar os dados.",
                ForeColor = Tema.Suave, Dock = DockStyle.Top, Padding = new Padding(0, 10, 0, 6)
            };
            nome.Dock = DockStyle.Top;
            nome.Font = Tema.Texto(11f);
            nome.BorderStyle = BorderStyle.FixedSingle;
            nome.AccessibleName = "Nome do formulário";
            nome.TextChanged += (s, e) => { if (!carregando) alterado = true; };
            var rotuloNome = new RotuloQuebra { Text = "Nome do formulário", Font = Tema.TextoForte(9.5f), ForeColor = Tema.Marinho, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 4) };

            // Dock: o que preenche entra primeiro; o que fica no topo entra por último, na ordem inversa de leitura.
            direita.Controls.Add(grade);
            direita.Controls.Add(acoesCampo);
            direita.Controls.Add(dicaCampos);
            direita.Controls.Add(nome);
            direita.Controls.Add(rotuloNome);

            // ----- Lado esquerdo: lista de formulários -----
            var esquerda = new Panel { Dock = DockStyle.Left, Width = 230, Padding = new Padding(12, 12, 4, 4) };
            lista.Dock = DockStyle.Fill;
            lista.DisplayMember = "Nome";
            lista.HorizontalScrollbar = true;
            lista.BorderStyle = BorderStyle.FixedSingle;
            lista.IntegralHeight = false;
            lista.SelectedIndexChanged += (s, e) => { if (!carregando) Selecionar((ModeloFormulario)lista.SelectedItem); };

            var novo = Botao("Novo formulário", true);
            excluir = Botao("Excluir", false);
            novo.Click += (s, e) => Novo();
            excluir.Click += (s, e) => Excluir();
            var acoesLista = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 6, 0, 0) };
            acoesLista.Controls.AddRange(new Control[] { novo, excluir });
            esquerda.Controls.Add(lista);
            esquerda.Controls.Add(acoesLista);

            // ----- Rodapé -----
            var salvar = Botao("Salvar", true);
            var fechar = Botao("Fechar", false);
            salvar.Click += (s, e) => Salvar();
            fechar.Click += (s, e) => Close();
            var rodape = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 10, 12, 10), BackColor = Color.White
            };
            rodape.Controls.Add(salvar);
            rodape.Controls.Add(fechar);

            Controls.Add(direita);
            Controls.Add(esquerda);
            Controls.Add(rodape);

            carregando = true;
            foreach (var m in modelos) lista.Items.Add(m);
            carregando = false;
            lista.SelectedIndex = 0;
        }

        // ---------- Montagem ----------

        static Button Botao(string texto, bool principal)
        {
            return new BotaoMarca(texto, principal ? EstiloBotao.Primario : EstiloBotao.Contorno) { AutoSize = true };
        }

        void ConfigurarGrade()
        {
            grade.Dock = DockStyle.Fill;
            grade.BackgroundColor = Color.White;
            grade.BorderStyle = BorderStyle.FixedSingle;
            grade.RowHeadersVisible = false;
            grade.AllowUserToAddRows = true;
            grade.AllowUserToDeleteRows = false;
            grade.AllowUserToResizeRows = false;
            grade.MultiSelect = false;
            grade.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grade.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; // linhas crescem em vez de cortar o rótulo
            grade.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grade.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grade.DefaultCellStyle.Padding = new Padding(2, 3, 2, 3);
            grade.EnableHeadersVisualStyles = false;
            grade.ColumnHeadersDefaultCellStyle.BackColor = Tema.Fundo;
            grade.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Marinho;
            grade.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            var colRotulo = new DataGridViewTextBoxColumn { HeaderText = "Rótulo (como aparece no formulário)", FillWeight = 100, MinimumWidth = 120 };
            var colTipo = new DataGridViewComboBoxColumn { HeaderText = "Tipo", FillWeight = 38, MinimumWidth = 110, FlatStyle = FlatStyle.Flat };
            colTipo.Items.AddRange(NomesTipo);
            // A lista suspensa abre com a largura do texto mais longo, mesmo com a coluna estreita.
            colTipo.DropDownWidth = NomesTipo.Max(n => TextRenderer.MeasureText(n, Font).Width) + SystemInformation.VerticalScrollBarWidth + 12;
            var colObrig = new DataGridViewCheckBoxColumn { HeaderText = "Obrigatório", FillWeight = 32, MinimumWidth = 80 };
            var colOpcoes = new DataGridViewTextBoxColumn { HeaderText = "Opções da lista (separe com ;)", FillWeight = 60, MinimumWidth = 100 };
            var colHoje = new DataGridViewCheckBoxColumn { HeaderText = "Data de hoje (automática, oculta)", FillWeight = 26, MinimumWidth = 70 };
            var colLado = new DataGridViewCheckBoxColumn { HeaderText = "Na linha do anterior", FillWeight = 30, MinimumWidth = 80 };
            grade.Columns.AddRange(colRotulo, colTipo, colObrig, colOpcoes, colHoje, colLado);

            grade.DefaultValuesNeeded += (s, e) =>
            {
                carregando = true; // valores iniciais da linha nova não contam como alteração do usuário
                e.Row.Cells[1].Value = NomesTipo[0];
                e.Row.Cells[2].Value = false;
                e.Row.Cells[4].Value = false;
                e.Row.Cells[5].Value = false;
                carregando = false;
            };
            grade.DataError += (s, e) => e.ThrowException = false;
            grade.CellValueChanged += (s, e) => { if (!carregando) alterado = true; };
            grade.RowsRemoved += (s, e) => { if (!carregando) alterado = true; };
            // Marcar a caixa "Obrigatório" ou escolher o tipo vale já no primeiro clique.
            grade.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grade.IsCurrentCellDirty && grade.CurrentCell is DataGridViewCheckBoxCell) grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        // ---------- Edição ----------

        void Selecionar(ModeloFormulario novo)
        {
            if (novo == null) return;
            GuardarAtual();
            atual = novo;

            carregando = true;
            nome.Text = atual.Nome;
            grade.Rows.Clear();
            foreach (var c in atual.Campos)
            {
                int i = grade.Rows.Add(c.Rotulo, NomesTipo[Math.Max(0, Array.IndexOf(ChavesTipo, (c.Tipo ?? "").ToLowerInvariant()))], c.Obrigatorio,
                    c.Opcoes == null ? "" : string.Join("; ", c.Opcoes), c.Hoje, c.AoLado);
                grade.Rows[i].Tag = c.Chave;
            }
            excluir.Enabled = atual.Id != RepositorioFormularios.IdPadrao;
            carregando = false;
        }

        /// <summary>Copia o que está na tela para o formulário atual (ainda em memória, sem gravar).</summary>
        void GuardarAtual()
        {
            if (atual == null) return;
            grade.EndEdit();

            atual.Nome = nome.Text.Trim();
            var campos = new List<CampoForm>();
            // NovaChave consulta atual.Campos para não repetir chaves: reserva antes as de todas as linhas que já têm uma.
            atual.Campos = grade.Rows.Cast<DataGridViewRow>()
                .Where(l => !l.IsNewRow && l.Tag is string)
                .Select(l => new CampoForm { Chave = (string)l.Tag }).ToList();
            foreach (DataGridViewRow linha in grade.Rows)
            {
                if (linha.IsNewRow) continue;
                var rotulo = Convert.ToString(linha.Cells[0].Value ?? "").Trim();
                if (rotulo == "") continue;

                var chave = linha.Tag as string;
                if (chave == null)
                {
                    chave = atual.NovaChave(rotulo);
                    linha.Tag = chave;
                    atual.Campos.Add(new CampoForm { Chave = chave });
                }
                int tipo = Array.IndexOf(NomesTipo, Convert.ToString(linha.Cells[1].Value));
                var campo = new CampoForm
                {
                    Chave = chave, Rotulo = rotulo, Tipo = ChavesTipo[Math.Max(0, tipo)],
                    Obrigatorio = linha.Cells[2].Value is bool && (bool)linha.Cells[2].Value
                };
                if (campo.EhLista)
                    campo.Opcoes = Convert.ToString(linha.Cells[3].Value ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim()).Where(o => o != "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (campo.EhData) campo.Hoje = linha.Cells[4].Value is bool && (bool)linha.Cells[4].Value;
                campo.AoLado = linha.Cells[5].Value is bool && (bool)linha.Cells[5].Value;
                campos.Add(campo);
            }
            atual.Campos = campos;
            // O nome na lista acompanha o que foi digitado.
            int posicao = lista.Items.IndexOf(atual);
            if (posicao >= 0)
            {
                bool estava = carregando;
                int selecionado = lista.SelectedIndex;
                carregando = true;
                lista.Items[posicao] = atual;
                lista.SelectedIndex = selecionado;
                carregando = estava;
            }
        }

        void Novo()
        {
            GuardarAtual();
            var m = new ModeloFormulario { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Nome = "Novo formulário", Campos = new List<CampoForm>() };
            modelos.Add(m);
            alterado = true;
            carregando = true;
            lista.Items.Add(m);
            carregando = false;
            lista.SelectedItem = m;
            nome.Focus();
            nome.SelectAll();
        }

        void Excluir()
        {
            if (atual == null || atual.Id == RepositorioFormularios.IdPadrao) return;
            var texto = "Excluir o formulário \"" + atual.Nome + "\"? Os sistemas que usavam ele passam a usar o formulário padrão.";
            if (MessageBox.Show(this, texto, "Excluir formulário", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var removido = atual;
            atual = null;
            modelos.Remove(removido);
            alterado = true;
            carregando = true;
            lista.Items.Remove(removido);
            carregando = false;
            lista.SelectedIndex = 0;
        }

        void Mover(int sentido)
        {
            if (grade.CurrentRow == null || grade.CurrentRow.IsNewRow) return;
            int de = grade.CurrentRow.Index, para = de + sentido;
            if (para < 0 || para >= grade.Rows.Count || grade.Rows[para].IsNewRow) return;

            grade.EndEdit();
            var a = grade.Rows[de];
            var b = grade.Rows[para];
            for (int i = 0; i < grade.Columns.Count; i++)
            {
                var valor = a.Cells[i].Value;
                a.Cells[i].Value = b.Cells[i].Value;
                b.Cells[i].Value = valor;
            }
            var tag = a.Tag; a.Tag = b.Tag; b.Tag = tag;
            grade.CurrentCell = b.Cells[0];
            alterado = true;
        }

        void RemoverCampo()
        {
            if (grade.CurrentRow == null || grade.CurrentRow.IsNewRow) return;
            grade.Rows.Remove(grade.CurrentRow);
            alterado = true;
        }

        // ---------- Gravação ----------

        bool Salvar()
        {
            GuardarAtual();

            foreach (var m in modelos)
            {
                if (m.Nome == "") return Recusar("Todo formulário precisa de um nome.", m);
                if (modelos.Any(x => !ReferenceEquals(x, m) && string.Equals(x.Nome, m.Nome, StringComparison.OrdinalIgnoreCase)))
                    return Recusar("Já existe outro formulário chamado \"" + m.Nome + "\". Use nomes diferentes.", m);
                if (m.Campos.Count == 0) return Recusar("O formulário \"" + m.Nome + "\" ainda não tem campos.", m);
                var semOpcoes = m.Campos.FirstOrDefault(c => c.EhLista && (c.Opcoes == null || c.Opcoes.Count == 0));
                if (semOpcoes != null) return Recusar("O campo \"" + semOpcoes.Rotulo + "\" é uma lista: escreva as opções na coluna \"Opções da lista\", separadas por ponto e vírgula.", m);
            }

            var removidos = RepositorioFormularios.Todos().Where(antigo => !modelos.Any(m => m.Id == antigo.Id)).ToList();
            RepositorioFormularios.Salvar(modelos.Select(m => m.Clonar()).ToList());
            foreach (var r in removidos) Dados.Limpar(r.Id);
            alterado = false;
            Aviso.Mostrar("Formulários salvos.");
            return true;
        }

        bool Recusar(string mensagem, ModeloFormulario m)
        {
            lista.SelectedItem = m;
            MessageBox.Show(this, mensagem, "Meus formulários", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (alterado)
            {
                var r = MessageBox.Show(this, "Salvar as alterações nos formulários?", "Meus formulários", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Salvar())) e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }
}
