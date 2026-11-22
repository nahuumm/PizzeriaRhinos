using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>
    /// Inventario del administrador:
    ///  • Ingredientes: CRUD y "HAY / NO HAY" (boceto pág. 5, Regla 8 — control manual sin gramaje).
    ///  • Bebidas: stock del refrigerador (Regla 7 — el trigger descuenta al vender).
    ///  • Productos y precios: tarifas por tamaño y activar/desactivar productos del menú.
    /// </summary>
    [DesignerCategory("Code")]
    public class VistaInventario : VistaBase
    {
        private readonly IngredienteDAO _ingredienteDao = new();
        private readonly ProductoDAO _productoDao = new();
        private readonly PedidoDAO _pedidoDao = new();

        private readonly GrupoOpciones _tabs;
        private readonly Panel _host;
        private readonly Panel _pnlIngredientes, _pnlBebidas, _pnlProductos;
        private readonly PanelTabla _tablaIng, _tablaBeb, _tablaProd;
        private readonly GrupoOpciones _grpCategoria;
        private readonly Label _lblStockActual;
        private readonly TextBox _txtStock;
        private string _tab = "ING";

        public VistaInventario(FrmPrincipal shell) : base(shell)
        {
            var encabezado = new EncabezadoModulo("Inventario", "Ingredientes disponibles, stock de bebidas y precios del menú.", "📦");

            _tabs = new GrupoOpciones { TamanoBoton = UITheme.S(240, 58), Dock = DockStyle.Top };
            _tabs.Agregar("Ingredientes", "ING", "🧂");
            _tabs.Agregar("Bebidas (stock)", "BEB", "🥤");
            _tabs.Agregar("Productos y precios", "PROD", "🍕");
            _tabs.Seleccionar("ING");
            _tabs.ValorCambiado += (_, _) => { _tab = _tabs.Valor as string ?? "ING"; MostrarTab(); };

            _host = new Panel();

            // =========================== INGREDIENTES ===========================
            _pnlIngredientes = new Panel { Dock = DockStyle.Fill };
            _tablaIng = new PanelTabla();
            _tablaIng.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AlternarIngrediente(); };
            var colIng = ColumnaAcciones("Ingrediente seleccionado",
                Boton("Cambiar HAY / NO HAY", EstiloBoton.Ambar, "⇄", AlternarIngrediente),
                Boton("Nuevo ingrediente", EstiloBoton.Primario, "➕", NuevoIngrediente),
                Boton("Renombrar", EstiloBoton.Secundario, "✏", RenombrarIngrediente),
                Boton("Eliminar", EstiloBoton.Secundario, "🗑", EliminarIngrediente));
            var notaIng = Nota("Los ingredientes marcados como NO HAY no se pueden agregar en caja. No se lleva control por gramaje.");
            Diseno.Apilar(_pnlIngredientes, _tablaIng, notaIng);
            _pnlIngredientes.Controls.Add(colIng);

            // =========================== BEBIDAS ===========================
            _pnlBebidas = new Panel { Dock = DockStyle.Fill };
            _tablaBeb = new PanelTabla();
            _tablaBeb.Grid.SelectionChanged += (_, _) => BebidaSeleccionada();

            _lblStockActual = new Label { Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Height = UITheme.S(56), Text = "Selecciona una bebida" };
            _txtStock = new TextBox { MaxLength = 4, TextAlign = HorizontalAlignment.Center };
            UITheme.EstilizarTextBox(_txtStock);
            _txtStock.Font = UITheme.Fuente(24f, FontStyle.Bold);
            _txtStock.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            var filaStepper = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Height = UITheme.S(64), Margin = new Padding(0) };
            filaStepper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UITheme.S(64)));
            filaStepper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            filaStepper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UITheme.S(64)));
            var bMenos = new BotonRhino("", EstiloBoton.Secundario, "➖") { Dock = DockStyle.Fill, Margin = new Padding(0) };
            bMenos.Click += (_, _) => SumarStock(-1);
            var bMas = new BotonRhino("", EstiloBoton.Secundario, "➕") { Dock = DockStyle.Fill, Margin = new Padding(0) };
            bMas.Click += (_, _) => SumarStock(1);
            var celdaTxt = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(8, 6, 8, 0) };
            _txtStock.Dock = DockStyle.Top;
            celdaTxt.Controls.Add(_txtStock);
            filaStepper.Controls.Add(bMenos, 0, 0);
            filaStepper.Controls.Add(celdaTxt, 1, 0);
            filaStepper.Controls.Add(bMas, 2, 0);

            var rapidos = new FlowLayoutPanel { Height = UITheme.S(64), WrapContents = false };
            foreach (int n in new[] { 6, 12, 24 })
            {
                var b = new BotonRhino($"+{n}", EstiloBoton.Secundario) { Size = UITheme.S(76, 54) };
                int cantidad = n;
                b.Click += (_, _) => SumarStock(cantidad);
                rapidos.Controls.Add(b);
            }
            var btnGuardarStock = new BotonRhino("Guardar stock", EstiloBoton.Exito, "✔") { Height = UITheme.S(64) };
            btnGuardarStock.Click += (_, _) => GuardarStock();

            var cardStock = new PanelRedondeado { Padding = UITheme.P(16) };
            var lblTitStock = new Label { Text = "Reabastecer refrigerador", Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Height = UITheme.S(34) };
            var lblExpl = new Label { Text = "Captura las piezas que hay físicamente. Cada venta descuenta 1 pieza automáticamente (trigger).", Font = UITheme.FuenteChica, ForeColor = UITheme.TextoSuave, Height = UITheme.S(56) };
            Diseno.Apilar(cardStock, null, lblTitStock, lblExpl, _lblStockActual, filaStepper, Diseno.Espacio(8), rapidos, Diseno.Espacio(8), btnGuardarStock);
            var colBeb = new Panel { Dock = DockStyle.Right, Width = UITheme.S(300), Padding = UITheme.P(12, 0, 0, 0) };
            cardStock.Dock = DockStyle.Fill;
            colBeb.Controls.Add(cardStock);
            Diseno.Apilar(_pnlBebidas, _tablaBeb, Nota("Las bebidas con stock 0 aparecen como AGOTADO y su botón se deshabilita en caja."));
            _pnlBebidas.Controls.Add(colBeb);

            // =========================== PRODUCTOS ===========================
            _pnlProductos = new Panel { Dock = DockStyle.Fill };
            _grpCategoria = new GrupoOpciones { TamanoBoton = UITheme.S(150, 50), Dock = DockStyle.Top };
            _grpCategoria.Agregar("Todos", "");
            _grpCategoria.Agregar("Pizzas", "PIZZA");
            _grpCategoria.Agregar("Mostrador", "PIZZA_MOSTRADOR");
            _grpCategoria.Agregar("Calzone", "CALZONE");
            _grpCategoria.Agregar("Complementos", "COMPLEMENTO");
            _grpCategoria.Agregar("Bebidas", "BEBIDA");
            _grpCategoria.Seleccionar("PIZZA");
            _grpCategoria.ValorCambiado += (_, _) => ErroresUi.Intentar(Shell, CargarProductos);
            _tablaProd = new PanelTabla();
            _tablaProd.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) CambiarPrecio(); };
            var colProd = ColumnaAcciones("Producto seleccionado",
                Boton("Cambiar precio", EstiloBoton.Primario, "💲", CambiarPrecio),
                Boton("Activar / desactivar producto", EstiloBoton.Ambar, "⇄", AlternarProducto));
            Diseno.Apilar(_pnlProductos, _tablaProd, _grpCategoria, Diseno.Espacio(8));
            _pnlProductos.Controls.Add(colProd);

            _host.Controls.Add(_pnlIngredientes);
            _host.Controls.Add(_pnlBebidas);
            _host.Controls.Add(_pnlProductos);

            Diseno.Apilar(this, _host, encabezado, Diseno.Espacio(12), _tabs, Diseno.Espacio(10));
            MostrarTab(false);
        }

        // ---------------------------------------------------------------
        // AYUDAS DE INTERFAZ
        // ---------------------------------------------------------------
        private static BotonRhino Boton(string texto, EstiloBoton estilo, string icono, Action accion)
        {
            var b = new BotonRhino(texto, estilo, icono) { Height = UITheme.S(62), Font = UITheme.Fuente(11f, FontStyle.Bold) };
            b.Click += (_, _) => accion();
            return b;
        }

        private static Panel ColumnaAcciones(string titulo, params BotonRhino[] botones)
        {
            var card = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(14) };
            var controles = new System.Collections.Generic.List<Control>
            {
                new Label { Text = titulo, Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Height = UITheme.S(34) }
            };
            foreach (var b in botones)
            {
                controles.Add(Diseno.Espacio(8));
                controles.Add(b);
            }
            Diseno.Apilar(card, null, controles.ToArray());
            var col = new Panel { Dock = DockStyle.Right, Width = UITheme.S(290), Padding = UITheme.P(12, 0, 0, 0) };
            col.Controls.Add(card);
            return col;
        }

        private static Label Nota(string texto) => new Label
        {
            Text = "ℹ " + texto,
            Height = UITheme.S(34),
            Font = UITheme.FuenteChica,
            ForeColor = UITheme.TextoSuave,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private void MostrarTab(bool cargar = true)
        {
            _pnlIngredientes.Visible = _tab == "ING";
            _pnlBebidas.Visible = _tab == "BEB";
            _pnlProductos.Visible = _tab == "PROD";
            if (cargar) ErroresUi.Intentar(Shell, CargarTabActual, "No se pudo cargar el inventario");
        }

        public override void AlMostrar() => CargarTabActual();

        private void CargarTabActual()
        {
            if (_tab == "ING") CargarIngredientes();
            else if (_tab == "BEB") CargarBebidas();
            else CargarProductos();
        }

        // ---------------------------------------------------------------
        // INGREDIENTES
        // ---------------------------------------------------------------
        private void CargarIngredientes()
        {
            int? sel = IdFila(_tablaIng, "id_ingrediente");
            var dt = _ingredienteDao.Listar();
            dt.Columns.Add("Estado", typeof(string));
            foreach (DataRow r in dt.Rows) r["Estado"] = Convert.ToBoolean(r["disponible"]) ? "✔  HAY" : "✖  NO HAY";
            _tablaIng.Mostrar(dt, "No hay ingredientes registrados.");
            var g = _tablaIng.Grid;
            UITheme.OcultarColumnas(g, "id_ingrediente", "disponible");
            if (g.Columns.Contains("Estado"))
            {
                g.Columns["Estado"].DisplayIndex = 1;
                g.Columns["Estado"].DefaultCellStyle.Font = UITheme.FuenteNegrita;
            }
            g.CellFormatting -= ColorearHayNoHay;
            g.CellFormatting += ColorearHayNoHay;
            Reseleccionar(_tablaIng, "id_ingrediente", sel);
        }

        private void ColorearHayNoHay(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var g = _tablaIng.Grid;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle == null || g.Columns[e.ColumnIndex].Name != "Estado") return;
            bool hay = Convert.ToString(e.Value)?.Contains("NO HAY") != true;
            e.CellStyle.ForeColor = hay ? UITheme.Verde : UITheme.RojoOscuro;
            e.CellStyle.BackColor = hay ? UITheme.VerdeSuave : UITheme.RojoSuave;
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            e.CellStyle.SelectionBackColor = UITheme.Oscurecer(e.CellStyle.BackColor, 0.05f);
        }

        private void AlternarIngrediente()
        {
            var f = _tablaIng.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un ingrediente de la lista."); return; }
            int id = Convert.ToInt32(f.Cells["id_ingrediente"].Value);
            bool actual = Convert.ToBoolean(f.Cells["disponible"].Value);
            if (ErroresUi.Intentar(Shell, () => _ingredienteDao.CambiarDisponibilidad(id, !actual)))
                ErroresUi.Intentar(Shell, CargarIngredientes);
        }

        private void NuevoIngrediente()
        {
            using var frm = new FrmEntrada("Nuevo ingrediente", "Nombre del ingrediente", "", ModoEntrada.Texto, 60);
            if (frm.ShowDialog(Shell) != DialogResult.OK) return;
            string nombre = frm.ValorTexto;
            if (ErroresUi.Intentar(Shell, () => _ingredienteDao.Guardar(0, nombre, true), "No se pudo guardar el ingrediente"))
            {
                ErroresUi.Intentar(Shell, CargarIngredientes);
                Dialogo.Exito(Shell, $"Se agregó \"{nombre}\" con sus tarifas de ingrediente extra por tamaño.", "Ingrediente creado");
            }
        }

        private void RenombrarIngrediente()
        {
            var f = _tablaIng.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un ingrediente de la lista."); return; }
            int id = Convert.ToInt32(f.Cells["id_ingrediente"].Value);
            bool disp = Convert.ToBoolean(f.Cells["disponible"].Value);
            using var frm = new FrmEntrada("Renombrar ingrediente", "Nombre del ingrediente", Convert.ToString(f.Cells["Ingrediente"].Value) ?? "", ModoEntrada.Texto, 60);
            if (frm.ShowDialog(Shell) != DialogResult.OK) return;
            if (ErroresUi.Intentar(Shell, () => _ingredienteDao.Guardar(id, frm.ValorTexto, disp), "No se pudo renombrar"))
                ErroresUi.Intentar(Shell, CargarIngredientes);
        }

        private void EliminarIngrediente()
        {
            var f = _tablaIng.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un ingrediente de la lista."); return; }
            int id = Convert.ToInt32(f.Cells["id_ingrediente"].Value);
            string nombre = Convert.ToString(f.Cells["Ingrediente"].Value) ?? "";
            int enRecetas = Convert.ToInt32(f.Cells["En recetas"].Value);
            if (enRecetas > 0)
            {
                Dialogo.Advertencia(Shell, $"\"{nombre}\" forma parte de {enRecetas} receta(s). Para dejar de usarlo márcalo como NO HAY.", "No se puede eliminar");
                return;
            }
            if (!Dialogo.Confirmar(Shell, "Eliminar ingrediente", $"¿Eliminar \"{nombre}\" del catálogo?", "Sí, eliminar", "Cancelar", peligro: true)) return;
            if (ErroresUi.Intentar(Shell, () => _ingredienteDao.Eliminar(id), "No se pudo eliminar"))
                ErroresUi.Intentar(Shell, CargarIngredientes);
        }

        // ---------------------------------------------------------------
        // BEBIDAS
        // ---------------------------------------------------------------
        private void CargarBebidas()
        {
            int? sel = IdFila(_tablaBeb, "id_producto_precio");
            var dt = _productoDao.ObtenerBebidasConStock();
            _tablaBeb.Mostrar(dt, "No hay bebidas en el catálogo.");
            var g = _tablaBeb.Grid;
            UITheme.OcultarColumnas(g, "id_producto_precio", "id_producto", "categoria", "id_tamano", "aplica_promocion_2x1", "variante", "disponible");
            if (g.Columns.Contains("producto")) g.Columns["producto"].HeaderText = "Bebida";
            if (g.Columns.Contains("tamano_presentacion")) g.Columns["tamano_presentacion"].HeaderText = "Presentación";
            if (g.Columns.Contains("precio")) g.Columns["precio"].HeaderText = "Precio";
            if (g.Columns.Contains("stock_actual")) { g.Columns["stock_actual"].HeaderText = "Stock"; g.Columns["stock_actual"].DefaultCellStyle.Font = UITheme.FuenteNegrita; }
            UITheme.FormatoMoneda(g, "precio");
            g.CellFormatting -= ColorearStock;
            g.CellFormatting += ColorearStock;
            Reseleccionar(_tablaBeb, "id_producto_precio", sel);
            BebidaSeleccionada();
        }

        private void ColorearStock(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var g = _tablaBeb.Grid;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle == null || g.Columns[e.ColumnIndex].Name != "stock_actual") return;
            int stock = e.Value == null || e.Value == DBNull.Value ? 0 : Convert.ToInt32(e.Value);
            if (stock == 0) { e.Value = "AGOTADO"; e.CellStyle.ForeColor = UITheme.RojoOscuro; e.CellStyle.BackColor = UITheme.RojoSuave; e.FormattingApplied = true; }
            else if (stock <= 5) { e.CellStyle.ForeColor = Color.FromArgb(146, 84, 0); e.CellStyle.BackColor = UITheme.AmbarSuave; }
        }

        private void BebidaSeleccionada()
        {
            var f = _tablaBeb.FilaSeleccionada;
            if (f == null)
            {
                _lblStockActual.Text = "Selecciona una bebida";
                _txtStock.Text = "";
                return;
            }
            int stock = f.Cells["stock_actual"].Value == DBNull.Value ? 0 : Convert.ToInt32(f.Cells["stock_actual"].Value);
            _lblStockActual.Text = $"{f.Cells["producto"].Value} {f.Cells["tamano_presentacion"].Value}";
            _txtStock.Text = stock.ToString();
        }

        private void SumarStock(int delta)
        {
            if (_tablaBeb.FilaSeleccionada == null) return;
            int actual = int.TryParse(_txtStock.Text, out int v) ? v : 0;
            _txtStock.Text = Math.Clamp(actual + delta, 0, 9999).ToString();
        }

        private void GuardarStock()
        {
            var f = _tablaBeb.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona una bebida de la lista."); return; }
            if (!int.TryParse(_txtStock.Text, out int nuevo) || nuevo < 0)
            {
                Dialogo.Advertencia(Shell, "Escribe una cantidad válida (0 o más piezas).");
                return;
            }
            int idPp = Convert.ToInt32(f.Cells["id_producto_precio"].Value);
            if (ErroresUi.Intentar(Shell, () => _pedidoDao.ActualizarStockBebida(idPp, nuevo), "No se pudo guardar el stock"))
            {
                ErroresUi.Intentar(Shell, CargarBebidas);
                Dialogo.Exito(Shell, $"Stock actualizado: {nuevo} pieza(s).", "Inventario actualizado");
            }
        }

        // ---------------------------------------------------------------
        // PRODUCTOS Y PRECIOS
        // ---------------------------------------------------------------
        private void CargarProductos()
        {
            int? sel = IdFila(_tablaProd, "id_producto_precio");
            var dt = _productoDao.ListarCatalogoAdmin(_grpCategoria.Valor as string ?? "");
            _tablaProd.Mostrar(dt, "No hay productos en esta categoría.");
            var g = _tablaProd.Grid;
            UITheme.OcultarColumnas(g, "id_producto_precio", "id_producto");
            UITheme.FormatoMoneda(g, "Precio");
            if (g.Columns.Contains("Producto")) g.Columns["Producto"].FillWeight = 160;
            Reseleccionar(_tablaProd, "id_producto_precio", sel);
        }

        private void CambiarPrecio()
        {
            var f = _tablaProd.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un producto de la lista."); return; }
            int idPp = Convert.ToInt32(f.Cells["id_producto_precio"].Value);
            decimal actual = Convert.ToDecimal(f.Cells["Precio"].Value);
            string desc = $"{f.Cells["Producto"].Value} · {f.Cells["Tamaño"].Value}";
            using var frm = new FrmEntrada("Cambiar precio", desc, actual.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ModoEntrada.Decimal, 8, 1m, 9999m);
            if (frm.ShowDialog(Shell) != DialogResult.OK) return;
            decimal nuevo = frm.ValorNumero;
            if (ErroresUi.Intentar(Shell, () => _productoDao.ActualizarPrecio(idPp, nuevo), "No se pudo cambiar el precio"))
                ErroresUi.Intentar(Shell, CargarProductos);
        }

        private void AlternarProducto()
        {
            var f = _tablaProd.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un producto de la lista."); return; }
            int idProd = Convert.ToInt32(f.Cells["id_producto"].Value);
            bool disp = Convert.ToBoolean(f.Cells["Disponible"].Value);
            string nombre = Convert.ToString(f.Cells["Producto"].Value) ?? "";
            string accion = disp ? "desactivar" : "activar";
            if (!Dialogo.Confirmar(Shell, $"{(disp ? "Desactivar" : "Activar")} producto",
                    $"¿Deseas {accion} \"{nombre}\" en todos sus tamaños?" + (disp ? "\nYa no aparecerá en el punto de venta." : ""),
                    $"Sí, {accion}", "Cancelar"))
                return;
            if (ErroresUi.Intentar(Shell, () => _productoDao.CambiarDisponibilidadProducto(idProd, !disp)))
                ErroresUi.Intentar(Shell, CargarProductos);
        }

        // ---------------------------------------------------------------
        // UTILIDADES
        // ---------------------------------------------------------------
        private static int? IdFila(PanelTabla t, string columna)
        {
            var f = t.FilaSeleccionada;
            if (f == null || !t.Grid.Columns.Contains(columna)) return null;
            return Convert.ToInt32(f.Cells[columna].Value);
        }

        private static void Reseleccionar(PanelTabla t, string columna, int? id)
        {
            if (id == null || !t.Grid.Columns.Contains(columna)) return;
            foreach (DataGridViewRow r in t.Grid.Rows)
            {
                if (Convert.ToInt32(r.Cells[columna].Value) == id.Value)
                {
                    var celda = r.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                    if (celda != null) t.Grid.CurrentCell = celda;
                    break;
                }
            }
        }
    }
}
