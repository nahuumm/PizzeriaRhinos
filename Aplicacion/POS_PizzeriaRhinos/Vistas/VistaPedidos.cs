using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Impresion;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>
    /// Seguimiento de pedidos del turno: despacho a ruta con el procedimiento sp_asignar_repartidor_pedido,
    /// marcar entregado, cancelar (restaura stock de bebidas), ver detalle y reimprimir comanda/ticket.
    /// Flujo: DOMICILIO = EN_PREPARACION → EN_RUTA → ENTREGADO · MOSTRADOR = EN_PREPARACION → ENTREGADO.
    /// </summary>
    [DesignerCategory("Code")]
    public class VistaPedidos : VistaBase
    {
        private readonly ReporteDAO _reporteDao = new();
        private readonly PedidoDAO _pedidoDao = new();
        private readonly EmpleadoDAO _empleadoDao = new();

        private readonly GrupoOpciones _grpEstado, _grpServicio, _grpFecha;
        private readonly TextBox _txtBuscar;
        private readonly PanelTabla _tabla;
        private readonly ComboBox _cmbRepartidor;
        private readonly BotonRhino _btnDetalle, _btnDespachar, _btnEntregado, _btnCancelar, _btnReimprimir;
        private readonly Label _lblResumen;
        private readonly System.Windows.Forms.Timer _autoRefresco;

        public VistaPedidos(FrmPrincipal shell) : base(shell)
        {
            var encabezado = new EncabezadoModulo("Pedidos y despacho",
                "Asigna repartidor (\"el que llegue primero\"), marca entregas, cancela y reimprime comandas.", "📋");

            // ---------------- Filtros (fluyen a la siguiente línea si no caben) ----------------
            _grpEstado = new GrupoOpciones { TamanoBoton = UITheme.S(136, 50) };
            _grpEstado.Agregar("Activos", "ACTIVOS", "⏳");
            _grpEstado.Agregar("Preparación", "EN_PREPARACION", "🔥");
            _grpEstado.Agregar("En ruta", "EN_RUTA", "🛵");
            _grpEstado.Agregar("Entregados", "ENTREGADO", "✔");
            _grpEstado.Agregar("Cancelados", "CANCELADO", "✖");
            _grpEstado.Agregar("Todos", "", "≡");
            _grpEstado.Seleccionar("ACTIVOS");
            _grpEstado.ValorCambiado += (_, _) => Recargar();

            _grpServicio = new GrupoOpciones { TamanoBoton = UITheme.S(124, 50) };
            _grpServicio.Agregar("Todos", "");
            _grpServicio.Agregar("Domicilio", "DOMICILIO", "🛵");
            _grpServicio.Agregar("Mostrador", "MOSTRADOR", "🏪");
            _grpServicio.Seleccionar("");
            _grpServicio.ValorCambiado += (_, _) => Recargar();

            _grpFecha = new GrupoOpciones { TamanoBoton = UITheme.S(104, 50) };
            _grpFecha.Agregar("Hoy", 0);
            _grpFecha.Agregar("7 días", 7);
            _grpFecha.Agregar("30 días", 30);
            _grpFecha.Seleccionar(0);
            _grpFecha.ValorCambiado += (_, _) => Recargar();

            _txtBuscar = new TextBox { Width = UITheme.S(240), MaxLength = 60, PlaceholderText = "Folio, cliente o teléfono", Margin = UITheme.P(10, 10, 4, 4) };
            UITheme.EstilizarTextBox(_txtBuscar);
            _txtBuscar.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Recargar(); } };
            var btnBuscar = new BotonRhino("Buscar", EstiloBoton.Ambar, "🔍") { Size = UITheme.S(120, 50) };
            btnBuscar.Click += (_, _) => Recargar();
            // Recarga desde MySQL conservando los filtros elegidos (estado, modalidad, periodo y búsqueda)
            var btnActualizar = new BotonRhino("Actualizar", EstiloBoton.Azul, "⟳") { Size = UITheme.S(160, 50) };
            btnActualizar.Click += (_, _) => Recargar();

            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            filtros.Controls.AddRange(new Control[]
            {
                _grpEstado, new Panel { Width = UITheme.S(16), Height = 1 }, _grpServicio, new Panel { Width = UITheme.S(16), Height = 1 },
                _grpFecha, _txtBuscar, btnBuscar, btnActualizar
            });

            // ---------------- Tabla ----------------
            _tabla = new PanelTabla();
            _tabla.Grid.SelectionChanged += (_, _) => ActualizarBotones();
            _tabla.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) VerDetalle(); };
            _lblResumen = new Label { Height = UITheme.S(30), Font = UITheme.FuenteEtiqueta, ForeColor = UITheme.TextoSuave, TextAlign = ContentAlignment.MiddleLeft };

            // ---------------- Acciones (columna derecha, botones grandes) ----------------
            var cardAcciones = new PanelRedondeado { Padding = UITheme.P(14) };
            var lblAcciones = new Label { Text = "Acciones del pedido", Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Height = UITheme.S(34) };
            _btnDetalle = new BotonRhino("Ver detalle", EstiloBoton.Secundario, "👁") { Height = UITheme.S(60) };
            _btnDetalle.Click += (_, _) => VerDetalle();
            _btnReimprimir = new BotonRhino("Reimprimir comanda y ticket", EstiloBoton.Secundario, "🖨") { Height = UITheme.S(60), Font = UITheme.Fuente(10.5f, FontStyle.Bold) };
            _btnReimprimir.Click += (_, _) => Reimprimir();

            var lblRep = UITheme.CrearEtiqueta("REPARTIDOR QUE SALE");
            lblRep.AutoSize = false;
            lblRep.Height = UITheme.S(24);
            _cmbRepartidor = new ComboBox();
            UITheme.EstilizarCombo(_cmbRepartidor);
            _btnDespachar = new BotonRhino("Despachar a ruta", EstiloBoton.Azul, "🛵") { Height = UITheme.S(64) };
            _btnDespachar.Click += (_, _) => Despachar();
            _btnEntregado = new BotonRhino("Marcar entregado", EstiloBoton.Exito, "✔") { Height = UITheme.S(64) };
            _btnEntregado.Click += (_, _) => CambiarEstado("ENTREGADO");
            _btnCancelar = new BotonRhino("Cancelar pedido", EstiloBoton.Peligro, "✖") { Height = UITheme.S(60), Dock = DockStyle.Bottom };
            _btnCancelar.Click += (_, _) => CambiarEstado("CANCELADO");

            Diseno.Apilar(cardAcciones, null, lblAcciones, Diseno.Espacio(4), _btnDetalle, Diseno.Espacio(8), _btnReimprimir,
                Diseno.Espacio(22), lblRep, _cmbRepartidor, Diseno.Espacio(8), _btnDespachar, Diseno.Espacio(8), _btnEntregado);
            cardAcciones.Controls.Add(_btnCancelar);

            var colAcciones = new Panel { Dock = DockStyle.Right, Width = UITheme.S(290), Padding = UITheme.P(12, 0, 0, 0) };
            cardAcciones.Dock = DockStyle.Fill;
            colAcciones.Controls.Add(cardAcciones);

            var cuerpo = new Panel();
            Diseno.Apilar(cuerpo, _tabla, _lblResumen);
            cuerpo.Controls.Add(colAcciones);

            Diseno.Apilar(this, cuerpo, encabezado, Diseno.Espacio(12), filtros, Diseno.Espacio(4));

            // Actualización automática cada 30 s mientras el módulo está visible
            _autoRefresco = new System.Windows.Forms.Timer { Interval = 30000 };
            _autoRefresco.Tick += (_, _) => { if (Visible && Shell.ContainsFocus) RecargarSilencioso(); };
            VisibleChanged += (_, _) => { if (Visible) _autoRefresco.Start(); else _autoRefresco.Stop(); };
        }

        public override void AlMostrar()
        {
            var lista = new List<OpcionRep> { new(0, "— Elegir repartidor —") };
            lista.AddRange(_empleadoDao.ObtenerRepartidoresActivos().Select(r => new OpcionRep(r.IdEmpleado, r.Nombre)));
            _cmbRepartidor.DataSource = lista;
            _cmbRepartidor.DisplayMember = nameof(OpcionRep.Texto);
            _cmbRepartidor.ValueMember = nameof(OpcionRep.Id);
            CargarDatos();
        }

        private void Recargar() => ErroresUi.Intentar(Shell, CargarDatos, "No se pudieron cargar los pedidos");

        private void RecargarSilencioso()
        {
            try { CargarDatos(); } catch { /* se reintentará en el siguiente ciclo */ }
        }

        private void CargarDatos()
        {
            int? folioSel = FolioSeleccionado();
            int dias = _grpFecha.Valor is int d ? d : 0;
            DateTime hasta = DateTime.Today;
            DateTime desde = hasta.AddDays(-dias);
            var dt = _reporteDao.ObtenerPedidos(desde, hasta, _grpServicio.Valor as string ?? "", _grpEstado.Valor as string ?? "", _txtBuscar.Text);

            _tabla.Mostrar(dt, "No hay pedidos con estos filtros. 🎉 ¡Todo al corriente!");
            var g = _tabla.Grid;
            UITheme.FormatoMoneda(g, "Subtotal", "Extras", "Total");
            UITheme.OcultarColumnas(g, "Subtotal", "Extras");
            if (g.Columns.Contains("Fecha")) g.Columns["Fecha"].DefaultCellStyle.Format = dias == 0 ? "HH:mm" : "dd/MM HH:mm";
            if (g.Columns.Contains("Folio")) { g.Columns["Folio"].FillWeight = 45; g.Columns["Folio"].DefaultCellStyle.Font = UITheme.FuenteNegrita; }
            if (g.Columns.Contains("Fecha")) g.Columns["Fecha"].FillWeight = 60;
            if (g.Columns.Contains("Estado")) g.Columns["Estado"].FillWeight = 110;
            FormatoEstados.Aplicar(g);

            decimal total = 0;
            int activos = 0;
            foreach (DataRow r in dt.Rows)
            {
                string est = Convert.ToString(r["Estado"]) ?? "";
                if (est != "CANCELADO") total += Convert.ToDecimal(r["Total"]);
                if (est == "EN_PREPARACION" || est == "EN_RUTA") activos++;
            }
            _lblResumen.Text = $"{dt.Rows.Count} pedido(s) · {activos} activo(s) · Total sin cancelados: {UITheme.Dinero(total)}" +
                               $"   ·   Actualizado a las {DateTime.Now:HH:mm:ss}";

            if (folioSel.HasValue)
            {
                foreach (DataGridViewRow row in g.Rows)
                {
                    if (Convert.ToInt32(row.Cells["Folio"].Value) == folioSel.Value)
                    {
                        g.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }
            ActualizarBotones();
        }

        // ---------------------------------------------------------------
        // SELECCIÓN Y REGLAS DE ESTADO
        // ---------------------------------------------------------------
        private int? FolioSeleccionado()
        {
            var f = _tabla.FilaSeleccionada;
            return f == null ? null : Convert.ToInt32(f.Cells["Folio"].Value);
        }

        private (string estado, string servicio, string repartidor)? DatosSeleccion()
        {
            var f = _tabla.FilaSeleccionada;
            if (f == null) return null;
            return (Convert.ToString(f.Cells["Estado"].Value) ?? "", Convert.ToString(f.Cells["Servicio"].Value) ?? "",
                    Convert.ToString(f.Cells["Repartidor"].Value) ?? "");
        }

        private void ActualizarBotones()
        {
            var d = DatosSeleccion();
            bool hay = d != null;
            string est = d?.estado ?? "", srv = d?.servicio ?? "";
            _btnDetalle.Enabled = hay;
            _btnReimprimir.Enabled = hay;
            _btnDespachar.Enabled = hay && srv == "DOMICILIO" && est == "EN_PREPARACION";
            _cmbRepartidor.Enabled = _btnDespachar.Enabled;
            _btnEntregado.Enabled = hay && ((srv == "DOMICILIO" && est == "EN_RUTA") || (srv == "MOSTRADOR" && est == "EN_PREPARACION"));
            _btnCancelar.Enabled = hay && (est == "EN_PREPARACION" || est == "EN_RUTA");
        }

        // ---------------------------------------------------------------
        // ACCIONES
        // ---------------------------------------------------------------
        private void VerDetalle()
        {
            int? folio = FolioSeleccionado();
            if (folio == null) return;
            // Si desde el detalle se despachó, entregó o canceló, la lista se recarga al cerrar
            if (FrmDetallePedido.Mostrar(Shell, folio.Value)) Recargar();
        }

        private void Reimprimir()
        {
            int? folio = FolioSeleccionado();
            if (folio == null) return;
            ErroresUi.Intentar(Shell, () =>
            {
                var cab = _reporteDao.ObtenerCabecera(folio.Value);
                var det = _pedidoDao.ObtenerDetallePedido(folio.Value);
                if (cab != null) ImpresorTickets.VistaPrevia(Shell, cab, det, ImpresorTickets.TipoDocumento.Ambos);
            }, "No se pudo reimprimir");
        }

        private void Despachar()
        {
            int? folio = FolioSeleccionado();
            if (folio == null) return;
            if (_cmbRepartidor.SelectedValue is not int idRep || idRep <= 0)
            {
                Dialogo.Advertencia(Shell, "Elige al repartidor que va a salir con el pedido.", "Falta el repartidor");
                _cmbRepartidor.Focus();
                return;
            }
            if (ErroresUi.Intentar(Shell, () => _pedidoDao.AsignarRepartidorSP(folio.Value, idRep), "No se pudo despachar"))
            {
                Dialogo.Exito(Shell, $"El pedido #{folio} salió a ruta con {_cmbRepartidor.Text}.", "Pedido despachado");
                _cmbRepartidor.SelectedIndex = 0;
                Recargar();
            }
        }

        private void CambiarEstado(string nuevo)
        {
            int? folio = FolioSeleccionado();
            var d = DatosSeleccion();
            if (folio == null || d == null) return;

            if (nuevo == "CANCELADO")
            {
                if (!Dialogo.Confirmar(Shell, $"Cancelar pedido #{folio}",
                        "El pedido quedará CANCELADO y no se podrá reactivar. Las bebidas regresarán al inventario.\n\n¿Confirmas la cancelación?",
                        "Sí, cancelar pedido", "No", peligro: true))
                    return;
            }
            else if (nuevo == "ENTREGADO")
            {
                if (d.Value.servicio == "DOMICILIO" && d.Value.estado != "EN_RUTA")
                {
                    Dialogo.Advertencia(Shell, "Un pedido a domicilio primero debe despacharse con un repartidor (EN RUTA).", "Falta despachar");
                    return;
                }
                if (!Dialogo.Confirmar(Shell, $"Entregar pedido #{folio}", "¿El pedido ya fue entregado al cliente?", "Sí, entregado", "No"))
                    return;
            }

            if (ErroresUi.Intentar(Shell, () => _pedidoDao.ActualizarEstadoPedido(folio.Value, nuevo), "No se pudo cambiar el estado"))
                Recargar();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _autoRefresco.Dispose();
            base.Dispose(disposing);
        }

        private sealed record OpcionRep(int Id, string Texto);
    }
}
