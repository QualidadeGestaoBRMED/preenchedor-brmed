using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Programa que fica na bandeja do Windows e atende os atalhos globais.</summary>
    class AppContexto : ApplicationContext
    {
        const int ATALHO_FORMULARIO = 1, ATALHO_COLAR = 2, ATALHO_MAPEAR = 3, ATALHO_CAPTURAR = 4, ATALHO_MAPEAR_CAPTURA = 5;

        readonly NotifyIcon icone;
        readonly JanelaAtalhos atalhos;
        readonly SynchronizationContext ui;
        FormDados formDados;
        FormPerfis formPerfis;
        FormMapear formMapear;
        bool ocupado;

        public AppContexto()
        {
            // Garante um contexto de sincronização para voltar à thread da interface.
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ui = SynchronizationContext.Current;
            Aviso.Configurar(ui);
            Dados.FormularioAtivo = RepositorioFormularios.IdPrincipal;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir formulário  (Ctrl+Alt+F)", null, (s, e) => AbrirFormulario());
            menu.Items.Add("Gerenciar formulários…", null, (s, e) => FormModelos.Abrir());
            var principal = new ToolStripMenuItem("Formulário principal");
            principal.DropDownOpening += (s, e) => MontarMenuPrincipal(principal);
            principal.DropDownItems.Add("…");
            var config = new ToolStripMenuItem("Configurações");
            config.DropDownItems.Add(principal);
            menu.Items.Add(config);
            menu.Items.Add("Sistemas mapeados…", null, (s, e) => AbrirPerfis());
            menu.Items.Add("Abrir sistema de teste", null, (s, e) => new SistemaTeste().Show());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Apagar dados agora", null, (s, e) => { Dados.Limpar(); Aviso.Mostrar("Dados apagados."); });
            menu.Items.Add("Sair", null, (s, e) => Sair());

            icone = new NotifyIcon
            {
                Icon = Tema.CriarIcone(),
                Text = "Preenchedor de Cadastro · BR MED",
                ContextMenuStrip = menu,
                Visible = true
            };
            icone.DoubleClick += (s, e) => AbrirFormulario();

            atalhos = new JanelaAtalhos();
            atalhos.Pressionado += AoPressionar;
            var falhas = new System.Collections.Generic.List<string>();
            if (!atalhos.Registrar(ATALHO_FORMULARIO, Nativo.MOD_CONTROL | Nativo.MOD_ALT, Keys.F)) falhas.Add("Ctrl+Alt+F");
            if (!atalhos.Registrar(ATALHO_COLAR, Nativo.MOD_CONTROL | Nativo.MOD_ALT, Keys.V)) falhas.Add("Ctrl+Alt+V");
            if (!atalhos.Registrar(ATALHO_MAPEAR, Nativo.MOD_CONTROL | Nativo.MOD_ALT, Keys.M)) falhas.Add("Ctrl+Alt+M");
            if (!atalhos.Registrar(ATALHO_CAPTURAR, Nativo.MOD_CONTROL | Nativo.MOD_ALT, Keys.C)) falhas.Add("Ctrl+Alt+C");
            if (!atalhos.Registrar(ATALHO_MAPEAR_CAPTURA, Nativo.MOD_CONTROL | Nativo.MOD_ALT, Keys.K)) falhas.Add("Ctrl+Alt+K");

            RepositorioPerfis.Todos(); // cria o arquivo de perfis na primeira execução

            if (falhas.Count > 0)
                Aviso.Mostrar("Outro programa já usa " + string.Join(", ", falhas) + ". Esses atalhos não vão funcionar.", true);
            else
                Aviso.Mostrar("Preenchedor ativo. Ctrl+Alt+F abre o formulário.");
        }

        void AoPressionar(int id)
        {
            if (id == ATALHO_FORMULARIO) AbrirFormulario();
            else if (id == ATALHO_COLAR) Colar();
            else if (id == ATALHO_MAPEAR) Mapear(false);
            else if (id == ATALHO_MAPEAR_CAPTURA) Mapear(true);
            else if (id == ATALHO_CAPTURAR) Capturar();
        }

        /// <summary>Lista os formulários no menu; o marcado é o que sempre abre com Ctrl+Alt+F.</summary>
        static void MontarMenuPrincipal(ToolStripMenuItem pai)
        {
            pai.DropDownItems.Clear();
            var atual = RepositorioFormularios.IdPrincipal;
            foreach (var m in RepositorioFormularios.Todos())
            {
                var id = m.Id;
                var item = new ToolStripMenuItem(m.Nome) { Checked = id == atual };
                item.Click += (s, e) =>
                {
                    RepositorioFormularios.IdPrincipal = id;
                    Aviso.Mostrar("\"" + RepositorioFormularios.Obter(id).Nome + "\" é o formulário principal.");
                };
                pai.DropDownItems.Add(item);
            }
        }

        void AbrirFormulario()
        {
            if (formDados == null || formDados.IsDisposed)
            {
                formDados = new FormDados();
                formDados.Show();
            }
            else formDados.Selecionar(RepositorioFormularios.IdPrincipal);
            formDados.WindowState = FormWindowState.Normal;
            formDados.Activate();
        }

        void AbrirPerfis()
        {
            if (formPerfis == null || formPerfis.IsDisposed) formPerfis = new FormPerfis();
            formPerfis.Show();
            formPerfis.Activate();
        }

        void Colar()
        {
            if (ocupado) return;
            var janela = Nativo.GetForegroundWindow();
            uint pid;
            Nativo.GetWindowThreadProcessId(janela, out pid);
            string processo, titulo = Nativo.TituloDaJanela(janela);
            try { processo = Process.GetProcessById((int)pid).ProcessName; }
            catch (ArgumentException) { return; }

            var perfil = RepositorioPerfis.Encontrar(processo, titulo);
            if (perfil == null)
            {
                Aviso.Mostrar("O programa \"" + processo + "\" ainda não foi mapeado. Aponte o mouse para um campo e use Ctrl+Alt+M.", true);
                return;
            }

            if (!Dados.TemDados(perfil.FormularioId))
            {
                Aviso.Mostrar("Nenhum dado salvo para o formulário \"" + RepositorioFormularios.Obter(perfil.FormularioId).Nome
                    + "\". Use Ctrl+Alt+F para preenchê-lo.", true);
                return;
            }

            ocupado = true;
            // A UI Automation roda fora da thread da interface para não travar (inclusive no sistema de teste).
            Task.Run(() => Preenchedor.Colar(perfil, janela)).ContinueWith(t =>
            {
                ocupado = false;
                var r = t.Exception == null ? t.Result : new Resultado { Erro = t.Exception.GetBaseException().Message };
                Aviso.Mostrar(r.Resumo(), r.Problema);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void Mapear(bool paraCaptura)
        {
            if (ocupado) return;
            Nativo.POINT p;
            Nativo.GetCursorPos(out p);
            ocupado = true;
            Task.Run(() => Mapeador.Capturar(p.X, p.Y)).ContinueWith(t =>
            {
                ocupado = false;
                var c = t.Result;
                if (c.Erro != null) { Aviso.Mostrar(c.Erro, true); return; }
                if (formMapear != null && !formMapear.IsDisposed) formMapear.Close();
                formMapear = new FormMapear(c, new Point(p.X, p.Y), paraCaptura);
                formMapear.Show();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void Capturar()
        {
            if (ocupado) return;
            var janela = Nativo.GetForegroundWindow();
            uint pid;
            Nativo.GetWindowThreadProcessId(janela, out pid);
            string processo, titulo = Nativo.TituloDaJanela(janela);
            try { processo = Process.GetProcessById((int)pid).ProcessName; }
            catch (ArgumentException) { return; }

            var perfil = RepositorioPerfis.Capturavel(processo, titulo);
            if (perfil == null)
            {
                Aviso.Mostrar("Nenhum campo de captura mapeado para \"" + processo + "\". Aponte o mouse para um campo e use Ctrl+Alt+K.", true);
                return;
            }

            ocupado = true;
            Task.Run(() => Capturador.Capturar(perfil, janela)).ContinueWith(t =>
            {
                ocupado = false;
                if (t.Exception != null) { Aviso.Mostrar("Não consegui capturar: " + t.Exception.GetBaseException().Message, true); return; }
                var r = t.Result;
                if (r.Erro != null || r.Valores.Count == 0) { Aviso.Mostrar(r.Resumo(), true); return; }
                AbrirFormulario();
                formDados.Receber(perfil.FormularioId, r.Valores);
                Aviso.Mostrar(r.Resumo());
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void Sair()
        {
            Dados.Limpar();
            atalhos.Dispose();
            icone.Visible = false;
            icone.Dispose();
            ExitThread();
        }
    }

    /// <summary>Janela invisível que recebe os atalhos globais registrados no Windows.</summary>
    class JanelaAtalhos : NativeWindow, IDisposable
    {
        public event Action<int> Pressionado;
        readonly System.Collections.Generic.List<int> registrados = new System.Collections.Generic.List<int>();

        public JanelaAtalhos() { CreateHandle(new CreateParams()); }

        public bool Registrar(int id, uint modificadores, Keys tecla)
        {
            bool ok = Nativo.RegisterHotKey(Handle, id, modificadores | Nativo.MOD_NOREPEAT, (uint)tecla);
            if (ok) registrados.Add(id);
            return ok;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Nativo.WM_HOTKEY && Pressionado != null) Pressionado(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            foreach (var id in registrados) Nativo.UnregisterHotKey(Handle, id);
            registrados.Clear();
            DestroyHandle();
        }
    }
}
