using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace PreenchedorBRMED
{
    class Dado
    {
        public string Chave;
        public string Rotulo;
        public Color Cor;
        public Dado(string chave, string rotulo, Color cor) { Chave = chave; Rotulo = rotulo; Cor = cor; }
    }

    /// <summary>
    /// Os dados digitados ficam só na memória do programa e somem quando ele é fechado (LGPD).
    /// Cada formulário (ver Formularios.cs) guarda os seus valores separadamente.
    /// </summary>
    static class Dados
    {
        static readonly Color[] Paleta = { Tema.Marinho, Tema.Petroleo, Tema.Cobre, Tema.Ambar, Tema.Verde };

        static readonly Dictionary<string, Dictionary<string, string>> atuais = new Dictionary<string, Dictionary<string, string>>();
        static readonly object trava = new object();

        /// <summary>Último formulário usado; é o sugerido ao abrir o formulário e ao mapear um sistema novo.</summary>
        public static string FormularioAtivo = RepositorioFormularios.IdPadrao;

        public static Color CorDoCampo(ModeloFormulario modelo, CampoForm campo)
        {
            return Paleta[Math.Max(0, modelo.Campos.IndexOf(campo)) % Paleta.Length];
        }

        /// <summary>O que pode ir para um campo de outro sistema. O CPF tem duas versões.</summary>
        public static Dado[] Destinos(ModeloFormulario modelo)
        {
            var lista = new List<Dado>();
            foreach (var c in modelo.Campos)
            {
                var cor = CorDoCampo(modelo, c);
                if (c.EhCpf)
                {
                    lista.Add(new Dado(c.Chave, c.Rotulo + " com pontos", cor));
                    lista.Add(new Dado(c.Chave + SufixoNumeros, c.Rotulo + " só números", cor));
                }
                else lista.Add(new Dado(c.Chave, c.Rotulo, cor));
            }
            return lista.ToArray();
        }

        const string SufixoNumeros = "_numeros";

        public static bool TemDados(string formularioId)
        {
            var id = RepositorioFormularios.Obter(formularioId).Id; // formulário excluído cai no padrão, como em Valor()
            lock (trava) return atuais.ContainsKey(id);
        }

        public static Dictionary<string, string> Copia(string formularioId)
        {
            lock (trava)
            {
                Dictionary<string, string> valores;
                return atuais.TryGetValue(formularioId, out valores) ? new Dictionary<string, string>(valores) : new Dictionary<string, string>();
            }
        }

        public static void Salvar(string formularioId, Dictionary<string, string> dados)
        {
            lock (trava) atuais[formularioId] = new Dictionary<string, string>(dados);
        }

        public static void Limpar() { lock (trava) atuais.Clear(); }

        public static void Limpar(string formularioId) { lock (trava) atuais.Remove(formularioId); }

        public static string Valor(string formularioId, string chave)
        {
            var modelo = RepositorioFormularios.Obter(formularioId);
            bool numeros = chave.EndsWith(SufixoNumeros) && modelo.Campo(chave) == null;
            var chaveCampo = numeros ? chave.Substring(0, chave.Length - SufixoNumeros.Length) : chave;
            var campo = modelo.Campo(chaveCampo);
            if (campo == null) return "";

            string bruto = null;
            lock (trava)
            {
                Dictionary<string, string> valores;
                if (atuais.TryGetValue(modelo.Id, out valores)) valores.TryGetValue(chaveCampo, out bruto);
            }
            if (campo.EhCpf) return numeros ? SomenteDigitos(bruto) : FormatarCpf(bruto);
            return numeros ? "" : bruto ?? "";
        }

        public static string Rotulo(string formularioId, string chave)
        {
            var d = Destinos(RepositorioFormularios.Obter(formularioId)).FirstOrDefault(x => x.Chave == chave);
            return d != null ? d.Rotulo : chave;
        }

        public static Color Cor(string formularioId, string chave)
        {
            var d = Destinos(RepositorioFormularios.Obter(formularioId)).FirstOrDefault(x => x.Chave == chave);
            return d != null ? d.Cor : Tema.Suave;
        }

        public static string SomenteDigitos(string s) { return Regex.Replace(s ?? "", @"\D", ""); }

        public static string FormatarCpf(string s)
        {
            var d = SomenteDigitos(s);
            if (d.Length > 11) d = d.Substring(0, 11);
            if (d.Length > 9) return d.Substring(0, 3) + "." + d.Substring(3, 3) + "." + d.Substring(6, 3) + "-" + d.Substring(9);
            if (d.Length > 6) return d.Substring(0, 3) + "." + d.Substring(3, 3) + "." + d.Substring(6);
            if (d.Length > 3) return d.Substring(0, 3) + "." + d.Substring(3);
            return d;
        }

        public static bool CpfValido(string cpf)
        {
            var d = SomenteDigitos(cpf);
            if (d.Length != 11 || d.Distinct().Count() == 1) return false;
            for (int n = 9; n <= 10; n++)
            {
                int soma = 0;
                for (int i = 0; i < n; i++) soma += (d[i] - '0') * (n + 1 - i);
                int resto = (soma * 10) % 11;
                if (resto == 10) resto = 0;
                if (resto != d[n] - '0') return false;
            }
            return true;
        }
    }
}
