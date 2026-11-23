using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Impresion;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>Detalle de un pedido: cabecera, partidas con mitades/modificadores y reimpresión de comanda o ticket.</summary>
    [DesignerCategory("Code")]
    public class FrmDetallePedido : Form
    {
        private readonly CabeceraPedido _cab;
        private readonly DataTable _detalle;
        private readonly PedidoDAO _pedidoDao = new();
        private ComboBox? _cmbRepartidor;

        /// <summary>True si desde esta ventana se cambió el estado del pedido (la lista debe recargarse).</summary>
        public bool HuboCambios { get; private set; }

        private FrmDetallePedido(CabeceraPedido cab, DataTable detalle)
        {
            _cab = cab;
            _detalle = detalle;
            Text = $"Pedido #{cab.Folio}";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            var pantalla = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(UITheme.S(1180), pantalla.Width - 40), Math.Min(UITheme.S(800), pantalla.Height - 40));
            MinimumSize = UITheme.S(900, 600);

            var (_, _, etiquetaEstado) = UITheme.ColorEstado(cab.Estado);
            var encabezado = new EncabezadoModulo($"Pedido #{cab.Folio}",
                $"{cab.FechaHora:dd/MM/yyyy HH:mm} · {(cab.TipoServicio == "DOMICILIO" ? "A domicilio" : "Mostrador")} · {etiquetaEstado}", "🧾");

            // ---------------- Tarjetas de información ----------------
            var info = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Height = UITheme.S(150), Margin = new Padding(0) };
            for (int i = 0; i < 3; i++) info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            PanelRedondeado Tarjeta(string titulo, string cuerpo)
            {
                var card = new PanelRedondeado { Dock = DockStyle.Fill, Margin = UITheme.P(0, 0, 10, 0), Padding = UITheme.P(14, 10, 14, 10) };
                var lt = UITheme.CrearEtiqueta(titulo);
                lt.AutoSize = false;
                lt.Height = UITheme.S(24);
                var lc = new Label { Text = cuerpo, Font = UITheme.Fuente(10.5f), ForeColor = UITheme.Texto, AutoSize = false, AutoEllipsis = true };
                Diseno.Apilar(card, lc, lt);
                return card;
            }

            string cliente = cab.TipoServicio == "DOMICILIO" || cab.Telefono != "N/A"
                ? $"{cab.Cliente}\n📞 {cab.Telefono}" + (cab.Direccion.Length > 0 ? $"\n📍 {cab.Direccion}" : "") +
                  (cab.Referencias.Length > 0 ? $"\n{cab.Referencias}" : "")
                : "Público general (mostrador)";
            info.Controls.Add(Tarjeta("CLIENTE", cliente), 0, 0);
            info.Controls.Add(Tarjeta("ATENCIÓN", $"Cajera: {cab.Cajera}\nRepartidor: {cab.Repartidor}\nEstado: {etiquetaEstado}"), 1, 0);
            info.Controls.Add(Tarjeta("IMPORTE", $"Subtotal: {UITheme.Dinero(cab.Subtotal)}\nExtras: {UITheme.Dinero(cab.Extras)}\nTOTAL: {UITheme.Dinero(cab.Total)}"), 2, 0);

            // ---------------- Partidas ----------------
            var tabla = new PanelTabla();
            tabla.Grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            tabla.Grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            // ---------------- Botones ----------------
            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(76), Padding = UITheme.P(0, 12, 0, 0) };
            var btnCerrar = new BotonRhino("Cerrar", EstiloBoton.Secundario) { Dock = DockStyle.Left, Width = UITheme.S(160), DialogResult = DialogResult.Cancel };
            var btnTicket = new BotonRhino("Ticket", EstiloBoton.Primario, "🖨") { Dock = DockStyle.Right, Width = UITheme.S(190) };
            btnTicket.Click += (_, _) => ImpresorTickets.VistaPrevia(this, _cab, _detalle, ImpresorTickets.TipoDocumento.Ticket);
            var btnComanda = new BotonRhino("Comanda", EstiloBoton.Oscuro, "🖨") { Dock = DockStyle.Right, Width = UITheme.S(190) };
            btnComanda.Click += (_, _) => ImpresorTickets.VistaPrevia(this, _cab, _detalle, ImpresorTickets.TipoDocumento.Comanda);
            // Cancelar pedido en la barra inferior (solo pedidos activos: en preparación o en ruta)
            string estadoActual = cab.Estado.ToUpperInvariant();
            if (estadoActual == "EN_PREPARACION" || estadoActual == "EN_RUTA")
            {
                var btnCancelar = new BotonRhino("Cancelar pedido", EstiloBoton.Peligro, "✖") { Dock = DockStyle.Left, Width = UITheme.S(220) };
                btnCancelar.Click += (_, _) => CambiarEstado("CANCELADO");
                barra.Controls.Add(btnCancelar);
                barra.Controls.Add(new Panel { Dock = DockStyle.Left, Width = UITheme.S(10) });
            }
            barra.Controls.Add(btnCerrar);
            barra.Controls.Add(btnTicket);
            barra.Controls.Add(new Panel { Dock = DockStyle.Right, Width = UITheme.S(10) });
            barra.Controls.Add(btnComanda);
            CancelButton = btnCerrar;

            var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            Diseno.Apilar(cuerpo, tabla, encabezado, Diseno.Espacio(12), info, Diseno.Espacio(12));
            cuerpo.Controls.Add(CrearPanelActualizar()); // queda arriba de la barra de botones
            cuerpo.Controls.Add(barra);
            Controls.Add(cuerpo);

            // El enlace de datos se hace en Load: hasta entonces la tabla no pertenece a un formulario y no genera columnas
            Load += (_, _) =>
            {
                // Calzones: Chico / Mediano / Grande / Extra grande
                if (detalle.Columns.Contains("Categoria") && detalle.Columns.Contains("Tamaño"))
                    foreach (DataRow r in detalle.Rows)
                        r["Tamaño"] = UITheme.NombreTamano(Convert.ToString(r["Categoria"]), Convert.ToString(r["Tamaño"]));
                tabla.Mostrar(detalle, "Este pedido no tiene partidas registradas.");
                UITheme.OcultarColumnas(tabla.Grid, "ID_Detalle", "Categoria", "Variante");
                UITheme.FormatoMoneda(tabla.Grid, "Subtotal_Linea");
                void Encabezado(string col, string texto, float peso)
                {
                    if (!tabla.Grid.Columns.Contains(col)) return;
                    tabla.Grid.Columns[col].HeaderText = texto;
                    tabla.Grid.Columns[col].FillWeight = peso;
                }
                Encabezado("Cantidad", "Cant.", 40);
                Encabezado("Producto_Especialidad", "Producto / mitades", 160);
                Encabezado("Tamaño", "Tamaño", 70);
                Encabezado("Masa", "Masa", 60);
                Encabezado("Orilla_Queso", "Orilla", 50);
                Encabezado("Queso_Extra", "Queso extra", 60);
                Encabezado("Promo_2x1", "2x1", 70);
                Encabezado("Modificadores_Ingredientes", "Ingredientes SIN / CON", 220);
                Encabezado("Subtotal_Linea", "Importe", 70);
            };
        }

        // =====================================================================
        // ACTUALIZAR ESTADO DEL PEDIDO
        // DOMICILIO: EN_PREPARACION → EN_RUTA (sp_asignar_repartidor_pedido) → ENTREGADO
        // MOSTRADOR: EN_PREPARACION → ENTREGADO · Cualquiera activo → CANCELADO (regresa stock de bebidas)
        // =====================================================================
        private Control CrearPanelActualizar()
        {
            var card = new PanelRedondeado { Dock = DockStyle.Bottom, Height = UITheme.S(96), Padding = UITheme.P(14, 10, 14, 10), Margin = new Padding(0) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            var lbl = new Label
            {
                Text = "Actualizar pedido:",
                Font = UITheme.Titulo(14f),
                ForeColor = UITheme.Texto,
                AutoSize = false,
                Size = UITheme.S(190, 64),
                TextAlign = ContentAlignment.MiddleLeft
            };
            flow.Controls.Add(lbl);

            string estado = _cab.Estado.ToUpperInvariant();
            bool domicilio = _cab.TipoServicio == "DOMICILIO";

            if (estado == "EN_PREPARACION" && domicilio)
            {
                _cmbRepartidor = new ComboBox { Width = UITheme.S(240), Margin = UITheme.P(4, 16, 8, 4) };
                UITheme.EstilizarCombo(_cmbRepartidor);
                try
                {
                    var lista = new System.Collections.Generic.List<OpcionRep> { new(0, "— Repartidor que sale —") };
                    foreach (var r in new EmpleadoDAO().ObtenerRepartidoresActivos()) lista.Add(new OpcionRep(r.IdEmpleado, r.Nombre));
                    _cmbRepartidor.DataSource = lista;
                    _cmbRepartidor.DisplayMember = nameof(OpcionRep.Texto);
                    _cmbRepartidor.ValueMember = nameof(OpcionRep.Id);
                }
                catch (Exception ex)
                {
                    lbl.Text = "Sin repartidores: " + ErroresUi.Amigable(ex).Split('\n')[0];
                }
                var btnDespachar = new BotonRhino("Despachar a ruta", EstiloBoton.Azul, "🛵") { Size = UITheme.S(210, 64) };
                btnDespachar.Click += (_, _) => Despachar();
                flow.Controls.Add(_cmbRepartidor);
                flow.Controls.Add(btnDespachar);
            }
            if ((estado == "EN_RUTA" && domicilio) || (estado == "EN_PREPARACION" && !domicilio))
            {
                var btnEntregado = new BotonRhino("Marcar entregado", EstiloBoton.Exito, "✔") { Size = UITheme.S(210, 64) };
                btnEntregado.Click += (_, _) => CambiarEstado("ENTREGADO");
                flow.Controls.Add(btnEntregado);
            }
            if (estado != "EN_PREPARACION" && estado != "EN_RUTA")
            {
                flow.Controls.Add(new Label
                {
                    Text = $"El pedido ya está {UITheme.ColorEstado(estado).etiqueta.ToLowerInvariant()}; no admite más cambios.",
                    AutoSize = false,
                    Size = UITheme.S(560, 64),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = UITheme.Fuente(11f),
                    ForeColor = UITheme.TextoSuave
                });
            }

            card.Controls.Add(flow);
            var contenedor = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(96 + 10) };
            card.Dock = DockStyle.Top;
            contenedor.Controls.Add(card);
            return contenedor;
        }

        private void Despachar()
        {
            if (_cmbRepartidor?.SelectedValue is not int idRep || idRep <= 0)
            {
                Dialogo.Advertencia(this, "Elige al repartidor que va a salir con el pedido.", "Falta el repartidor");
                _cmbRepartidor?.Focus();
                return;
            }
            string nombre = _cmbRepartidor.Text;
            if (ErroresUi.Intentar(this, () => _pedidoDao.AsignarRepartidorSP(_cab.Folio, idRep), "No se pudo despachar"))
                Terminar($"El pedido #{_cab.Folio} salió a ruta con {nombre}.", "Pedido despachado");
        }

        private void CambiarEstado(string nuevo)
        {
            if (nuevo == "CANCELADO" &&
                !Dialogo.Confirmar(this, $"Cancelar pedido #{_cab.Folio}",
                    $"¿Estás seguro de cancelar el pedido #{_cab.Folio}? Esta acción no se puede deshacer.\nLas bebidas del pedido regresarán al inventario.",
                    "Sí, cancelar pedido", "No, conservarlo", peligro: true))
                return;
            if (nuevo == "ENTREGADO" &&
                !Dialogo.Confirmar(this, $"Entregar pedido #{_cab.Folio}", "¿El pedido ya fue entregado al cliente?", "Sí, entregado", "No"))
                return;

            if (ErroresUi.Intentar(this, () => _pedidoDao.ActualizarEstadoPedido(_cab.Folio, nuevo), "No se pudo actualizar el pedido"))
                Terminar(nuevo == "CANCELADO" ? $"El pedido #{_cab.Folio} fue cancelado." : $"El pedido #{_cab.Folio} quedó entregado.",
                         "Pedido actualizado");
        }

        private void Terminar(string mensaje, string titulo)
        {
            HuboCambios = true;
            Dialogo.Exito(this, mensaje, titulo);
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record OpcionRep(int Id, string Texto);

        /// <summary>Carga y muestra el detalle del folio indicado. Devuelve true si se actualizó el estado del pedido.</summary>
        public static bool Mostrar(IWin32Window owner, int folio)
        {
            CabeceraPedido? cab = null;
            DataTable? det = null;
            if (!ErroresUi.Intentar(owner, () =>
                {
                    cab = new ReporteDAO().ObtenerCabecera(folio);
                    det = new PedidoDAO().ObtenerDetallePedido(folio);
                }, "No se pudo cargar el pedido"))
                return false;
            if (cab == null || det == null)
            {
                Dialogo.Advertencia(owner, $"No se encontró el pedido #{folio}.", "Pedido no encontrado");
                return false;
            }
            using var frm = new FrmDetallePedido(cab, det);
            frm.ShowDialog(owner);
            return frm.HuboCambios;
        }
    }
}
