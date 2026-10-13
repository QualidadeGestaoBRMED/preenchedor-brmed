using System;
using System.Drawing;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
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
