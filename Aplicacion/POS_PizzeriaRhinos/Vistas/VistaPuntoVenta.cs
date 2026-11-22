using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Impresion;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>
    /// Punto de venta (boceto pág. 4): modalidad, cliente por teléfono, menú en tarjetas grandes y ticket.
    /// Al cobrar se registra el pedido completo (PEDIDO + DETALLE_PEDIDO + DETALLE_MODIFICADOR) en una sola transacción.
    /// </summary>
    [DesignerCategory("Code")]
    public class VistaPuntoVenta : VistaBase
    {
        private readonly ClienteDAO _clienteDao = new();
        private readonly PedidoDAO _pedidoDao = new();
        private readonly EmpleadoDAO _empleadoDao = new();
        private readonly ProductoDAO _productoDao = new();
        private readonly ReporteDAO _reporteDao = new();

        private string _tipoServicio = "MOSTRADOR";
        private Cliente? _cliente;
        private readonly List<ItemCarrito> _carrito = new();
        private int _grupoPromo = 1;
        private string _categoria = "PIZZA";

        private DataTable _menu = new();
        private DataTable _bebidas = new();
        private DataTable _ingredientes = new();
        private Dictionary<int, string> _recetasTexto = new();

        // UI
        private readonly GrupoOpciones _grpServicio;
        private readonly TextBox _txtTelefono;
        private readonly BotonRhino _btnHistorial, _btnQuitarCliente;
        private readonly Label _lblCliente;
        private readonly GrupoOpciones _grpCategorias;
        private readonly Label _lblAyuda;
        private readonly FlowLayoutPanel _flowProductos;
        private readonly DataGridView _dgvTicket;
        private readonly Label _lblPartidas, _lblSubtotal, _lblExtras, _lblTotal;
        private readonly Panel _pnlRepartidor;
        private readonly Panel _abajoTicket;
        private readonly ComboBox _cmbRepartidor;
        private readonly BotonRhino _btnMenos, _btnMas, _btnQuitar, _btnVaciar, _btnCobrar;

        public VistaPuntoVenta(FrmPrincipal shell) : base(shell)
        {
            var encabezado = new EncabezadoModulo("Nuevo pedido", $"Cajera en turno: {Sesion.NombreCorto} · Elige modalidad, cliente y productos", "🍕");

            // =============================== COLUMNA IZQUIERDA ===============================
            // ---- Tarjeta de modalidad y cliente ----
            var cardCliente = new PanelRedondeado { Height = UITheme.S(232), Padding = UITheme.P(18, 14, 18, 12) };

            _grpServicio = new GrupoOpciones { TamanoBoton = UITheme.S(190, 56) };
            _grpServicio.Agregar("Mostrador", "MOSTRADOR", "🏪");
            _grpServicio.Agregar("A domicilio", "DOMICILIO", "🛵");
            _grpServicio.Seleccionar("MOSTRADOR");
            _grpServicio.ValorCambiado += (_, _) => CambiarServicio(_grpServicio.Valor as string ?? "MOSTRADOR");
            var filaServicio = new FlowLayoutPanel { Height = UITheme.S(64), WrapContents = false };
            filaServicio.Controls.Add(_grpServicio);

            var lblTel = UITheme.CrearEtiqueta("TELÉFONO DEL CLIENTE (10 DÍGITOS)");
            lblTel.AutoSize = false;
            lblTel.Height = UITheme.S(24);

            _txtTelefono = new TextBox { Width = UITheme.S(210), MaxLength = 15, Margin = UITheme.P(4, 10, 6, 4) };
            UITheme.EstilizarTextBox(_txtTelefono);
            _txtTelefono.Font = UITheme.Fuente(16f);
            _txtTelefono.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BuscarCliente(); } };
            _txtTelefono.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

            var btnBuscar = new BotonRhino("Buscar", EstiloBoton.Ambar, "🔍") { Size = UITheme.S(124, 52) };
            btnBuscar.Click += (_, _) => BuscarCliente();
            var btnNuevo = new BotonRhino("Nuevo", EstiloBoton.Secundario, "➕") { Size = UITheme.S(116, 52) };
            btnNuevo.Click += (_, _) => RegistrarClienteNuevo(_txtTelefono.Text.Trim());
            _btnHistorial = new BotonRhino("Historial", EstiloBoton.Secundario, "🕘") { Size = UITheme.S(132, 52), Enabled = false };
            _btnHistorial.Click += (_, _) => { if (_cliente != null) FrmHistorialCliente.Mostrar(Shell, _cliente); };
            _btnQuitarCliente = new BotonRhino("", EstiloBoton.Secundario, "✕") { Size = UITheme.S(52, 52), Enabled = false };
            _btnQuitarCliente.Click += (_, _) => QuitarCliente();
            // La fila del teléfono fluye a dos renglones en pantallas angostas y la tarjeta crece para contenerla
            var filaTel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            filaTel.Controls.AddRange(new Control[] { _txtTelefono, btnBuscar, btnNuevo, _btnHistorial, _btnQuitarCliente });

            _lblCliente = new Label { AutoSize = false, Height = UITheme.S(50), Font = UITheme.Fuente(11f, FontStyle.Bold), ForeColor = UITheme.TextoSuave, AutoEllipsis = true };
            Diseno.Apilar(cardCliente, null, filaServicio, lblTel, filaTel, _lblCliente);
            filaTel.SizeChanged += (_, _) =>
                cardCliente.Height = cardCliente.Padding.Vertical + filaServicio.Height + lblTel.Height + filaTel.Height + _lblCliente.Height + UITheme.S(4);

            // ---- Categorías ----
            _grpCategorias = new GrupoOpciones { TamanoBoton = UITheme.S(146, 52), Dock = DockStyle.Top };
            _grpCategorias.ValorCambiado += (_, _) => { _categoria = _grpCategorias.Valor as string ?? "PIZZA"; ConstruirTarjetas(); };
            _lblAyuda = new Label { AutoSize = false, Height = UITheme.S(30), Font = UITheme.Fuente(10.5f), ForeColor = UITheme.TextoSuave, TextAlign = ContentAlignment.MiddleLeft };

            // ---- Productos ----
            var cardProductos = new PanelRedondeado { Padding = UITheme.P(10), BackColor = UITheme.SuperficieAlt };
            _flowProductos = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = UITheme.P(4) };
            cardProductos.Controls.Add(_flowProductos);

            var colIzq = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(0, 0, 10, 0) };
            Diseno.Apilar(colIzq, cardProductos, cardCliente, Diseno.Espacio(10), _grpCategorias, _lblAyuda);

            // =============================== COLUMNA DERECHA (TICKET) ===============================
            var cardTicket = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(16) };
            var filaTitulo = new Panel { Height = UITheme.S(40) };
            var lblTitTicket = new Label { Text = "🧾  Ticket del pedido", Font = UITheme.Titulo(17f), ForeColor = UITheme.Texto, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _lblPartidas = new Label { Dock = DockStyle.Right, Width = UITheme.S(120), TextAlign = ContentAlignment.MiddleRight, Font = UITheme.FuenteEtiqueta, ForeColor = UITheme.TextoSuave };
            filaTitulo.Controls.Add(lblTitTicket);
            filaTitulo.Controls.Add(_lblPartidas);

            _dgvTicket = new DataGridView();
            UITheme.EstilizarGrid(_dgvTicket);
            _dgvTicket.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _dgvTicket.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _dgvTicket.DefaultCellStyle.Padding = new Padding(UITheme.S(6), UITheme.S(8), UITheme.S(6), UITheme.S(8));
            _dgvTicket.ColumnHeadersDefaultCellStyle.Padding = new Padding(UITheme.S(6), 0, UITheme.S(6), 0);
            var colCant = new DataGridViewTextBoxColumn { Name = "cant", HeaderText = "Cant.", AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = UITheme.S(66) };
            colCant.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopCenter;
            colCant.DefaultCellStyle.Font = UITheme.Fuente(12f, FontStyle.Bold);
            var colDesc = new DataGridViewTextBoxColumn { Name = "desc", HeaderText = "Producto", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            var colImp = new DataGridViewTextBoxColumn { Name = "imp", HeaderText = "Importe", AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = UITheme.S(110) };
            colImp.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopRight;
            colImp.DefaultCellStyle.Font = UITheme.Fuente(11f, FontStyle.Bold);
            _dgvTicket.Columns.AddRange(colCant, colDesc, colImp);
            foreach (DataGridViewColumn c in _dgvTicket.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            _dgvTicket.SelectionChanged += (_, _) => ActualizarBotonesTicket();

            _btnMenos = new BotonRhino("", EstiloBoton.Secundario, "➖") { Dock = DockStyle.Fill, Margin = UITheme.P(0, 6, 6, 0) };
            _btnMenos.Click += (_, _) => CambiarCantidad(-1);
            _btnMas = new BotonRhino("", EstiloBoton.Secundario, "➕") { Dock = DockStyle.Fill, Margin = UITheme.P(0, 6, 6, 0) };
            _btnMas.Click += (_, _) => CambiarCantidad(1);
            _btnQuitar = new BotonRhino("Quitar", EstiloBoton.Secundario, "🗑") { Dock = DockStyle.Fill, Margin = UITheme.P(0, 6, 6, 0) };
            _btnQuitar.Click += (_, _) => QuitarLinea();
            _btnVaciar = new BotonRhino("Vaciar", EstiloBoton.Secundario) { Dock = DockStyle.Fill, Margin = UITheme.P(0, 6, 0, 0) };
            _btnVaciar.Click += (_, _) => VaciarTicket();
            // Columnas en porcentaje: los botones siempre caben, aunque el ticket sea angosto
            var filaAcciones = new TableLayoutPanel { Height = UITheme.S(64), ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            filaAcciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            filaAcciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            filaAcciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            filaAcciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            filaAcciones.Controls.Add(_btnMenos, 0, 0);
            filaAcciones.Controls.Add(_btnMas, 1, 0);
            filaAcciones.Controls.Add(_btnQuitar, 2, 0);
            filaAcciones.Controls.Add(_btnVaciar, 3, 0);

            _pnlRepartidor = new Panel { Height = UITheme.S(86), Visible = false };
            var lblRep = UITheme.CrearEtiqueta("REPARTIDOR (PUEDES ASIGNARLO AL DESPACHAR)");
            lblRep.AutoSize = false;
            lblRep.Height = UITheme.S(24);
            _cmbRepartidor = new ComboBox();
            UITheme.EstilizarCombo(_cmbRepartidor);
            Diseno.Apilar(_pnlRepartidor, null, Diseno.Espacio(6), lblRep, _cmbRepartidor);

            Panel FilaTotal(string texto, out Label valor, Font fuente, Color color)
            {
                var p = new Panel { Height = UITheme.S(fuente.Size > 15 ? 48 : 28) };
                var l = new Label { Text = texto, Dock = DockStyle.Fill, Font = fuente, ForeColor = color, TextAlign = ContentAlignment.MiddleLeft };
                valor = new Label { Dock = DockStyle.Right, Width = UITheme.S(180), Font = fuente, ForeColor = color, TextAlign = ContentAlignment.MiddleRight };
                p.Controls.Add(l);
                p.Controls.Add(valor);
                return p;
            }
            var pnlTotales = new Panel { Height = UITheme.S(118) };
            var filaSub = FilaTotal("Subtotal", out _lblSubtotal, UITheme.Fuente(11f), UITheme.TextoSuave);
            var filaExt = FilaTotal("Extras (ingredientes y fase final)", out _lblExtras, UITheme.Fuente(11f), UITheme.TextoSuave);
            var filaTot = FilaTotal("TOTAL", out _lblTotal, UITheme.Titulo(24f), UITheme.Rojo);
            Diseno.Apilar(pnlTotales, null, filaSub, filaExt, filaTot);

            _btnCobrar = new BotonRhino("COBRAR", EstiloBoton.Primario, "💵")
            {
                Height = UITheme.S(74),
                Font = UITheme.Titulo(20f),
                Dock = DockStyle.Bottom
            };
            _btnCobrar.Click += (_, _) => Cobrar();

            _abajoTicket = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(64 + 118 + 12) };
            Diseno.Apilar(_abajoTicket, null, filaAcciones, _pnlRepartidor, pnlTotales);

            Diseno.Apilar(cardTicket, _dgvTicket, filaTitulo, Diseno.Espacio(6));
            cardTicket.Controls.Add(_abajoTicket);
            cardTicket.Controls.Add(_btnCobrar);

            var colDer = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(10, 0, 0, 0) };
            colDer.Controls.Add(cardTicket);

            var cuerpo = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            cuerpo.Controls.Add(colIzq, 0, 0);
            cuerpo.Controls.Add(colDer, 1, 0);

            Diseno.Apilar(this, cuerpo, encabezado, Diseno.Espacio(12));
            ActualizarCliente();
            RefrescarTicket();
        }

        // =====================================================================
        // CARGA DE DATOS
        // =====================================================================
        public override void AlMostrar()
        {
            _ingredientes = _pedidoDao.ObtenerIngredientesDisponibles();
            _recetasTexto = _productoDao.ObtenerRecetasTexto();
            CargarRepartidores();
            RecargarMenu();
        }

        private void RecargarMenu()
        {
            _menu = _pedidoDao.ObtenerMenuActivo("", _tipoServicio);
            _bebidas = _productoDao.ObtenerBebidasConStock();
            ConstruirCategorias();
            ConstruirTarjetas();
        }

        private void CargarRepartidores()
        {
            var lista = new List<OpcionCombo> { new OpcionCombo(0, "— Asignar al despachar —") };
            lista.AddRange(_empleadoDao.ObtenerRepartidoresActivos().Select(r => new OpcionCombo(r.IdEmpleado, r.Nombre)));
            _cmbRepartidor.DataSource = lista;
            _cmbRepartidor.DisplayMember = nameof(OpcionCombo.Texto);
            _cmbRepartidor.ValueMember = nameof(OpcionCombo.Id);
        }

        private List<DataRow> FilasMenu(string categoria)
        {
            var l = new List<DataRow>();
            foreach (DataRow r in _menu.Rows)
                if (string.Equals(Convert.ToString(r["categoria"]), categoria, StringComparison.OrdinalIgnoreCase)) l.Add(r);
            return l;
        }

        // =====================================================================
        // CATEGORÍAS Y TARJETAS
        // =====================================================================
        private void ConstruirCategorias()
        {
            _grpCategorias.SuspendLayout();
            _grpCategorias.Limpiar();
            _grpCategorias.Agregar("Pizzas", "PIZZA", "🍕");
            _grpCategorias.Agregar("Promo 2x1", "PROMO", "⭐");
            if (_tipoServicio == "MOSTRADOR" && FilasMenu("PIZZA_MOSTRADOR").Count > 0)
                _grpCategorias.Agregar("Mostrador", "PIZZA_MOSTRADOR", "🏪");
            _grpCategorias.Agregar("Calzone", "CALZONE", "🥟");
            _grpCategorias.Agregar("Complementos", "COMPLEMENTO", "🍗");
            _grpCategorias.Agregar("Bebidas", "BEBIDA", "🥤");
            if (!_grpCategorias.Botones.Any(b => Equals(b.Valor, _categoria))) _categoria = "PIZZA";
            _grpCategorias.Seleccionar(_categoria);
            _grpCategorias.ResumeLayout(true);
        }

        private void ConstruirTarjetas()
        {
            _flowProductos.SuspendLayout();
            Diseno.LimpiarControles(_flowProductos);

            switch (_categoria)
            {
                case "PIZZA":
                case "PROMO":
                    {
                        bool promo = _categoria == "PROMO";
                        _lblAyuda.Text = promo
                            ? "⭐ 2x1 en Mediana, Grande y Extragrande: elige la 1.ª pizza; después configuras la 2.ª (se cobra la de mayor precio)."
                            : "Toca una especialidad para elegir tamaño, mitades, ingredientes y fase final.";
                        var filas = FilasMenu("PIZZA").Where(r => string.Equals(Convert.ToString(r["variante"]), "NORMAL", StringComparison.OrdinalIgnoreCase));
                        if (promo) filas = filas.Where(r => Convert.ToBoolean(r["aplica_promocion_2x1"]));
                        foreach (var g in filas.GroupBy(r => Convert.ToInt32(r["id_producto"])).OrderBy(g => Convert.ToString(g.First()["producto"])))
                        {
                            int id = g.Key;
                            _recetasTexto.TryGetValue(id, out string? receta);
                            var t = new TarjetaProducto
                            {
                                Titulo = Convert.ToString(g.First()["producto"]) ?? "",
                                Descripcion = string.IsNullOrEmpty(receta) ? "Receta de la casa" : receta,
                                Precio = "Desde " + UITheme.Dinero(g.Min(r => Convert.ToDecimal(r["precio"]))),
                                Insignia = promo ? "2x1" : null,
                                ColorInsignia = UITheme.Rojo,
                                Valor = id
                            };
                            t.Click += (_, _) => { if (promo) ArmarPromo2x1(id); else ConfigurarPizza(id, ModoPizza.Normal); };
                            _flowProductos.Controls.Add(t);
                        }
                        break;
                    }
                case "PIZZA_MOSTRADOR":
                    {
                        _lblAyuda.Text = "🏪 Tarifa especial solo para venta en mostrador (Grande y Extragrande, un ingrediente).";
                        foreach (var g in FilasMenu("PIZZA_MOSTRADOR").GroupBy(r => Convert.ToInt32(r["id_producto"])))
                        {
                            int id = g.Key;
                            var t = new TarjetaProducto
                            {
                                Titulo = Convert.ToString(g.First()["producto"]) ?? "",
                                Descripcion = string.Join(" · ", g.Select(r => Convert.ToString(r["tamano_presentacion"]))),
                                Precio = "Desde " + UITheme.Dinero(g.Min(r => Convert.ToDecimal(r["precio"]))),
                                Insignia = "Mostrador",
                                ColorInsignia = UITheme.RojoOscuro,
                                Valor = id
                            };
                            t.Click += (_, _) => ConfigurarPizza(id, ModoPizza.Mostrador);
                            _flowProductos.Controls.Add(t);
                        }
                        break;
                    }
                case "BEBIDA":
                    {
                        _lblAyuda.Text = "🥤 El stock se descuenta automáticamente al cobrar. Las bebidas agotadas aparecen deshabilitadas.";
                        foreach (DataRow r in _bebidas.Rows)
                        {
                            int stock = r["stock_actual"] == DBNull.Value ? 0 : Convert.ToInt32(r["stock_actual"]);
                            bool activo = Convert.ToBoolean(r["disponible"]);
                            int idPp = Convert.ToInt32(r["id_producto_precio"]);
                            int restante = stock - _carrito.Where(c => c.IdProductoPrecio == idPp).Sum(c => c.Cantidad);
                            var t = new TarjetaProducto
                            {
                                Titulo = Convert.ToString(r["producto"]) ?? "",
                                Descripcion = Convert.ToString(r["tamano_presentacion"]) ?? "",
                                Precio = UITheme.Dinero(Convert.ToDecimal(r["precio"])),
                                Enabled = activo && stock > 0,
                                Valor = r
                            };
                            AsignarInsigniaStock(t, activo, stock, restante);
                            t.Click += (_, _) => AgregarSimple(r, "BEBIDA", t);
                            _flowProductos.Controls.Add(t);
                        }
                        break;
                    }
                default:
                    {
                        string cat = _categoria;
                        _lblAyuda.Text = cat == "CALZONE" ? "🥟 Calzone por orden." : "🍗 Alitas, boneless, papas y pan de ajo.";
                        foreach (var r in FilasMenu(cat).OrderBy(x => Convert.ToString(x["producto"])).ThenBy(x => Convert.ToInt32(x["id_tamano"])))
                        {
                            int idProd = Convert.ToInt32(r["id_producto"]);
                            _recetasTexto.TryGetValue(idProd, out string? receta);
                            string tam = Convert.ToString(r["tamano_presentacion"]) ?? "";
                            var t = new TarjetaProducto
                            {
                                Titulo = Convert.ToString(r["producto"]) ?? "",
                                Descripcion = string.IsNullOrEmpty(receta) ? tam : $"{tam} · {receta}",
                                Precio = UITheme.Dinero(Convert.ToDecimal(r["precio"])),
                                Valor = r
                            };
                            t.Click += (_, _) => AgregarSimple(r, cat, t);
                            _flowProductos.Controls.Add(t);
                        }
                        break;
                    }
            }

            if (_flowProductos.Controls.Count == 0)
            {
                _flowProductos.Controls.Add(new Label
                {
                    Text = "🍕  No hay productos disponibles en esta categoría.",
                    AutoSize = true,
                    Font = UITheme.Fuente(12f),
                    ForeColor = UITheme.TextoSuave,
                    Margin = UITheme.P(12)
                });
            }
            _flowProductos.ResumeLayout(true);
        }

        private static void AsignarInsigniaStock(TarjetaProducto t, bool activo, int stock, int restante)
        {
            if (!activo || stock <= 0)
            {
                t.Insignia = "AGOTADO";
                t.ColorInsignia = UITheme.RojoOscuro;
            }
            else
            {
                t.Insignia = $"Quedan {Math.Max(0, restante)}";
                t.ColorInsignia = restante <= 5 ? UITheme.Ambar : UITheme.Verde;
            }
            t.Invalidate();
        }

        // =====================================================================
        // AGREGAR PRODUCTOS
        // =====================================================================
        private void ConfigurarPizza(int idProducto, ModoPizza modo)
        {
            string titulo = modo == ModoPizza.Mostrador ? "🏪 Pizza de mostrador" : "🍕 Configurar pizza";
            using var frm = new FrmConfigurarPizza(_menu, _ingredientes, _recetasTexto, modo, idProducto, null, titulo);
            if (frm.ShowDialog(Shell) == DialogResult.OK && frm.Resultado != null)
            {
                _carrito.Add(frm.Resultado);
                RefrescarTicket(_carrito.Count - 1);
            }
        }

        private void ArmarPromo2x1(int idProducto)
        {
            using var f1 = new FrmConfigurarPizza(_menu, _ingredientes, _recetasTexto, ModoPizza.Promo2x1, idProducto, null, "⭐ Promo 2x1 — Pizza 1 de 2");
            if (f1.ShowDialog(Shell) != DialogResult.OK || f1.Resultado == null) return;
            var p1 = f1.Resultado;

            using var f2 = new FrmConfigurarPizza(_menu, _ingredientes, _recetasTexto, ModoPizza.Promo2x1, null, p1.IdTamano,
                $"⭐ Promo 2x1 — Pizza 2 de 2 ({p1.TamanoNombre})");
            if (f2.ShowDialog(Shell) != DialogResult.OK || f2.Resultado == null)
            {
                Dialogo.Info(Shell, "Se canceló la promoción; no se agregó ninguna pizza al ticket.", "Promo 2x1 cancelada");
                return;
            }
            var p2 = f2.Resultado;

            int grupo = _grupoPromo++;
            p1.GrupoPromo = grupo;
            p2.GrupoPromo = grupo;
            // Se cobra la pizza de mayor precio; la otra va en $0. Los extras de cada pizza sí se cobran.
            var gratis = p2.PrecioBase <= p1.PrecioBase ? p2 : p1;
            gratis.PrecioBase = 0m;

            _carrito.Add(p1);
            _carrito.Add(p2);
            RefrescarTicket(_carrito.Count - 1);
        }

        private void AgregarSimple(DataRow r, string categoria, TarjetaProducto? tarjeta)
        {
            int idPp = Convert.ToInt32(r["id_producto_precio"]);
            if (categoria == "BEBIDA")
            {
                int stock = r["stock_actual"] == DBNull.Value ? 0 : Convert.ToInt32(r["stock_actual"]);
                int enTicket = _carrito.Where(c => c.IdProductoPrecio == idPp).Sum(c => c.Cantidad);
                if (enTicket + 1 > stock)
                {
                    Dialogo.Advertencia(Shell, $"Solo hay {stock} pieza(s) de {r["producto"]} {r["tamano_presentacion"]} en el refrigerador.", "Sin existencias suficientes");
                    return;
                }
            }

            var existente = _carrito.FirstOrDefault(c => c.IdProductoPrecio == idPp && !c.EsPromocion2x1 && c.Modificadores.Count == 0);
            if (existente != null)
            {
                existente.Cantidad++;
                RefrescarTicket(_carrito.IndexOf(existente));
            }
            else
            {
                decimal precio = Convert.ToDecimal(r["precio"]);
                _carrito.Add(new ItemCarrito
                {
                    IdProductoPrecio = idPp,
                    IdProducto = Convert.ToInt32(r["id_producto"]),
                    IdTamano = Convert.ToInt32(r["id_tamano"]),
                    NombreProducto = Convert.ToString(r["producto"]) ?? "",
                    TamanoNombre = Convert.ToString(r["tamano_presentacion"]) ?? "",
                    Categoria = categoria,
                    Cantidad = 1,
                    PrecioBase = precio,
                    PrecioLista = precio,
                    CostoExtras = 0m
                });
                RefrescarTicket(_carrito.Count - 1);
            }

            if (tarjeta != null && categoria == "BEBIDA")
            {
                int stock = r["stock_actual"] == DBNull.Value ? 0 : Convert.ToInt32(r["stock_actual"]);
                int restante = stock - _carrito.Where(c => c.IdProductoPrecio == idPp).Sum(c => c.Cantidad);
                AsignarInsigniaStock(tarjeta, true, stock, restante);
            }
        }

        // =====================================================================
        // TICKET
        // =====================================================================
        private static bool EsPizza(ItemCarrito c) => c.Categoria == "PIZZA" || c.Categoria == "PIZZA_MOSTRADOR";

        public static string Describir(ItemCarrito c)
        {
            var sb = new StringBuilder();
            string nombre = c.IdProductoMitad2 != null ? $"{c.NombreProducto} / {c.NombreMitad2}" : c.NombreProducto;
            sb.Append($"{nombre} · {c.TamanoNombre}");
            if (EsPizza(c))
            {
                foreach (var m in c.Modificadores)
                {
                    string mitad = m.MitadAplicada == "MITAD_1" ? " (mitad 1)" : m.MitadAplicada == "MITAD_2" ? " (mitad 2)" : "";
                    string costo = m.CostoAplicado > 0 ? $" +{UITheme.Dinero(m.CostoAplicado)}" : "";
                    sb.Append($"\n   {m.TipoAccion} {m.NombreIngrediente}{mitad}{costo}");
                }
                if (c.TipoMasa == "DELGADA") sb.Append("\n   Masa delgada");
                if (c.TipoMasa == "SARTEN") sb.Append("\n   Masa de sartén");
                if (c.ConOrillaQueso) sb.Append("\n   Orilla de queso");
                if (c.ConQuesoExtra) sb.Append("\n   Queso extra");
            }
            if (c.EsPromocion2x1)
                sb.Append($"\n   ⭐ Promo 2x1 #{c.GrupoPromo}" + (c.PrecioBase == 0 ? " (pizza sin costo)" : ""));
            return sb.ToString();
        }

        private void RefrescarTicket(int? seleccionar = null)
        {
            _dgvTicket.Rows.Clear();
            foreach (var c in _carrito)
                _dgvTicket.Rows.Add(c.Cantidad.ToString(), Describir(c), UITheme.Dinero(c.SubtotalLinea));

            if (_dgvTicket.Rows.Count > 0)
            {
                int i = Math.Clamp(seleccionar ?? _dgvTicket.Rows.Count - 1, 0, _dgvTicket.Rows.Count - 1);
                _dgvTicket.ClearSelection();
                _dgvTicket.CurrentCell = _dgvTicket.Rows[i].Cells[1];
                _dgvTicket.Rows[i].Selected = true;
                try { _dgvTicket.FirstDisplayedScrollingRowIndex = i; } catch (InvalidOperationException) { /* la tabla aún no tiene tamaño */ }
            }

            decimal sub = _carrito.Sum(c => c.PrecioBase * c.Cantidad);
            decimal ext = _carrito.Sum(c => c.CostoExtras * c.Cantidad);
            _lblSubtotal.Text = UITheme.Dinero(sub);
            _lblExtras.Text = UITheme.Dinero(ext);
            _lblTotal.Text = UITheme.Dinero(sub + ext);
            int piezas = _carrito.Sum(c => c.Cantidad);
            _lblPartidas.Text = piezas == 0 ? "Vacío" : $"{piezas} pieza(s)";
            _btnCobrar.Enabled = _carrito.Count > 0;
            _btnVaciar.Enabled = _carrito.Count > 0;
            _btnCobrar.Text = _carrito.Count > 0 ? $"COBRAR  {UITheme.Dinero(sub + ext)}" : "COBRAR";
            ActualizarBotonesTicket();
        }

        private ItemCarrito? LineaSeleccionada()
        {
            var row = _dgvTicket.CurrentRow;
            if (row == null || row.Index < 0 || row.Index >= _carrito.Count) return null;
            return _carrito[row.Index];
        }

        private void ActualizarBotonesTicket()
        {
            var linea = LineaSeleccionada();
            _btnQuitar.Enabled = linea != null;
            _btnMas.Enabled = linea != null && !linea.EsPromocion2x1;
            _btnMenos.Enabled = linea != null && !linea.EsPromocion2x1 && linea.Cantidad > 1;
        }

        private void CambiarCantidad(int delta)
        {
            var linea = LineaSeleccionada();
            if (linea == null) return;
            if (linea.EsPromocion2x1)
            {
                Dialogo.Info(Shell, "Las pizzas de una promoción 2x1 van por pares. Para otra promoción usa la categoría 'Promo 2x1'.");
                return;
            }
            int nueva = linea.Cantidad + delta;
            if (nueva < 1) return;
            if (linea.Categoria == "BEBIDA" && delta > 0)
            {
                DataRow? r = _bebidas.Rows.Cast<DataRow>().FirstOrDefault(x => Convert.ToInt32(x["id_producto_precio"]) == linea.IdProductoPrecio);
                int stock = r == null || r["stock_actual"] == DBNull.Value ? 0 : Convert.ToInt32(r["stock_actual"]);
                if (nueva > stock)
                {
                    Dialogo.Advertencia(Shell, $"Solo hay {stock} pieza(s) disponibles de esta bebida.", "Sin existencias suficientes");
                    return;
                }
            }
            linea.Cantidad = nueva;
            RefrescarTicket(_carrito.IndexOf(linea));
            if (_categoria == "BEBIDA") ConstruirTarjetas();
        }

        private void QuitarLinea()
        {
            var linea = LineaSeleccionada();
            if (linea == null) return;
            int indice = _carrito.IndexOf(linea);
            if (linea.EsPromocion2x1 && linea.GrupoPromo.HasValue)
            {
                if (!Dialogo.Confirmar(Shell, "Quitar promoción 2x1", "Se quitarán las DOS pizzas de esta promoción. ¿Continuar?", "Sí, quitar ambas", "Cancelar"))
                    return;
                int grupo = linea.GrupoPromo.Value;
                _carrito.RemoveAll(c => c.EsPromocion2x1 && c.GrupoPromo == grupo);
            }
            else
            {
                _carrito.Remove(linea);
            }
            RefrescarTicket(Math.Max(0, indice - 1));
            if (_categoria == "BEBIDA") ConstruirTarjetas();
        }

        private void VaciarTicket()
        {
            if (_carrito.Count == 0) return;
            if (!Dialogo.Confirmar(Shell, "Vaciar ticket", "¿Quitar todos los productos del pedido actual?", "Sí, vaciar", "Cancelar", peligro: true))
                return;
            _carrito.Clear();
            _grupoPromo = 1;
            RefrescarTicket();
            if (_categoria == "BEBIDA") ConstruirTarjetas();
        }

        // =====================================================================
        // MODALIDAD Y CLIENTE
        // =====================================================================
        private void CambiarServicio(string nuevo)
        {
            if (nuevo == _tipoServicio) return;
            if (nuevo == "DOMICILIO" && _carrito.Any(c => c.Categoria == "PIZZA_MOSTRADOR"))
            {
                if (!Dialogo.Confirmar(Shell, "Pizzas exclusivas de mostrador",
                        "El ticket tiene pizzas con precio de mostrador, que no se venden a domicilio. ¿Quitarlas y cambiar a domicilio?",
                        "Sí, quitarlas", "No, seguir en mostrador"))
                {
                    _grpServicio.Seleccionar(_tipoServicio);
                    return;
                }
                _carrito.RemoveAll(c => c.Categoria == "PIZZA_MOSTRADOR");
                RefrescarTicket();
            }
            _tipoServicio = nuevo;
            _grpServicio.Seleccionar(nuevo);
            _pnlRepartidor.Visible = nuevo == "DOMICILIO";
            _abajoTicket.Height = UITheme.S(64 + 118 + 12) + (nuevo == "DOMICILIO" ? UITheme.S(86) : 0);
            ActualizarCliente();
            ErroresUi.Intentar(Shell, RecargarMenu, "No se pudo recargar el menú");
        }

        private void BuscarCliente()
        {
            string tel = new string(_txtTelefono.Text.Where(char.IsDigit).ToArray());
            if (tel.Length != 10)
            {
                Dialogo.Advertencia(Shell, "Escribe el teléfono del cliente a 10 dígitos (solo números).", "Teléfono incompleto");
                _txtTelefono.Focus();
                return;
            }
            Cliente? encontrado = null;
            if (!ErroresUi.Intentar(Shell, () => encontrado = _clienteDao.BuscarPorTelefono(tel), "No se pudo buscar el cliente")) return;

            if (encontrado != null)
            {
                CargarCliente(encontrado);
            }
            else if (Dialogo.Confirmar(Shell, "Cliente nuevo", $"El teléfono {tel} no está registrado.\n¿Deseas darlo de alta ahora?", "Sí, registrar", "No"))
            {
                RegistrarClienteNuevo(tel);
            }
        }

        private void RegistrarClienteNuevo(string telefono)
        {
            using var frm = new FrmCliente(null, telefono);
            if (frm.ShowDialog(Shell) == DialogResult.OK && frm.ClienteGuardado != null)
                CargarCliente(frm.ClienteGuardado);
        }

        /// <summary>Carga un cliente en el pedido. Un cliente identificado por teléfono normalmente es pedido a domicilio.</summary>
        public void CargarCliente(Cliente c)
        {
            _cliente = c;
            _txtTelefono.Text = c.Telefono;
            if (_tipoServicio != "DOMICILIO")
            {
                CambiarServicio("DOMICILIO");
                if (_tipoServicio != "DOMICILIO") { ActualizarCliente(); return; }
            }
            ActualizarCliente();
        }

        private void QuitarCliente()
        {
            _cliente = null;
            _txtTelefono.Clear();
            ActualizarCliente();
        }

        private void ActualizarCliente()
        {
            _btnHistorial.Enabled = _cliente != null;
            _btnQuitarCliente.Enabled = _cliente != null;
            if (_cliente != null)
            {
                _lblCliente.Text = $"✔ {_cliente.Nombre}\n📍 {_cliente.Calle} #{_cliente.NumeroCasa}, {_cliente.Colonia}" +
                                   (string.IsNullOrWhiteSpace(_cliente.Referencias) ? "" : $" · {_cliente.Referencias}");
                _lblCliente.ForeColor = UITheme.Verde;
            }
            else if (_tipoServicio == "DOMICILIO")
            {
                _lblCliente.Text = "⚠ Para pedidos a domicilio busca o registra al cliente por teléfono.";
                _lblCliente.ForeColor = UITheme.RojoOscuro;
            }
            else
            {
                _lblCliente.Text = "Venta en mostrador · Público general (el cliente es opcional).";
                _lblCliente.ForeColor = UITheme.TextoSuave;
            }
        }

        // =====================================================================
        // COBRO Y REGISTRO DEL PEDIDO
        // =====================================================================
        private void Cobrar()
        {
            if (_carrito.Count == 0)
            {
                Dialogo.Advertencia(Shell, "Agrega al menos un producto al ticket.", "Pedido vacío");
                return;
            }
            if (_tipoServicio == "DOMICILIO" && _cliente == null)
            {
                Dialogo.Advertencia(Shell, "Todo pedido a domicilio requiere un cliente registrado. Búscalo por teléfono o regístralo.", "Cliente requerido");
                _txtTelefono.Focus();
                return;
            }
            var emp = Sesion.Empleado;
            if (emp == null) return;

            int? idRep = null;
            if (_tipoServicio == "DOMICILIO" && _cmbRepartidor.SelectedValue is int idSel && idSel > 0) idRep = idSel;

            decimal total = _carrito.Sum(c => c.SubtotalLinea);
            using var cobro = new FrmCobro(total, _tipoServicio, _cliente?.Nombre ?? "Público general", _carrito.Count);
            if (cobro.ShowDialog(Shell) != DialogResult.OK) return;

            int folio;
            try
            {
                Cursor = Cursors.WaitCursor;
                folio = _pedidoDao.RegistrarPedidoCompleto(_cliente?.IdCliente, emp.IdEmpleado, idRep, _tipoServicio, _carrito);
            }
            catch (Exception ex)
            {
                Dialogo.Error(Shell, ErroresUi.Amigable(ex) + "\n\nEl pedido NO se guardó (se revirtió la transacción). El ticket sigue intacto.", "No se pudo registrar el pedido");
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            string metodo = cobro.MetodoPago;
            decimal recibido = cobro.Recibido, cambio = cobro.Cambio;
            string mensaje = $"Folio #{folio} · Total {UITheme.Dinero(total)}" +
                             (metodo == "EFECTIVO" ? $"\nCambio: {UITheme.Dinero(cambio)}" : "") +
                             "\n\nEl pedido quedó EN PREPARACIÓN.";
            if (Dialogo.Confirmar(Shell, "¡Pedido registrado!", mensaje, "🖨 Imprimir comanda y ticket", "Listo"))
            {
                ErroresUi.Intentar(Shell, () =>
                {
                    var cab = _reporteDao.ObtenerCabecera(folio);
                    var det = _pedidoDao.ObtenerDetallePedido(folio);
                    if (cab != null) ImpresorTickets.VistaPrevia(Shell, cab, det, ImpresorTickets.TipoDocumento.Ambos, metodo, recibido, cambio);
                }, "No se pudo imprimir");
            }

            _carrito.Clear();
            _grupoPromo = 1;
            QuitarCliente();
            _cmbRepartidor.SelectedIndex = _cmbRepartidor.Items.Count > 0 ? 0 : -1;
            RefrescarTicket();
            ErroresUi.Intentar(Shell, RecargarMenu, "No se pudo recargar el menú");
        }

        /// <summary>Elemento simple para ComboBox (id + texto).</summary>
        private sealed record OpcionCombo(int Id, string Texto);
    }
}
