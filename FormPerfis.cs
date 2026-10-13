using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Lista os sistemas mapeados e permite excluir perfis e campos.</summary>
    class FormPerfis : Form
    {
        readonly ListView listaPerfis = NovaLista();
        readonly ListView listaCampos = NovaLista();

        public FormPerfis()
        {
            Text = "Sistemas mapeados · Preenchedor de Cadastro";
            Icon = Tema.CriarIcone();
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Tema.Fundo;
            ClientSize = new Size(760, 520);
            MinimumSize = new Size(640, 420);
            Font = Tema.Texto(9.5f);

            listaPerfis.Columns.Add("Sistema", 250);
            listaPerfis.Columns.Add("Programa", 130);
            listaPerfis.Columns.Add("Formulário", 150);
            listaPerfis.Columns.Add("Método", 100);
            listaPerfis.Columns.Add("Campos", 70);
            listaCampos.Columns.Add("Dado", 150);
            listaCampos.Columns.Add("Campo no sistema", 230);
            listaCampos.Columns.Add("Como é localizado", 300);

            var trocarForm = Botao("Trocar formulário…", false);
            var excluirPerfil = Botao("Excluir sistema", false);
            var excluirCampo = Botao("Excluir campo", false);
            var editar = Botao("Editar arquivo de perfis", false);
            var recarregar = Botao("Recarregar", false);
            var fechar = Botao("Fechar", true);

            trocarForm.Click += (s, e) => TrocarFormulario();
            excluirPerfil.Click += (s, e) => ExcluirPerfil();
            excluirCampo.Click += (s, e) => ExcluirCampo();
            editar.Click += (s, e) => Process.Start("notepad.exe", "\"" + RepositorioPerfis.Caminho + "\"");
            recarregar.Click += (s, e) => { RepositorioPerfis.Recarregar(); Preencher(); };
            fechar.Click += (s, e) => Close();
            listaPerfis.SelectedIndexChanged += (s, e) => MostrarCampos();

            // Os botões passam para a linha de baixo quando a janela é estreita, em vez de ficarem cortados.
            var acoes = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 10, 12, 10), BackColor = Color.White
            };
            acoes.Controls.AddRange(new Control[] { trocarForm, excluirPerfil, excluirCampo, editar, recarregar, fechar });

            var dica = new RotuloQuebra
            {
                Dock = DockStyle.Top, Padding = new Padding(14, 12, 14, 8), ForeColor = Tema.Marinho,
                Text = "Para mapear: abra o sistema, aponte o mouse para um campo e pressione " + Atalhos.Texto(Acao.Mapear) + " (colar) ou " + Atalhos.Texto(Acao.MapearCaptura) + " (capturar)."
            };

            var divisor = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 200, Panel1MinSize = 120 };
            divisor.Panel1.Controls.Add(listaPerfis);
            divisor.Panel2.Controls.Add(listaCampos);
            divisor.Panel1.Padding = divisor.Panel2.Padding = new Padding(12, 0, 12, 8);

            Controls.Add(divisor);
            Controls.Add(dica);
            Controls.Add(acoes);
            Preencher();
        }

        static ListView NovaLista()
        {
            return new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, ShowItemToolTips = true };
        }

        static Button Botao(string texto, bool principal)
        {
            return new BotaoMarca(texto, principal ? EstiloBotao.Primario : EstiloBotao.Contorno) { AutoSize = true };
        }

        void Preencher()
        {
            listaPerfis.Items.Clear();
            foreach (var p in RepositorioPerfis.Todos())
            {
                var qtd = p.PorTeclado ? (p.Sequencia == null ? 0 : p.Sequencia.Count(x => !x.StartsWith("{"))) : (p.Campos == null ? 0 : p.Campos.Count);
                var item = new ListViewItem(new[] { p.Nome, p.Processo, RepositorioFormularios.Obter(p.FormularioId).Nome, p.PorTeclado ? "Digitação" : "Automação", qtd.ToString() }) { Tag = p };
                listaPerfis.Items.Add(item);
            }
            if (listaPerfis.Items.Count > 0) listaPerfis.Items[0].Selected = true;
            MostrarCampos();
        }

        Perfil Selecionado { get { return listaPerfis.SelectedItems.Count == 0 ? null : (Perfil)listaPerfis.SelectedItems[0].Tag; } }

        void MostrarCampos()
        {
            listaCampos.Items.Clear();
            var p = Selecionado;
            if (p == null) return;
            if (p.PorTeclado)
            {
                if (p.Sequencia == null) return;
                foreach (var passo in p.Sequencia)
                    listaCampos.Items.Add(new ListViewItem(new[] { passo.StartsWith("{") ? passo : Dados.Rotulo(p.FormularioId, passo), "", "Digitado em sequência" }));
                return;
            }
            if (p.Campos == null) return;
            foreach (var c in p.Campos)
                listaCampos.Items.Add(new ListViewItem(new[] { Dados.Rotulo(p.FormularioId, c.Dado) + (c.Captura ? "  (captura)" : ""), c.Descricao ?? "", c.Identificacao }) { Tag = c });
        }

        void TrocarFormulario()
        {
            var p = Selecionado;
            if (p == null) return;
            var modelos = RepositorioFormularios.Todos();
            var escolha = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Nome", Dock = DockStyle.Top, DataSource = modelos
            };
            escolha.SelectedItem = modelos.FirstOrDefault(m => m.Id == RepositorioFormularios.Obter(p.FormularioId).Id);

            var ok = new BotaoMarca("Usar este formulário", EstiloBotao.Primario) { AutoSize = true, DialogResult = DialogResult.OK };
            var cancelar = new BotaoMarca("Cancelar", EstiloBotao.Contorno) { AutoSize = true, DialogResult = DialogResult.Cancel };
            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 10, 0, 0) };
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancelar);
            var texto = new RotuloQuebra { Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8), Text = "De qual formulário o sistema \"" + p.Nome + "\" recebe os dados?" };

            using (var dialogo = new Form
            {
                Text = "Trocar formulário", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(380, 160),
                Padding = new Padding(14), AcceptButton = ok, CancelButton = cancelar, Font = Font
            })
            {
                dialogo.Controls.Add(botoes);
                dialogo.Controls.Add(escolha);
                dialogo.Controls.Add(texto);
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
            }

            var novo = (ModeloFormulario)escolha.SelectedItem;
            if (novo.Id == RepositorioFormularios.Obter(p.FormularioId).Id) return;

            // Campos que apontam para um dado inexistente no novo formulário deixam de ter sentido.
            var validos = Dados.Destinos(novo).Select(d => d.Chave).ToList();
            int perdidos = p.Campos == null ? 0 : p.Campos.Count(c => !validos.Contains(c.Dado));
            if (perdidos > 0 && MessageBox.Show(this,
                    perdidos + (perdidos == 1 ? " campo mapeado não existe" : " campos mapeados não existem") + " no formulário \"" + novo.Nome + "\" e será removido do mapeamento. Continuar?",
                    "Trocar formulário", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            if (p.Campos != null) p.Campos.RemoveAll(c => !validos.Contains(c.Dado));
            p.FormularioSalvo = novo.Id == RepositorioFormularios.IdPadrao ? null : novo.Id;
            RepositorioPerfis.SalvarTodos(RepositorioPerfis.Todos());
            Preencher();
        }

        void ExcluirPerfil()
        {
            var p = Selecionado;
            if (p == null) return;
            if (MessageBox.Show(this, "Excluir o mapeamento de \"" + p.Nome + "\"?", "Excluir sistema", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var todos = RepositorioPerfis.Todos();
            todos.RemoveAll(x => ReferenceEquals(x, p));
            RepositorioPerfis.SalvarTodos(todos);
            Preencher();
        }

        void ExcluirCampo()
        {
            var p = Selecionado;
            if (p == null || p.Campos == null || listaCampos.SelectedItems.Count == 0) return;
            var c = listaCampos.SelectedItems[0].Tag as CampoMapeado;
            if (c == null) return;
            p.Campos.Remove(c);
            RepositorioPerfis.SalvarTodos(RepositorioPerfis.Todos());
            MostrarCampos();
        }
    }
}
