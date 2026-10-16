using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PreenchedorBRMED
{
    /// <summary>Um campo de um formulário criado pelo colaborador. A Chave não muda, mesmo que o rótulo seja renomeado.</summary>
    [DataContract]
    class CampoForm
    {
        public const string TipoTexto = "texto", TipoEmail = "email", TipoNumero = "numero",
            TipoCpf = "cpf", TipoRg = "rg", TipoPassaporte = "passaporte", // CPF, RG, Passaporte e CPF ou Passaporte são os tipos de "Documento"
            TipoDocumento = "documento", TipoData = "data", TipoLista = "lista", TipoDecimal = "decimal";

        [DataMember(Order = 1)] public string Chave { get; set; }
        [DataMember(Order = 2)] public string Rotulo { get; set; }
        [DataMember(Order = 3)] public string Tipo { get; set; }
        [DataMember(Order = 4)] public bool Obrigatorio { get; set; }
        /// <summary>Só para o tipo Lista: as opções que o colaborador pode escolher.</summary>
        [DataMember(Order = 5, EmitDefaultValue = false)] public List<string> Opcoes { get; set; }
        /// <summary>Só para o tipo Data: já vem preenchido com a data do dia.</summary>
        [DataMember(Order = 6, EmitDefaultValue = false)] public bool Hoje { get; set; }
        /// <summary>Fica na mesma linha do campo anterior (para valores curtos, como idade, altura e peso).</summary>
        [DataMember(Order = 7, EmitDefaultValue = false)] public bool AoLado { get; set; }

        public bool EhCpf { get { return string.Equals(Tipo, TipoCpf, StringComparison.OrdinalIgnoreCase); } }
        public bool EhRg { get { return string.Equals(Tipo, TipoRg, StringComparison.OrdinalIgnoreCase); } }
        public bool EhPassaporte { get { return string.Equals(Tipo, TipoPassaporte, StringComparison.OrdinalIgnoreCase); } }
        public bool EhNumero { get { return string.Equals(Tipo, TipoNumero, StringComparison.OrdinalIgnoreCase); } }
        /// <summary>Documentos que podem ir com pontos ou só com números/letras para o outro sistema.</summary>
        public bool TemMascara { get { return EhCpf || EhRg || EhDocumento; } }
        /// <summary>Aceita CPF ou passaporte (estrangeiros): o que for digitado define qual é.</summary>
        public bool EhDocumento { get { return string.Equals(Tipo, TipoDocumento, StringComparison.OrdinalIgnoreCase); } }
        public bool EhData { get { return string.Equals(Tipo, TipoData, StringComparison.OrdinalIgnoreCase); } }
        public bool EhDecimal { get { return string.Equals(Tipo, TipoDecimal, StringComparison.OrdinalIgnoreCase); } }
        public bool EhLista { get { return string.Equals(Tipo, TipoLista, StringComparison.OrdinalIgnoreCase); } }
        public bool EhEmail { get { return string.Equals(Tipo, TipoEmail, StringComparison.OrdinalIgnoreCase); } }
    }

    /// <summary>Conjunto de campos que o colaborador preenche de uma vez (ex.: "Cadastro", "Admissão").</summary>
    [DataContract]
    class ModeloFormulario
    {
        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string Nome { get; set; }
        [DataMember(Order = 3)] public List<CampoForm> Campos { get; set; }

        public CampoForm Campo(string chave)
        {
            return Campos == null ? null : Campos.FirstOrDefault(c => c.Chave == chave);
        }

        public ModeloFormulario Clonar()
        {
            return new ModeloFormulario
            {
                Id = Id, Nome = Nome,
                Campos = (Campos ?? new List<CampoForm>())
                    .Select(c => new CampoForm
                    {
                        Chave = c.Chave, Rotulo = c.Rotulo, Tipo = c.Tipo, Obrigatorio = c.Obrigatorio, Hoje = c.Hoje, AoLado = c.AoLado,
                        Opcoes = c.Opcoes == null ? null : c.Opcoes.ToList()
                    })
                    .ToList()
            };
        }

        /// <summary>Gera uma chave estável a partir do rótulo, sem repetir as já usadas.</summary>
        public string NovaChave(string rotulo)
        {
            var sb = new StringBuilder();
            foreach (var ch in (rotulo ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                var c = char.ToLowerInvariant(ch);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            var baseChave = sb.ToString().Trim('_');
            if (baseChave == "") baseChave = "campo";

            var usadas = new HashSet<string>((Campos ?? new List<CampoForm>()).Select(c => c.Chave));
            var chave = baseChave;
            // "_numeros" é reservado para a versão sem pontos de CPF e RG.
            for (int i = 2; usadas.Contains(chave) || chave.EndsWith("_numeros"); i++) chave = baseChave + "_" + i;
            return chave;
        }
    }

    /// <summary>
    /// Lê e grava os formulários em %AppData%\BRMED\PreenchedorDeCadastro\formularios.json.
    /// Só a estrutura (nomes e tipos dos campos) é gravada, nunca os dados do colaborador.
    /// </summary>
    static class RepositorioFormularios
    {
        /// <summary>O formulário que sempre existe (não pode ser excluído): o Cadastro BRNET.</summary>
        public const string IdPadrao = "brnet";
        public static readonly string Caminho = Path.Combine(RepositorioPerfis.Pasta, "formularios.json");

        static readonly string CaminhoPrincipal = Path.Combine(RepositorioPerfis.Pasta, "principal.txt");
        /// <summary>O mesmo formulário padrão; recebe a captura do BRNET.</summary>
        public const string IdBrnet = IdPadrao;

        /// <summary>Tipos de exame do portal de telemedicina (também usados para reconhecer o exame na tela do BRNET).</summary>
        public static readonly string[] TiposDeExame =
        {
            "Admissional", "Demissional", "Exames Complementares", "Mudança de Riscos Ocupacionais",
            "Periódico", "Periódico Semestral", "Retorno ao Trabalho", "Semestral pós Admissão"
        };

        /// <summary>Formulário que o programa sempre abre (escolhido em Configurações). Se foi excluído, vale o padrão.</summary>
        public static string IdPrincipal
        {
            get
            {
                try { if (File.Exists(CaminhoPrincipal)) return Obter(File.ReadAllText(CaminhoPrincipal).Trim()).Id; }
                catch (IOException) { }
                return IdPadrao;
            }
            set
            {
                Directory.CreateDirectory(RepositorioPerfis.Pasta);
                File.WriteAllText(CaminhoPrincipal, value ?? IdPadrao);
            }
        }

        /// <summary>Disparado depois de salvar, para as telas abertas se atualizarem.</summary>
        public static event Action Alterado;

        static readonly object trava = new object();
        static List<ModeloFormulario> modelos;

        public static List<ModeloFormulario> Todos()
        {
            lock (trava)
            {
                if (modelos == null) modelos = Carregar();
                return modelos.ToList();
            }
        }

        /// <summary>Devolve o formulário pedido; se ele não existir mais, o padrão.</summary>
        public static ModeloFormulario Obter(string id)
        {
            var todos = Todos();
            return todos.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? todos.First(m => m.Id == IdPadrao);
        }

        public static void Salvar(List<ModeloFormulario> lista)
        {
            lock (trava)
            {
                modelos = lista.ToList();
                Gravar(modelos);
            }
            var aviso = Alterado;
            if (aviso != null) aviso();
        }

        static List<ModeloFormulario> Carregar()
        {
            List<ModeloFormulario> lista = null;
            try
            {
                if (File.Exists(Caminho))
                    using (var fluxo = File.OpenRead(Caminho))
                        lista = (List<ModeloFormulario>)new DataContractJsonSerializer(typeof(List<ModeloFormulario>)).ReadObject(fluxo);
            }
            catch (Exception erro)
            {
                Aviso.Mostrar("Não consegui ler formularios.json: " + erro.Message, true);
            }

            bool criar = lista == null || lista.Count == 0;
            if (criar) lista = new List<ModeloFormulario>();
            // "Cadastro padrão" das versões antigas: o Cadastro BRNET o substitui.
            lista.RemoveAll(m => m == null || string.IsNullOrEmpty(m.Id) || m.Id == "padrao");
            foreach (var m in lista) if (m.Campos == null) m.Campos = new List<CampoForm>();
            if (!lista.Any(m => m.Id == IdPadrao)) { lista.Insert(0, Padrao()); criar = true; }
            if (criar) Gravar(lista);
            return lista;
        }

        static void Gravar(List<ModeloFormulario> lista)
        {
            Directory.CreateDirectory(RepositorioPerfis.Pasta);
            using (var fluxo = File.Create(Caminho))
            using (var escritor = JsonReaderWriterFactory.CreateJsonWriter(fluxo, Encoding.UTF8, false, true, "  "))
                new DataContractJsonSerializer(typeof(List<ModeloFormulario>)).WriteObject(escritor, lista);
        }

        static CampoForm Campo(string chave, string rotulo, string tipo, bool obrigatorio = false)
        {
            return new CampoForm { Chave = chave, Rotulo = rotulo, Tipo = tipo, Obrigatorio = obrigatorio };
        }

        /// <summary>
        /// O Cadastro BRNET: um único formulário com tudo o que os sistemas pedem. O colaborador preenche uma vez e, a cada
        /// sistema, clica em "Colar no sistema" (cada sistema pega só os campos que mapeou).
        /// </summary>
        static ModeloFormulario Padrao()
        {
            return new ModeloFormulario
            {
                Id = IdPadrao, Nome = "Cadastro BRNET",
                Campos = new List<CampoForm>
                {
                    Campo("nome", "Nome", CampoForm.TipoTexto),
                    Campo("empresa", "Empresa", CampoForm.TipoTexto),
                    Campo("documento", "CPF ou Passaporte", CampoForm.TipoDocumento),
                    Campo("data_nascimento", "Data de nascimento", CampoForm.TipoData),
                    new CampoForm { Chave = "idade", Rotulo = "Idade", Tipo = CampoForm.TipoNumero, AoLado = true },
                    Campo("altura", "Altura (cm)", CampoForm.TipoNumero),
                    new CampoForm { Chave = "peso", Rotulo = "Peso (kg)", Tipo = CampoForm.TipoDecimal, AoLado = true },
                    new CampoForm { Chave = "sexo", Rotulo = "Sexo", Tipo = CampoForm.TipoLista, AoLado = true, Opcoes = new List<string> { "Masculino", "Feminino" } },
                    new CampoForm { Chave = "tipo_exame", Rotulo = "Tipo de exame", Tipo = CampoForm.TipoLista, Opcoes = TiposDeExame.ToList() },
                    new CampoForm { Chave = "data_exame", Rotulo = "Data do exame", Tipo = CampoForm.TipoData, Hoje = true }
                }
            };
        }
    }
}
