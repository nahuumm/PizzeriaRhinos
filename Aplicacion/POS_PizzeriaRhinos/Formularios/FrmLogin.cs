using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>Inicio de sesión de cajeras y administrador (Regla de negocio 2: sin cuentas para cocina ni repartidores).</summary>
    [DesignerCategory("Code")]
    public class FrmLogin : Form
    {
        private readonly TextBox txtUsuario;
        private readonly TextBox txtContrasena;
        private readonly BotonRhino btnIngresar;
        private readonly Label lblConexion;
        private readonly Label lblError;
        private readonly EmpleadoDAO _empleadoDao = new();

        public Empleado? EmpleadoAutenticado { get; private set; }

        public FrmLogin()
        {
            Text = "Rhinos Pizza - Iniciar sesión";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = UITheme.S(1000, 620);
            BackColor = UITheme.Superficie;
            Font = UITheme.FuenteNormal;
            Icon = UITheme.IconoApp;
            DoubleBuffered = true;
            KeyPreview = true;

            // ---------------- Lado izquierdo: marca ----------------
            var marca = new PanelMarcaLogin { Dock = DockStyle.Left, Width = UITheme.S(480) };

            // ---------------- Lado derecho: formulario ----------------
            var derecho = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.Superficie, Padding = UITheme.P(56, 40, 56, 30) };

            var btnCerrar = new BotonRhino("✕", EstiloBoton.Fantasma)
            {
                Size = UITheme.S(44, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            btnCerrar.Location = new Point(ClientSize.Width - UITheme.S(480) - btnCerrar.Width - UITheme.S(12), UITheme.S(12));
            btnCerrar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            var lblHola = new Label
            {
                Text = "¡Bienvenida al turno! 🍕",
                Font = UITheme.Titulo(26f),
                ForeColor = UITheme.Texto,
                AutoSize = false,
                Height = UITheme.S(50)
            };
            var lblSub = new Label
            {
                Text = "Ingresa con tu usuario de caja o de administrador.",
                Font = UITheme.Fuente(11f),
                ForeColor = UITheme.TextoSuave,
                AutoSize = false,
                Height = UITheme.S(30)
            };

            var lblUsuario = UITheme.CrearEtiqueta("USUARIO");
            lblUsuario.AutoSize = false;
            lblUsuario.Height = UITheme.S(26);
            txtUsuario = new TextBox { MaxLength = 30 };
            UITheme.EstilizarTextBox(txtUsuario);
            txtUsuario.Font = UITheme.Fuente(16f);

            var lblPwd = UITheme.CrearEtiqueta("CONTRASEÑA");
            lblPwd.AutoSize = false;
            lblPwd.Height = UITheme.S(26);

            var filaPwd = new Panel { Height = UITheme.S(44) };
            txtContrasena = new TextBox { UseSystemPasswordChar = true, MaxLength = 255, Dock = DockStyle.Fill };
            UITheme.EstilizarTextBox(txtContrasena);
            txtContrasena.Font = UITheme.Fuente(16f);
            var btnVer = new BotonRhino("👁", EstiloBoton.Secundario) { Dock = DockStyle.Right, Width = UITheme.S(56), TabStop = false };
            btnVer.Click += (_, _) =>
            {
                txtContrasena.UseSystemPasswordChar = !txtContrasena.UseSystemPasswordChar;
                btnVer.Seleccionado = !txtContrasena.UseSystemPasswordChar;
            };
            filaPwd.Controls.Add(txtContrasena);
            filaPwd.Controls.Add(new Panel { Dock = DockStyle.Right, Width = UITheme.S(8) });
            filaPwd.Controls.Add(btnVer);

            lblError = new Label
            {
                Text = "",
                ForeColor = UITheme.Rojo,
                Font = UITheme.Fuente(10.5f, FontStyle.Bold),
                AutoSize = false,
                Height = UITheme.S(40)
            };

            btnIngresar = new BotonRhino("INICIAR SESIÓN", EstiloBoton.Primario, "➜")
            {
                Height = UITheme.S(64),
                Font = new Font(UITheme.FamiliaBase, 14f, FontStyle.Bold)
            };
            btnIngresar.Click += BtnIngresar_Click;
            AcceptButton = btnIngresar;

            lblConexion = new Label
            {
                Text = "● Verificando conexión con MySQL…",
                ForeColor = UITheme.TextoSuave,
                Font = UITheme.FuenteChica,
                AutoSize = false,
                Height = UITheme.S(28),
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleLeft
            };

            derecho.Controls.Add(lblConexion);
            Diseno.Apilar(derecho, null,
                lblHola, lblSub, Diseno.Espacio(26),
                lblUsuario, txtUsuario, Diseno.Espacio(16),
                lblPwd, filaPwd, Diseno.Espacio(6),
                lblError, btnIngresar);

            Controls.Add(derecho);
            Controls.Add(marca);
            Controls.Add(btnCerrar);
            btnCerrar.BringToFront();

            Load += (_, _) =>
            {
                using var path = UITheme.RutaRedondeada(new Rectangle(0, 0, Width, Height), UITheme.S(22));
                Region = new Region(path);
                btnCerrar.Location = new Point(ClientSize.Width - btnCerrar.Width - UITheme.S(12), UITheme.S(12));
            };
            Shown += async (_, _) =>
            {
                txtUsuario.Focus();
                await VerificarConexionAsync();
            };
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        private async Task VerificarConexionAsync()
        {
            var (ok, mensaje) = await Task.Run(() =>
            {
                bool r = ConexionMySQL.ProbarConexion(out string m);
                return (r, m);
            });
            if (IsDisposed) return;
            lblConexion.Text = ok ? "● Conectado a la base de datos pizzeria_rhinos" : "● Sin conexión con MySQL — revisa el servicio y los secretos de usuario";
            lblConexion.ForeColor = ok ? UITheme.Verde : UITheme.Rojo;
            if (!ok) lblError.Text = mensaje.Length > 120 ? mensaje[..120] + "…" : mensaje;
        }

        private void BtnIngresar_Click(object? sender, EventArgs e)
        {
            lblError.Text = "";
            if (string.IsNullOrWhiteSpace(txtUsuario.Text) || string.IsNullOrWhiteSpace(txtContrasena.Text))
            {
                lblError.Text = "Escribe tu usuario y contraseña.";
                (string.IsNullOrWhiteSpace(txtUsuario.Text) ? txtUsuario : txtContrasena).Focus();
                return;
            }

            btnIngresar.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                var emp = _empleadoDao.Autenticar(txtUsuario.Text, txtContrasena.Text);
                if (emp == null)
                {
                    lblError.Text = "Usuario o contraseña incorrectos, o el usuario está inactivo.";
                    txtContrasena.SelectAll();
                    txtContrasena.Focus();
                    return;
                }
                EmpleadoAutenticado = emp;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Dialogo.Error(this, ErroresUi.Amigable(ex), "No se pudo iniciar sesión");
            }
            finally
            {
                btnIngresar.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(UITheme.Borde, 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = UITheme.RutaRedondeada(new Rectangle(1, 1, Width - 3, Height - 3), UITheme.S(22));
            e.Graphics.DrawPath(pen, path);
        }

        /// <summary>Panel de marca: degradado rojo, patrón de cuadros de pizzería y logo de Rhinos Pizza.</summary>
        [DesignerCategory("Code")]
        private class PanelMarcaLogin : Control
        {
            public PanelMarcaLogin()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var r = ClientRectangle;
                using (var b = new LinearGradientBrush(r, UITheme.RojoProfundo, UITheme.Rojo, 60f)) g.FillRectangle(b, r);

                // Cuadros estilo mantel de pizzería en la parte superior e inferior
                int lado = UITheme.S(28);
                using (var bc = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                {
                    for (int x = 0, c = 0; x < Width; x += lado, c++)
                    {
                        for (int fila = 0; fila < 2; fila++)
                        {
                            if ((c + fila) % 2 == 0)
                            {
                                g.FillRectangle(bc, x, fila * lado, lado, lado);
                                g.FillRectangle(bc, x, Height - (fila + 1) * lado, lado, lado);
                            }
                        }
                    }
                }

                // Tarjeta blanca con el logo (el logo original tiene contornos blancos)
                int margen = UITheme.S(36);
                var logo = UITheme.Logo;
                int anchoTarjeta = Width - margen * 2;
                int altoLogo = logo != null ? (int)(anchoTarjeta * (logo.Height / (float)logo.Width)) : UITheme.S(120);
                var tarjeta = new Rectangle(margen, Height / 2 - altoLogo / 2 - UITheme.S(70), anchoTarjeta, altoLogo + UITheme.S(30));
                using (var path = UITheme.RutaRedondeada(tarjeta, UITheme.S(22)))
                using (var bw = new SolidBrush(Color.FromArgb(250, 255, 255, 255)))
                    g.FillPath(bw, path);

                var rLogo = Rectangle.Inflate(tarjeta, -UITheme.S(14), -UITheme.S(14));
                if (logo != null)
                {
                    float escala = Math.Min(rLogo.Width / (float)logo.Width, rLogo.Height / (float)logo.Height);
                    int w = (int)(logo.Width * escala), h = (int)(logo.Height * escala);
                    g.DrawImage(logo, rLogo.X + (rLogo.Width - w) / 2, rLogo.Y + (rLogo.Height - h) / 2, w, h);
                }
                else
                {
                    using var f = UITheme.Titulo(34f);
                    TextRenderer.DrawText(g, "RHINOS PIZZA", f, rLogo, UITheme.Rojo,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                using var fTitulo = UITheme.Titulo(24f);
                var rTexto = new Rectangle(margen, tarjeta.Bottom + UITheme.S(28), anchoTarjeta, UITheme.S(110));
                Pintura.Lineas(g, rTexto, new System.Collections.Generic.List<(string, Font, Color)>
                {
                    ("PUNTO DE VENTA", fTitulo, Color.White),
                    ("Pedidos a domicilio y mostrador · Reparto · Inventario · Corte de caja",
                        UITheme.Fuente(10.5f), Color.FromArgb(230, 255, 236, 220))
                });
            }
        }
    }
}
