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
            Size = new Size(480, 490);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = UITheme.BgBase;
            ForeColor = UITheme.TextPrimary;
            UITheme.ApplyRoundedCorners(this, 22);

            Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, 190),
                    Color.FromArgb(85, 217, 56, 58),
                    Color.FromArgb(0, 18, 20, 28),
                    90f);
                e.Graphics.FillRectangle(brush, 0, 0, Width, 190);
                UITheme.PaintGlassBorder(e, this, 22);
            };

            var btnCerrar = new Button
            {
                Text = "✕",
                Location = new Point(432, 12),
                Size = new Size(34, 34),
                FlatStyle = FlatStyle.Flat,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            // Contenedor superior con el Logo Oficial de Rhinos Pizza
            var pnlLogo = new Panel
            {
                Location = new Point(55, 22),
                Size = new Size(370, 125),
                BackColor = Color.White
            };
            UITheme.ApplyRoundedCorners(pnlLogo, 16);

            var picLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Image = UITheme.CargarLogoRhinos()
            };

            if (picLogo.Image == null)
            {
                var lblFallback = new Label
                {
                    Text = "RHINOS PIZZA\nEs Rica y Crujiente!",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = UITheme.AccentPrimary
                };
                pnlLogo.Controls.Add(lblFallback);
            }
            else
            {
                pnlLogo.Controls.Add(picLogo);
            }

            var lblSub = new Label
            {
                Text = "PUNTO DE VENTA, REPARTO E INVENTARIO",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(30, 156),
                Size = new Size(420, 22),
                BackColor = Color.Transparent
            };

            var card = new Panel
            {
                Location = new Point(40, 188),
                Size = new Size(400, 268)
            };
            UITheme.StyleGlassCard(card, 18);

            var lblUsr = new Label
            {
                Text = "USUARIO DE CAJA / ADMINISTRADOR",
                Location = new Point(28, 24),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = UITheme.TextMuted
            };
            txtUsuario = new TextBox { Location = new Point(28, 48), Size = new Size(344, 34), Text = "cajera1" };
            UITheme.StyleTextBox(txtUsuario);

            var lblPwd = new Label
            {
                Text = "CONTRASEÑA",
                Location = new Point(28, 98),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = UITheme.TextMuted
            };
            txtContrasena = new TextBox { Location = new Point(28, 122), Size = new Size(344, 34), UseSystemPasswordChar = true, Text = "caja123" };
            UITheme.StyleTextBox(txtContrasena);

            btnIngresar = new Button
            {
                Text = "INICIAR TURNO EN CAJA",
                Location = new Point(28, 182),
                Size = new Size(344, 52)
            };
            UITheme.StyleButton(btnIngresar, UITheme.AccentPrimary, 12, 11f);
            btnIngresar.Click += BtnIngresar_Click;
            AcceptButton = btnIngresar;

            card.Controls.AddRange(new Control[] { lblUsr, txtUsuario, lblPwd, txtContrasena, btnIngresar });
            Controls.AddRange(new Control[] { btnCerrar, pnlLogo, lblSub, card });
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
                MessageBox.Show($"Error de conexión con la base de datos:\n{ex.Message}", "Error SGBD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}