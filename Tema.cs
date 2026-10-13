using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Cores e fontes da identidade BR MED.</summary>
    static class Tema
    {
        // Business
        public static readonly Color Marinho = ColorTranslator.FromHtml("#193B4F");
        public static readonly Color Petroleo = ColorTranslator.FromHtml("#007891");
        // Human / Sophisticated
        public static readonly Color Verde = ColorTranslator.FromHtml("#2CAD6E");
        public static readonly Color Cobre = ColorTranslator.FromHtml("#A05E1E");
        public static readonly Color Ambar = ColorTranslator.FromHtml("#CC851E");
        // Apoio
        public static readonly Color Fundo = ColorTranslator.FromHtml("#F3F3F3");
        public static readonly Color Linha = ColorTranslator.FromHtml("#DFE0E2");
        public static readonly Color LinhaForte = ColorTranslator.FromHtml("#BFC3C6");
        public static readonly Color Suave = ColorTranslator.FromHtml("#767A7B");

        static readonly string familiaTitulo = Primeira("Work Sans", "Segoe UI Semibold", "Segoe UI");
        static readonly string familiaTexto = Primeira("Roboto", "Segoe UI");

        public static Font Titulo(float tamanho) { return new Font(familiaTitulo, tamanho, FontStyle.Bold); }
        public static Font Texto(float tamanho) { return new Font(familiaTexto, tamanho, FontStyle.Regular); }
        public static Font TextoForte(float tamanho) { return new Font(familiaTexto, tamanho, FontStyle.Bold); }

        // Usa Work Sans e Roboto se estiverem instaladas; senão, Segoe UI (padrão do Windows).
        static string Primeira(params string[] candidatas)
        {
            using (var instaladas = new InstalledFontCollection())
            {
                var nomes = instaladas.Families.Select(f => f.Name).ToList();
                foreach (var c in candidatas)
                    if (nomes.Any(n => string.Equals(n, c, StringComparison.OrdinalIgnoreCase))) return c;
            }
            return "Segoe UI";
        }

        /// <summary>Altura que um texto ocupa quando quebrado em linhas dentro da largura dada.</summary>
        public static int AlturaTexto(string texto, Font fonte, int largura)
        {
            // Folga de 6 px na largura: garante que o rótulo real nunca precise de mais linhas que a medida.
            var tamanho = TextRenderer.MeasureText(string.IsNullOrEmpty(texto) ? " " : texto, fonte,
                new Size(Math.Max(20, largura - 6), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return tamanho.Height + 2;
        }

        /// <summary>Ícone da bandeja: prancheta branca sobre quadrado petróleo.</summary>
        public static Icon CriarIcone()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var fundo = new SolidBrush(Petroleo)) g.FillRectangle(fundo, 0, 0, 32, 32);
                using (var branco = new SolidBrush(Color.White)) g.FillRectangle(branco, 8, 6, 16, 22);
                using (var marinho = new SolidBrush(Marinho)) g.FillRectangle(marinho, 12, 4, 8, 5);
                using (var traco = new Pen(Petroleo, 2))
                {
                    g.DrawLine(traco, 11, 14, 21, 14);
                    g.DrawLine(traco, 11, 18, 21, 18);
                    g.DrawLine(traco, 11, 22, 17, 22);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        /// <summary>Desenha a bolinha colorida que identifica cada dado.</summary>
        public static Bitmap Bolinha(Color cor, int tamanho = 10)
        {
            var bmp = new Bitmap(tamanho, tamanho);
            using (var g = Graphics.FromImage(bmp))
            using (var pincel = new SolidBrush(cor))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(pincel, 0, 0, tamanho - 1, tamanho - 1);
            }
            return bmp;
        }
    }
}
