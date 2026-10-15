using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    class Novidade
    {
        public Version Versao;
        public string Url;
    }

    /// <summary>Procura versões novas nas Releases do GitHub e troca o próprio .exe por elas.</summary>
    static class Atualizador
    {
        const string Repositorio = "QualidadeGestaoBRMED/preenchedor-brmed";
        const string NomeExe = "PreenchedorBRMED.exe";
        const string PaginaLatest = "https://github.com/" + Repositorio + "/releases/latest";
        const string Prefixo = "https://github.com/" + Repositorio + "/releases/download/";

        public static string ArquivoNovo { get { return Application.ExecutablePath + ".novo"; } }
        public static string ArquivoAntigo { get { return Application.ExecutablePath + ".antigo"; } }

        public static Version VersaoAtual
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        /// <summary>
        /// Devolve a versão mais nova publicada, ou null se já está atualizado. Lança exceção se estiver sem rede.
        /// Lê o redirecionamento de /releases/latest em vez da API, que limita 60 consultas por hora por IP
        /// (um escritório inteiro costuma sair por um IP só).
        /// </summary>
        public static Novidade Verificar()
        {
            // Compilado sem número de versão (compilar.bat): não há como comparar.
            if (VersaoAtual == new Version(0, 0, 0)) return null;

            var tag = Regex.Match(DestinoDoRedirecionamento(PaginaLatest), "/releases/tag/(v?[0-9][0-9.]*)$");
            Version versao;
            if (!tag.Success || !Version.TryParse(tag.Groups[1].Value.TrimStart('v'), out versao) || versao <= VersaoAtual) return null;
            return new Novidade { Versao = versao, Url = Prefixo + tag.Groups[1].Value + "/" + NomeExe };
        }

        /// <summary>Baixa o .exe novo para ao lado do atual.</summary>
        public static void Baixar(Novidade n)
        {
            Preparar();
            using (var web = new WebClient { Proxy = WebRequest.DefaultWebProxy })
            {
                web.Headers[HttpRequestHeader.UserAgent] = "PreenchedorBRMED";
                if (web.Proxy != null) web.Proxy.Credentials = CredentialCache.DefaultCredentials;
                web.DownloadFile(n.Url, ArquivoNovo);
            }
            if (!ParecePrograma(ArquivoNovo))
            {
                File.Delete(ArquivoNovo);
                throw new InvalidDataException("O arquivo baixado não é um programa válido.");
            }
        }

        /// <summary>
        /// Troca o .exe em uso (o Windows deixa renomear, mas não apagar) e abre a versão nova.
        /// Quem chama deve encerrar o programa logo depois.
        /// </summary>
        public static void Instalar()
        {
            var exe = Application.ExecutablePath;
            if (File.Exists(ArquivoAntigo)) File.Delete(ArquivoAntigo);
            File.Move(exe, ArquivoAntigo);
            try { File.Move(ArquivoNovo, exe); }
            catch { File.Move(ArquivoAntigo, exe); throw; }
            Process.Start(exe, Program.ArgumentoAtualizado);
        }

        /// <summary>Apaga o .exe da versão anterior, deixado pela atualização.</summary>
        public static void Limpar()
        {
            try { if (File.Exists(ArquivoAntigo)) File.Delete(ArquivoAntigo); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { if (File.Exists(ArquivoNovo)) File.Delete(ArquivoNovo); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        static void Preparar()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2 não vem ligado por padrão no .NET 4.x
        }

        static string DestinoDoRedirecionamento(string url)
        {
            Preparar();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "PreenchedorBRMED";
            req.Timeout = 15000;
            req.AllowAutoRedirect = false;
            if (req.Proxy != null) req.Proxy.Credentials = CredentialCache.DefaultCredentials;
            using (var resp = req.GetResponse())
                return resp.Headers[HttpResponseHeader.Location] ?? "";
        }

        static bool ParecePrograma(string caminho)
        {
            using (var f = File.OpenRead(caminho))
                return f.Length > 10000 && f.ReadByte() == 'M' && f.ReadByte() == 'Z';
        }
    }
}
