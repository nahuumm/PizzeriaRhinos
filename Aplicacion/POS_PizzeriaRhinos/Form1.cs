using System;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Config;

namespace POS_PizzeriaRhinos
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            if (ConexionMySQL.ProbarConexion(out string mensaje))
            {
                MessageBox.Show(mensaje, "POS Pizzería Rhinos - BD Conectada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(mensaje, "Error de Conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
