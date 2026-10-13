using System;
using System.Threading;
using System.Windows.Forms;

namespace PreenchedorBRMED
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool primeiraInstancia;
            using (var mutex = new Mutex(true, "BRMED.PreenchedorDeCadastro", out primeiraInstancia))
            {
                if (!primeiraInstancia)
                {
                    MessageBox.Show("O Preenchedor de Cadastro já está aberto. Procure o ícone perto do relógio.",
                        "Preenchedor de Cadastro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContexto());
            }
        }
    }
}
