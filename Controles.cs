using System;
using System.Drawing;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    enum EstiloBotao { Primario, Contorno }

    /// <summary>Botão no padrão BR MED: cantos de 6 px, teal na ação principal, contorno cinza no secundário.</summary>
    class BotaoMarca : Button
    {
        readonly EstiloBotao estilo;
        bool sobre, apertado;

        public BotaoMarca(string texto, EstiloBotao estilo)
        {
            this.estilo = estilo;
            Text = texto;
            UseMnemonic = false;
            FlatStyle = FlatStyle.Flat;
            Font = Tema.TextoForte(10f);
            Cursor = Cursors.Hand;
            Height = 38;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { sobre = false; apertado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { apertado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { apertado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var t = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return new Size(t.Width + 36, Math.Max(Height, 38));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color fundo, borda, texto;
            if (estilo == EstiloBotao.Primario)
            {
                fundo = apertado ? Tema.Petroleo900 : sobre ? Tema.Petroleo700 : Tema.Petroleo;
                borda = fundo;
                texto = Color.White;
            }
            else
            {
                fundo = apertado ? Tema.Linha : sobre ? Tema.Fundo : Color.White;
                borda = Tema.LinhaForte;
                texto = Tema.Petroleo;
            }
            if (!Enabled) { fundo = Tema.Fundo; borda = Tema.Linha; texto = Tema.Suave; }

            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var caminho = Arredondado(area, 6))
            using (var pincel = new SolidBrush(fundo))
            using (var caneta = new Pen(borda))
            {
                g.FillPath(pincel, caminho);
                g.DrawPath(caneta, caminho);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, texto,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
            {
                var foco = Rectangle.Inflate(area, -3, -3);
                ControlPaint.DrawFocusRectangle(g, foco);
            }
        }

        static System.Drawing.Drawing2D.GraphicsPath Arredondado(Rectangle r, int raio)
        {
            int d = raio * 2;
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>
    /// Rótulo que quebra linha em vez de cortar o texto: a largura vem do layout (Dock/Anchor)
    /// e a altura é recalculada para caber todo o texto.
    /// </summary>
    class RotuloQuebra : Label
    {
        public RotuloQuebra()
        {
            AutoSize = false;
            UseMnemonic = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Ajustar();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Ajustar();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Ajustar();
        }

        void Ajustar()
        {
            int largura = Width - Padding.Horizontal;
            if (largura <= 0) return;
            int altura = Tema.AlturaTexto(Text, Font, largura) + Padding.Vertical;
            if (altura != Height) Height = altura;
        }
    }
}
