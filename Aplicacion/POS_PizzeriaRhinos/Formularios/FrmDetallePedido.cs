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
            barra.Controls.Add(btnCerrar);
            barra.Controls.Add(btnTicket);
            barra.Controls.Add(new Panel { Dock = DockStyle.Right, Width = UITheme.S(10) });
            barra.Controls.Add(btnComanda);
            CancelButton = btnCerrar;

            var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            Diseno.Apilar(cuerpo, tabla, encabezado, Diseno.Espacio(12), info, Diseno.Espacio(12));
            cuerpo.Controls.Add(barra);
            Controls.Add(cuerpo);

            // El enlace de datos se hace en Load: hasta entonces la tabla no pertenece a un formulario y no genera columnas
            Load += (_, _) =>
            {
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

        /// <summary>Carga y muestra el detalle del folio indicado.</summary>
        public static void Mostrar(IWin32Window owner, int folio)
        {
            CabeceraPedido? cab = null;
            DataTable? det = null;
            if (!ErroresUi.Intentar(owner, () =>
                {
                    cab = new ReporteDAO().ObtenerCabecera(folio);
                    det = new PedidoDAO().ObtenerDetallePedido(folio);
                }, "No se pudo cargar el pedido"))
                return;
            if (cab == null || det == null)
            {
                Dialogo.Advertencia(owner, $"No se encontró el pedido #{folio}.", "Pedido no encontrado");
                return;
            }
            using var frm = new FrmDetallePedido(cab, det);
            frm.ShowDialog(owner);
        }
    }
}
