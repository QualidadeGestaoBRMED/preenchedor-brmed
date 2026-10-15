using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Automation;

namespace PreenchedorBRMED
{
    /// <summary>
    /// Log temporário para descobrir por que um programa de terceiros recusa o preenchimento.
    /// Fica desligado por padrão, desliga sozinho em 24 horas (ou ao fechar o app) e nunca grava o que foi digitado.
    /// </summary>
    static class Diagnostico
    {
        const long LimiteBytes = 1024 * 1024;
        static readonly TimeSpan Validade = TimeSpan.FromHours(24);
        static readonly object trava = new object();
        static DateTime expira = DateTime.MinValue;

        public static readonly string Caminho = Path.Combine(RepositorioPerfis.Pasta, "diagnostico.log");

        public static bool Ativo { get { return DateTime.Now < expira; } }

        public static void Ligar()
        {
            expira = DateTime.Now + Validade;
            Escrever("=== Modo diagnóstico ligado · app " + Atualizador.VersaoAtual + " · administrador: " + SouAdministrador() + " ===");
        }

        public static void Desligar()
        {
            if (Ativo) Escrever("=== Modo diagnóstico desligado ===");
            expira = DateTime.MinValue;
        }

        public static void Apagar()
        {
            lock (trava) { try { File.Delete(Caminho); } catch (IOException) { } }
        }

        /// <summary>Anota uma linha (só com o modo ligado).</summary>
        public static void Nota(string texto)
        {
            if (Ativo) Escrever(texto);
        }

        /// <summary>Anota a falha de um campo com os dados do controle que recusou a ação.</summary>
        public static void Falha(string etapa, string rotulo, CampoMapeado campo, AutomationElement el, Exception erro)
        {
            if (!Ativo) return;
            var sb = new StringBuilder();
            sb.Append("FALHA em ").Append(etapa).Append(" · campo \"").Append(rotulo).Append("\"");
            sb.Append(" · mapeado como ").Append(campo.Tipo).Append(" id=").Append(campo.AutomationId)
              .Append(" nome=").Append(campo.NomeControle).Append(" índice=").Append(campo.Indice).AppendLine();
            sb.Append("  controle: ").Append(Descrever(el)).AppendLine();
            sb.Append("  erro: ").Append(erro);
            Escrever(sb.ToString());
        }

        static string Descrever(AutomationElement el)
        {
            if (el == null) return "(controle não localizado)";
            var sb = new StringBuilder();
            try
            {
                var c = el.Current;
                sb.Append(c.ControlType.ProgrammaticName).Append(" classe=").Append(c.ClassName)
                  .Append(" framework=").Append(c.FrameworkId).Append(" habilitado=").Append(c.IsEnabled)
                  .Append(" foco=").Append(c.IsKeyboardFocusable).Append(" id=").Append(c.AutomationId)
                  .Append(" nome=").Append(c.Name).Append(" · ").Append(Processo(c.ProcessId));
                sb.Append(" · padrões: ").Append(string.Join(", ",
                    el.GetSupportedPatterns().Select(p => p.ProgrammaticName.Replace("Identifiers.Pattern", "")).ToArray()));
                object padrao;
                if (el.TryGetCurrentPattern(ValuePattern.Pattern, out padrao))
                    sb.Append(" · somenteLeitura=").Append(((ValuePattern)padrao).Current.IsReadOnly);
            }
            catch (Exception erro) { sb.Append("(controle indisponível: " + erro.GetType().Name + ")"); }
            return sb.ToString();
        }

        static string Processo(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                var nome = p.ProcessName;
                try { var caminho = p.MainModule.FileName; return "processo=" + nome + " (" + caminho + ")"; }
                catch (Win32Exception) { return "processo=" + nome + " (acesso negado: provavelmente roda como administrador)"; }
            }
            catch (ArgumentException) { return "processo " + pid + " encerrado"; }
        }

        static bool SouAdministrador()
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void Escrever(string texto)
        {
            lock (trava)
            {
                try
                {
                    Directory.CreateDirectory(RepositorioPerfis.Pasta);
                    Reduzir();
                    File.AppendAllText(Caminho, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + texto + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException) { }
            }
        }

        /// <summary>Passou de 1 MB: descarta a metade mais antiga.</summary>
        static void Reduzir()
        {
            var info = new FileInfo(Caminho);
            if (!info.Exists || info.Length < LimiteBytes) return;
            var linhas = File.ReadAllLines(Caminho, Encoding.UTF8);
            File.WriteAllLines(Caminho, linhas.Skip(linhas.Length / 2).ToArray(), Encoding.UTF8);
        }
    }
}
