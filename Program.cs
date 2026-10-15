using System;
using System.Threading;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    static class Program
    {
        public const string ArgumentoAtualizado = "--atualizado";

        [STAThread]
        static void Main(string[] args)
        {
            // Logo após uma atualização a instância antiga ainda está fechando: espera ela soltar o mutex.
            var espera = Array.IndexOf(args, ArgumentoAtualizado) >= 0 ? 10000 : 0;
            using (var mutex = new Mutex(false, "BRMED.PreenchedorDeCadastro"))
            {
                bool primeiraInstancia;
                try { primeiraInstancia = mutex.WaitOne(espera); }
                catch (AbandonedMutexException) { primeiraInstancia = true; }
                if (!primeiraInstancia)
                {
                    MessageBox.Show("O Preenchedor de Cadastro já está aberto. Procure o ícone perto do relógio.",
                        "Preenchedor de Cadastro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Atualizador.Limpar();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContexto());
            }
        }
    }
}
