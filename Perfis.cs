using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PreenchedorBRMED
{
    /// <summary>
    /// Um perfil diz como preencher um sistema: qual programa (processo), opcionalmente
    /// qual janela (parte do título) e o método.
    /// "uia": cada campo é localizado pela UI Automation (mais confiável).
    /// "teclado": o programa digita a Sequencia como se fosse o teclado (plano B).
    /// </summary>
    [DataContract]
    class Perfil
    {
        [DataMember(Order = 1)] public string Nome { get; set; }
        [DataMember(Order = 2)] public string Processo { get; set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public string TituloContem { get; set; }
        [DataMember(Order = 4)] public string Metodo { get; set; }
        [DataMember(Order = 5, EmitDefaultValue = false)] public List<CampoMapeado> Campos { get; set; }
        [DataMember(Order = 6, EmitDefaultValue = false)] public List<string> Sequencia { get; set; }

        /// <summary>Formulário de onde vêm os dados deste sistema. Vazio = o formulário padrão.</summary>
        [DataMember(Order = 7, Name = "Formulario", EmitDefaultValue = false)] public string FormularioSalvo { get; set; }

        public string FormularioId { get { return string.IsNullOrEmpty(FormularioSalvo) ? RepositorioFormularios.IdPadrao : FormularioSalvo; } }

        public bool PorTeclado { get { return string.Equals(Metodo, "teclado", StringComparison.OrdinalIgnoreCase); } }

        public bool Atende(string processo, string titulo)
        {
            if (!string.Equals(Processo, processo, StringComparison.OrdinalIgnoreCase)) return false;
            return string.IsNullOrEmpty(TituloContem)
                || (titulo ?? "").IndexOf(TituloContem, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Como reencontrar um campo de um programa desktop.</summary>
    [DataContract]
    class CampoMapeado
    {
        [DataMember(Order = 1)] public string Dado { get; set; }
        [DataMember(Order = 2, EmitDefaultValue = false)] public string AutomationId { get; set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public string NomeControle { get; set; }
        [DataMember(Order = 4)] public string Tipo { get; set; }
        /// <summary>Último recurso: posição entre os campos do mesmo tipo na janela (-1 = não usa).</summary>
        [DataMember(Order = 5)] public int Indice { get; set; }
        [DataMember(Order = 6, EmitDefaultValue = false)] public string Descricao { get; set; }
        /// <summary>true = o campo é lido com Ctrl+Alt+C (alimenta o formulário); false = recebe o dado com Ctrl+Alt+V.</summary>
        [DataMember(Order = 7, EmitDefaultValue = false)] public bool Captura { get; set; }
        /// <summary>
        /// Para programas sem AutomationId estável (ex.: Delphi, que cria as janelas de novo a cada abertura do formulário):
        /// a classe do controle (ex.: TEdit) e a posição dele entre os controles da mesma classe na janela.
        /// </summary>
        [DataMember(Order = 8, EmitDefaultValue = false)] public string ClassName { get; set; }
        [DataMember(Order = 9, EmitDefaultValue = false)] public int IndiceClasse { get; set; }
        /// <summary>Para programas sem id nem nome (Qt): o texto do rótulo ao lado do campo e de que lado ele fica ("E" esquerda, "A" acima).</summary>
        [DataMember(Order = 10, EmitDefaultValue = false)] public string Rotulo { get; set; }
        [DataMember(Order = 11, EmitDefaultValue = false)] public string PosRotulo { get; set; }
        /// <summary>
        /// Para programas sem janelas nativas (Qt): o campo é achado pela posição dentro da tela (fração da área interna da janela)
        /// e preenchido com um clique e a digitação, sem consultar a árvore de acessibilidade, que é lenta nesses programas.
        /// JanelaTitulo diz a que tela a posição pertence; em outra tela o campo é ignorado.
        /// </summary>
        [DataMember(Order = 12, EmitDefaultValue = false)] public bool Posicao { get; set; }
        [DataMember(Order = 13, EmitDefaultValue = false)] public double PosX { get; set; }
        [DataMember(Order = 14, EmitDefaultValue = false)] public double PosY { get; set; }
        [DataMember(Order = 15, EmitDefaultValue = false)] public string JanelaTitulo { get; set; }

        public string Identificacao
        {
            get
            {
                if (Posicao) return "Posição na tela \"" + JanelaTitulo + "\" (" + (int)Math.Round(PosX * 100) + "% · " + (int)Math.Round(PosY * 100) + "%)";
                if (!string.IsNullOrEmpty(AutomationId)) return "AutomationId = " + AutomationId;
                if (!string.IsNullOrEmpty(NomeControle)) return "Nome = " + NomeControle;
                if (!string.IsNullOrEmpty(Rotulo)) return "Rótulo \"" + Rotulo + "\" (" + (PosRotulo == "A" ? "acima" : "à esquerda") + ")";
                if (!string.IsNullOrEmpty(ClassName)) return "Classe " + ClassName + " nº " + (IndiceClasse + 1);
                return Tipo + " nº " + (Indice + 1);
            }
        }

        public bool MesmoCampo(CampoMapeado outro)
        {
            return outro != null && Captura == outro.Captura && Tipo == outro.Tipo && Indice == outro.Indice
                && AutomationId == outro.AutomationId && NomeControle == outro.NomeControle
                && ClassName == outro.ClassName && IndiceClasse == outro.IndiceClasse && Rotulo == outro.Rotulo
                && Posicao == outro.Posicao && (!Posicao || (Math.Abs(PosX - outro.PosX) < 0.015 && Math.Abs(PosY - outro.PosY) < 0.015 && JanelaTitulo == outro.JanelaTitulo));
        }
    }

    /// <summary>Lê e grava os perfis em %AppData%\BRMED\PreenchedorDeCadastro\perfis.json.</summary>
    static class RepositorioPerfis
    {
        public static readonly string Pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRMED", "PreenchedorDeCadastro");
        public static readonly string Caminho = Path.Combine(Pasta, "perfis.json");

        static readonly object trava = new object();
        static List<Perfil> perfis;

        public static List<Perfil> Todos()
        {
            lock (trava)
            {
                if (perfis == null) perfis = Carregar();
                return perfis.ToList();
            }
        }

        public static void Recarregar() { lock (trava) perfis = Carregar(); }

        public static Perfil Encontrar(string processo, string titulo)
        {
            // Perfis com filtro de título têm prioridade sobre os genéricos do mesmo programa.
            return Todos()
                .Where(p => p.Atende(processo, titulo))
                .OrderByDescending(p => string.IsNullOrEmpty(p.TituloContem) ? 0 : 1)
                .FirstOrDefault();
        }

        /// <summary>O perfil de automação que recebe os mapeamentos feitos com Ctrl+Alt+M (null se ainda não existe).</summary>
        public static Perfil PerfilDeMapeamento(string processo, string titulo)
        {
            return Todos().FirstOrDefault(p => p.Atende(processo, titulo) && !p.PorTeclado);
        }

        /// <summary>O perfil com campos de captura que vale para esta janela (null se não há).</summary>
        public static Perfil Capturavel(string processo, string titulo)
        {
            return Todos()
                .Where(p => p.Atende(processo, titulo) && !p.PorTeclado && p.Campos != null && p.Campos.Any(c => c.Captura))
                .OrderByDescending(p => string.IsNullOrEmpty(p.TituloContem) ? 0 : 1)
                .FirstOrDefault();
        }

        /// <summary>Grava (ou substitui) o mapeamento de um campo, criando o perfil se preciso.</summary>
        public static Perfil MapearCampo(string processo, string titulo, CampoMapeado campo, string formularioId)
        {
            lock (trava)
            {
                if (perfis == null) perfis = Carregar();
                var perfil = perfis.FirstOrDefault(p => p.Atende(processo, titulo) && !p.PorTeclado);
                if (perfil != null && (perfil.Campos == null || perfil.Campos.Count == 0)) perfil.FormularioSalvo = FormularioParaGravar(formularioId);
                if (perfil == null)
                {
                    perfil = new Perfil
                    {
                        Nome = string.IsNullOrWhiteSpace(titulo) ? processo : titulo,
                        Processo = processo,
                        Metodo = "uia",
                        FormularioSalvo = FormularioParaGravar(formularioId),
                        Campos = new List<CampoMapeado>()
                    };
                    perfis.Add(perfil);
                }
                if (perfil.Campos == null) perfil.Campos = new List<CampoMapeado>();
                perfil.Campos.RemoveAll(c => c.MesmoCampo(campo));
                perfil.Campos.Add(campo);
                Gravar(perfis);
                return perfil;
            }
        }

        static string FormularioParaGravar(string id)
        {
            return id == RepositorioFormularios.IdPadrao ? null : id;
        }

        public static void RemoverCampo(string processo, string titulo, CampoMapeado campo)
        {
            lock (trava)
            {
                if (perfis == null) perfis = Carregar();
                foreach (var p in perfis.Where(p => p.Atende(processo, titulo) && p.Campos != null))
                    p.Campos.RemoveAll(c => c.MesmoCampo(campo));
                Gravar(perfis);
            }
        }

        public static void SalvarTodos(List<Perfil> lista)
        {
            lock (trava)
            {
                perfis = lista.ToList();
                Gravar(perfis);
            }
        }

        static List<Perfil> Carregar()
        {
            try
            {
                if (!File.Exists(Caminho))
                {
                    var vazia = new List<Perfil>();
                    Gravar(vazia);
                    return vazia;
                }
                using (var fluxo = File.OpenRead(Caminho))
                {
                    var serializador = new DataContractJsonSerializer(typeof(List<Perfil>));
                    var lida = (List<Perfil>)serializador.ReadObject(fluxo) ?? new List<Perfil>();
                    lida.RemoveAll(EhExemploAntigo);
                    return lida;
                }
            }
            catch (Exception erro)
            {
                Aviso.Mostrar("Não consegui ler perfis.json: " + erro.Message, true);
                return new List<Perfil>();
            }
        }

        static void Gravar(List<Perfil> lista)
        {
            Directory.CreateDirectory(Pasta);
            using (var fluxo = File.Create(Caminho))
            using (var escritor = JsonReaderWriterFactory.CreateJsonWriter(fluxo, Encoding.UTF8, false, true, "  "))
            {
                new DataContractJsonSerializer(typeof(List<Perfil>)).WriteObject(escritor, lista);
            }
        }

        /// <summary>Exemplos que vinham nas versões antigas (sistema de teste e Bloco de Notas): saem ao carregar.</summary>
        static bool EhExemploAntigo(Perfil p)
        {
            return p != null && (p.Nome == "Sistema de teste (exemplo de UI Automation)" || p.Nome == "Bloco de Notas (exemplo de digitação)");
        }
    }
}
