using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>
    /// Sistema de diseño de Rhinos Pizza (equivalente a las variables CSS del proyecto Veterinaria).
    /// Paleta basada en el logo y en los menús de referencia: rojo pizzería, ámbar, crema y café oscuro.
    /// Todas las medidas en píxeles pasan por S() para escalar correctamente en pantallas con DPI alto.
    /// </summary>
    public static class UITheme
    {
        // ---------------------------------------------------------------
        // PALETA (equivalente a :root { --rh-... } en CSS)
        // ---------------------------------------------------------------
        public static readonly Color Rojo = Color.FromArgb(214, 40, 40);          // Rojo Rhinos (acción principal)
        public static readonly Color RojoOscuro = Color.FromArgb(155, 28, 28);    // Hover / encabezados
        public static readonly Color RojoProfundo = Color.FromArgb(110, 18, 18);  // Degradados
        public static readonly Color RojoSuave = Color.FromArgb(253, 226, 225);   // Selección / fondos de alerta
        public static readonly Color Ambar = Color.FromArgb(246, 165, 26);        // Pastillas de precio (estilo menú)
        public static readonly Color AmbarSuave = Color.FromArgb(255, 239, 204);
        public static readonly Color Verde = Color.FromArgb(46, 158, 91);         // Éxito / HAY
        public static readonly Color VerdeSuave = Color.FromArgb(220, 245, 230);
        public static readonly Color Azul = Color.FromArgb(29, 99, 196);          // EN_RUTA (azul cromado del logo)
        public static readonly Color AzulSuave = Color.FromArgb(222, 235, 252);

        public static readonly Color Fondo = Color.FromArgb(255, 248, 240);       // Crema de fondo
        public static readonly Color Superficie = Color.White;                    // Tarjetas
        public static readonly Color SuperficieAlt = Color.FromArgb(255, 251, 246);
        public static readonly Color Borde = Color.FromArgb(240, 225, 208);
        public static readonly Color Texto = Color.FromArgb(43, 33, 24);          // Café casi negro
        public static readonly Color TextoSuave = Color.FromArgb(122, 106, 93);
        public static readonly Color Deshabilitado = Color.FromArgb(236, 230, 224);
        public static readonly Color TextoDeshabilitado = Color.FromArgb(170, 160, 150);

        // ---------------------------------------------------------------
        // ESCALA DPI
        // ---------------------------------------------------------------
        private static float? _escala;
        public static float Escala
        {
            get
            {
                if (_escala == null)
                {
                    try
                    {
                        using var g = Graphics.FromHwnd(IntPtr.Zero);
                        _escala = Math.Max(1f, g.DpiX / 96f);
                    }
                    catch
                    {
                        _escala = 1f;
                    }
                }
                return _escala.Value;
            }
        }

        /// <summary>Convierte píxeles de diseño (a 96 DPI) a píxeles reales.</summary>
        public static int S(int px) => (int)Math.Round(px * Escala);
        public static Size S(int w, int h) => new Size(S(w), S(h));
        public static Padding P(int all) => new Padding(S(all));
        public static Padding P(int l, int t, int r, int b) => new Padding(S(l), S(t), S(r), S(b));

        // ---------------------------------------------------------------
        // TIPOGRAFÍA
        // ---------------------------------------------------------------
        private static HashSet<string>? _familias;
        private static bool Existe(string familia)
        {
            if (_familias == null)
            {
                using var col = new InstalledFontCollection();
                _familias = new HashSet<string>(col.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            }
            return _familias.Contains(familia);
        }

        private static string? _familiaTitulo;
        /// <summary>Fuente condensada estilo "menú de pizzería" (Bahnschrift en Windows 10/11).</summary>
        public static string FamiliaTitulo
        {
            get
            {
                if (_familiaTitulo == null)
                {
                    string[] candidatas = { "Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Bahnschrift", "Segoe UI Black", "Segoe UI" };
                    _familiaTitulo = candidatas.FirstOrDefault(Existe) ?? "Segoe UI";
                }
                return _familiaTitulo;
            }
        }

        public const string FamiliaBase = "Segoe UI";

        private static FontStyle? _estiloTitulo;
        /// <summary>
        /// Algunas familias (p. ej. "Bahnschrift SemiBold Condensed") no tienen variante Bold y Font lanzaría
        /// una excepción; por eso se verifica qué estilo está disponible.
        /// </summary>
        private static FontStyle EstiloTitulo
        {
            get
            {
                if (_estiloTitulo == null)
                {
                    try
                    {
                        using var fam = new FontFamily(FamiliaTitulo);
                        _estiloTitulo = fam.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold
                                      : fam.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular
                                      : FontStyle.Bold;
                    }
                    catch
                    {
                        _familiaTitulo = "Segoe UI";
                        _estiloTitulo = FontStyle.Bold;
                    }
                }
                return _estiloTitulo.Value;
            }
        }

        public static Font Titulo(float pt) => new Font(FamiliaTitulo, pt, EstiloTitulo);
        public static Font Fuente(float pt, FontStyle estilo = FontStyle.Regular) => new Font(FamiliaBase, pt, estilo);

        public static readonly Font FuenteNormal = new Font(FamiliaBase, 11f);
        public static readonly Font FuenteNegrita = new Font(FamiliaBase, 11f, FontStyle.Bold);
        public static readonly Font FuenteBoton = new Font(FamiliaBase, 11.5f, FontStyle.Bold);
        public static readonly Font FuenteEtiqueta = new Font(FamiliaBase, 9.5f, FontStyle.Bold);
        public static readonly Font FuenteChica = new Font(FamiliaBase, 9.5f);
        public static readonly Font FuenteInput = new Font(FamiliaBase, 13f);

        // ---------------------------------------------------------------
        // RECURSOS (logo)
        // ---------------------------------------------------------------
        private static Image? _logo;
        private static bool _logoIntentado;
        /// <summary>Logo cargado desde la carpeta Recursos (se copia a la salida al compilar).</summary>
        public static Image? Logo
        {
            get
            {
                if (!_logoIntentado)
                {
                    _logoIntentado = true;
                    try
                    {
                        string ruta = Path.Combine(AppContext.BaseDirectory, "Recursos", "logo_rhinos.png");
                        if (File.Exists(ruta))
                        {
                            // Se copia a memoria para no bloquear el archivo
                            using var tmp = Image.FromFile(ruta);
                            _logo = new Bitmap(tmp);
                        }
                    }
                    catch
                    {
                        _logo = null;
                    }
                }
                return _logo;
            }
        }

        public static Icon? IconoApp
        {
            get
            {
                try
                {
                    string ruta = Path.Combine(AppContext.BaseDirectory, "Recursos", "rhinos.ico");
                    return File.Exists(ruta) ? new Icon(ruta) : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        // ---------------------------------------------------------------
        // UTILIDADES DE DIBUJO
        // ---------------------------------------------------------------
        public static GraphicsPath RutaRedondeada(Rectangle r, int radio)
        {
            var path = new GraphicsPath();
            int d = Math.Max(1, Math.Min(radio * 2, Math.Min(r.Width, r.Height)));
            if (radio <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Mezcla dos colores (t = 0 devuelve a, t = 1 devuelve b).</summary>
        public static Color Mezclar(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Oscurecer(Color c, float t) => Mezclar(c, Color.Black, t);
        public static Color Aclarar(Color c, float t) => Mezclar(c, Color.White, t);

        /// <summary>Dibuja texto multilínea centrado vertical y horizontalmente dentro de un rectángulo.</summary>
        public static void TextoCentrado(Graphics g, string texto, Font fuente, Rectangle area, Color color,
            bool alinearIzquierda = false)
        {
            if (string.IsNullOrEmpty(texto) || area.Width <= 0 || area.Height <= 0) return;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;
            flags |= alinearIzquierda ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
            Size medida = TextRenderer.MeasureText(g, texto, fuente, new Size(area.Width, int.MaxValue), flags);
            int alto = Math.Min(medida.Height, area.Height);
            var r = new Rectangle(area.X, area.Y + (area.Height - alto) / 2, area.Width, alto);
            TextRenderer.DrawText(g, texto, fuente, r, color, flags | TextFormatFlags.EndEllipsis);
        }

        /// <summary>Color de fondo, texto y etiqueta legible para cada estado de pedido.</summary>
        public static (Color fondo, Color texto, string etiqueta) ColorEstado(string? estado)
        {
            switch ((estado ?? "").ToUpperInvariant())
            {
                case "EN_PREPARACION": return (AmbarSuave, Color.FromArgb(146, 84, 0), "🔥 En preparación");
                case "EN_RUTA": return (AzulSuave, Azul, "🛵 En ruta");
                case "ENTREGADO": return (VerdeSuave, Color.FromArgb(22, 101, 52), "✔ Entregado");
                case "CANCELADO": return (RojoSuave, RojoOscuro, "✖ Cancelado");
                default: return (Deshabilitado, TextoSuave, estado ?? "");
            }
        }

        // ---------------------------------------------------------------
        // ESTILOS DE CONTROLES ESTÁNDAR
        // ---------------------------------------------------------------
        public static void EstilizarTextBox(TextBox tb, bool grande = true)
        {
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = SuperficieAlt;
            tb.ForeColor = Texto;
            tb.Font = grande ? FuenteInput : FuenteNormal;
        }

        public static void EstilizarCombo(ComboBox cb)
        {
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.FlatStyle = FlatStyle.Flat;
            cb.BackColor = SuperficieAlt;
            cb.ForeColor = Texto;
            cb.Font = FuenteInput;
            cb.IntegralHeight = false;
            cb.MaxDropDownItems = 10;
        }

        public static void EstilizarFecha(DateTimePicker dtp)
        {
            dtp.Format = DateTimePickerFormat.Custom;
            dtp.CustomFormat = "dd/MM/yyyy";
            dtp.Font = FuenteInput;
            dtp.CalendarFont = FuenteNormal;
        }

        /// <summary>Tabla clara con filas altas (aptas para pantalla táctil), igual que las tablas de Veterinaria.</summary>
        public static void EstilizarGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = Superficie;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = Color.FromArgb(245, 234, 222);
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToOrderColumns = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.RowTemplate.Height = S(44);
            dgv.ScrollBars = ScrollBars.Both;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 241, 224);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = RojoOscuro;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font(FamiliaBase, 10.5f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 241, 224);
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = RojoOscuro;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(S(8), 0, S(8), 0);
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = S(46);

            dgv.DefaultCellStyle.BackColor = Superficie;
            dgv.DefaultCellStyle.ForeColor = Texto;
            dgv.DefaultCellStyle.Font = new Font(FamiliaBase, 10.5f);
            dgv.DefaultCellStyle.SelectionBackColor = RojoSuave;
            dgv.DefaultCellStyle.SelectionForeColor = Texto;
            dgv.DefaultCellStyle.Padding = new Padding(S(8), 0, S(8), 0);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = SuperficieAlt;
        }

        /// <summary>Aplica formato de moneda a columnas con estos nombres (si existen).</summary>
        public static void FormatoMoneda(DataGridView dgv, params string[] columnas)
        {
            foreach (var c in columnas)
            {
                if (dgv.Columns.Contains(c))
                {
                    dgv.Columns[c].DefaultCellStyle.Format = "C2";
                    dgv.Columns[c].DefaultCellStyle.FormatProvider = Cultura;
                    dgv.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
        }

        public static void OcultarColumnas(DataGridView dgv, params string[] columnas)
        {
            foreach (var c in columnas)
            {
                if (dgv.Columns.Contains(c)) dgv.Columns[c].Visible = false;
            }
        }

        /// <summary>Cultura es-MX para mostrar precios como $1,234.50</summary>
        public static readonly System.Globalization.CultureInfo Cultura = new System.Globalization.CultureInfo("es-MX");
        public static string Dinero(decimal valor) => valor.ToString("C2", Cultura);

        public static Label CrearEtiqueta(string texto, Font? fuente = null, Color? color = null)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                Font = fuente ?? FuenteEtiqueta,
                ForeColor = color ?? TextoSuave,
                Margin = new Padding(0, S(4), 0, S(4))
            };
        }
    }
}
