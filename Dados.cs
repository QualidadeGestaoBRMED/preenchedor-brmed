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

        /// <summary>O que pode ir para um campo de outro sistema. CPF e RG têm duas versões (com e sem pontos).</summary>
        public static Dado[] Destinos(ModeloFormulario modelo)
        {
            var lista = new List<Dado>();
            foreach (var c in modelo.Campos)
            {
                var cor = CorDoCampo(modelo, c);
                if (c.TemMascara)
                {
                    lista.Add(new Dado(c.Chave, c.Rotulo + " com pontos", cor));
                    lista.Add(new Dado(c.Chave + SufixoNumeros, c.Rotulo + " sem pontos", cor));
                }
                else lista.Add(new Dado(c.Chave, c.Rotulo, cor));
            }
            return lista.ToArray();
        }

        /// <summary>
        /// Uma opção de um campo de lista como destino próprio (ex.: "Sexo: Feminino"), para mapear botões de opção:
        /// cada botão é um ponto da tela, e só é clicado quando o valor do campo é aquela opção.
        /// </summary>
        public static Dado[] DestinosDeOpcao(ModeloFormulario modelo)
        {
            var lista = new List<Dado>();
            foreach (var c in modelo.Campos.Where(x => x.EhLista && x.Opcoes != null))
                foreach (var o in c.Opcoes)
                    lista.Add(new Dado(c.Chave + SepOpcao + o, c.Rotulo + ": " + o, CorDoCampo(modelo, c)));
            return lista.ToArray();
        }

        /// <summary>Todas as chaves que um mapeamento pode usar: os campos, suas versões sem pontos e as opções das listas.</summary>
        public static Dado[] TodosDestinos(ModeloFormulario modelo)
        {
            return Destinos(modelo).Concat(DestinosDeOpcao(modelo)).ToArray();
        }

        const char SepOpcao = '|';

        /// <summary>"sexo|Feminino" vira campo "sexo" e opção "Feminino".</summary>
        public static bool EhOpcao(string chave, out string campo, out string opcao)
        {
            campo = opcao = null;
            int i = (chave ?? "").IndexOf(SepOpcao);
            if (i <= 0) return false;
            campo = chave.Substring(0, i);
            opcao = chave.Substring(i + 1);
            return true;
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
            if (campo.EhDocumento)
            {
                if (ParecaCpf(bruto)) return numeros ? SomenteDigitos(bruto) : FormatarCpf(bruto);
                return numeros ? SomenteAlfanumerico(bruto) : (bruto ?? "").Trim().ToUpperInvariant();
            }
            if (campo.EhRg) return numeros ? SomenteAlfanumerico(bruto) : FormatarRg(bruto);
            return numeros ? "" : bruto ?? "";
        }

        public static string Rotulo(string formularioId, string chave)
        {
            var d = TodosDestinos(RepositorioFormularios.Obter(formularioId)).FirstOrDefault(x => x.Chave == chave);
            return d != null ? d.Rotulo : chave;
        }

        public static Color Cor(string formularioId, string chave)
        {
            var d = TodosDestinos(RepositorioFormularios.Obter(formularioId)).FirstOrDefault(x => x.Chave == chave);
            return d != null ? d.Cor : Tema.Suave;
        }

        public static string SomenteDigitos(string s) { return Regex.Replace(s ?? "", @"\D", ""); }

        public static string SomenteAlfanumerico(string s) { return Regex.Replace(s ?? "", @"[^0-9A-Za-z]", "").ToUpperInvariant(); }

        /// <summary>RG de 9 caracteres vira 00.000.000-0; outros tamanhos (cada estado tem o seu) ficam como foram digitados.</summary>
        public static string FormatarRg(string s)
        {
            var d = SomenteAlfanumerico(s);
            if (d.Length != 9) return (s ?? "").Trim();
            return d.Substring(0, 2) + "." + d.Substring(2, 3) + "." + d.Substring(5, 3) + "-" + d.Substring(8);
        }

        public static string FormatarCpf(string s)
        {
            var d = SomenteDigitos(s);
            if (d.Length > 11) d = d.Substring(0, 11);
            if (d.Length > 9) return d.Substring(0, 3) + "." + d.Substring(3, 3) + "." + d.Substring(6, 3) + "-" + d.Substring(9);
            if (d.Length > 6) return d.Substring(0, 3) + "." + d.Substring(3, 3) + "." + d.Substring(6);
            if (d.Length > 3) return d.Substring(0, 3) + "." + d.Substring(3);
            return d;
        }

        /// <summary>Só dígitos e pontuação de CPF: é CPF. Qualquer letra indica passaporte.</summary>
        public static bool ParecaCpf(string s)
        {
            return !string.IsNullOrWhiteSpace(s) && Regex.IsMatch(s.Trim(), @"^[\d.\-\s]+$");
        }

        /// <summary>Vai montando dd/MM/aaaa conforme os dígitos são digitados.</summary>
        public static string FormatarData(string s)
        {
            var d = SomenteDigitos(s);
            if (d.Length > 8) d = d.Substring(0, 8);
            if (d.Length > 4) return d.Substring(0, 2) + "/" + d.Substring(2, 2) + "/" + d.Substring(4);
            if (d.Length > 2) return d.Substring(0, 2) + "/" + d.Substring(2);
            return d;
        }

        public static bool DataValida(string s)
        {
            DateTime ignorada;
            return DateTime.TryParseExact(s, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out ignorada);
        }

        public static string DataDeHoje()
        {
            return DateTime.Today.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
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
