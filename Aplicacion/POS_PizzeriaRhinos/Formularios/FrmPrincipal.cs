using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Modelos;
using POS_PizzeriaRhinos.Vistas;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>
    /// Ventana principal: barra lateral (como el boceto) + área de contenido donde se cargan los módulos.
    /// Los módulos de administración solo aparecen para el rol ADMIN.
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmPrincipal : Form
    {
        private record ItemMenu(string Clave, string Texto, string Icono, bool SoloAdmin, Func<VistaBase> Crear);

        private readonly Panel _barraLateral;
        private readonly Panel _contenedorNav;
        private readonly Panel _contenido;
        private readonly PictureBox _picLogo;
        private readonly Label _lblUsuario;
        private readonly Label _lblRol;
        private readonly Label _lblReloj;
        private readonly BotonRhino _btnSalir;
        private readonly List<ItemMenu> _items;
        private readonly Dictionary<string, BotonRhino> _botonesNav = new();
        private readonly Dictionary<string, VistaBase> _vistas = new();
        private readonly System.Windows.Forms.Timer _reloj;
        private string _vistaActual = "";
        private bool _compacta;
        private Image? _logoCompacto;

        public bool CerroSesion { get; private set; }

        public FrmPrincipal()
        {
            var emp = Sesion.Empleado ?? throw new InvalidOperationException("No hay una sesión iniciada.");
            Text = $"Rhinos Pizza · Punto de venta — {emp.Nombre} ({emp.Rol})";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterScreen;
            Size = UITheme.S(1366, 800);
            MinimumSize = UITheme.S(1024, 680);
            WindowState = FormWindowState.Maximized;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            DoubleBuffered = true;

            _items = new List<ItemMenu>
            {
                new("inicio", "Inicio", "🏠", false, () => new VistaDashboard(this)),
                new("venta", "Nuevo pedido", "🍕", false, () => new VistaPuntoVenta(this)),
                new("pedidos", "Pedidos", "📋", false, () => new VistaPedidos(this)),
                new("clientes", "Clientes", "👥", false, () => new VistaClientes(this)),
                new("inventario", "Inventario", "📦", true, () => new VistaInventario(this)),
                new("empleados", "Empleados", "🪪", true, () => new VistaEmpleados(this)),
                new("reportes", "Reportes", "📊", true, () => new VistaReportes(this)),
            };

            // ---------------- Contenido ----------------
            _contenido = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(22, 18, 22, 18), BackColor = UITheme.Fondo };

            // ---------------- Barra lateral ----------------
            _barraLateral = new Panel { Dock = DockStyle.Left, Width = UITheme.S(250), BackColor = UITheme.Superficie };
            _barraLateral.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.Borde, 1);
                e.Graphics.DrawLine(pen, _barraLateral.Width - 1, 0, _barraLateral.Width - 1, _barraLateral.Height);
            };

            _picLogo = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = UITheme.S(110),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = UITheme.Logo,
                Padding = UITheme.P(14, 14, 14, 6),
                Cursor = Cursors.Hand
            };
            _picLogo.Click += (_, _) => Navegar("inicio");

            // Sección intermedia con desplazamiento vertical: el logo (arriba) y la tarjeta de usuario (abajo) quedan fijos
            _contenedorNav = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(12, 10, 12, 10), AutoScroll = true };
            _contenedorNav.HorizontalScroll.Maximum = 0;
            _contenedorNav.HorizontalScroll.Visible = false;
            _contenedorNav.HorizontalScroll.Enabled = false;
            _contenedorNav.MouseWheel += (_, e) => DesplazarNav(e);
            foreach (var item in _items.Where(i => !i.SoloAdmin || Sesion.EsAdmin))
            {
                var b = new BotonRhino(item.Texto, EstiloBoton.Fantasma, item.Icono)
                {
                    Disposicion = DisposicionBoton.Izquierda,
                    TamanoIcono = 16f,
                    Height = UITheme.S(56),
                    Font = new Font(UITheme.FamiliaBase, 12f, FontStyle.Bold),
                    Valor = item.Clave
                };
                string clave = item.Clave;
                b.Click += (_, _) => Navegar(clave);
                // La rueda del ratón sobre un botón mueve el menú completo
                b.MouseWheel += (_, e) => DesplazarNav(e);
                _botonesNav[item.Clave] = b;
                _contenedorNav.Controls.Add(b);
            }
            _contenedorNav.Layout += (_, _) => AcomodarNav();
            _contenedorNav.ClientSizeChanged += (_, _) => AcomodarNav();

            // Tarjeta de usuario en la parte inferior
            var pnlUsuario = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(170), Padding = UITheme.P(14, 8, 14, 14) };
            pnlUsuario.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.Borde);
                e.Graphics.DrawLine(pen, UITheme.S(14), 0, pnlUsuario.Width - UITheme.S(14), 0);
            };
            _lblUsuario = new Label { Text = Sesion.NombreCorto, Font = UITheme.Fuente(11.5f, FontStyle.Bold), ForeColor = UITheme.Texto, AutoSize = false, Height = UITheme.S(26), AutoEllipsis = true };
            _lblRol = new Label
            {
                Text = Sesion.EsAdmin ? "● Administrador" : "● Cajera en turno",
                Font = UITheme.FuenteEtiqueta,
                ForeColor = Sesion.EsAdmin ? UITheme.RojoOscuro : UITheme.Verde,
                AutoSize = false,
                Height = UITheme.S(22)
            };
            _lblReloj = new Label { Font = UITheme.FuenteChica, ForeColor = UITheme.TextoSuave, AutoSize = false, Height = UITheme.S(24) };
            _btnSalir = new BotonRhino("Cerrar sesión", EstiloBoton.Secundario, "⏻") { Height = UITheme.S(52) };
            _btnSalir.Click += (_, _) => CerrarSesion();
            Diseno.Apilar(pnlUsuario, null, Diseno.Espacio(10), _lblUsuario, _lblRol, _lblReloj, Diseno.Espacio(6), _btnSalir);

            _barraLateral.Controls.Add(_contenedorNav);
            _barraLateral.Controls.Add(pnlUsuario);
            _barraLateral.Controls.Add(_picLogo);

            Controls.Add(_contenido);
            Controls.Add(_barraLateral);

            _reloj = new System.Windows.Forms.Timer { Interval = 15000 };
            _reloj.Tick += (_, _) => ActualizarReloj();
            _reloj.Start();
            ActualizarReloj();

            Resize += (_, _) => AjustarResponsivo();
            Load += (_, _) =>
            {
                AjustarResponsivo();
                Navegar("inicio");
            };
            FormClosing += FrmPrincipal_FormClosing;
        }

        // ---------------------------------------------------------------
        // NAVEGACIÓN
        // ---------------------------------------------------------------
        public void Navegar(string clave)
        {
            var item = _items.FirstOrDefault(i => i.Clave == clave);
            if (item == null) return;
            if (item.SoloAdmin && !Sesion.EsAdmin)
            {
                Dialogo.Advertencia(this, "Este módulo es exclusivo del administrador.", "Acceso restringido");
                return;
            }

            if (!_vistas.TryGetValue(clave, out var vista))
            {
                try
                {
                    vista = item.Crear();
                }
                catch (Exception ex)
                {
                    Dialogo.Error(this, ErroresUi.Amigable(ex), "No se pudo abrir el módulo");
                    return;
                }
                vista.Visible = false;
                _vistas[clave] = vista;
                _contenido.Controls.Add(vista);
            }

            SuspendLayout();
            foreach (var v in _vistas.Values) v.Visible = false;
            vista.Visible = true;
            vista.BringToFront();
            ResumeLayout(true);

            foreach (var kv in _botonesNav) kv.Value.Seleccionado = kv.Key == clave;
            _vistaActual = clave;

            try
            {
                Cursor = Cursors.WaitCursor;
                vista.AlMostrar();
            }
            catch (Exception ex)
            {
                Dialogo.Error(this, ErroresUi.Amigable(ex), "No se pudieron cargar los datos");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        /// <summary>Abre "Nuevo pedido" con un cliente ya cargado (desde el módulo de Clientes).</summary>
        public void AbrirNuevoPedidoPara(Cliente cliente)
        {
            Navegar("venta");
            if (_vistas.TryGetValue("venta", out var v) && v is VistaPuntoVenta pos)
                pos.CargarCliente(cliente);
        }

        /// <summary>Marca que los datos cambiaron para que otros módulos se recarguen al mostrarse.</summary>
        public string VistaActual => _vistaActual;

        // ---------------------------------------------------------------
        // DISEÑO RESPONSIVO
        // ---------------------------------------------------------------
        private void AjustarResponsivo()
        {
            if (WindowState == FormWindowState.Minimized) return;
            bool compacta = ClientSize.Width < UITheme.S(1280);
            if (compacta == _compacta && _barraLateral.Width > 0) { AcomodarNav(); return; }
            _compacta = compacta;
            _barraLateral.Width = compacta ? UITheme.S(96) : UITheme.S(250);
            if (compacta)
            {
                _logoCompacto ??= UITheme.IconoApp is Icon ic ? new Icon(ic, 128, 128).ToBitmap() : UITheme.Logo;
                _picLogo.Image = _logoCompacto;
                _picLogo.Height = UITheme.S(84);
            }
            else
            {
                _picLogo.Image = UITheme.Logo;
                _picLogo.Height = UITheme.S(110);
            }
            _lblUsuario.Visible = !compacta;
            _lblRol.Visible = !compacta;
            _lblReloj.Visible = !compacta;
            _btnSalir.Text = compacta ? "" : "Cerrar sesión";
            _contenido.Padding = compacta ? UITheme.P(14, 12, 14, 12) : UITheme.P(22, 18, 22, 18);
            AcomodarNav();
        }

        private bool _acomodandoNav;

        /// <summary>
        /// Coloca los botones del menú uno debajo de otro. El ancho siempre es el visible (sin barra horizontal);
        /// si no caben a lo alto, el panel muestra la barra vertical.
        /// </summary>
        private void AcomodarNav()
        {
            if (_acomodandoNav) return;
            _acomodandoNav = true;
            try
            {
                int gap = UITheme.S(6);
                int alto = UITheme.S(56);
                var pad = _contenedorNav.Padding;
                for (int intento = 0; intento < 3; intento++)
                {
                    int ancho = Math.Max(UITheme.S(40), _contenedorNav.ClientSize.Width - pad.Horizontal);
                    int y = pad.Top + _contenedorNav.AutoScrollPosition.Y;
                    foreach (var b in _botonesNav.Values)
                    {
                        b.SetBounds(pad.Left, y, ancho, alto);
                        y += alto + gap;
                    }
                    int total = _botonesNav.Count * (alto + gap) - gap + pad.Vertical;
                    _contenedorNav.AutoScrollMinSize = new Size(0, total);
                    if (_contenedorNav.ClientSize.Width - pad.Horizontal == ancho) break;
                }
            }
            finally
            {
                _acomodandoNav = false;
            }
        }

        private void DesplazarNav(MouseEventArgs e)
        {
            int actual = -_contenedorNav.AutoScrollPosition.Y;
            int maximo = Math.Max(0, _contenedorNav.DisplayRectangle.Height - _contenedorNav.ClientSize.Height);
            int nuevo = Math.Clamp(actual - e.Delta / 120 * UITheme.S(62), 0, maximo);
            _contenedorNav.AutoScrollPosition = new Point(0, nuevo);
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        private void ActualizarReloj()
        {
            _lblReloj.Text = DateTime.Now.ToString("ddd dd MMM yyyy · HH:mm", UITheme.Cultura);
        }

        // ---------------------------------------------------------------
        // SESIÓN
        // ---------------------------------------------------------------
        private void CerrarSesion()
        {
            if (!Dialogo.Confirmar(this, "Cerrar sesión", $"¿Terminar el turno de {Sesion.NombreCorto}? Volverás a la pantalla de inicio de sesión.",
                    "Sí, cerrar sesión", "Cancelar"))
                return;
            CerroSesion = true;
            Close();
        }

        private void FrmPrincipal_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (CerroSesion || e.CloseReason != CloseReason.UserClosing) return;
            if (!Dialogo.Confirmar(this, "Salir del sistema", "¿Deseas cerrar el punto de venta?", "Sí, salir", "Cancelar"))
                e.Cancel = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reloj.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
