using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Aviso no canto da tela que NÃO rouba o foco da janela onde o colaborador está trabalhando.</summary>
    class Aviso : Form
    {
        static readonly List<Aviso> abertos = new List<Aviso>();
        static SynchronizationContextHolder ui;

        public static void Configurar(System.Threading.SynchronizationContext contexto) { ui = new SynchronizationContextHolder(contexto); }

        public static void Mostrar(string texto, bool problema = false)
        {
            if (ui == null) return;
            ui.Post(() => new Aviso(texto, problema).Exibir());
        }

        readonly Timer temporizador = new Timer();

        Aviso(string texto, bool problema)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = problema ? Tema.Cobre : Tema.Marinho;
            Padding = new Padding(16, 12, 16, 12);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var rotulo = new Label
            {
                Text = texto,
                ForeColor = Color.White,
                Font = Tema.Texto(10f),
                AutoSize = true,
                MaximumSize = new Size(340, 0)
            };
            Controls.Add(rotulo);
            rotulo.Click += (s, e) => Close();
            Click += (s, e) => Close();

            temporizador.Interval = problema ? 6500 : 4000;
            temporizador.Tick += (s, e) => Close();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        void Exibir()
        {
            Show();
            abertos.Add(this);
            Reposicionar();
            temporizador.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            temporizador.Stop();
            abertos.Remove(this);
            Reposicionar();
            base.OnFormClosed(e);
        }

        static void Reposicionar()
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int y = area.Bottom - 16;
            for (int i = abertos.Count - 1; i >= 0; i--)
            {
                var a = abertos[i];
                y -= a.Height;
                a.Location = new Point(area.Right - a.Width - 16, y);
                y -= 8;
            }
        }
    }

    /// <summary>Envia trabalho para a thread da interface a partir de qualquer thread.</summary>
    class SynchronizationContextHolder
    {
        readonly System.Threading.SynchronizationContext contexto;
        public SynchronizationContextHolder(System.Threading.SynchronizationContext c) { contexto = c; }
        public void Post(Action acao) { contexto.Post(_ => acao(), null); }
    }
}
