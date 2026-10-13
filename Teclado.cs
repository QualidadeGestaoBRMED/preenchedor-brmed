using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PreenchedorBRMED
{
    /// <summary>Simula o teclado com SendInput. Envia caracteres Unicode, então acentos e "ç" funcionam em qualquer layout.</summary>
    static class Teclado
    {
        const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4;
        public const ushort VK_TAB = 0x09, VK_RETURN = 0x0D, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_SHIFT = 0x10, VK_LWIN = 0x5B, VK_A = 0x41;

        /// <summary>Espera o usuário soltar Ctrl/Alt/Shift do atalho; senão eles se misturam com o que for digitado.</summary>
        public static void EsperarSoltarModificadores(int limiteMs = 2000)
        {
            int esperado = 0;
            while (esperado < limiteMs && (Pressionada(VK_CONTROL) || Pressionada(VK_MENU) || Pressionada(VK_SHIFT) || Pressionada(VK_LWIN)))
            {
                Thread.Sleep(25);
                esperado += 25;
            }
            Thread.Sleep(60);
        }

        static bool Pressionada(int vk) { return (Nativo.GetAsyncKeyState(vk) & 0x8000) != 0; }

        public static void Digitar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            var entradas = new List<Nativo.INPUT>();
            foreach (char c in texto)
            {
                entradas.Add(Unicode(c, false));
                entradas.Add(Unicode(c, true));
            }
            Enviar(entradas);
        }

        public static void Tecla(ushort vk)
        {
            Enviar(new List<Nativo.INPUT> { Virtual(vk, false), Virtual(vk, true) });
        }

        public static void SelecionarTudo()
        {
            Enviar(new List<Nativo.INPUT> { Virtual(VK_CONTROL, false), Virtual(VK_A, false), Virtual(VK_A, true), Virtual(VK_CONTROL, true) });
        }

        static void Enviar(List<Nativo.INPUT> entradas)
        {
            Nativo.SendInput((uint)entradas.Count, entradas.ToArray(), Marshal.SizeOf(typeof(Nativo.INPUT)));
            Thread.Sleep(30);
        }

        static Nativo.INPUT Unicode(char c, bool soltar)
        {
            var e = new Nativo.INPUT { type = INPUT_KEYBOARD };
            e.u.ki = new Nativo.KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | (soltar ? KEYEVENTF_KEYUP : 0) };
            return e;
        }

        static Nativo.INPUT Virtual(ushort vk, bool soltar)
        {
            var e = new Nativo.INPUT { type = INPUT_KEYBOARD };
            e.u.ki = new Nativo.KEYBDINPUT { wVk = vk, dwFlags = soltar ? KEYEVENTF_KEYUP : 0 };
            return e;
        }
    }
}
