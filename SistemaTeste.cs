using System.Drawing;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>
    /// Imita um sistema desktop de terceiros para testar a colagem sem depender dos sistemas reais.
    /// Os nomes dos controles (txtNomeCompleto, cboDepartamento...) viram o AutomationId que o perfil usa.
    /// </summary>
    class SistemaTeste : Form
    {
        public SistemaTeste()
        {
            Text = "Sistema de teste · Ficha de integração";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(520, 330);
            MinimumSize = new Size(500, 340);
            Font = new Font("Segoe UI", 9.5f);

            var campos = new[]
            {
                new[] { "Nome completo:", "txtNomeCompleto" },
                new[] { "E-mail corporativo:", "txtEmail" },
                new[] { "Cargo:", "txtCargo" },
                new[] { "Departamento:", "cboDepartamento" },
                new[] { "CPF:", "txtCpf" },
            };

            int y = 24;
            foreach (var c in campos)
            {
                Controls.Add(new Label { Text = c[0], Location = new Point(24, y + 4), AutoSize = true });
                Control caixa;
                if (c[1].StartsWith("cbo"))
                {
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                    combo.Items.AddRange(new object[] { "Customer Experience", "Financeiro", "Operações", "Recursos Humanos", "Tecnologia" });
                    caixa = combo;
                }
                else caixa = new TextBox();
                caixa.Name = c[1];
                caixa.AccessibleName = c[0].TrimEnd(':');
                caixa.Location = new Point(170, y);
                caixa.Width = 320;
                caixa.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // acompanha a largura da janela
                Controls.Add(caixa);
                y += 44;
            }

            var enviar = new Button { Text = "Enviar", Location = new Point(390, y + 8), Size = new Size(100, 32) };
            enviar.Click += (s, e) =>
            {
                var resumo = "";
                foreach (var c in campos) resumo += c[0] + " " + Controls[c[1]].Text + "\n";
                MessageBox.Show(this, resumo, "Dados recebidos pelo sistema de teste");
            };
            enviar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(enviar);

            Controls.Add(new Label
            {
                Text = "Clique no primeiro campo e pressione Ctrl+Alt+V.", ForeColor = Color.DimGray,
                Location = new Point(24, y + 14), AutoSize = true
            });
        }
    }
}
