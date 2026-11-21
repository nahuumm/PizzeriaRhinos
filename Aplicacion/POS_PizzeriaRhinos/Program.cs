using System;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using var login = new FrmLogin();
            if (login.ShowDialog() == DialogResult.OK && login.EmpleadoAutenticado != null)
            {
                Application.Run(new FrmPrincipalPOS(login.EmpleadoAutenticado));
            }
        }
    }
}
