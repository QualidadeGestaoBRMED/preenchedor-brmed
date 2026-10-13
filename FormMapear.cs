using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Menu que aparece ao lado do campo apontado: o colaborador escolhe qual dado vai nele.</summary>
    class FormMapear : Form
    {
        public FormMapear(Captura captura, Point perto)
        {
            Text = "Mapear campo";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            KeyPreview = true;

            var perfilAtual = RepositorioPerfis.PerfilDeMapeamento(captura.Processo, captura.TituloJanela);
            var jaMapeado = perfilAtual != null && perfilAtual.Campos != null
                ? perfilAtual.Campos.FirstOrDefault(c => c.MesmoCampo(captura.Campo)) : null;
            // Um sistema com campos já mapeados fica preso ao seu formulário; para trocar, use "Sistemas mapeados…".
            bool travado = perfilAtual != null && perfilAtual.Campos != null && perfilAtual.Campos.Count > 0;
            var modelos = RepositorioFormularios.Todos();
            var idInicial = perfilAtual != null && travado ? perfilAtual.FormularioId : Dados.FormularioAtivo;

            var painel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Location = new Point(0, 0)
            };

            painel.Controls.Add(new Label
            {
                Text = "Campo: " + captura.Campo.Descricao, Font = Tema.TextoForte(9.5f), ForeColor = Tema.Marinho,
                AutoSize = true, MaximumSize = new Size(230, 0), Margin = new Padding(0, 0, 0, 2), UseMnemonic = false
            });
            painel.Controls.Add(new Label
            {
                Text = captura.Processo + " · " + captura.Campo.Identificacao, Font = Tema.Texto(8.5f), ForeColor = Tema.Suave,
                AutoSize = true, MaximumSize = new Size(230, 0), Margin = new Padding(0, 0, 0, 10), UseMnemonic = false
            });

            var opcoes = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0)
            };

            ComboBox seletor = null;
            if (modelos.Count > 1)
            {
                painel.Controls.Add(new Label
                {
                    Text = travado ? "Formulário deste sistema:" : "Dados de qual formulário?", Font = Tema.Texto(8.5f), ForeColor = Tema.Suave,
                    AutoSize = true, MaximumSize = new Size(230, 0), Margin = new Padding(0, 0, 0, 2)
                });
                seletor = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Nome", Width = 230, Font = Tema.Texto(9.5f),
                    Margin = new Padding(0, 0, 0, 8), Enabled = !travado, DataSource = modelos
                };
                seletor.DropDownWidth = 320;
                seletor.SelectedItem = modelos.FirstOrDefault(m => m.Id == idInicial) ?? modelos[0];
                painel.Controls.Add(seletor);
            }
            painel.Controls.Add(opcoes);

            Action montar = () =>
            {
                var modelo = seletor != null ? (ModeloFormulario)seletor.SelectedItem : modelos[0];
                Dados.FormularioAtivo = modelo.Id;
                opcoes.SuspendLayout();
                foreach (var antigo in opcoes.Controls.Cast<Control>().ToList()) { opcoes.Controls.Remove(antigo); antigo.Dispose(); }

                foreach (var d in Dados.Destinos(modelo))
                {
                    var atual = jaMapeado != null && jaMapeado.Dado == d.Chave;
                    var b = BotaoMenu((atual ? "✓  " : "") + d.Rotulo, d.Cor, atual);
                    var chave = d.Chave;
                    b.Click += (s, e) =>
                    {
                        captura.Campo.Dado = chave;
                        RepositorioPerfis.MapearCampo(captura.Processo, captura.TituloJanela, captura.Campo, modelo.Id);
                        Aviso.Mostrar("\"" + captura.Campo.Descricao + "\" vai receber " + Dados.Rotulo(modelo.Id, chave) + ".");
                        Close();
                    };
                    opcoes.Controls.Add(b);
                }

                if (jaMapeado != null)
                {
                    var remover = BotaoMenu("Remover mapeamento", Tema.Cobre, false);
                    remover.ForeColor = Tema.Cobre;
                    remover.Image = null;
                    remover.Click += (s, e) =>
                    {
                        RepositorioPerfis.RemoverCampo(captura.Processo, captura.TituloJanela, captura.Campo);
                        Aviso.Mostrar("Mapeamento removido.");
                        Close();
                    };
                    opcoes.Controls.Add(remover);
                }
                opcoes.ResumeLayout(true);
            };
            montar();
            if (seletor != null) seletor.SelectedIndexChanged += (s, e) => { montar(); Posicionar(perto); };

            Controls.Add(painel);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            Load += (s, e) => Posicionar(perto);
        }

        /// <summary>Posiciona ao lado do mouse sem sair da tela.</summary>
        void Posicionar(Point perto)
        {
            var area = Screen.FromPoint(perto).WorkingArea;
            int x = Math.Min(perto.X + 16, area.Right - Width - 8);
            int y = Math.Min(perto.Y + 16, area.Bottom - Height - 8);
            Location = new Point(Math.Max(area.Left + 8, x), Math.Max(area.Top + 8, y));
        }

        static Button BotaoMenu(string texto, Color cor, bool destaque)
        {
            const int largura = 230;
            var fonte = destaque ? Tema.TextoForte(10f) : Tema.Texto(10f);
            // O texto quebra linha quando o rótulo é longo; a altura do botão cresce para caber.
            // Descontado: recuo de 6 + bolinha de 10 + folgas.
            int altura = Math.Max(34, Tema.AlturaTexto(texto, fonte, largura - 34) + 14);
            var b = new Button
            {
                Text = texto, TextAlign = ContentAlignment.MiddleLeft, ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText, Image = Tema.Bolinha(cor),
                Font = fonte, ForeColor = Tema.Marinho, UseMnemonic = false,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, Size = new Size(largura, altura),
                Margin = new Padding(0, 0, 0, 2), Padding = new Padding(6, 0, 0, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Tema.Fundo;
            return b;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }
    }
}
