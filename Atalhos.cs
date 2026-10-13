using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    enum Acao { Formulario, Colar, Mapear, MapearCaptura, Capturar }

    /// <summary>Uma combinação de teclas global: modificadores do Windows + uma tecla.</summary>
    struct Combinacao
    {
        public uint Modificadores;
        public Keys Tecla;

        public bool Valida { get { return Modificadores != 0 && Tecla != Keys.None; } }

        public override string ToString()
        {
            var partes = new List<string>();
            if ((Modificadores & Nativo.MOD_CONTROL) != 0) partes.Add("Ctrl");
            if ((Modificadores & Nativo.MOD_ALT) != 0) partes.Add("Alt");
            if ((Modificadores & Nativo.MOD_SHIFT) != 0) partes.Add("Shift");
            if (Tecla != Keys.None) partes.Add(NomeTecla(Tecla));
            return string.Join("+", partes);
        }

        static string NomeTecla(Keys t)
        {
            if (t >= Keys.D0 && t <= Keys.D9) return ((int)t - (int)Keys.D0).ToString();
            return t.ToString();
        }

        public static bool TentarLer(string texto, out Combinacao resultado)
        {
            resultado = new Combinacao();
            if (string.IsNullOrWhiteSpace(texto)) return false;
            foreach (var parte in texto.Split('+').Select(p => p.Trim()))
            {
                if (parte.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) resultado.Modificadores |= Nativo.MOD_CONTROL;
                else if (parte.Equals("Alt", StringComparison.OrdinalIgnoreCase)) resultado.Modificadores |= Nativo.MOD_ALT;
                else if (parte.Equals("Shift", StringComparison.OrdinalIgnoreCase)) resultado.Modificadores |= Nativo.MOD_SHIFT;
                else
                {
                    Keys t;
                    string nome = parte.Length == 1 && char.IsDigit(parte[0]) ? "D" + parte : parte;
                    if (!Enum.TryParse(nome, true, out t)|| resultado.Tecla != Keys.None) return false;
                    resultado.Tecla = t;
                }
            }
            return resultado.Valida;
        }
    }

    /// <summary>Atalhos globais configuráveis, guardados em atalhos.txt (uma linha "Acao=Ctrl+Alt+F" por atalho).</summary>
    static class Atalhos
    {
        static readonly string Caminho = Path.Combine(RepositorioPerfis.Pasta, "atalhos.txt");

        public static readonly Acao[] Todas = (Acao[])Enum.GetValues(typeof(Acao));

        public static string Descricao(Acao a)
        {
            switch (a)
            {
                case Acao.Formulario: return "Abrir o formulário";
                case Acao.Colar: return "Colar os dados na janela ativa";
                case Acao.Mapear: return "Mapear campo sob o mouse (colar)";
                case Acao.MapearCaptura: return "Mapear campo sob o mouse (capturar)";
                default: return "Capturar os campos da janela ativa";
            }
        }

        public static Combinacao Padrao(Acao a)
        {
            Keys t;
            switch (a)
            {
                case Acao.Formulario: t = Keys.F; break;
                case Acao.Colar: t = Keys.V; break;
                case Acao.Mapear: t = Keys.M; break;
                case Acao.MapearCaptura: t = Keys.K; break;
                default: t = Keys.L; break; // Ctrl+Alt+C já é usado pelo Windows
            }
            return new Combinacao { Modificadores = Nativo.MOD_CONTROL | Nativo.MOD_ALT, Tecla = t };
        }

        public static Dictionary<Acao, Combinacao> Carregar()
        {
            var r = Todas.ToDictionary(a => a, a => Padrao(a));
            try
            {
                if (File.Exists(Caminho))
                    foreach (var linha in File.ReadAllLines(Caminho))
                    {
                        var i = linha.IndexOf('=');
                        Acao a;
                        Combinacao c;
                        if (i > 0 && Enum.TryParse(linha.Substring(0, i).Trim(), out a) && Combinacao.TentarLer(linha.Substring(i + 1), out c)) r[a] = c;
                    }
            }
            catch (IOException) { }
            return r;
        }

        public static void Salvar(Dictionary<Acao, Combinacao> atalhos)
        {
            Directory.CreateDirectory(RepositorioPerfis.Pasta);
            File.WriteAllLines(Caminho, Todas.Select(a => a + "=" + atalhos[a]).ToArray());
        }

        /// <summary>Texto do atalho atual, para as mensagens ("Ctrl+Alt+F").</summary>
        public static string Texto(Acao a) { return Carregar()[a].ToString(); }
    }
}
