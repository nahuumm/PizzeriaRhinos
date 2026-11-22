using System.ComponentModel;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>Base de todos los módulos que se muestran dentro de la ventana principal.</summary>
    [DesignerCategory("Code")]
    public abstract class VistaBase : UserControl
    {
        protected FrmPrincipal Shell { get; }

        protected VistaBase(FrmPrincipal shell)
        {
            Shell = shell;
            Dock = DockStyle.Fill;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            DoubleBuffered = true;
        }

        /// <summary>
        /// Se invoca cada vez que el módulo se vuelve visible: recarga los datos desde MySQL
        /// para que siempre se vea la información persistida más reciente.
        /// </summary>
        public abstract void AlMostrar();
    }
}
