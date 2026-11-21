using System;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    public class FrmLogin : Form
    {
        private readonly TextBox txtUsuario;
        private readonly TextBox txtContrasena;
        private readonly Button btnIngresar;
        private readonly EmpleadoDAO _empleadoDao = new();

        public Empleado? EmpleadoAutenticado { get; private set; }

        public FrmLogin()
        {
            Text = "Pizzería Rhinos - Acceso al Sistema POS";
            Size = new Size(420, 310);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(30, 30, 36);
            ForeColor = Color.White;

            var lblTitulo = new Label
            {
                Text = "PIZZERÍA RHINOS - POS",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 165, 0),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 55
            };

            var lblSub = new Label
            {
                Text = "Acceso exclusivo para Cajeras y Administrador",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.LightGray,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 55),
                Size = new Size(365, 22)
            };

            var lblUsr = new Label { Text = "Usuario:", Location = new Point(55, 95), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtUsuario = new TextBox { Location = new Point(55, 118), Size = new Size(295, 27), Font = new Font("Segoe UI", 10), Text = "cajera1" };

            var lblPwd = new Label { Text = "Contraseña:", Location = new Point(55, 155), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtContrasena = new TextBox { Location = new Point(55, 178), Size = new Size(295, 27), Font = new Font("Segoe UI", 10), UseSystemPasswordChar = true, Text = "caja123" };

            btnIngresar = new Button
            {
                Text = "Iniciar Turno",
                Location = new Point(55, 220),
                Size = new Size(295, 36),
                BackColor = Color.FromArgb(211, 47, 47),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnIngresar.FlatAppearance.BorderSize = 0;
            btnIngresar.Click += BtnIngresar_Click;
            AcceptButton = btnIngresar;

            Controls.AddRange(new Control[] { lblTitulo, lblSub, lblUsr, txtUsuario, lblPwd, txtContrasena, btnIngresar });
        }

        private void BtnIngresar_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsuario.Text) || string.IsNullOrWhiteSpace(txtContrasena.Text))
            {
                MessageBox.Show("Ingresa usuario y contraseña.", "Campos obligatorios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var emp = _empleadoDao.Autenticar(txtUsuario.Text, txtContrasena.Text);
                if (emp == null)
                {
                    MessageBox.Show("Credenciales incorrectas o usuario sin permisos de caja/administración.", "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                EmpleadoAutenticado = emp;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de conexión con la base de datos: {ex.Message}", "Error SGBD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
