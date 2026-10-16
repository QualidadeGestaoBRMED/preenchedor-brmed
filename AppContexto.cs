using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    /// <summary>Programa que fica na bandeja do Windows e atende os atalhos globais.</summary>
    class AppContexto : ApplicationContext
    {
        readonly NotifyIcon icone;
        readonly JanelaAtalhos atalhos;
        readonly SynchronizationContext ui;
        FormDados formDados;
        FormPerfis formPerfis;
        FormMapear formMapear;
        FormAtalhos formAtalhos;
        ToolStripItem itemFormulario;
        ToolStripItem itemAtualizar;
        Novidade novidade;
        bool ocupado;
        // Última janela de outro programa que esteve em primeiro plano: é o destino dos botões do formulário.
        IntPtr ultimaJanela;
        readonly uint meuPid = (uint)Process.GetCurrentProcess().Id;
        System.Windows.Forms.Timer rastreador;

        public AppContexto()
        {
            // Garante um contexto de sincronização para voltar à thread da interface.
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ui = SynchronizationContext.Current;
            Aviso.Configurar(ui);
            Dados.FormularioAtivo = RepositorioFormularios.IdPrincipal;

            var menu = new ContextMenuStrip();
            itemFormulario = menu.Items.Add("Abrir formulário", null, (s, e) => AbrirFormulario());
            menu.Items.Add("Gerenciar formulários…", null, (s, e) => FormModelos.Abrir());
            var principal = new ToolStripMenuItem("Formulário principal");
            principal.DropDownOpening += (s, e) => MontarMenuPrincipal(principal);
            principal.DropDownItems.Add("…");
            var config = new ToolStripMenuItem("Configurações");
            config.DropDownItems.Add(principal);
            config.DropDownItems.Add("Atalhos…", null, (s, e) => AbrirAtalhos());
            config.DropDownItems.Add(MontarMenuDiagnostico());
            menu.Items.Add(config);
            menu.Items.Add("Sistemas mapeados…", null, (s, e) => AbrirPerfis());
            menu.Items.Add("Abrir sistema de teste", null, (s, e) => new SistemaTeste().Show());
            menu.Items.Add(new ToolStripSeparator());
            itemAtualizar = menu.Items.Add("Verificar atualizações", null, (s, e) => AoClicarAtualizar());
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
            var falhas = RegistrarAtalhos();

            AtualizarMenu();
            RepositorioPerfis.Todos(); // cria o arquivo de perfis na primeira execução

            if (falhas.Count > 0)
                Aviso.Mostrar("Outro programa já usa " + string.Join(", ", falhas) + ". Esses atalhos não vão funcionar.", true);
            else
                Aviso.Mostrar("Preenchedor ativo. " + Atalhos.Texto(Acao.Formulario) + " abre o formulário.");

            IniciarVerificacaoAutomatica();
            IniciarRastreio();
        }

        void IniciarRastreio()
        {
            rastreador = new System.Windows.Forms.Timer { Interval = 300 };
            rastreador.Tick += (s, e) =>
            {
                var janela = Nativo.GetForegroundWindow();
                if (janela == IntPtr.Zero) return;
                uint pid;
                Nativo.GetWindowThreadProcessId(janela, out pid);
                if (pid == meuPid || Nativo.EhJanelaDoShell(janela)) return;
                ultimaJanela = janela;
            };
            rastreador.Start();
        }

        /// <summary>Janela do sistema onde o usuário estava antes de voltar ao formulário; zero se não houver.</summary>
        IntPtr JanelaDeDestino()
        {
            if (ultimaJanela == IntPtr.Zero || !Nativo.IsWindow(ultimaJanela))
            {
                Aviso.Mostrar("Abra o sistema onde quer preencher, clique nele e volte ao formulário.", true);
                return IntPtr.Zero;
            }
            return ultimaJanela;
        }

        void IniciarVerificacaoAutomatica()
        {
            var inicio = new System.Windows.Forms.Timer { Interval = 20 * 1000 };
            inicio.Tick += (s, e) =>
            {
                inicio.Stop();
                VerificarAtualizacao(false);
                var periodico = new System.Windows.Forms.Timer { Interval = 60 * 60 * 1000 };
                periodico.Tick += (s2, e2) => VerificarAtualizacao(false);
                periodico.Start();
            };
            inicio.Start();
        }

        /// <summary>Consulta as Releases fora da thread da interface; só fala com o usuário se achar versão nova (ou se ele pediu).</summary>
        void VerificarAtualizacao(bool manual)
        {
            if (ocupado || novidade != null) return;
            Task.Run(() => Atualizador.Verificar()).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    if (manual) Aviso.Mostrar("Não consegui verificar atualizações. Confira a conexão com a internet.", true);
                    return;
                }
                if (t.Result == null)
                {
                    if (manual) Aviso.Mostrar("Você já está na versão mais recente (" + Atualizador.VersaoAtual + ").");
                    return;
                }
                novidade = t.Result;
                itemAtualizar.Text = "Atualizar para a versão " + novidade.Versao;
                Aviso.Mostrar("Há uma versão nova (" + novidade.Versao + "). Clique no ícone perto do relógio e escolha \"Atualizar\".");
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void AoClicarAtualizar()
        {
            if (novidade == null) { VerificarAtualizacao(true); return; }
            if (ocupado) return;
            ocupado = true;
            var alvo = novidade;
            Aviso.Mostrar("Baixando a versão " + alvo.Versao + "…");
            Task.Run(() => { Atualizador.Baixar(alvo); Atualizador.Instalar(); }).ContinueWith(t =>
            {
                ocupado = false;
                if (t.Exception != null)
                {
                    Aviso.Mostrar("Não consegui atualizar: " + t.Exception.GetBaseException().Message, true);
                    return;
                }
                Sair();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Liga o log temporário que registra por que um programa de terceiros recusou o preenchimento.</summary>
        ToolStripMenuItem MontarMenuDiagnostico()
        {
            var menu = new ToolStripMenuItem("Modo diagnóstico");
            var ligar = new ToolStripMenuItem("Ligar por 24 horas");
            ligar.Click += (s, e) =>
            {
                if (Diagnostico.Ativo) { Diagnostico.Desligar(); Aviso.Mostrar("Modo diagnóstico desligado."); }
                else { Diagnostico.Ligar(); Aviso.Mostrar("Modo diagnóstico ligado por 24 horas. Os valores digitados não são gravados."); }
            };
            menu.DropDownOpening += (s, e) => { ligar.Text = Diagnostico.Ativo ? "Desligar (está ligado)" : "Ligar por 24 horas"; };
            menu.DropDownItems.Add(ligar);
            menu.DropDownItems.Add("Abrir o log", null, (s, e) =>
            {
                if (File.Exists(Diagnostico.Caminho)) Process.Start("notepad.exe", "\"" + Diagnostico.Caminho + "\"");
                else Aviso.Mostrar("Ainda não há log. Ligue o modo diagnóstico e repita o que deu erro.");
            });
            menu.DropDownItems.Add("Apagar o log", null, (s, e) => { Diagnostico.Apagar(); Aviso.Mostrar("Log apagado."); });
            return menu;
        }

        void AtualizarMenu() { itemFormulario.Text = "Abrir formulário  (" + Atalhos.Texto(Acao.Formulario) + ")"; }

        /// <summary>Registra os atalhos salvos; devolve os que o Windows recusou (já em uso por outro programa).</summary>
        System.Collections.Generic.List<string> RegistrarAtalhos()
        {
            atalhos.Limpar();
            var falhas = new System.Collections.Generic.List<string>();
            var salvos = Atalhos.Carregar();
            foreach (var a in Atalhos.Todas)
                if (!atalhos.Registrar((int)a + 1, salvos[a].Modificadores, salvos[a].Tecla)) falhas.Add(salvos[a].ToString());
            return falhas;
        }

        void AbrirAtalhos()
        {
            if (formAtalhos != null && !formAtalhos.IsDisposed) { formAtalhos.Activate(); return; }
            formAtalhos = new FormAtalhos();
            formAtalhos.FormClosed += (s, e) =>
            {
                if (formAtalhos.DialogResult != DialogResult.OK) return;
                var falhas = RegistrarAtalhos();
                AtualizarMenu();
                if (falhas.Count > 0) Aviso.Mostrar("Outro programa já usa " + string.Join(", ", falhas) + ". Esses atalhos não vão funcionar.", true);
                else Aviso.Mostrar("Atalhos atualizados.");
            };
            formAtalhos.Show();
        }

        void AoPressionar(int id)
        {
            switch ((Acao)(id - 1))
            {
                case Acao.Formulario: AbrirFormulario(); break;
                case Acao.Colar: Colar(Nativo.GetForegroundWindow(), false); break;
                case Acao.Mapear: Mapear(false); break;
                case Acao.MapearCaptura: Mapear(true); break;
                case Acao.Capturar: Capturar(Nativo.GetForegroundWindow(), false); break;
            }
        }

        /// <summary>Lista os formulários no menu; o marcado é o que sempre abre com o atalho do formulário.</summary>
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

        /// <param name="manterAtual">true = se a janela já existe, não troca o formulário que o colaborador está usando.</param>
        void AbrirFormulario(bool manterAtual = false)
        {
            if (formDados == null || formDados.IsDisposed)
            {
                formDados = new FormDados();
                formDados.PedirColar += () => { var j = JanelaDeDestino(); if (j != IntPtr.Zero) Colar(j, true); };
                formDados.PedirCapturar += () => { var j = JanelaDeDestino(); if (j != IntPtr.Zero) Capturar(j, true); };
                formDados.Show();
            }
            else if (!manterAtual) formDados.Selecionar(RepositorioFormularios.IdPrincipal);
            formDados.WindowState = FormWindowState.Normal;
            formDados.Activate();
        }

        void AbrirPerfis()
        {
            if (formPerfis == null || formPerfis.IsDisposed) formPerfis = new FormPerfis();
            formPerfis.Show();
            formPerfis.Activate();
        }

        void Colar(IntPtr janela, bool ativar)
        {
            if (ocupado) return;
            uint pid;
            Nativo.GetWindowThreadProcessId(janela, out pid);
            string processo, titulo = Nativo.TituloDaJanela(janela);
            try { processo = Process.GetProcessById((int)pid).ProcessName; }
            catch (ArgumentException) { return; }

            var perfil = RepositorioPerfis.Encontrar(processo, titulo);
            if (perfil == null)
            {
                Aviso.Mostrar("O programa \"" + processo + "\" ainda não foi mapeado. Aponte o mouse para um campo e use " + Atalhos.Texto(Acao.Mapear) + ".", true);
                return;
            }

            if (!Dados.TemDados(perfil.FormularioId))
            {
                Aviso.Mostrar("Nenhum dado salvo para o formulário \"" + RepositorioFormularios.Obter(perfil.FormularioId).Nome
                    + "\". Use " + Atalhos.Texto(Acao.Formulario) + " para preenchê-lo.", true);
                return;
            }

            ocupado = true;
            // A UI Automation roda fora da thread da interface para não travar (inclusive no sistema de teste).
            Task.Run(() => { if (ativar) AtivarEEsperar(janela); return Preenchedor.Colar(perfil, janela); }).ContinueWith(t =>
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
                try
                {
                    var c = t.Result;
                    if (c.Erro != null) { Aviso.Mostrar(c.Erro, true); return; }
                    if (formMapear != null && !formMapear.IsDisposed) formMapear.Close();
                    formMapear = new FormMapear(c, new Point(p.X, p.Y), paraCaptura);
                    formMapear.Show();
                }
                catch (Exception erro)
                {
                    Aviso.Mostrar("Não consegui abrir o menu de mapeamento: " + erro.GetBaseException().Message, true);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Devolve o foco ao sistema de destino (o clique no botão o tirou de lá) e espera ele assentar.</summary>
        static void AtivarEEsperar(IntPtr janela)
        {
            Nativo.Ativar(janela);
            Thread.Sleep(250);
        }

        void Capturar(IntPtr janela, bool ativar)
        {
            if (ocupado) return;
            uint pid;
            Nativo.GetWindowThreadProcessId(janela, out pid);
            string processo, titulo = Nativo.TituloDaJanela(janela);
            try { processo = Process.GetProcessById((int)pid).ProcessName; }
            catch (ArgumentException) { return; }

            var perfil = RepositorioPerfis.Capturavel(processo, titulo);
            // Em navegador, tenta primeiro a captura padrão do BRNET; os campos mapeados ficam de reserva.
            bool navegador = CapturaBrnet.EhNavegador(processo);
            if (perfil == null && !navegador)
            {
                Aviso.Mostrar("Nenhum campo de captura mapeado para \"" + processo + "\". Aponte o mouse para um campo e use " + Atalhos.Texto(Acao.MapearCaptura) + ".", true);
                return;
            }

            ocupado = true;
            Task.Run(() =>
            {
                if (ativar) AtivarEEsperar(janela);
                if (navegador)
                {
                    var brnet = CapturaBrnet.Capturar(janela);
                    if (brnet.Encontrados > 0 || perfil == null) return brnet;
                }
                return Capturador.Capturar(perfil, janela);
            }).ContinueWith(t =>
            {
                ocupado = false;
                if (t.Exception != null) { Aviso.Mostrar("Não consegui capturar: " + t.Exception.GetBaseException().Message, true); return; }
                var r = t.Result;
                if (r.Erro != null || r.Valores.Count == 0) { Aviso.Mostrar(r.Resumo(), true); return; }
                AbrirFormulario(r.Brnet);
                if (r.Brnet)
                {
                    int faltam = formDados.ReceberBrnet(r.Valores);
                    Aviso.Mostrar((r.Valores.Count == 1 ? "1 campo copiado." : r.Valores.Count + " campos copiados.")
                        + (faltam > 0 ? " Preencha " + (faltam == 1 ? "o campo destacado" : "os " + faltam + " campos destacados") + " e salve." : " Salve para poder colar."));
                }
                else
                {
                    formDados.Receber(perfil.FormularioId, r.Valores);
                    Aviso.Mostrar(r.Resumo());
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void Sair()
        {
            Dados.Limpar();
            atalhos.Dispose();
            rastreador.Dispose();
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

        public void Limpar()
        {
            foreach (var id in registrados) Nativo.UnregisterHotKey(Handle, id);
            registrados.Clear();
        }

        public void Dispose()
        {
            Limpar();
            DestroyHandle();
        }
    }
}
