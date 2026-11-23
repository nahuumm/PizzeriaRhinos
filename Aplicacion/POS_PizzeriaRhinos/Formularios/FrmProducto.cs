using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>
    /// Alta y edición de productos del menú (solo administrador): nombre, categoría, receta base
    /// y precio por tamaño. Se guarda en una sola transacción (PRODUCTO + PRODUCTO_PRECIO + RECETA_PRODUCTO).
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmProducto : Form
    {
        private readonly ProductoDAO _dao = new();
        private readonly ProductoEdicion _prod;
        private readonly bool _edicion;
        private readonly HashSet<int> _receta;
        private DataTable _ingredientes = new();

        private readonly TextBox _txtNombre, _txtMax;
        private readonly GrupoOpciones _grpCategoria;
        private readonly Panel _pnlMax;
        private readonly Label _lblReceta, _lblError;
        private readonly FlowLayoutPanel _flowReceta;
        private readonly TableLayoutPanel _tablaPrecios;
        private readonly Panel _scrollPrecios;
        private readonly TecladoVirtual _teclado;
        private readonly Dictionary<int, (TextBox precio, TextBox? stock, string nombre)> _filasPrecio = new();

        /// <param name="existente">Producto a editar, o null para dar de alta uno nuevo.</param>
        /// <param name="categoriaInicial">Categoría preseleccionada al dar de alta.</param>
        public FrmProducto(ProductoEdicion? existente, string categoriaInicial = "PIZZA")
        {
            _edicion = existente != null;
            _prod = existente ?? new ProductoEdicion { Categoria = categoriaInicial };
            _receta = new HashSet<int>(_prod.Receta);

            Text = _edicion ? "Editar producto" : "Nuevo producto";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            var pantalla = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(UITheme.S(1200), pantalla.Width - 40), Math.Min(UITheme.S(860), pantalla.Height - 40));
            MinimumSize = UITheme.S(960, 680);

            var encabezado = new EncabezadoModulo(_edicion ? $"Editar: {_prod.Nombre}" : "Nuevo producto",
                "Nombre, receta base y precio por tamaño. Un tamaño sin precio no se vende.", _edicion ? "✏" : "➕");

            Label Etiqueta(string t)
            {
                var l = UITheme.CrearEtiqueta(t);
                l.AutoSize = false;
                l.Height = UITheme.S(26);
                return l;
            }

            // ======================= Columna izquierda: datos y receta =======================
            _txtNombre = new TextBox { MaxLength = 80, Text = _prod.Nombre, Tag = "nombre" };
            UITheme.EstilizarTextBox(_txtNombre);
            _txtNombre.Font = UITheme.Fuente(15f);

            _grpCategoria = new GrupoOpciones { TamanoBoton = UITheme.S(150, 54), Dock = DockStyle.Top };
            _grpCategoria.Agregar("Pizza", "PIZZA", "🍕");
            _grpCategoria.Agregar("Calzone", "CALZONE", "🥟");
            _grpCategoria.Agregar("Complemento", "COMPLEMENTO", "🍗");
            _grpCategoria.Agregar("Bebida", "BEBIDA", "🥤");
            if (_prod.Categoria == "PIZZA_MOSTRADOR") _grpCategoria.Agregar("Mostrador", "PIZZA_MOSTRADOR", "🏪");
            _grpCategoria.Seleccionar(_prod.Categoria);
            // En edición no se cambia la categoría: cambiarían las reglas del producto y sus ventas pasadas
            foreach (var b in _grpCategoria.Botones) b.Enabled = !_edicion || Equals(b.Valor, _prod.Categoria);
            _grpCategoria.ValorCambiado += (_, _) => CategoriaCambiada();

            _pnlMax = new Panel { Height = UITheme.S(26 + 46) };
            _txtMax = new TextBox { MaxLength = 2, Width = UITheme.S(90), Text = _prod.MaxIngredientesBase.ToString(), TextAlign = HorizontalAlignment.Center };
            UITheme.EstilizarTextBox(_txtMax);
            Diseno.SoloNumeros(_txtMax);
            var filaMax = new FlowLayoutPanel { Height = UITheme.S(46), WrapContents = false };
            filaMax.Controls.Add(_txtMax);
            filaMax.Controls.Add(new Label { Text = "ingredientes sin costo (a partir del siguiente se cobra extra)", AutoSize = true, ForeColor = UITheme.TextoSuave, Margin = UITheme.P(8, 10, 0, 0) });
            Diseno.Apilar(_pnlMax, null, Etiqueta("MÁXIMO DE INGREDIENTES INCLUIDOS"), filaMax);

            _lblReceta = new Label { Font = UITheme.Titulo(14f), ForeColor = UITheme.Texto, Height = UITheme.S(34), TextAlign = ContentAlignment.BottomLeft };
            _flowReceta = new FlowLayoutPanel { AutoScroll = true, WrapContents = true, Padding = UITheme.P(2) };

            var cardIzq = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(18, 12, 18, 12), Margin = UITheme.P(0, 0, 8, 0) };
            Diseno.Apilar(cardIzq, _flowReceta, Etiqueta("NOMBRE DEL PRODUCTO *"), _txtNombre, Diseno.Espacio(10),
                Etiqueta(_edicion ? "CATEGORÍA (no se puede cambiar al editar)" : "CATEGORÍA *"), _grpCategoria, Diseno.Espacio(6), _pnlMax, _lblReceta);

            // ======================= Columna derecha: precios =======================
            var lblPrecios = new Label { Text = "Precio por tamaño", Font = UITheme.Titulo(14f), ForeColor = UITheme.Texto, Height = UITheme.S(34) };
            var lblNotaPrecios = new Label { Text = "Escribe el precio de cada tamaño que se vende; deja vacío el que no aplique.", Font = UITheme.FuenteChica, ForeColor = UITheme.TextoSuave, Height = UITheme.S(40) };
            _tablaPrecios = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
            _tablaPrecios.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            _tablaPrecios.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29f));
            _tablaPrecios.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29f));
            _scrollPrecios = new Panel { AutoScroll = true };
            _scrollPrecios.Controls.Add(_tablaPrecios);

            var cardDer = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(18, 12, 18, 12), Margin = UITheme.P(8, 0, 0, 0) };
            Diseno.Apilar(cardDer, _scrollPrecios, lblPrecios, lblNotaPrecios);

            var cuerpo = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
            cuerpo.Controls.Add(cardIzq, 0, 0);
            cuerpo.Controls.Add(cardDer, 1, 0);

            // ======================= Teclado y botones =======================
            _teclado = new TecladoVirtual { Dock = DockStyle.Bottom, Height = UITheme.S(270), Visible = false };
            _lblError = new Label { Dock = DockStyle.Bottom, Height = UITheme.S(32), ForeColor = UITheme.Rojo, Font = UITheme.Fuente(11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };

            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(74), Padding = UITheme.P(0, 12, 0, 0) };
            var btnCancelar = new BotonRhino("Cancelar", EstiloBoton.Secundario, "✕") { Dock = DockStyle.Left, Width = UITheme.S(170), DialogResult = DialogResult.Cancel };
            var btnTeclado = new BotonRhino("Teclado", EstiloBoton.Secundario, "⌨") { Dock = DockStyle.Left, Width = UITheme.S(160) };
            btnTeclado.Click += (_, _) => { _teclado.Visible = !_teclado.Visible; btnTeclado.Seleccionado = _teclado.Visible; };
            var btnGuardar = new BotonRhino(_edicion ? "GUARDAR CAMBIOS" : "GUARDAR PRODUCTO", EstiloBoton.Exito, "✔") { Dock = DockStyle.Right, Width = UITheme.S(300) };
            btnGuardar.Click += (_, _) => Guardar();
            barra.Controls.Add(btnTeclado);
            barra.Controls.Add(new Panel { Dock = DockStyle.Left, Width = UITheme.S(10) });
            barra.Controls.Add(btnCancelar);
            barra.Controls.Add(btnGuardar);
            CancelButton = btnCancelar;

            var raiz = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            Diseno.Apilar(raiz, cuerpo, encabezado, Diseno.Espacio(12));
            raiz.Controls.Add(_lblError);
            raiz.Controls.Add(_teclado);
            raiz.Controls.Add(barra);
            Controls.Add(raiz);

            Load += (_, _) =>
            {
                ErroresUi.Intentar(this, () => _ingredientes = new IngredienteDAO().Listar(), "No se pudieron cargar los ingredientes");
                CategoriaCambiada();
                _txtNombre.Focus();
                _txtNombre.SelectionStart = _txtNombre.TextLength;
            };
        }

        private string Categoria => _grpCategoria.Valor as string ?? "PIZZA";
        private bool LlevaReceta => Categoria is "PIZZA" or "CALZONE" or "PIZZA_MOSTRADOR";

        // ---------------------------------------------------------------
        // CONSTRUCCIÓN SEGÚN CATEGORÍA
        // ---------------------------------------------------------------
        private void CategoriaCambiada()
        {
            _pnlMax.Visible = LlevaReceta;
            _lblReceta.Visible = LlevaReceta;
            _flowReceta.Visible = LlevaReceta;
            _lblReceta.Text = Categoria == "CALZONE" ? "Receta base del calzone (toca para incluir)" : "Receta base (toca para incluir)";
            ConstruirReceta();
            ConstruirPrecios();
        }

        private void ConstruirReceta()
        {
            _flowReceta.SuspendLayout();
            Diseno.LimpiarControles(_flowReceta);
            if (LlevaReceta)
            {
                foreach (DataRow r in _ingredientes.Rows)
                {
                    int id = Convert.ToInt32(r["id_ingrediente"]);
                    string nombre = Convert.ToString(r["Ingrediente"]) ?? "";
                    bool hay = Convert.ToBoolean(r["disponible"]);
                    var b = new BotonRhino(nombre, EstiloBoton.Secundario, _receta.Contains(id) ? "✔" : "")
                    {
                        Size = UITheme.S(170, 54),
                        Seleccionado = _receta.Contains(id),
                        Subtexto = hay ? null : "Hoy NO HAY",
                        Font = UITheme.Fuente(10.5f, FontStyle.Bold)
                    };
                    b.Click += (_, _) =>
                    {
                        if (!_receta.Remove(id)) _receta.Add(id);
                        b.Seleccionado = _receta.Contains(id);
                        b.Icono = b.Seleccionado ? "✔" : "";
                        ActualizarTituloReceta();
                    };
                    _flowReceta.Controls.Add(b);
                }
            }
            _flowReceta.ResumeLayout(true);
            ActualizarTituloReceta();
        }

        private void ActualizarTituloReceta()
        {
            if (!LlevaReceta) return;
            string baseTxt = Categoria == "CALZONE" ? "Receta base del calzone" : "Receta base";
            _lblReceta.Text = $"{baseTxt} · {_receta.Count} ingrediente(s) seleccionados";
        }

        private void ConstruirPrecios()
        {
            // Conserva lo que ya se haya escrito al cambiar de categoría
            var escritos = _filasPrecio.ToDictionary(kv => kv.Key, kv => kv.Value.precio.Text);

            List<PrecioTamano> tamanos = new();
            ErroresUi.Intentar(this, () => tamanos = _dao.ObtenerTamanosParaCategoria(Categoria), "No se pudieron cargar los tamaños");
            // En edición se agregan también los tamaños que el producto ya tiene (aunque no sean los típicos)
            foreach (var p in _prod.Precios.Where(p => tamanos.All(t => t.IdTamano != p.IdTamano)))
                tamanos.Add(new PrecioTamano { IdTamano = p.IdTamano, NombreTamano = p.NombreTamano });
            tamanos = tamanos.OrderBy(t => t.IdTamano).ToList();

            _tablaPrecios.SuspendLayout();
            Diseno.LimpiarControles(_tablaPrecios);
            _tablaPrecios.RowStyles.Clear();
            _filasPrecio.Clear();
            bool bebida = Categoria == "BEBIDA";

            _tablaPrecios.RowCount = tamanos.Count + 1;
            _tablaPrecios.Controls.Add(Encabezado("TAMAÑO"), 0, 0);
            _tablaPrecios.Controls.Add(Encabezado("PRECIO $"), 1, 0);
            _tablaPrecios.Controls.Add(Encabezado(bebida ? "STOCK INICIAL" : ""), 2, 0);

            if (tamanos.Count == 0)
            {
                _tablaPrecios.RowCount = 2;
                var vacio = new Label { Text = "No hay tamaños registrados para esta categoría.", AutoSize = true, ForeColor = UITheme.Rojo, Margin = UITheme.P(4, 10, 4, 4) };
                _tablaPrecios.Controls.Add(vacio, 0, 1);
                _tablaPrecios.SetColumnSpan(vacio, 3);
            }

            int fila = 1;
            foreach (var t in tamanos)
            {
                var existente = _prod.Precios.FirstOrDefault(p => p.IdTamano == t.IdTamano);
                var lbl = new Label { Text = t.NombreTamano, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UITheme.Fuente(12f, FontStyle.Bold), ForeColor = UITheme.Texto, Height = UITheme.S(46) };
                var txtPrecio = new TextBox { MaxLength = 8, TextAlign = HorizontalAlignment.Right, Dock = DockStyle.Fill, Margin = UITheme.P(4, 6, 8, 6) };
                UITheme.EstilizarTextBox(txtPrecio);
                txtPrecio.Text = escritos.TryGetValue(t.IdTamano, out var previo) ? previo
                               : existente != null ? existente.Precio.ToString("0.00", CultureInfo.InvariantCulture) : "";
                txtPrecio.KeyPress += (_, e) =>
                {
                    if (e.KeyChar == ',') e.KeyChar = '.';
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !(e.KeyChar == '.' && !txtPrecio.Text.Contains('.'))) e.Handled = true;
                };

                TextBox? txtStock = null;
                if (bebida)
                {
                    txtStock = new TextBox { MaxLength = 4, TextAlign = HorizontalAlignment.Center, Dock = DockStyle.Fill, Margin = UITheme.P(4, 6, 4, 6) };
                    UITheme.EstilizarTextBox(txtStock);
                    if (existente != null)
                    {
                        // El stock de bebidas existentes se maneja en Inventario → Bebidas
                        txtStock.Text = (existente.Stock ?? 0).ToString();
                        txtStock.Enabled = false;
                    }
                    else
                    {
                        txtStock.Text = "0";
                    }
                    Diseno.SoloNumeros(txtStock);
                }

                _tablaPrecios.RowStyles.Add(new RowStyle(SizeType.Absolute, UITheme.S(52)));
                _tablaPrecios.Controls.Add(lbl, 0, fila);
                _tablaPrecios.Controls.Add(txtPrecio, 1, fila);
                if (txtStock != null) _tablaPrecios.Controls.Add(txtStock, 2, fila);
                _filasPrecio[t.IdTamano] = (txtPrecio, txtStock, t.NombreTamano);
                fila++;
            }
            _tablaPrecios.ResumeLayout(true);

            // El teclado en pantalla escribe en el nombre, el máximo y los precios
            _teclado.LimpiarVinculos();
            var cajas = new List<TextBox> { _txtNombre };
            if (LlevaReceta) cajas.Add(_txtMax);
            foreach (var f in _filasPrecio.Values)
            {
                cajas.Add(f.precio);
                if (f.stock != null && f.stock.Enabled) cajas.Add(f.stock);
            }
            _teclado.Vincular(cajas.ToArray());
        }

        private static Label Encabezado(string texto) => new Label
        {
            Text = texto,
            Dock = DockStyle.Fill,
            Height = UITheme.S(28),
            Font = UITheme.FuenteEtiqueta,
            ForeColor = UITheme.TextoSuave,
            TextAlign = ContentAlignment.BottomLeft
        };

        // ---------------------------------------------------------------
        // GUARDAR
        // ---------------------------------------------------------------
        private void Error(string mensaje, Control? foco = null)
        {
            _lblError.Text = "⚠ " + mensaje;
            foco?.Focus();
        }

        private void Guardar()
        {
            _lblError.Text = "";
            string nombre = _txtNombre.Text.Trim();
            if (nombre.Length < 3) { Error("Escribe el nombre del producto (mínimo 3 caracteres).", _txtNombre); return; }

            int max = 5;
            if (LlevaReceta)
            {
                if (!int.TryParse(_txtMax.Text, out max) || max < 1 || max > 10) { Error("El máximo de ingredientes incluidos debe estar entre 1 y 10.", _txtMax); return; }
                if (_receta.Count == 0) { Error("Selecciona al menos un ingrediente para la receta base."); return; }
            }

            var precios = new List<PrecioTamano>();
            foreach (var kv in _filasPrecio)
            {
                string txt = kv.Value.precio.Text.Trim();
                if (txt.Length == 0) continue;
                if (!decimal.TryParse(txt, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal precio) || precio <= 0 || precio > 9999)
                {
                    Error($"El precio de {kv.Value.nombre} no es válido (mayor a 0 y menor a 10,000).", kv.Value.precio);
                    return;
                }
                int? stock = null;
                if (kv.Value.stock != null)
                {
                    if (!int.TryParse(kv.Value.stock.Text, out int st) || st < 0) { Error($"El stock de {kv.Value.nombre} debe ser 0 o más piezas.", kv.Value.stock); return; }
                    stock = st;
                }
                precios.Add(new PrecioTamano { IdTamano = kv.Key, NombreTamano = kv.Value.nombre, Precio = Math.Round(precio, 2), Stock = stock });
            }
            if (precios.Count == 0) { Error("Captura el precio de al menos un tamaño."); return; }

            if (LlevaReceta && _receta.Count > max &&
                !Dialogo.Confirmar(this, "Receta con muchos ingredientes",
                    $"La receta tiene {_receta.Count} ingredientes y el máximo incluido es {max}: cualquier ingrediente CON se cobrará. ¿Guardar así?",
                    "Sí, guardar", "Revisar"))
                return;

            try
            {
                if (_dao.ExisteNombre(nombre, Categoria, _prod.IdProducto))
                {
                    Error($"Ya existe un producto llamado \"{nombre}\" en esta categoría.", _txtNombre);
                    return;
                }
                _prod.Nombre = nombre;
                _prod.Categoria = Categoria;
                _prod.MaxIngredientesBase = LlevaReceta ? max : 0;
                _prod.Receta = _receta.ToList();
                _prod.Precios = precios;

                var (_, avisos) = _dao.GuardarProducto(_prod);
                if (avisos.Count > 0) Dialogo.Advertencia(this, string.Join("\n\n", avisos), "Guardado con avisos");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Error(ErroresUi.Amigable(ex).Split('\n')[0]);
                Dialogo.Error(this, ErroresUi.Amigable(ex), "No se pudo guardar el producto");
            }
        }
    }
}
