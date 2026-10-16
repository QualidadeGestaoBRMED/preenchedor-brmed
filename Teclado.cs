using System;
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
            // Só vale esperar um pouco se o usuário estava segurando o atalho; com o botão do formulário não há o que soltar.
            if (esperado > 0) Thread.Sleep(60);
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

        /// <summary>Clica com o botão esquerdo em (x, y) e devolve o cursor ao lugar de antes. Dá foco a controles que recusam SetFocus.</summary>
        public static void Clicar(int x, int y)
        {
            const uint INPUT_MOUSE = 0, BAIXO = 0x2, CIMA = 0x4;
            Nativo.POINT antes;
            Nativo.GetCursorPos(out antes);
            Nativo.SetCursorPos(x, y);
            var baixo = new Nativo.INPUT { type = INPUT_MOUSE };
            baixo.u.mi = new Nativo.MOUSEINPUT { dwFlags = BAIXO };
            var cima = new Nativo.INPUT { type = INPUT_MOUSE };
            cima.u.mi = new Nativo.MOUSEINPUT { dwFlags = CIMA };
            Enviar(new List<Nativo.INPUT> { baixo, cima });
            Thread.Sleep(80);
            Nativo.SetCursorPos(antes.X, antes.Y);
        }

        static void Enviar(List<Nativo.INPUT> entradas, int pausaMs = 30)
        {
            Nativo.SendInput((uint)entradas.Count, entradas.ToArray(), Marshal.SizeOf(typeof(Nativo.INPUT)));
            Thread.Sleep(pausaMs);
        }

        /// <summary>
        /// Clica em (x, y) e, se houver texto, seleciona tudo e digita. O clique usa coordenadas absolutas dentro do próprio evento
        /// (e a volta do cursor vai na mesma remessa), então não é preciso esperar o programa tratar o clique antes de mexer
        /// no cursor, como em Clicar. Texto null = só clicar (botões de opção).
        /// </summary>
        public static void ClicarEDigitar(int x, int y, string texto)
        {
            const uint INPUT_MOUSE = 0, MOVER = 0x1, BAIXO = 0x2, CIMA = 0x4, VIRTUAL = 0x4000, ABSOLUTO = 0x8000;
            int vx = Nativo.GetSystemMetrics(76), vy = Nativo.GetSystemMetrics(77), vw = Nativo.GetSystemMetrics(78), vh = Nativo.GetSystemMetrics(79);
            Nativo.POINT antes;
            Nativo.GetCursorPos(out antes);

            Func<int, int, uint, Nativo.INPUT> mouse = (px, py, flags) =>
            {
                var e = new Nativo.INPUT { type = INPUT_MOUSE };
                e.u.mi = new Nativo.MOUSEINPUT
                {
                    dx = (int)((px - vx) * 65535L / Math.Max(1, vw - 1)), dy = (int)((py - vy) * 65535L / Math.Max(1, vh - 1)),
                    dwFlags = flags | MOVER | ABSOLUTO | VIRTUAL
                };
                return e;
            };
            Enviar(new List<Nativo.INPUT> { mouse(x, y, BAIXO), mouse(x, y, CIMA), mouse(antes.X, antes.Y, 0) }, 15);

            if (texto == null) return;
            var teclas = new List<Nativo.INPUT> { Virtual(VK_CONTROL, false), Virtual(VK_A, false), Virtual(VK_A, true), Virtual(VK_CONTROL, true) };
            foreach (char c in texto)
            {
                teclas.Add(Unicode(c, false));
                teclas.Add(Unicode(c, true));
            }
            Enviar(teclas, 10);
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
