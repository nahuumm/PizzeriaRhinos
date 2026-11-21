using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            Size = new Size(460, 390);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = UITheme.BgBase;
            ForeColor = UITheme.TextPrimary;
            UITheme.ApplyRoundedCorners(this, 20);

            // Halo decorativo superior tipo Glassmorphism
            Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, 150),
                    Color.FromArgb(65, 239, 83, 80),
                    Color.FromArgb(0, 16, 18, 26),
                    90f);
                e.Graphics.FillRectangle(brush, 0, 0, Width, 150);
                UITheme.PaintGlassBorder(e, this, 20);
            };

            var btnCerrar = new Button
            {
                Text = "✕",
                Location = new Point(415, 12),
                Size = new Size(32, 32),
                FlatStyle = FlatStyle.Flat,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var lblMarca = new Label
            {
                Text = "PIZZERÍA RHINOS",
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(30, 26),
                Size = new Size(400, 34),
                BackColor = Color.Transparent
            };

            var lblSub = new Label
            {
                Text = "PUNTO DE VENTA & ADMINISTRACIÓN",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = UITheme.TextMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(30, 60),
                Size = new Size(400, 20),
                BackColor = Color.Transparent
            };

            // Tarjeta central Glass
            var card = new Panel
            {
                Location = new Point(40, 95),
                Size = new Size(380, 255)
            };
            UITheme.StyleGlassCard(card, 16);

            var lblUsr = new Label { Text = "USUARIO DE CAJA / ADMIN", Location = new Point(28, 24), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted };
            txtUsuario = new TextBox { Location = new Point(28, 45), Size = new Size(324, 30), Text = "cajera1" };
            UITheme.StyleTextBox(txtUsuario);

            var lblPwd = new Label { Text = "CONTRASEÑA", Location = new Point(28, 92), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted };
            txtContrasena = new TextBox { Location = new Point(28, 113), Size = new Size(324, 30), UseSystemPasswordChar = true, Text = "caja123" };
            UITheme.StyleTextBox(txtContrasena);

            btnIngresar = new Button
            {
                Text = "INICIAR TURNO",
                Location = new Point(28, 170),
                Size = new Size(324, 44)
            };
            UITheme.StyleButton(btnIngresar, UITheme.AccentPrimary, 12);
            btnIngresar.Click += BtnIngresar_Click;
            AcceptButton = btnIngresar;

            card.Controls.AddRange(new Control[] { lblUsr, txtUsuario, lblPwd, txtContrasena, btnIngresar });
            Controls.AddRange(new Control[] { btnCerrar, lblMarca, lblSub, card });
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