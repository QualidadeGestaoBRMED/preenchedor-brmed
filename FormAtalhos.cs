using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Tela para trocar os atalhos globais. Clique no campo e pressione a combinação desejada.</summary>
    class FormAtalhos : Form
    {
        readonly Dictionary<Acao, Combinacao> atual = Atalhos.Carregar();
        readonly Dictionary<Acao, TextBox> campos = new Dictionary<Acao, TextBox>();

        public FormAtalhos()
        {
            Text = "Atalhos · Preenchedor de Cadastro";
            Icon = Tema.CriarIcone();
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Tema.Fundo;
            Font = Tema.Texto(9.5f);
            ClientSize = new Size(520, 360);

            var dica = new RotuloQuebra
            {
                Dock = DockStyle.Top, Padding = new Padding(14, 12, 14, 8), ForeColor = Tema.Marinho,
                Text = "Clique no campo e pressione a combinação desejada (use Ctrl, Alt ou Shift com uma tecla). Backspace limpa o campo."
            };

            var grade = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(14, 4, 14, 4) };
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            foreach (var acao in Atalhos.Todas)
            {
                var a = acao;
                var caixa = new TextBox { ReadOnly = true, Width = 160, BackColor = Color.White, TabStop = true, Text = atual[a].ToString() };
                caixa.KeyDown += (s, e) => AoTeclar(a, caixa, e);
                caixa.KeyPress += (s, e) => e.Handled = true;
                campos[a] = caixa;
                grade.Controls.Add(new Label { Text = Atalhos.Descricao(a), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 8) });
                grade.Controls.Add(caixa);
            }

            var salvar = new BotaoMarca("Salvar", EstiloBotao.Primario) { AutoSize = true };
            var padrao = new BotaoMarca("Restaurar padrão", EstiloBotao.Contorno) { AutoSize = true };
            var cancelar = new BotaoMarca("Cancelar", EstiloBotao.Contorno) { AutoSize = true };
            salvar.Click += (s, e) => Salvar();
            padrao.Click += (s, e) =>
            {
                foreach (var a in Atalhos.Todas) { atual[a] = Atalhos.Padrao(a); campos[a].Text = atual[a].ToString(); }
            };
            cancelar.Click += (s, e) => Close();
            var acoes = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 10, 12, 10), BackColor = Color.White
            };
            acoes.Controls.AddRange(new Control[] { salvar, padrao, cancelar });

            Controls.Add(grade);
            Controls.Add(dica);
            Controls.Add(acoes);
            ClientSize = new Size(520, 330);
        }

        void AoTeclar(Acao a, TextBox caixa, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            var tecla = e.KeyCode;
            if (tecla == Keys.Back && e.Modifiers == Keys.None) { atual[a] = new Combinacao(); caixa.Text = ""; return; }
            if (tecla == Keys.ControlKey || tecla == Keys.Menu || tecla == Keys.ShiftKey || tecla == Keys.LWin || tecla == Keys.RWin) return;
            uint mod = 0;
            if (e.Control) mod |= Nativo.MOD_CONTROL;
            if (e.Alt) mod |= Nativo.MOD_ALT;
            if (e.Shift) mod |= Nativo.MOD_SHIFT;
            var c = new Combinacao { Modificadores = mod, Tecla = tecla };
            atual[a] = c;
            caixa.Text = c.ToString();
        }

        void Salvar()
        {
            foreach (var a in Atalhos.Todas)
                if (!atual[a].Valida)
                {
                    MessageBox.Show(this, "\"" + Atalhos.Descricao(a) + "\" precisa de Ctrl, Alt ou Shift junto com uma tecla.", "Atalhos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            var repetido = Atalhos.Todas.GroupBy(a => atual[a].ToString()).FirstOrDefault(g => g.Count() > 1);
            if (repetido != null)
            {
                MessageBox.Show(this, "O atalho " + repetido.Key + " está em mais de uma ação.", "Atalhos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Atalhos.Salvar(atual);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
