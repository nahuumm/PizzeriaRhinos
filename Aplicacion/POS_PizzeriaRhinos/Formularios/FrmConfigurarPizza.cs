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
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    public enum ModoPizza { Normal, Promo2x1, Mostrador }

    /// <summary>
    /// Asistente para configurar una pizza en pasos grandes (pensado para pantalla táctil):
    ///   1. Tamaño  →  2. Especialidad (completa o mitad y mitad)  →  3. Ingredientes SIN / CON  →  4. Fase final.
    /// Implementa las reglas de negocio 3 (límite de 5 ingredientes, cobro desde el 6.°), 4 (fase final obligatoria),
    /// 5 (2x1 solo en Mediana/Grande/Extragrande y mitades) y 6 (pizza de mostrador sin mitades ni agregados).
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmConfigurarPizza : Form
    {
        private const string PasoTamano = "Tamaño";
        private const string PasoEspecialidad = "Especialidad";
        private const string PasoIngredientes = "Ingredientes";
        private const string PasoFinal = "Fase final";

        private readonly DataTable _menu;
        private readonly DataTable _ingredientes;
        private readonly ModoPizza _modo;
        private readonly string _categoria;
        private readonly bool _tamanoFijo;
        private readonly ProductoDAO _productoDao = new();
        private readonly PedidoDAO _pedidoDao = new();
        private readonly Dictionary<int, DataTable> _recetas = new();
        private readonly Dictionary<int, string> _textoRecetas;

        // Estado de la pizza
        private int? _idProd1, _idProd2, _idTamano;
        private string _nomProd1 = "", _nomProd2 = "", _nomTamano = "";
        private bool _mitades;
        private bool _eligiendoMitad2;
        private string _destinoCon = "COMPLETA";
        private readonly List<ModificadorItem> _mods = new();
        private string _masa = "GRUESA";
        private bool _orilla, _quesoExtra;
        private Dictionary<string, decimal> _tarifas = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _pasos = new();
        private int _paso;

        // UI
        private readonly Label _lblTitulo;
        private readonly FlowLayoutPanel _flowPasos;
        private readonly FlowLayoutPanel _contenido;
        private readonly Label _lblResumen;
        private readonly Label _lblTotal;
        private readonly BotonRhino _btnAtras, _btnSiguiente;

        public ItemCarrito? Resultado { get; private set; }

        public FrmConfigurarPizza(DataTable menu, DataTable ingredientes, Dictionary<int, string> textoRecetas,
            ModoPizza modo, int? idProductoInicial, int? idTamanoFijo, string titulo)
        {
            _menu = menu;
            _ingredientes = ingredientes;
            _textoRecetas = textoRecetas;
            _modo = modo;
            _categoria = modo == ModoPizza.Mostrador ? "PIZZA_MOSTRADOR" : "PIZZA";
            _tamanoFijo = idTamanoFijo.HasValue;

            Text = titulo;
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            KeyPreview = true;
            var pantalla = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(UITheme.S(1220), pantalla.Width - 40), Math.Min(UITheme.S(800), pantalla.Height - 40));
            MinimumSize = UITheme.S(960, 640);

            // Pasos según el modo
            if (!_tamanoFijo) _pasos.Add(PasoTamano);
            if (modo != ModoPizza.Mostrador)
            {
                _pasos.Add(PasoEspecialidad);
                _pasos.Add(PasoIngredientes);
            }
            _pasos.Add(PasoFinal);

            // ---------------- Encabezado ----------------
            var encabezado = new Panel { Dock = DockStyle.Top, Height = UITheme.S(122), Padding = UITheme.P(24, 14, 24, 6), BackColor = UITheme.Superficie };
            encabezado.Paint += (_, e) =>
            {
                using var b = new SolidBrush(UITheme.Rojo);
                e.Graphics.FillRectangle(b, 0, 0, encabezado.Width, UITheme.S(6));
                using var p = new Pen(UITheme.Borde);
                e.Graphics.DrawLine(p, 0, encabezado.Height - 1, encabezado.Width, encabezado.Height - 1);
            };
            _lblTitulo = new Label { Text = titulo, Font = UITheme.Titulo(22f), ForeColor = UITheme.Texto, AutoSize = false, Height = UITheme.S(44), Dock = DockStyle.Top };
            _flowPasos = new FlowLayoutPanel { Dock = DockStyle.Top, Height = UITheme.S(56), WrapContents = false };
            encabezado.Controls.Add(_flowPasos);
            encabezado.Controls.Add(_lblTitulo);

            // ---------------- Barra inferior ----------------
            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(88), Padding = UITheme.P(24, 14, 24, 14), BackColor = UITheme.Superficie };
            var btnCancelar = new BotonRhino("Cancelar", EstiloBoton.Secundario, "✕") { Dock = DockStyle.Left, Width = UITheme.S(170) };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _btnSiguiente = new BotonRhino("Siguiente", EstiloBoton.Primario, "▶") { Dock = DockStyle.Right, Width = UITheme.S(260) };
            _btnSiguiente.Click += (_, _) => Avanzar();
            _btnAtras = new BotonRhino("Atrás", EstiloBoton.Secundario, "◀") { Dock = DockStyle.Right, Width = UITheme.S(170) };
            _btnAtras.Click += (_, _) => IrAPaso(_paso - 1);
            barra.Controls.Add(_btnAtras);
            barra.Controls.Add(new Panel { Dock = DockStyle.Right, Width = UITheme.S(12) });
            barra.Controls.Add(_btnSiguiente);
            barra.Controls.Add(btnCancelar);

            // ---------------- Resumen (derecha) ----------------
            var colResumen = new Panel { Dock = DockStyle.Right, Width = UITheme.S(350), Padding = UITheme.P(0, 18, 20, 18) };
            var cardResumen = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            var lblTitResumen = new Label { Text = "🧾  Resumen de la pizza", Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, AutoSize = false, Height = UITheme.S(34) };
            _lblResumen = new Label { Font = UITheme.Fuente(10.5f), ForeColor = UITheme.Texto, AutoSize = false };
            _lblTotal = new Label
            {
                Font = UITheme.Titulo(22f),
                ForeColor = UITheme.Rojo,
                AutoSize = false,
                Height = UITheme.S(46),
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Bottom
            };
            Diseno.Apilar(cardResumen, _lblResumen, lblTitResumen);
            cardResumen.Controls.Add(_lblTotal);
            colResumen.Controls.Add(cardResumen);

            // ---------------- Contenido del paso ----------------
            _contenido = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = UITheme.P(24, 18, 18, 18)
            };
            _contenido.Resize += (_, _) => AjustarAnchos();

            Controls.Add(_contenido);
            Controls.Add(colResumen);
            Controls.Add(barra);
            Controls.Add(encabezado);

            // Estado inicial
            if (idProductoInicial.HasValue)
            {
                _idProd1 = idProductoInicial;
                _nomProd1 = NombreProducto(idProductoInicial.Value);
            }
            if (idTamanoFijo.HasValue) SeleccionarTamano(idTamanoFijo.Value);

            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
            Load += (_, _) => IrAPaso(0);
        }

        // =====================================================================
        // DATOS DEL MENÚ
        // =====================================================================
        private List<DataRow> FilasPizza(Func<DataRow, bool>? filtro = null)
        {
            var lista = new List<DataRow>();
            foreach (DataRow r in _menu.Rows)
            {
                if (!string.Equals(Convert.ToString(r["categoria"]), _categoria, StringComparison.OrdinalIgnoreCase)) continue;
                if (_modo != ModoPizza.Mostrador && !string.Equals(Convert.ToString(r["variante"]), "NORMAL", StringComparison.OrdinalIgnoreCase)) continue;
                if (filtro == null || filtro(r)) lista.Add(r);
            }
            return lista;
        }

        private DataRow? Fila(int idProducto, int idTamano) =>
            FilasPizza(r => Convert.ToInt32(r["id_producto"]) == idProducto && Convert.ToInt32(r["id_tamano"]) == idTamano).FirstOrDefault();

        private string NombreProducto(int idProducto)
        {
            var r = FilasPizza(x => Convert.ToInt32(x["id_producto"]) == idProducto).FirstOrDefault();
            return r == null ? $"Producto {idProducto}" : Convert.ToString(r["producto"]) ?? "";
        }

        private int MaxIngredientesBase()
        {
            if (_idProd1 == null) return 5;
            var r = FilasPizza(x => Convert.ToInt32(x["id_producto"]) == _idProd1.Value).FirstOrDefault();
            return r == null ? 5 : Convert.ToInt32(r["max_ingredientes_base"]);
        }

        private DataTable Receta(int idProducto)
        {
            if (!_recetas.TryGetValue(idProducto, out var dt))
            {
                dt = _productoDao.ObtenerReceta(idProducto);
                _recetas[idProducto] = dt;
            }
            return dt;
        }

        private HashSet<int> IdsReceta(int? idProducto)
        {
            var set = new HashSet<int>();
            if (idProducto == null) return set;
            foreach (DataRow r in Receta(idProducto.Value).Rows) set.Add(Convert.ToInt32(r["id_ingrediente"]));
            return set;
        }

        // =====================================================================
        // NAVEGACIÓN ENTRE PASOS
        // =====================================================================
        private void IrAPaso(int indice)
        {
            if (indice < 0 || indice >= _pasos.Count) return;
            _paso = indice;
            ConstruirIndicadorPasos();
            ConstruirPaso();
            _btnAtras.Visible = _paso > 0;
            bool ultimo = _paso == _pasos.Count - 1;
            _btnSiguiente.Text = ultimo ? "AGREGAR AL TICKET" : "Siguiente";
            _btnSiguiente.Icono = ultimo ? "✔" : "▶";
            _btnSiguiente.Estilo = ultimo ? EstiloBoton.Exito : EstiloBoton.Primario;
            ActualizarResumen();
        }

        private bool ValidarPaso(string paso, bool mostrar)
        {
            string? error = null;
            if (paso == PasoTamano && _idTamano == null) error = "Elige el tamaño de la pizza.";
            if (paso == PasoEspecialidad)
            {
                if (_idProd1 == null) error = _mitades ? "Elige la especialidad de la Mitad 1." : "Elige la especialidad de la pizza.";
                else if (_mitades && _idProd2 == null) error = "Elige la especialidad de la Mitad 2.";
                else if (_idTamano != null && Fila(_idProd1.Value, _idTamano.Value) == null) error = "La especialidad no se vende en el tamaño elegido.";
            }
            if (error != null && mostrar) Dialogo.Advertencia(this, error, "Falta un dato");
            return error == null;
        }

        private void Avanzar()
        {
            if (!ValidarPaso(_pasos[_paso], true)) return;
            if (_paso < _pasos.Count - 1)
            {
                IrAPaso(_paso + 1);
                return;
            }
            // Último paso: validar todo y construir la partida
            foreach (var p in _pasos)
                if (!ValidarPaso(p, true)) { IrAPaso(_pasos.IndexOf(p)); return; }
            Resultado = ConstruirItem();
            if (Resultado != null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void ConstruirIndicadorPasos()
        {
            _flowPasos.SuspendLayout();
            Diseno.LimpiarControles(_flowPasos);
            for (int i = 0; i < _pasos.Count; i++)
            {
                int indice = i;
                var b = new BotonRhino($"{i + 1}. {_pasos[i]}", EstiloBoton.Secundario)
                {
                    Size = UITheme.S(190, 44),
                    Seleccionado = i == _paso,
                    Enabled = i <= _paso,
                    TabStop = false,
                    Icono = i < _paso ? "✔" : ""
                };
                b.Click += (_, _) => { if (indice < _paso) BeginInvoke(new Action(() => IrAPaso(indice))); };
                _flowPasos.Controls.Add(b);
                if (i < _pasos.Count - 1)
                    _flowPasos.Controls.Add(new Label { Text = "›", AutoSize = false, Size = UITheme.S(24, 44), TextAlign = ContentAlignment.MiddleCenter, Font = UITheme.Fuente(16f), ForeColor = UITheme.TextoSuave });
            }
            _flowPasos.ResumeLayout();
        }

        // =====================================================================
        // CONSTRUCCIÓN DE CADA PASO
        // =====================================================================
        private int AnchoContenido => Math.Max(UITheme.S(300), _contenido.ClientSize.Width - _contenido.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - UITheme.S(4));

        private void AjustarAnchos()
        {
            foreach (Control c in _contenido.Controls)
            {
                if (c is FlowLayoutPanel f) f.MaximumSize = new Size(AnchoContenido, 0);
                else if (c is Label l && !l.AutoSize) l.Width = AnchoContenido;
            }
        }

        private Label Seccion(string texto, string? ayuda = null)
        {
            var l = new Label
            {
                Text = texto,
                Font = UITheme.Titulo(15f),
                ForeColor = UITheme.Texto,
                AutoSize = false,
                Width = AnchoContenido,
                Height = UITheme.S(36),
                Margin = UITheme.P(0, 10, 0, 2)
            };
            _contenido.Controls.Add(l);
            if (ayuda != null) Nota(ayuda);
            return l;
        }

        private Label Nota(string texto, Color? color = null)
        {
            var fuente = UITheme.Fuente(10.5f);
            int alto = TextRenderer.MeasureText(texto, fuente, new Size(AnchoContenido, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + UITheme.S(6);
            var l = new Label
            {
                Text = texto,
                Font = fuente,
                ForeColor = color ?? UITheme.TextoSuave,
                AutoSize = false,
                Width = AnchoContenido,
                Height = alto,
                Margin = UITheme.P(0, 0, 0, 6)
            };
            _contenido.Controls.Add(l);
            return l;
        }

        private FlowLayoutPanel Bloque()
        {
            var f = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                MaximumSize = new Size(AnchoContenido, 0),
                Margin = UITheme.P(0, 0, 0, 10)
            };
            _contenido.Controls.Add(f);
            return f;
        }

        private GrupoOpciones Grupo(Size tamano)
        {
            var g = new GrupoOpciones { TamanoBoton = tamano, MaximumSize = new Size(AnchoContenido, 0), Margin = UITheme.P(0, 0, 0, 10) };
            _contenido.Controls.Add(g);
            return g;
        }

        /// <summary>
        /// Reconstruye el paso actual DESPUÉS de que termine el evento Click en curso
        /// (el botón presionado se destruye al reconstruir, por eso se difiere con BeginInvoke).
        /// </summary>
        private void Refrescar()
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                ConstruirPaso();
                ActualizarResumen();
            }));
        }

        private void ConstruirPaso()
        {
            _contenido.SuspendLayout();
            Diseno.LimpiarControles(_contenido);
            _contenido.AutoScrollPosition = new Point(0, 0);

            switch (_pasos[_paso])
            {
                case PasoTamano: ConstruirPasoTamano(); break;
                case PasoEspecialidad: ConstruirPasoEspecialidad(); break;
                case PasoIngredientes: ConstruirPasoIngredientes(); break;
                case PasoFinal: ConstruirPasoFinal(); break;
            }
            _contenido.ResumeLayout(true);
        }

        private void ConstruirPasoTamano()
        {
            Seccion("¿De qué tamaño?");
            if (_modo == ModoPizza.Promo2x1)
                Nota("⭐ Promoción 2x1: aplica solamente en Mediana, Grande y Extragrande.", UITheme.RojoOscuro);
            if (_modo == ModoPizza.Mostrador)
                Nota("🏪 Precio especial de mostrador (solo Grande y Extragrande, un ingrediente).", UITheme.RojoOscuro);

            // Tamaños disponibles: si ya hay especialidad, los de esa especialidad; si no, los de la categoría
            var filas = _idProd1.HasValue
                ? FilasPizza(r => Convert.ToInt32(r["id_producto"]) == _idProd1.Value)
                : FilasPizza();
            if (_modo == ModoPizza.Promo2x1) filas = filas.Where(r => Convert.ToBoolean(r["aplica_promocion_2x1"])).ToList();

            var tamanos = filas
                .GroupBy(r => Convert.ToInt32(r["id_tamano"]))
                .Select(g => new
                {
                    Id = g.Key,
                    Nombre = Convert.ToString(g.First()["tamano_presentacion"]) ?? "",
                    Precio = g.Min(r => Convert.ToDecimal(r["precio"]))
                })
                .OrderBy(t => t.Id)
                .ToList();

            if (tamanos.Count == 0)
            {
                Nota("No hay tamaños disponibles para esta selección. Revisa el catálogo en Inventario.", UITheme.Rojo);
                return;
            }

            var grupo = Grupo(UITheme.S(200, 86));
            foreach (var t in tamanos)
            {
                var b = grupo.Agregar(t.Nombre, t.Id, "", UITheme.Dinero(t.Precio));
                b.Font = UITheme.Titulo(15f);
            }
            if (_idTamano.HasValue) grupo.Seleccionar(_idTamano.Value);
            grupo.ValorCambiado += (_, _) =>
            {
                if (grupo.Valor is int id) SeleccionarTamano(id);
                ActualizarResumen();
            };
        }

        private void SeleccionarTamano(int idTamano)
        {
            _idTamano = idTamano;
            var r = FilasPizza(x => Convert.ToInt32(x["id_tamano"]) == idTamano).FirstOrDefault();
            _nomTamano = r == null ? "" : Convert.ToString(r["tamano_presentacion"]) ?? "";
            try
            {
                _tarifas = _productoDao.ObtenerTarifasOpcionFinal(idTamano);
            }
            catch (Exception ex)
            {
                Dialogo.Error(this, ErroresUi.Amigable(ex), "No se pudieron leer las tarifas de fase final");
            }
            RecalcularCostos();
        }

        private void ConstruirPasoEspecialidad()
        {
            Seccion("¿Pizza completa o mitad y mitad?");
            var tipo = Grupo(UITheme.S(230, 64));
            tipo.Agregar("Pizza completa", false, "🍕");
            tipo.Agregar("Mitad y mitad", true, "◐");
            tipo.Seleccionar(_mitades);
            tipo.ValorCambiado += (_, _) =>
            {
                bool nuevo = tipo.Valor is bool v && v;
                if (nuevo == _mitades) return;
                if (_mods.Count > 0 &&
                    !Dialogo.Confirmar(this, "Cambiar tipo de pizza", "Se quitarán los ingredientes SIN / CON que ya elegiste. ¿Continuar?"))
                {
                    tipo.Seleccionar(_mitades);
                    return;
                }
                _mitades = nuevo;
                _mods.Clear();
                _destinoCon = "COMPLETA";
                if (!_mitades) { _idProd2 = null; _nomProd2 = ""; }
                _eligiendoMitad2 = _mitades && _idProd1 != null;
                Refrescar();
            };

            if (_mitades)
            {
                Seccion("¿Qué mitad estás eligiendo?");
                var mitad = Grupo(UITheme.S(300, 72));
                mitad.Agregar("Mitad 1", false, "◧", _idProd1 == null ? "Sin elegir" : _nomProd1);
                mitad.Agregar("Mitad 2", true, "◨", _idProd2 == null ? "Sin elegir" : _nomProd2);
                mitad.Seleccionar(_eligiendoMitad2);
                mitad.ValorCambiado += (_, _) => { _eligiendoMitad2 = mitad.Valor is bool v && v; Refrescar(); };
            }

            Seccion(_mitades ? (_eligiendoMitad2 ? "Especialidad de la Mitad 2" : "Especialidad de la Mitad 1") : "Elige la especialidad");
            var bloque = Bloque();
            var especialidades = FilasPizza(r => _idTamano == null || Convert.ToInt32(r["id_tamano"]) == _idTamano.Value)
                .GroupBy(r => Convert.ToInt32(r["id_producto"]))
                .Select(g => g.First())
                .OrderBy(r => Convert.ToString(r["producto"]))
                .ToList();

            int? seleccion = _mitades && _eligiendoMitad2 ? _idProd2 : _idProd1;
            foreach (var r in especialidades)
            {
                int id = Convert.ToInt32(r["id_producto"]);
                string nombre = Convert.ToString(r["producto"]) ?? "";
                _textoRecetas.TryGetValue(id, out string? receta);
                var b = new BotonRhino(nombre, EstiloBoton.Secundario)
                {
                    Size = UITheme.S(232, 84),
                    Subtexto = string.IsNullOrEmpty(receta) ? "Receta de la casa" : receta,
                    Seleccionado = seleccion == id,
                    Valor = id
                };
                b.Click += (_, _) => ElegirEspecialidad(id, nombre);
                bloque.Controls.Add(b);
            }
        }

        private void ElegirEspecialidad(int id, string nombre)
        {
            if (_mitades && _eligiendoMitad2)
            {
                if (id == _idProd1)
                {
                    Dialogo.Advertencia(this, "La Mitad 2 debe ser una especialidad diferente a la Mitad 1. Si quieres la misma, elige 'Pizza completa'.");
                    return;
                }
                if (_idProd2 != id && _mods.Any(m => m.MitadAplicada == "MITAD_2"))
                    _mods.RemoveAll(m => m.MitadAplicada == "MITAD_2");
                _idProd2 = id;
                _nomProd2 = nombre;
            }
            else
            {
                if (_idProd1 != id && _mods.Count > 0)
                {
                    // La receta cambió: los SIN / CON de la mitad 1 o de la pizza completa ya no aplican
                    _mods.RemoveAll(m => m.MitadAplicada != "MITAD_2");
                }
                _idProd1 = id;
                _nomProd1 = nombre;
                if (_idProd2 == id) { _idProd2 = null; _nomProd2 = ""; }
                if (_mitades && _idProd2 == null) _eligiendoMitad2 = true;
            }
            RecalcularCostos();
            Refrescar();
        }

        private void ConstruirPasoIngredientes()
        {
            if (_idProd1 == null) return;
            int max = MaxIngredientesBase();
            int efectivos = ContarIngredientesEfectivos();
            Seccion("Ingredientes", $"Los primeros {max} ingredientes no tienen costo; a partir del {max + 1}.° se cobra según el tamaño.");
            Nota($"Ingredientes en la pizza: {efectivos} de {max} incluidos" + (efectivos > max ? $"  ·  {efectivos - max} con costo extra" : ""),
                efectivos > max ? UITheme.RojoOscuro : UITheme.Verde);

            // ---- SIN: quitar ingredientes de la receta ----
            void BloqueSin(int idProducto, string nombre, string mitad)
            {
                var receta = Receta(idProducto);
                Seccion($"Quitar de {nombre}", "Toca un ingrediente para pedirlo SIN él (no tiene costo).");
                if (receta.Rows.Count == 0)
                {
                    Nota("Esta especialidad no tiene receta base registrada.");
                    return;
                }
                var bloque = Bloque();
                foreach (DataRow r in receta.Rows)
                {
                    int idIng = Convert.ToInt32(r["id_ingrediente"]);
                    string nomIng = Convert.ToString(r["nombre"]) ?? "";
                    bool quitado = _mods.Any(m => m.TipoAccion == "SIN" && m.IdIngrediente == idIng && m.MitadAplicada == mitad);
                    var b = new BotonRhino(quitado ? $"SIN {nomIng}" : nomIng, EstiloBoton.Secundario, quitado ? "✖" : "✔")
                    {
                        Size = UITheme.S(190, 58),
                        Seleccionado = quitado
                    };
                    b.Click += (_, _) =>
                    {
                        if (quitado) _mods.RemoveAll(m => m.TipoAccion == "SIN" && m.IdIngrediente == idIng && m.MitadAplicada == mitad);
                        else _mods.Add(new ModificadorItem { IdIngrediente = idIng, NombreIngrediente = nomIng, TipoAccion = "SIN", MitadAplicada = mitad, CostoAplicado = 0m });
                        RecalcularCostos();
                        Refrescar();
                    };
                    bloque.Controls.Add(b);
                }
            }

            if (_mitades && _idProd2 != null)
            {
                BloqueSin(_idProd1.Value, $"Mitad 1 · {_nomProd1}", "MITAD_1");
                BloqueSin(_idProd2.Value, $"Mitad 2 · {_nomProd2}", "MITAD_2");
            }
            else
            {
                BloqueSin(_idProd1.Value, _nomProd1, "COMPLETA");
            }

            // ---- CON: agregar ingredientes ----
            Seccion("Agregar ingredientes (CON)", "No existe 'extra' del mismo ingrediente: solo se agregan ingredientes que no trae la receta.");
            if (_mitades)
            {
                var destino = Grupo(UITheme.S(210, 56));
                destino.Agregar("Toda la pizza", "COMPLETA");
                destino.Agregar("Solo Mitad 1", "MITAD_1");
                destino.Agregar("Solo Mitad 2", "MITAD_2");
                destino.Seleccionar(_destinoCon);
                destino.ValorCambiado += (_, _) => { _destinoCon = destino.Valor as string ?? "COMPLETA"; Refrescar(); };
            }
            else
            {
                _destinoCon = "COMPLETA";
            }

            var receta1 = IdsReceta(_idProd1);
            var receta2 = _mitades ? IdsReceta(_idProd2) : new HashSet<int>();
            var bloqueCon = Bloque();
            foreach (DataRow r in _ingredientes.Rows)
            {
                int idIng = Convert.ToInt32(r["id_ingrediente"]);
                string nomIng = Convert.ToString(r["nombre"]) ?? "";
                bool disponible = Convert.ToBoolean(r["disponible"]);
                bool yaIncluido = _destinoCon switch
                {
                    "MITAD_1" => receta1.Contains(idIng),
                    "MITAD_2" => receta2.Contains(idIng),
                    _ => receta1.Contains(idIng) || receta2.Contains(idIng)
                };
                var existente = _mods.FirstOrDefault(m => m.TipoAccion == "CON" && m.IdIngrediente == idIng && m.MitadAplicada == _destinoCon);
                var b = new BotonRhino(nomIng, EstiloBoton.Secundario, existente != null ? "＋" : "")
                {
                    Size = UITheme.S(190, 64),
                    Seleccionado = existente != null,
                    Enabled = disponible && !yaIncluido,
                    Subtexto = !disponible ? "Agotado" : yaIncluido ? "Ya incluido" :
                               existente != null ? (existente.CostoAplicado > 0 ? "+" + UITheme.Dinero(existente.CostoAplicado) : "Sin costo") : null
                };
                string destinoActual = _destinoCon;
                b.Click += (_, _) =>
                {
                    if (existente != null) _mods.Remove(existente);
                    else _mods.Add(new ModificadorItem { IdIngrediente = idIng, NombreIngrediente = nomIng, TipoAccion = "CON", MitadAplicada = destinoActual });
                    RecalcularCostos();
                    Refrescar();
                };
                bloqueCon.Controls.Add(b);
            }
        }

        private void ConstruirPasoFinal()
        {
            Seccion("Fase final", "Antes de mandar a cocina, pregunta al cliente por la masa, la orilla y el queso.");
            if (_idTamano == null)
            {
                Nota("Primero elige el tamaño.", UITheme.Rojo);
                return;
            }
            decimal sarten = _tarifas.TryGetValue("MASA_SARTEN", out var s) ? s : 0m;
            decimal orilla = _tarifas.TryGetValue("ORILLA_QUESO", out var o) ? o : 0m;
            decimal queso = _tarifas.TryGetValue("QUESO_EXTRA", out var q) ? q : 0m;

            Seccion("Tipo de masa");
            var masa = Grupo(UITheme.S(230, 80));
            masa.Agregar("Gruesa", "GRUESA", "", "Incluida (por defecto)");
            masa.Agregar("Delgada", "DELGADA", "", "Sin costo");
            masa.Agregar("Sartén", "SARTEN", "", "+" + UITheme.Dinero(sarten));
            masa.Seleccionar(_masa);
            masa.ValorCambiado += (_, _) => { _masa = masa.Valor as string ?? "GRUESA"; ActualizarResumen(); };

            Seccion("Extras de queso");
            var bloque = Bloque();
            var bOrilla = new BotonRhino("Orilla de queso", EstiloBoton.Secundario, "🧀") { Size = UITheme.S(260, 80), Subtexto = "+" + UITheme.Dinero(orilla), Seleccionado = _orilla };
            bOrilla.Click += (_, _) => { _orilla = !_orilla; bOrilla.Seleccionado = _orilla; ActualizarResumen(); };
            var bQueso = new BotonRhino("Queso extra", EstiloBoton.Secundario, "🧀") { Size = UITheme.S(260, 80), Subtexto = "+" + UITheme.Dinero(queso), Seleccionado = _quesoExtra };
            bQueso.Click += (_, _) => { _quesoExtra = !_quesoExtra; bQueso.Seleccionado = _quesoExtra; ActualizarResumen(); };
            bloque.Controls.Add(bOrilla);
            bloque.Controls.Add(bQueso);

            if (_modo == ModoPizza.Promo2x1)
                Nota("En el 2x1 la fase final se cobra por separado en cada pizza.", UITheme.RojoOscuro);
        }

        // =====================================================================
        // CÁLCULOS
        // =====================================================================
        private int ContarIngredientesEfectivos()
        {
            if (_idProd1 == null) return 0;
            int baseCount = Receta(_idProd1.Value).Rows.Count;
            int sin = _mods.Count(m => m.TipoAccion == "SIN" && m.MitadAplicada != "MITAD_2");
            int con = _mods.Count(m => m.TipoAccion == "CON");
            return Math.Max(0, baseCount - sin) + con;
        }

        /// <summary>
        /// Recalcula el costo de cada ingrediente CON en orden usando la función almacenada
        /// fn_calcular_costo_extra_ingrediente. Se envían como "agregados previos" los CON anteriores
        /// menos los SIN de la receta, para que quitar un ingrediente libere un lugar sin costo.
        /// </summary>
        private void RecalcularCostos()
        {
            if (_idProd1 == null || _idTamano == null) return;
            try
            {
                int sinBase = _mods.Count(m => m.TipoAccion == "SIN" && m.MitadAplicada != "MITAD_2");
                int previos = 0;
                foreach (var m in _mods.Where(m => m.TipoAccion == "CON"))
                {
                    m.CostoAplicado = _pedidoDao.CalcularCostoExtraIngrediente(_idProd1.Value, _idTamano.Value, m.IdIngrediente, previos - sinBase);
                    previos++;
                }
                foreach (var m in _mods.Where(m => m.TipoAccion == "SIN")) m.CostoAplicado = 0m;
            }
            catch (Exception ex)
            {
                Dialogo.Error(this, ErroresUi.Amigable(ex), "No se pudo calcular el costo de ingredientes");
            }
        }

        private (decimal baseP, decimal ingredientes, decimal fase) CalcularPrecios()
        {
            decimal baseP = 0m;
            if (_idProd1 != null && _idTamano != null)
            {
                var r1 = Fila(_idProd1.Value, _idTamano.Value);
                if (r1 != null) baseP = Convert.ToDecimal(r1["precio"]);
                if (_mitades && _idProd2 != null)
                {
                    var r2 = Fila(_idProd2.Value, _idTamano.Value);
                    if (r2 != null) baseP = Math.Max(baseP, Convert.ToDecimal(r2["precio"])); // se cobra la mitad de mayor precio
                }
            }
            decimal ing = _mods.Sum(m => m.CostoAplicado);
            decimal fase = 0m;
            if (_idTamano != null)
            {
                if (_masa == "SARTEN" && _tarifas.TryGetValue("MASA_SARTEN", out var s)) fase += s;
                if (_orilla && _tarifas.TryGetValue("ORILLA_QUESO", out var o)) fase += o;
                if (_quesoExtra && _tarifas.TryGetValue("QUESO_EXTRA", out var q)) fase += q;
            }
            return (baseP, ing, fase);
        }

        private static string NombreMitad(string mitad) => mitad switch
        {
            "MITAD_1" => "Mitad 1",
            "MITAD_2" => "Mitad 2",
            _ => "Toda"
        };

        private void ActualizarResumen()
        {
            var sb = new StringBuilder();
            string especialidad = _idProd1 == null ? "Sin elegir" :
                (_mitades ? $"{_nomProd1} / {(_idProd2 == null ? "¿Mitad 2?" : _nomProd2)}" : _nomProd1);
            sb.AppendLine($"🍕 {especialidad}");
            sb.AppendLine($"📏 Tamaño: {(_idTamano == null ? "Sin elegir" : _nomTamano)}");
            if (_modo == ModoPizza.Promo2x1) sb.AppendLine("⭐ Parte de una promoción 2x1");
            if (_modo == ModoPizza.Mostrador) sb.AppendLine("🏪 Tarifa exclusiva de mostrador");
            sb.AppendLine();
            if (_mods.Count > 0)
            {
                foreach (var m in _mods)
                {
                    string costo = m.CostoAplicado > 0 ? $"  +{UITheme.Dinero(m.CostoAplicado)}" : "";
                    string mitad = _mitades ? $" ({NombreMitad(m.MitadAplicada)})" : "";
                    sb.AppendLine($"{(m.TipoAccion == "SIN" ? "✖ SIN" : "＋ CON")} {m.NombreIngrediente}{mitad}{costo}");
                }
                sb.AppendLine();
            }
            string masaTexto = _masa switch { "DELGADA" => "Delgada", "SARTEN" => "Sartén", _ => "Gruesa" };
            sb.AppendLine($"Masa: {masaTexto}");
            if (_orilla) sb.AppendLine("Orilla de queso");
            if (_quesoExtra) sb.AppendLine("Queso extra");

            var (baseP, ing, fase) = CalcularPrecios();
            sb.AppendLine();
            sb.AppendLine($"Precio base:            {UITheme.Dinero(baseP)}");
            sb.AppendLine($"Ingredientes extra:   {UITheme.Dinero(ing)}");
            sb.AppendLine($"Fase final:                {UITheme.Dinero(fase)}");
            _lblResumen.Text = sb.ToString();
            _lblTotal.Text = UITheme.Dinero(baseP + ing + fase);
        }

        private ItemCarrito? ConstruirItem()
        {
            if (_idProd1 == null || _idTamano == null) return null;
            var r1 = Fila(_idProd1.Value, _idTamano.Value);
            if (r1 == null)
            {
                Dialogo.Error(this, "No se encontró el precio de la especialidad en el tamaño elegido.");
                return null;
            }

            // Pizza de mostrador: sin mitades ni ingredientes agregados (validado también en PedidoDAO)
            if (_modo == ModoPizza.Mostrador && (_mitades || _mods.Any(m => m.TipoAccion == "CON")))
            {
                Dialogo.Advertencia(this, "La pizza de mostrador solo lleva 1 ingrediente y no admite mitades ni ingredientes agregados.");
                return null;
            }

            var (baseP, ing, fase) = CalcularPrecios();
            return new ItemCarrito
            {
                IdProductoPrecio = Convert.ToInt32(r1["id_producto_precio"]),
                IdProducto = _idProd1.Value,
                IdTamano = _idTamano.Value,
                NombreProducto = _nomProd1,
                TamanoNombre = _nomTamano,
                Categoria = Convert.ToString(r1["categoria"]) ?? _categoria,
                IdProductoMitad2 = _mitades ? _idProd2 : null,
                NombreMitad2 = _mitades ? _nomProd2 : null,
                Cantidad = 1,
                TipoMasa = _masa,
                ConOrillaQueso = _orilla,
                ConQuesoExtra = _quesoExtra,
                EsPromocion2x1 = _modo == ModoPizza.Promo2x1,
                PrecioBase = baseP,
                PrecioLista = baseP,
                CostoExtras = ing + fase,
                Modificadores = _mods.Select(m => new ModificadorItem
                {
                    IdIngrediente = m.IdIngrediente,
                    NombreIngrediente = m.NombreIngrediente,
                    TipoAccion = m.TipoAccion,
                    MitadAplicada = _mitades ? m.MitadAplicada : "COMPLETA",
                    CostoAplicado = m.CostoAplicado
                }).ToList()
            };
        }
    }
}
