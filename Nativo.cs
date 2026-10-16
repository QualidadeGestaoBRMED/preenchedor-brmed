using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PreenchedorBRMED
{
    /// <summary>Chamadas diretas à API do Windows.</summary>
    static class Nativo
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int comando);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder nome, int max);

        /// <summary>Traz a janela para a frente (restaurando-a se estiver minimizada).</summary>
        public static void Ativar(IntPtr hWnd)
        {
            if (IsIconic(hWnd)) ShowWindow(hWnd, 9); // SW_RESTORE
            SetForegroundWindow(hWnd);
        }

        /// <summary>Barra de tarefas e área de trabalho não são sistemas para preencher.</summary>
        public static bool EhJanelaDoShell(IntPtr hWnd)
        {
            var sb = new StringBuilder(64);
            GetClassName(hWnd, sb, sb.Capacity);
            var c = sb.ToString();
            return c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd" || c == "Progman" || c == "WorkerW";
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder texto, int max);

        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);

        /// <summary>A área interna da janela (sem borda nem barra de título), em coordenadas de tela.</summary>
        public static bool AreaCliente(IntPtr hWnd, out int x, out int y, out int largura, out int altura)
        {
            x = y = largura = altura = 0;
            RECT r;
            var origem = new POINT();
            if (!GetClientRect(hWnd, out r) || !ClientToScreen(hWnd, ref origem)) return false;
            x = origem.X; y = origem.Y; largura = r.Right; altura = r.Bottom;
            return largura > 0 && altura > 0;
        }

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int indice);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint quantidade, INPUT[] entradas, int tamanho);

        public static string TituloDaJanela(IntPtr hWnd)
        {
            int tamanho = GetWindowTextLength(hWnd);
            if (tamanho <= 0) return "";
            var sb = new StringBuilder(tamanho + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // Estruturas do SendInput. O MOUSEINPUT precisa existir para a união ter o tamanho certo em 64 bits.
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public UNIAO u; }

        [StructLayout(LayoutKind.Explicit)]
        public struct UNIAO
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }
    }
}
