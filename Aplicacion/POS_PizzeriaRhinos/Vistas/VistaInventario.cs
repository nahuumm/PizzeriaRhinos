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
    ///  • Ingredientes: CRUD, filtros, detalle (recetas donde se usa y tarifa de extra) y estado Disponible / Agotado (Regla 8 — control manual sin gramaje).
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

        // Ingredientes
        private DataTable _dtIng = new();
        private readonly GrupoOpciones _grpFiltroIng;
        private readonly TextBox _txtBuscarIng;
        private readonly Label _lblIngNombre, _lblIngEstado, _lblIngRecetas, _lblIngPrecios;
        private readonly BotonRhino _btnIngDisponible, _btnIngAgotado, _btnIngRenombrar, _btnIngEliminar;

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
            // Mismo esquema que Bebidas: filtros arriba, tabla al centro y tarjeta de detalle con acciones a la derecha.
            _pnlIngredientes = new Panel { Dock = DockStyle.Fill };

            _grpFiltroIng = new GrupoOpciones { TamanoBoton = UITheme.S(200, 50) };
            _grpFiltroIng.Agregar("Todos", "TODOS", "≡");
            _grpFiltroIng.Agregar("Disponibles", "DISP", "✔");
            _grpFiltroIng.Agregar("Agotados", "AGOT", "✖");
            _grpFiltroIng.Seleccionar("TODOS");
            _grpFiltroIng.ValorCambiado += (_, _) => FiltrarIngredientes();
            _txtBuscarIng = new TextBox { Width = UITheme.S(240), MaxLength = 60, PlaceholderText = "Buscar ingrediente…", Margin = UITheme.P(14, 10, 4, 4) };
            UITheme.EstilizarTextBox(_txtBuscarIng);
            _txtBuscarIng.TextChanged += (_, _) => FiltrarIngredientes();
            var filtrosIng = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            filtrosIng.Controls.Add(_grpFiltroIng);
            filtrosIng.Controls.Add(_txtBuscarIng);

            _tablaIng = new PanelTabla();
            _tablaIng.Grid.SelectionChanged += (_, _) => IngredienteSeleccionado();
            _tablaIng.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) RenombrarIngrediente(); };

            // ---- Tarjeta de detalle ----
            Label Titulo(string t) => new Label { Text = t, Font = UITheme.FuenteEtiqueta, ForeColor = UITheme.TextoSuave, Height = UITheme.S(24), TextAlign = ContentAlignment.BottomLeft };
            var lblTitDet = new Label { Text = "Detalle del ingrediente", Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, Height = UITheme.S(34) };
            _lblIngNombre = new Label { Text = "Selecciona un ingrediente", Font = UITheme.Titulo(19f), ForeColor = UITheme.Texto, Height = UITheme.S(40), AutoEllipsis = true };
            _lblIngEstado = new Label { Font = UITheme.Fuente(11f, FontStyle.Bold), Height = UITheme.S(32), TextAlign = ContentAlignment.MiddleLeft, Padding = UITheme.P(10, 0, 0, 0) };

            _btnIngDisponible = new BotonRhino("Disponible", EstiloBoton.Exito, "✔") { Dock = DockStyle.Fill, Margin = UITheme.P(0, 0, 4, 0), Font = UITheme.Fuente(11f, FontStyle.Bold) };
            _btnIngDisponible.Click += (_, _) => CambiarDisponibilidadIngrediente(true);
            _btnIngAgotado = new BotonRhino("Agotado", EstiloBoton.Peligro, "✖") { Dock = DockStyle.Fill, Margin = UITheme.P(4, 0, 0, 0), Font = UITheme.Fuente(11f, FontStyle.Bold) };
            _btnIngAgotado.Click += (_, _) => CambiarDisponibilidadIngrediente(false);
            var filaEstado = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Height = UITheme.S(58), Margin = new Padding(0) };
            filaEstado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            filaEstado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            filaEstado.Controls.Add(_btnIngDisponible, 0, 0);
            filaEstado.Controls.Add(_btnIngAgotado, 1, 0);

            _lblIngRecetas = new Label { Font = UITheme.Fuente(10.5f), ForeColor = UITheme.Texto, Height = UITheme.S(66), AutoEllipsis = true };
            _lblIngPrecios = new Label { Font = new Font("Consolas", 10f), ForeColor = UITheme.Texto, Height = UITheme.S(92) };

            _btnIngRenombrar = new BotonRhino("Renombrar", EstiloBoton.Secundario, "✏") { Dock = DockStyle.Fill, Margin = UITheme.P(0, 0, 4, 0), Font = UITheme.Fuente(11f, FontStyle.Bold) };
            _btnIngRenombrar.Click += (_, _) => RenombrarIngrediente();
            _btnIngEliminar = new BotonRhino("Eliminar", EstiloBoton.Secundario, "🗑") { Dock = DockStyle.Fill, Margin = UITheme.P(4, 0, 0, 0), Font = UITheme.Fuente(11f, FontStyle.Bold) };
            _btnIngEliminar.Click += (_, _) => EliminarIngrediente();
            var filaEditar = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Height = UITheme.S(56), Margin = new Padding(0), Dock = DockStyle.Bottom };
            filaEditar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            filaEditar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            filaEditar.Controls.Add(_btnIngRenombrar, 0, 0);
            filaEditar.Controls.Add(_btnIngEliminar, 1, 0);
            var btnNuevoIng = new BotonRhino("Nuevo ingrediente", EstiloBoton.Primario, "➕") { Height = UITheme.S(58), Dock = DockStyle.Bottom };
            btnNuevoIng.Click += (_, _) => NuevoIngrediente();

            var cardIng = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(16), AutoScroll = true };
            Diseno.Apilar(cardIng, null, lblTitDet, _lblIngNombre, _lblIngEstado, Diseno.Espacio(8), Titulo("CAMBIAR ESTADO EN CAJA"), filaEstado,
                Diseno.Espacio(10), Titulo("SE USA EN LA RECETA DE"), _lblIngRecetas, Titulo("PRECIO COMO EXTRA (DEL 6.° EN ADELANTE)"), _lblIngPrecios);
            var espacioAcc = Diseno.Espacio(8);
            espacioAcc.Dock = DockStyle.Bottom;
            cardIng.Controls.Add(filaEditar);
            cardIng.Controls.Add(espacioAcc);
            cardIng.Controls.Add(btnNuevoIng);
            var colIng = new Panel { Dock = DockStyle.Right, Width = UITheme.S(330), Padding = UITheme.P(12, 0, 0, 0) };
            colIng.Controls.Add(cardIng);

            var notaIng = Nota("Un ingrediente AGOTADO no se ofrece en caja y la base de datos rechaza venderlo (trigger). No se controla por gramaje: se marca manualmente.");
            notaIng.Dock = DockStyle.Bottom;
            Diseno.Apilar(_pnlIngredientes, _tablaIng, filtrosIng, Diseno.Espacio(6));
            _pnlIngredientes.Controls.Add(notaIng);
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
            var colProd = ColumnaAcciones("Productos del menú",
                Boton("Nuevo producto", EstiloBoton.Primario, "➕", NuevoProducto),
                Boton("Editar producto", EstiloBoton.Secundario, "📝", EditarProducto),
                Boton("Renombrar", EstiloBoton.Secundario, "✏", RenombrarProducto),
                Boton("Cambiar precio", EstiloBoton.Secundario, "💲", CambiarPrecio),
                Boton("Activar / desactivar", EstiloBoton.Ambar, "⇄", AlternarProducto));
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
            _dtIng = _ingredienteDao.Listar();
            _dtIng.Columns.Add("Estado", typeof(string));
            _dtIng.Columns.Add("Usado en", typeof(string));
            _dtIng.Columns.Add("Precio como extra", typeof(string));
            int disponibles = 0;
            foreach (DataRow r in _dtIng.Rows)
            {
                bool disp = Convert.ToBoolean(r["disponible"]);
                if (disp) disponibles++;
                r["Estado"] = disp ? "● Disponible" : "● Agotado";
                int recetas = Convert.ToInt32(r["En recetas"]);
                r["Usado en"] = recetas == 0 ? "Sin recetas" : recetas == 1 ? "1 receta" : $"{recetas} recetas";
                r["Precio como extra"] = r["extra_min"] == DBNull.Value
                    ? "Sin tarifa"
                    : Convert.ToDecimal(r["extra_min"]) == Convert.ToDecimal(r["extra_max"])
                        ? UITheme.Dinero(Convert.ToDecimal(r["extra_min"]))
                        : $"{UITheme.Dinero(Convert.ToDecimal(r["extra_min"]))} a {UITheme.Dinero(Convert.ToDecimal(r["extra_max"]))}";
            }

            // Contadores en los filtros
            int total = _dtIng.Rows.Count;
            foreach (var b in _grpFiltroIng.Botones)
            {
                b.Text = (b.Valor as string) switch
                {
                    "DISP" => $"Disponibles ({disponibles})",
                    "AGOT" => $"Agotados ({total - disponibles})",
                    _ => $"Todos ({total})"
                };
            }

            FiltrarIngredientes();
            Reseleccionar(_tablaIng, "id_ingrediente", sel);
            IngredienteSeleccionado();
        }

        private void FiltrarIngredientes()
        {
            var vista = _dtIng.DefaultView;
            var filtros = new System.Collections.Generic.List<string>();
            if (_grpFiltroIng.Valor as string == "DISP") filtros.Add("disponible = true");
            if (_grpFiltroIng.Valor as string == "AGOT") filtros.Add("disponible = false");
            string q = _txtBuscarIng.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
            if (q.Length > 0) filtros.Add($"Ingrediente LIKE '%{q}%'");
            if (_dtIng.Columns.Contains("disponible")) vista.RowFilter = string.Join(" AND ", filtros);
            vista.Sort = _dtIng.Columns.Contains("Ingrediente") ? "Ingrediente ASC" : "";

            string vacio = _grpFiltroIng.Valor as string == "AGOT" ? "No hay ingredientes agotados. ¡Todo disponible para vender!"
                         : q.Length > 0 ? $"No se encontró \"{_txtBuscarIng.Text.Trim()}\"."
                         : "No hay ingredientes registrados.";
            _tablaIng.Mostrar(vista, vacio);
            var g = _tablaIng.Grid;
            UITheme.OcultarColumnas(g, "id_ingrediente", "disponible", "recetas", "extra_min", "extra_max", "En recetas");
            void Columna(string nombre, int orden, float peso)
            {
                if (!g.Columns.Contains(nombre)) return;
                g.Columns[nombre].DisplayIndex = orden;
                g.Columns[nombre].FillWeight = peso;
            }
            Columna("Ingrediente", 0, 150);
            Columna("Estado", 1, 100);
            Columna("Usado en", 2, 90);
            Columna("Precio como extra", 3, 120);
            if (g.Columns.Contains("Estado")) g.Columns["Estado"].DefaultCellStyle.Font = UITheme.FuenteNegrita;
            g.CellFormatting -= ColorearEstadoIngrediente;
            g.CellFormatting += ColorearEstadoIngrediente;
            IngredienteSeleccionado();
        }

        private void ColorearEstadoIngrediente(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var g = _tablaIng.Grid;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle == null || g.Columns[e.ColumnIndex].Name != "Estado") return;
            bool disp = Convert.ToString(e.Value)?.Contains("Disponible") == true;
            e.CellStyle.ForeColor = disp ? UITheme.Verde : UITheme.RojoOscuro;
            e.CellStyle.BackColor = disp ? UITheme.VerdeSuave : UITheme.RojoSuave;
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            e.CellStyle.SelectionBackColor = UITheme.Oscurecer(e.CellStyle.BackColor, 0.05f);
        }

        /// <summary>Llena la tarjeta de detalle con el ingrediente seleccionado.</summary>
        private void IngredienteSeleccionado()
        {
            var f = _tablaIng.FilaSeleccionada;
            bool hay = f != null;
            _btnIngRenombrar.Enabled = hay;
            _btnIngEliminar.Enabled = hay;
            if (!hay)
            {
                _lblIngNombre.Text = "Selecciona un ingrediente";
                _lblIngEstado.Text = "";
                _lblIngEstado.BackColor = UITheme.Superficie;
                _lblIngRecetas.Text = "—";
                _lblIngPrecios.Text = "—";
                _btnIngDisponible.Enabled = false;
                _btnIngAgotado.Enabled = false;
                return;
            }

            int id = Convert.ToInt32(f!.Cells["id_ingrediente"].Value);
            bool disp = Convert.ToBoolean(f.Cells["disponible"].Value);
            _lblIngNombre.Text = Convert.ToString(f.Cells["Ingrediente"].Value);
            _lblIngEstado.Text = disp ? "● Disponible: se ofrece en caja" : "● Agotado: no se ofrece en caja";
            _lblIngEstado.ForeColor = disp ? UITheme.Verde : UITheme.RojoOscuro;
            _lblIngEstado.BackColor = disp ? UITheme.VerdeSuave : UITheme.RojoSuave;
            // Solo se habilita el cambio que tiene sentido según el estado actual
            _btnIngDisponible.Enabled = !disp;
            _btnIngAgotado.Enabled = disp;

            string recetas = Convert.ToString(f.Cells["recetas"].Value) ?? "";
            _lblIngRecetas.Text = recetas.Length == 0 ? "No forma parte de ninguna receta (solo se agrega como extra)." : recetas;

            try
            {
                var precios = _ingredienteDao.ObtenerPreciosExtra(id);
                var lineas = new System.Collections.Generic.List<string>();
                for (int i = 0; i < precios.Rows.Count; i += 2)
                {
                    string Celda(int k) => $"{Convert.ToString(precios.Rows[k]["tamano"]),-11}{Convert.ToDecimal(precios.Rows[k]["precio_extra"]).ToString("C0", UITheme.Cultura),5}";
                    lineas.Add(i + 1 < precios.Rows.Count ? $"{Celda(i)}   {Celda(i + 1)}" : Celda(i));
                }
                _lblIngPrecios.Text = lineas.Count == 0 ? "Sin tarifa de extra registrada." : string.Join("\n", lineas);
            }
            catch (Exception ex)
            {
                _lblIngPrecios.Text = "No se pudieron leer las tarifas: " + ErroresUi.Amigable(ex).Split('\n')[0];
            }
        }

        private void CambiarDisponibilidadIngrediente(bool disponible)
        {
            var f = _tablaIng.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un ingrediente de la lista."); return; }
            int id = Convert.ToInt32(f.Cells["id_ingrediente"].Value);
            string nombre = Convert.ToString(f.Cells["Ingrediente"].Value) ?? "";
            int recetas = Convert.ToInt32(f.Cells["En recetas"].Value);
            if (!disponible && recetas > 0 &&
                !Dialogo.Confirmar(Shell, $"Marcar \"{nombre}\" como agotado",
                    $"Forma parte de {recetas} receta(s). En caja ya no se podrá agregar como extra y las cajeras deberán ofrecer quitarlo (SIN) de esas especialidades.\n\n¿Continuar?",
                    "Sí, marcar agotado", "Cancelar"))
                return;
            if (ErroresUi.Intentar(Shell, () => _ingredienteDao.CambiarDisponibilidad(id, disponible), "No se pudo cambiar el estado"))
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
                Dialogo.Advertencia(Shell, $"\"{nombre}\" forma parte de {enRecetas} receta(s). Para dejar de usarlo márcalo como Agotado, o quítalo primero de esas recetas en Productos.", "No se puede eliminar");
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

        private void NuevoProducto()
        {
            // Si se está viendo una categoría, el producto nuevo arranca en esa categoría
            string cat = _grpCategoria.Valor as string ?? "";
            if (cat is not ("PIZZA" or "CALZONE" or "COMPLEMENTO" or "BEBIDA")) cat = "PIZZA";
            using var frm = new FrmProducto(null, cat);
            if (frm.ShowDialog(Shell) != DialogResult.OK) return;
            ErroresUi.Intentar(Shell, CargarProductos);
            Dialogo.Exito(Shell, "El producto quedó dado de alta y ya aparece en el punto de venta.", "Producto creado");
        }

        private void EditarProducto()
        {
            var f = _tablaProd.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un producto de la lista."); return; }
            int idProd = Convert.ToInt32(f.Cells["id_producto"].Value);
            Modelos.ProductoEdicion? prod = null;
            if (!ErroresUi.Intentar(Shell, () => prod = _productoDao.ObtenerProductoEdicion(idProd), "No se pudo leer el producto") || prod == null) return;
            using var frm = new FrmProducto(prod);
            if (frm.ShowDialog(Shell) == DialogResult.OK) ErroresUi.Intentar(Shell, CargarProductos);
        }

        private void RenombrarProducto()
        {
            var f = _tablaProd.FilaSeleccionada;
            if (f == null) { Dialogo.Info(Shell, "Selecciona un producto de la lista."); return; }
            int idProd = Convert.ToInt32(f.Cells["id_producto"].Value);
            string actual = Convert.ToString(f.Cells["Producto"].Value) ?? "";
            string categoria = Convert.ToString(f.Cells["Categoría"].Value) ?? "";
            using var frm = new FrmEntrada("Renombrar producto", "Nombre del producto (se cambia en todos sus tamaños)", actual, ModoEntrada.Texto, 80);
            if (frm.ShowDialog(Shell) != DialogResult.OK) return;
            string nuevo = frm.ValorTexto;
            if (nuevo == actual) return;
            ErroresUi.Intentar(Shell, () =>
            {
                if (_productoDao.ExisteNombre(nuevo, categoria, idProd))
                {
                    Dialogo.Advertencia(Shell, $"Ya existe un producto llamado \"{nuevo}\" en esta categoría.", "Nombre repetido");
                    return;
                }
                _productoDao.RenombrarProducto(idProd, nuevo);
                CargarProductos();
            }, "No se pudo renombrar el producto");
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
