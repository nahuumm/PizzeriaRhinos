using System;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
                Dialogo.Error(null, ErroresUi.Amigable(e.Exception), "Error inesperado");

            // Ciclo de sesión: al "Cerrar sesión" se regresa al login para el siguiente turno.
            while (true)
            {
                using var login = new FrmLogin();
                if (login.ShowDialog() != DialogResult.OK || login.EmpleadoAutenticado == null)
                    break;

                Sesion.Iniciar(login.EmpleadoAutenticado);
                using var principal = new FrmPrincipal();
                Application.Run(principal);
                Sesion.Cerrar();

                if (!principal.CerroSesion)
                    break;
            }
        }
    }
}
