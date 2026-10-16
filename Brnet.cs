using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;

namespace PreenchedorBRMED
{
    /// <summary>
    /// Captura padrão do BRNET: lê, na aba do navegador, o quadro "Paciente" da tela de atendimento
    /// e separa nome, CPF, idade, data de nascimento, empresa e tipo de exame. Não precisa de mapeamento.
    /// </summary>
    static class CapturaBrnet
    {
        static readonly string[] Navegadores = { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" };

        public static bool EhNavegador(string processo)
        {
            return Navegadores.Contains((processo ?? "").ToLowerInvariant());
        }

        /// <summary>Sempre chamado fora da thread da interface.</summary>
        public static ResultadoCaptura Capturar(IntPtr janela)
        {
            var r = new ResultadoCaptura { Brnet = true };
            try
            {
                Teclado.EsperarSoltarModificadores();
                var raiz = AutomationElement.FromHandle(janela);
                // O navegador só monta a árvore de acessibilidade depois da primeira consulta: tenta de novo uma vez.
                for (int tentativa = 0; tentativa < 2 && r.Encontrados == 0; tentativa++)
                {
                    if (tentativa > 0) Thread.Sleep(400);
                    Interpretar(LerLinhas(raiz), r);
                }
                if (r.Encontrados == 0)
                    r.Erro = "Não encontrei o quadro \"Paciente\" do BRNET nesta janela. Abra a tela de atendimento na aba ativa do navegador.";
            }
            catch (Exception erro)
            {
                r.Erro = "Não consegui capturar: " + erro.Message;
            }
            return r;
        }

        /// <summary>A aba ativa do navegador mostra o quadro "Paciente" do BRNET? Sempre chamado fora da thread da interface.</summary>
        public static bool PaginaAberta(IntPtr janela)
        {
            try
            {
                var r = new ResultadoCaptura();
                Interpretar(LerLinhas(AutomationElement.FromHandle(janela)), r);
                return r.Encontrados > 0;
            }
            catch (Exception) { return false; }
        }

        /// <summary>O texto da página, uma linha por item, na ordem em que aparece na tela.</summary>
        static List<string> LerLinhas(AutomationElement raiz)
        {
            var linhas = new List<string>();
            var pagina = raiz.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            if (pagina == null) return linhas;

            object padrao;
            if (pagina.TryGetCurrentPattern(TextPattern.Pattern, out padrao))
            {
                var texto = ((TextPattern)padrao).DocumentRange.GetText(-1);
                if (!string.IsNullOrEmpty(texto)) linhas.AddRange(texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            }
            if (linhas.Any(l => l.Trim().StartsWith("Paciente", StringComparison.OrdinalIgnoreCase))) return linhas;

            // Plano B: um item de texto por vez.
            linhas.Clear();
            var itens = pagina.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
            foreach (AutomationElement item in itens)
                if (!string.IsNullOrWhiteSpace(item.Current.Name)) linhas.Add(item.Current.Name);
            return linhas;
        }

        // CPF no formato da tela e "43 anos - 28/06/1983" (bebês aparecem em meses ou dias).
        static readonly Regex Cpf = new Regex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b");
        static readonly Regex IdadeNascimento = new Regex(@"(\d+)\s*(anos?|mes(?:es)?|mês|dias?)\s*-\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);

        /// <summary>Separa os dados do paciente a partir das linhas de texto da tela. Público para facilitar testes.</summary>
        public static void Interpretar(List<string> linhas, ResultadoCaptura r)
        {
            var limpas = linhas.Select(l => (l ?? "").Trim()).Where(l => l != "").ToList();
            int inicio = limpas.FindIndex(l => l.StartsWith("Paciente", StringComparison.OrdinalIgnoreCase)
                && (l.Length == 8 || !char.IsLetter(l[8])));
            if (inicio < 0) return;

            r.Encontrados = 1;
            var resto = limpas.Skip(inicio).ToList();

            var nome = ValorDe(resto, "Paciente");
            if (!string.IsNullOrEmpty(nome)) r.Valores["nome"] = nome;

            var bloco = string.Join("\n", resto);
            var cpf = Cpf.Match(bloco);
            if (cpf.Success) r.Valores["documento"] = cpf.Value;

            var idade = IdadeNascimento.Match(bloco);
            if (idade.Success)
            {
                r.Valores["idade"] = idade.Groups[2].Value.StartsWith("ano", StringComparison.OrdinalIgnoreCase) ? idade.Groups[1].Value : "0";
                r.Valores["data_nascimento"] = idade.Groups[3].Value;
            }

            var empresa = ValorDe(resto, "Empresa");
            if (!string.IsNullOrEmpty(empresa)) r.Valores["empresa"] = empresa;

            // "Atendimento para o exame EXAMES COMPLEMENTARES": só vale se for um dos tipos de exame conhecidos.
            const string prefixo = "Atendimento para o exame ";
            foreach (var linha in limpas.Where(l => l.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)))
            {
                var tipo = RepositorioFormularios.TiposDeExame.FirstOrDefault(t => Normalizar(t) == Normalizar(linha.Substring(prefixo.Length)));
                if (tipo != null) { r.Valores["tipo_exame"] = tipo; break; }
            }
        }

        /// <summary>O valor de um rótulo: a linha seguinte, ou o resto da mesma linha ("Empresa BR MED").</summary>
        static string ValorDe(List<string> linhas, string rotulo)
        {
            for (int i = 0; i < linhas.Count; i++)
            {
                var l = linhas[i];
                if (!l.StartsWith(rotulo, StringComparison.OrdinalIgnoreCase)) continue;
                var depois = l.Substring(rotulo.Length);
                if (depois == "") return i + 1 < linhas.Count ? linhas[i + 1] : null;
                if (depois[0] == ' ' || depois[0] == ':') return depois.TrimStart(' ', ':').Trim();
            }
            return null;
        }

        static string Normalizar(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in (s ?? "").Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString().Trim();
        }
    }
}
