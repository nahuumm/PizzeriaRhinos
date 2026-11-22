using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>
    /// CRUD de clientes (boceto pág. 3: Añadir / Eliminar / Buscar + Lista de clientes) con historial de pedidos.
    /// </summary>
    [DesignerCategory("Code")]
    public class VistaClientes : VistaBase
    {
        private readonly ClienteDAO _clienteDao = new();
        private readonly TextBox _txtBuscar;
        private readonly PanelTabla _tablaClientes;
        private readonly PanelTabla _tablaHistorial;
        private readonly Label _lblHistorial;
        private readonly BotonRhino _btnEditar, _btnEliminar, _btnPedido, _btnDetalle;
        private readonly System.Windows.Forms.Timer _debounce;

        public VistaClientes(FrmPrincipal shell) : base(shell)
        {
            var encabezado = new EncabezadoModulo("Clientes",
                "Búsqueda por teléfono, nombre o colonia. Al seleccionar un cliente se muestra su historial de pedidos.", "👥");

            // ---------------- Barra de acciones (como el boceto) ----------------
            var btnAnadir = new BotonRhino("Añadir cliente", EstiloBoton.Primario, "➕") { Size = UITheme.S(200, 60) };
            btnAnadir.Click += (_, _) => Anadir();
            _btnEditar = new BotonRhino("Editar", EstiloBoton.Secundario, "✏") { Size = UITheme.S(150, 60) };
            _btnEditar.Click += (_, _) => Editar();
            _btnEliminar = new BotonRhino("Eliminar cliente", EstiloBoton.Secundario, "🗑") { Size = UITheme.S(200, 60) };
            _btnEliminar.Click += (_, _) => Eliminar();
            _btnPedido = new BotonRhino("Nuevo pedido", EstiloBoton.Exito, "🍕") { Size = UITheme.S(190, 60) };
            _btnPedido.Click += (_, _) => NuevoPedido();

            _txtBuscar = new TextBox { Width = UITheme.S(300), MaxLength = 60, PlaceholderText = "Buscar cliente…", Margin = UITheme.P(16, 12, 4, 4) };
            UITheme.EstilizarTextBox(_txtBuscar);
            _debounce = new System.Windows.Forms.Timer { Interval = 350 };
            _debounce.Tick += (_, _) => { _debounce.Stop(); Recargar(); };
            _txtBuscar.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
            var btnBuscar = new BotonRhino("Buscar cliente", EstiloBoton.Ambar, "🔍") { Size = UITheme.S(190, 60) };
            btnBuscar.Click += (_, _) => Recargar();

            var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            barra.Controls.AddRange(new Control[] { btnAnadir, _btnEditar, _btnEliminar, _btnPedido, _txtBuscar, btnBuscar });

            // ---------------- Lista de clientes ----------------
            var lblLista = new Label { Text = "Lista de clientes", Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Height = UITheme.S(34), TextAlign = ContentAlignment.BottomLeft };
            _tablaClientes = new PanelTabla();
            _tablaClientes.Grid.SelectionChanged += (_, _) => SeleccionCambiada();
            _tablaClientes.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Editar(); };

            // ---------------- Historial ----------------
            _lblHistorial = new Label { Text = "Historial de pedidos", Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _btnDetalle = new BotonRhino("Ver detalle", EstiloBoton.Secundario, "👁") { Dock = DockStyle.Right, Width = UITheme.S(170) };
            _btnDetalle.Click += (_, _) => VerDetalle();
            var filaHist = new Panel { Height = UITheme.S(52), Padding = UITheme.P(0, 6, 0, 2) };
            filaHist.Controls.Add(_lblHistorial);
            filaHist.Controls.Add(_btnDetalle);
            _tablaHistorial = new PanelTabla();
            _tablaHistorial.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) VerDetalle(); };

            var division = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            division.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));
            division.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            var arriba = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Diseno.Apilar(arriba, _tablaClientes, lblLista);
            var abajo = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Diseno.Apilar(abajo, _tablaHistorial, filaHist);
            division.Controls.Add(arriba, 0, 0);
            division.Controls.Add(abajo, 0, 1);

            Diseno.Apilar(this, division, encabezado, Diseno.Espacio(12), barra);
            SeleccionCambiada();
        }

        public override void AlMostrar() => CargarClientes();

        private void Recargar() => ErroresUi.Intentar(Shell, CargarClientes, "No se pudieron cargar los clientes");

        private void CargarClientes()
        {
            int? idSel = IdSeleccionado();
            var dt = _clienteDao.ListarTodos(_txtBuscar.Text);
            _tablaClientes.Mostrar(dt, string.IsNullOrWhiteSpace(_txtBuscar.Text)
                ? "Todavía no hay clientes registrados. Usa 'Añadir cliente'."
                : $"No se encontraron clientes con \"{_txtBuscar.Text.Trim()}\".");
            var g = _tablaClientes.Grid;
            UITheme.OcultarColumnas(g, "ID");
            if (g.Columns.Contains("Teléfono")) g.Columns["Teléfono"].FillWeight = 60;
            if (g.Columns.Contains("Dirección")) g.Columns["Dirección"].FillWeight = 140;
            if (idSel.HasValue)
                foreach (DataGridViewRow r in g.Rows)
                    if (Convert.ToInt32(r.Cells["ID"].Value) == idSel.Value) { g.CurrentCell = r.Cells["Nombre"]; break; }
            SeleccionCambiada();
        }

        private int? IdSeleccionado()
        {
            var f = _tablaClientes.FilaSeleccionada;
            return f == null ? null : Convert.ToInt32(f.Cells["ID"].Value);
        }

        private Cliente? ClienteSeleccionado()
        {
            var f = _tablaClientes.FilaSeleccionada;
            if (f == null) return null;
            string tel = Convert.ToString(f.Cells["Teléfono"].Value) ?? "";
            Cliente? c = null;
            ErroresUi.Intentar(Shell, () => c = _clienteDao.BuscarPorTelefono(tel), "No se pudo leer el cliente");
            return c;
        }

        private void SeleccionCambiada()
        {
            var f = _tablaClientes.FilaSeleccionada;
            bool hay = f != null;
            _btnEditar.Enabled = hay;
            _btnEliminar.Enabled = hay;
            _btnPedido.Enabled = hay;
            _btnDetalle.Enabled = false;
            if (!hay)
            {
                _lblHistorial.Text = "Historial de pedidos";
                _tablaHistorial.Mostrar(null, "Selecciona un cliente de la lista para ver su historial.");
                return;
            }
            int id = Convert.ToInt32(f!.Cells["ID"].Value);
            _lblHistorial.Text = $"Historial de pedidos · {f.Cells["Nombre"].Value}";
            try
            {
                _tablaHistorial.Mostrar(_clienteDao.ObtenerHistorialPedidos(id), "Este cliente todavía no tiene pedidos.");
                UITheme.FormatoMoneda(_tablaHistorial.Grid, "Total");
                if (_tablaHistorial.Grid.Columns.Contains("Fecha")) _tablaHistorial.Grid.Columns["Fecha"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                FormatoEstados.Aplicar(_tablaHistorial.Grid);
                _btnDetalle.Enabled = _tablaHistorial.Grid.Visible && _tablaHistorial.Grid.Rows.Count > 0;
            }
            catch (Exception ex)
            {
                _tablaHistorial.Mostrar(null, "No se pudo cargar el historial: " + ErroresUi.Amigable(ex));
            }
        }

        private void Anadir()
        {
            using var frm = new FrmCliente(null);
            if (frm.ShowDialog(Shell) == DialogResult.OK && frm.ClienteGuardado != null)
            {
                _txtBuscar.Text = frm.ClienteGuardado.Telefono;
                Recargar();
                Dialogo.Exito(Shell, $"{frm.ClienteGuardado.Nombre} quedó registrado.", "Cliente guardado");
            }
        }

        private void Editar()
        {
            var c = ClienteSeleccionado();
            if (c == null) return;
            using var frm = new FrmCliente(c);
            if (frm.ShowDialog(Shell) == DialogResult.OK)
            {
                Recargar();
                Dialogo.Exito(Shell, "Los cambios del cliente se guardaron.", "Cliente actualizado");
            }
        }

        private void Eliminar()
        {
            var f = _tablaClientes.FilaSeleccionada;
            if (f == null) return;
            int id = Convert.ToInt32(f.Cells["ID"].Value);
            string nombre = Convert.ToString(f.Cells["Nombre"].Value) ?? "";
            if (!Dialogo.Confirmar(Shell, "Eliminar cliente",
                    $"¿Eliminar a {nombre}?\n\nSolo se pueden eliminar clientes sin pedidos; los que tienen historial se conservan por integridad referencial.",
                    "Sí, eliminar", "Cancelar", peligro: true))
                return;
            try
            {
                _clienteDao.Eliminar(id);
                Recargar();
                Dialogo.Exito(Shell, $"{nombre} fue eliminado.", "Cliente eliminado");
            }
            catch (MySql.Data.MySqlClient.MySqlException ex) when (ex.Number == 1451)
            {
                Dialogo.Advertencia(Shell, $"{nombre} tiene pedidos registrados, por lo que no se puede eliminar (llave foránea PEDIDO → CLIENTE con ON DELETE RESTRICT).",
                    "No se puede eliminar");
            }
            catch (Exception ex)
            {
                Dialogo.Error(Shell, ErroresUi.Amigable(ex), "No se pudo eliminar");
            }
        }

        private void NuevoPedido()
        {
            var c = ClienteSeleccionado();
            if (c != null) Shell.AbrirNuevoPedidoPara(c);
        }

        private void VerDetalle()
        {
            var f = _tablaHistorial.FilaSeleccionada;
            if (f == null) return;
            FrmDetallePedido.Mostrar(Shell, Convert.ToInt32(f.Cells["Folio"].Value));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _debounce.Dispose();
            base.Dispose(disposing);
        }
    }
}
