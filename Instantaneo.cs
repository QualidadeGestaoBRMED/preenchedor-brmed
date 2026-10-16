using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;

namespace PreenchedorBRMED
{
    /// <summary>Um controle da janela, com as propriedades já lidas (consultar uma por uma é lento em programas Qt, Delphi e Java).</summary>
    class ItemUi
    {
        public AutomationElement El;
        public ControlType Tipo;
        public string Nome, Classe, Id;
        public Rect Area;
        public int[] RuntimeId;

        public double Esquerda { get { return Area.X; } }
        public double Direita { get { return Area.X + Area.Width; } }
        public double Topo { get { return Area.Y; } }
        public double Base { get { return Area.Y + Area.Height; } }
    }

    /// <summary>
    /// Lê todos os campos de uma janela de uma vez (uma única chamada com cache) e resolve os campos mapeados em memória.
    /// Antes cada campo fazia a sua própria busca na árvore inteira, o que levava segundos em programas sem janelas nativas (Qt).
    /// </summary>
    class Instantaneo
    {
        static readonly ControlType[] TiposDeCampo =
            { ControlType.Edit, ControlType.ComboBox, ControlType.RadioButton, ControlType.CheckBox, ControlType.Spinner, ControlType.Document };

        readonly AutomationElement raiz;
        List<ItemUi> textos;

        public readonly List<ItemUi> Campos;

        Instantaneo(AutomationElement raiz, List<ItemUi> campos) { this.raiz = raiz; Campos = campos; }

        public static Instantaneo Tirar(AutomationElement raiz)
        {
            return new Instantaneo(raiz, Ler(raiz, TiposDeCampo));
        }

        /// <summary>Rótulos (texto estático) da janela; só lidos quando algum campo é localizado pelo rótulo vizinho.</summary>
        public List<ItemUi> Textos
        {
            get { return textos ?? (textos = Ler(raiz, new[] { ControlType.Text }).Where(t => !string.IsNullOrWhiteSpace(t.Nome)).ToList()); }
        }

        static List<ItemUi> Ler(AutomationElement raiz, ControlType[] tipos)
        {
            var cache = new CacheRequest { TreeScope = TreeScope.Element };
            cache.Add(AutomationElement.NameProperty);
            cache.Add(AutomationElement.ControlTypeProperty);
            cache.Add(AutomationElement.AutomationIdProperty);
            cache.Add(AutomationElement.ClassNameProperty);
            cache.Add(AutomationElement.BoundingRectangleProperty);
            cache.Add(AutomationElement.RuntimeIdProperty);

            Condition condicao = tipos.Length == 1
                ? (Condition)new PropertyCondition(AutomationElement.ControlTypeProperty, tipos[0])
                : new OrCondition(tipos.Select(t => (Condition)new PropertyCondition(AutomationElement.ControlTypeProperty, t)).ToArray());

            AutomationElementCollection achados;
            using (cache.Activate()) achados = raiz.FindAll(TreeScope.Descendants, condicao);

            var lista = new List<ItemUi>(achados.Count);
            foreach (AutomationElement e in achados)
            {
                var c = e.Cached;
                Rect area = c.BoundingRectangle;
                lista.Add(new ItemUi
                {
                    El = e, Tipo = c.ControlType, Nome = c.Name, Classe = c.ClassName, Id = c.AutomationId, Area = area,
                    RuntimeId = e.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty) as int[]
                });
            }
            return lista;
        }

        public static bool MesmoTipo(ControlType a, ControlType b) { return a != null && b != null && a.Id == b.Id; }

        public List<ItemUi> DoTipo(ControlType tipo) { return Campos.Where(i => MesmoTipo(i.Tipo, tipo)).ToList(); }

        /// <summary>O item que corresponde a um elemento já em mãos (por RuntimeId); null se não está no instantâneo.</summary>
        public ItemUi De(AutomationElement elemento)
        {
            int[] id;
            try { id = elemento.GetRuntimeId(); }
            catch (Exception) { return null; }
            return Campos.FirstOrDefault(i => i.RuntimeId != null && i.RuntimeId.SequenceEqual(id));
        }

        // ---------- Localizar um campo mapeado ----------

        /// <summary>Do critério mais firme ao mais frágil; cada um só vale se encontrar o campo.</summary>
        public ItemUi Achar(CampoMapeado campo)
        {
            var tipo = Mapeador.TipoPorNome(campo.Tipo);
            var doTipo = DoTipo(tipo);
            ItemUi item;

            if (!string.IsNullOrEmpty(campo.AutomationId))
            {
                item = Campos.FirstOrDefault(i => i.Id == campo.AutomationId);
                if (item != null) return item;
            }
            if (!string.IsNullOrEmpty(campo.NomeControle))
            {
                item = doTipo.FirstOrDefault(i => i.Nome == campo.NomeControle);
                if (item != null) return item;
            }
            if (!string.IsNullOrEmpty(campo.Rotulo))
            {
                item = PorRotulo(doTipo, campo.Rotulo, campo.PosRotulo);
                if (item != null) return item;
            }
            if (!string.IsNullOrEmpty(campo.ClassName))
            {
                var daClasse = Campos.Where(i => i.Classe == campo.ClassName).ToList();
                if (campo.IndiceClasse >= 0 && campo.IndiceClasse < daClasse.Count) return daClasse[campo.IndiceClasse];
            }
            if (campo.Indice >= 0 && campo.Indice < doTipo.Count) return doTipo[campo.Indice];
            return null;
        }

        public ItemUi PorRotulo(List<ItemUi> candidatos, string rotulo, string posicao)
        {
            var chave = Normalizar(rotulo);
            foreach (var r in Textos.Where(t => Normalizar(t.Nome) == chave))
            {
                var f = MaisPerto(r, candidatos, posicao);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>O rótulo (à esquerda ou acima) que descreve um campo; null se não há um claro.</summary>
        public string RotuloDe(ItemUi campo, out string posicao)
        {
            posicao = null;
            if (campo.Area.IsEmpty) return null;
            var esquerda = Textos
                .Where(t => !t.Area.IsEmpty && t.Direita <= campo.Esquerda + 4 && SobrepoeLinha(t, campo) && campo.Esquerda - t.Direita < 500)
                .OrderBy(t => campo.Esquerda - t.Direita).FirstOrDefault();
            if (esquerda != null) { posicao = "E"; return esquerda.Nome.Trim(); }

            var acima = Textos
                .Where(t => !t.Area.IsEmpty && t.Base <= campo.Topo + 4 && campo.Topo - t.Base < 100 && Math.Abs(t.Esquerda - campo.Esquerda) < 120)
                .OrderBy(t => campo.Topo - t.Base).FirstOrDefault();
            if (acima != null) { posicao = "A"; return acima.Nome.Trim(); }
            return null;
        }

        /// <summary>O campo mais próximo de um rótulo, do lado indicado ("E" = campo à direita do rótulo, "A" = campo abaixo).</summary>
        static ItemUi MaisPerto(ItemUi rotulo, List<ItemUi> campos, string posicao)
        {
            if (rotulo.Area.IsEmpty) return null;
            if (posicao == "A")
                return campos.Where(f => !f.Area.IsEmpty && f.Topo >= rotulo.Base - 4 && f.Topo - rotulo.Base < 100 && Math.Abs(f.Esquerda - rotulo.Esquerda) < 120)
                    .OrderBy(f => f.Topo - rotulo.Base).FirstOrDefault();
            return campos.Where(f => !f.Area.IsEmpty && f.Esquerda >= rotulo.Direita - 4 && f.Esquerda - rotulo.Direita < 500 && SobrepoeLinha(rotulo, f))
                .OrderBy(f => f.Esquerda - rotulo.Direita).FirstOrDefault();
        }

        static bool SobrepoeLinha(ItemUi a, ItemUi b)
        {
            double sobra = Math.Min(a.Base, b.Base) - Math.Max(a.Topo, b.Topo);
            return sobra > 0.3 * Math.Min(a.Area.Height, b.Area.Height);
        }

        // ---------- Opções (botões de rádio) ----------

        /// <summary>"Masculino" casa com "&amp;Masculino", "male", "M"...; outros valores comparam o texto.</summary>
        public static bool CasaOpcao(string opcao, string valor)
        {
            var a = Normalizar(opcao);
            var b = Normalizar(valor);
            if (a == "" || b == "") return false;
            if (a == b) return true;
            var ga = Genero(a);
            return ga != null && ga == Genero(b);
        }

        static string Genero(string n)
        {
            if (n == "m" || n.StartsWith("masc") || n.StartsWith("male") || n.StartsWith("homem") || n == "h") return "M";
            if (n == "f" || n.StartsWith("fem") || n.StartsWith("mulher")) return "F";
            return null;
        }

        /// <summary>Sem acento, sem "&amp;" de atalho, sem pontuação final e em minúsculas.</summary>
        public static string Normalizar(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in (s ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>Linhas para o log de diagnóstico: o que a janela mostra para a automação.</summary>
        public IEnumerable<string> Descrever()
        {
            return Campos.Take(150).Select(i => string.Format(CultureInfo.InvariantCulture, "{0} nome=\"{1}\" classe={2} id={3} área=({4:0},{5:0},{6:0},{7:0})",
                i.Tipo.ProgrammaticName.Replace("ControlType.", ""), i.Nome, i.Classe, i.Id, i.Area.X, i.Area.Y, i.Area.Width, i.Area.Height));
        }
    }
}
