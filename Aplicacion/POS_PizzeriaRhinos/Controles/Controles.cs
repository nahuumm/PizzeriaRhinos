using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Controles
{
    public enum EstiloBoton { Primario, Secundario, Exito, Peligro, Ambar, Fantasma, Oscuro, Azul }
    public enum DisposicionBoton { Centro, Izquierda, Mosaico }

    internal static class Pintura
    {
        private static readonly Dictionary<float, Font> _iconos = new();

        /// <summary>Fuente para íconos (emoji monocromático de Windows).</summary>
        public static Font FuenteIcono(float pt)
        {
            if (!_iconos.TryGetValue(pt, out var f))
            {
                f = new Font("Segoe UI Emoji", pt);
                _iconos[pt] = f;
            }
            return f;
        }

        /// <summary>Busca el primer color de fondo opaco hacia arriba en la jerarquía.</summary>
        public static Color FondoEfectivo(Control? c)
        {
            while (c != null)
            {
                if (c.BackColor.A == 255) return c.BackColor;
                c = c.Parent;
            }
            return UITheme.Fondo;
        }

        /// <summary>Dibuja varias líneas de texto apiladas y centradas verticalmente.</summary>
        public static void Lineas(Graphics g, Rectangle area, IList<(string texto, Font fuente, Color color)> lineas, bool izquierda = false)
        {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl |
                        (izquierda ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            var alturas = new List<int>();
            int total = 0;
            foreach (var l in lineas)
            {
                if (string.IsNullOrEmpty(l.texto)) { alturas.Add(0); continue; }
                int h = TextRenderer.MeasureText(g, l.texto, l.fuente, new Size(Math.Max(1, area.Width), int.MaxValue), flags).Height;
                alturas.Add(h);
                total += h;
            }
            int y = area.Y + Math.Max(0, (area.Height - total) / 2);
            for (int i = 0; i < lineas.Count; i++)
            {
                if (alturas[i] == 0) continue;
                int h = Math.Min(alturas[i], Math.Max(0, area.Bottom - y));
                if (h <= 0) break;
                TextRenderer.DrawText(g, lineas[i].texto, lineas[i].fuente, new Rectangle(area.X, y, area.Width, h),
                    lineas[i].color, flags | TextFormatFlags.EndEllipsis);
                y += alturas[i];
            }
        }

        public static void Pastilla(Graphics g, string texto, Font fuente, Point origen, Color fondo, Color colorTexto, bool alinearDerecha = false)
        {
            Size t = TextRenderer.MeasureText(g, texto, fuente, Size.Empty, TextFormatFlags.NoPadding);
            int padX = UITheme.S(10), padY = UITheme.S(4);
            var r = new Rectangle(origen.X, origen.Y, t.Width + padX * 2, t.Height + padY * 2);
            if (alinearDerecha) r.X -= r.Width;
            using var path = UITheme.RutaRedondeada(r, r.Height / 2);
            using var b = new SolidBrush(fondo);
            g.FillPath(b, path);
            TextRenderer.DrawText(g, texto, fuente, r, colorTexto,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    // =====================================================================
    // PANEL REDONDEADO (equivalente a .vet-surface-card)
    // =====================================================================
    [DesignerCategory("Code")]
    public class PanelRedondeado : Panel
    {
        public int Radio { get; set; } = UITheme.S(16);
        public Color ColorBorde { get; set; } = UITheme.Borde;
        public int GrosorBorde { get; set; } = 1;

        public PanelRedondeado()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UITheme.Superficie;
            Padding = UITheme.P(16);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Pintura.FondoEfectivo(Parent));
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UITheme.RutaRedondeada(r, Radio);
            using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
            if (GrosorBorde > 0)
            {
                using var p = new Pen(ColorBorde, GrosorBorde);
                g.DrawPath(p, path);
            }
        }
    }

    // =====================================================================
    // BOTÓN GRANDE (equivalente a .vet-btn-primary, pensado para pantalla táctil)
    // =====================================================================
    [DesignerCategory("Code")]
    public class BotonRhino : Button
    {
        private bool _hover, _presionado, _seleccionado;
        private EstiloBoton _estilo;
        private string _icono;
        private string? _subtexto;
        private DisposicionBoton _disposicion = DisposicionBoton.Centro;

        public EstiloBoton Estilo { get => _estilo; set { _estilo = value; Invalidate(); } }
        public bool Seleccionado { get => _seleccionado; set { _seleccionado = value; Invalidate(); } }
        public string Icono { get => _icono; set { _icono = value ?? ""; Invalidate(); } }
        public string? Subtexto { get => _subtexto; set { _subtexto = value; Invalidate(); } }
        public DisposicionBoton Disposicion { get => _disposicion; set { _disposicion = value; Invalidate(); } }
        public int Radio { get; set; } = UITheme.S(12);
        public float TamanoIcono { get; set; } = 15f;
        /// <summary>Dato asociado al botón (id, objeto, etc.).</summary>
        public object? Valor { get; set; }

        public BotonRhino() : this("", EstiloBoton.Primario, "") { }

        public BotonRhino(string texto, EstiloBoton estilo = EstiloBoton.Primario, string icono = "")
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Text = texto;
            _estilo = estilo;
            _icono = icono ?? "";
            Font = UITheme.FuenteBoton;
            Cursor = Cursors.Hand;
            Size = UITheme.S(170, 54);
            Margin = UITheme.P(4);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _presionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _presionado = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _presionado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        private (Color fondo, Color texto, Color borde) Colores(Color fondoPadre)
        {
            if (!Enabled) return (UITheme.Deshabilitado, UITheme.TextoDeshabilitado, UITheme.Deshabilitado);
            switch (_estilo)
            {
                case EstiloBoton.Primario: return (UITheme.Rojo, Color.White, UITheme.Rojo);
                case EstiloBoton.Exito: return (UITheme.Verde, Color.White, UITheme.Verde);
                case EstiloBoton.Peligro: return (UITheme.RojoOscuro, Color.White, UITheme.RojoOscuro);
                case EstiloBoton.Ambar: return (UITheme.Ambar, UITheme.Texto, UITheme.Ambar);
                case EstiloBoton.Oscuro: return (UITheme.Texto, Color.White, UITheme.Texto);
                case EstiloBoton.Azul: return (UITheme.Azul, Color.White, UITheme.Azul);
                case EstiloBoton.Secundario:
                    return _seleccionado
                        ? (UITheme.Rojo, Color.White, UITheme.Rojo)
                        : (UITheme.Superficie, UITheme.Texto, UITheme.Borde);
                case EstiloBoton.Fantasma:
                default:
                    return _seleccionado
                        ? (UITheme.Rojo, Color.White, UITheme.Rojo)
                        : (fondoPadre, UITheme.Texto, fondoPadre);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fondoPadre = Pintura.FondoEfectivo(Parent);
            g.Clear(fondoPadre);

            var (fondo, texto, borde) = Colores(fondoPadre);
            if (Enabled && _presionado) fondo = UITheme.Oscurecer(fondo, 0.15f);
            else if (Enabled && _hover)
            {
                fondo = (_estilo == EstiloBoton.Secundario || _estilo == EstiloBoton.Fantasma) && !_seleccionado
                    ? UITheme.Mezclar(fondo, UITheme.RojoSuave, 0.7f)
                    : UITheme.Oscurecer(fondo, 0.08f);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UITheme.RutaRedondeada(rect, Radio))
            {
                using (var b = new SolidBrush(fondo)) g.FillPath(b, path);
                if (borde != fondo)
                {
                    using var p = new Pen(borde, Math.Max(1, UITheme.S(1)));
                    g.DrawPath(p, path);
                }
            }

            if (Focused && ShowFocusCues)
            {
                var rf = Rectangle.Inflate(rect, -UITheme.S(4), -UITheme.S(4));
                using var pf = new Pen(Color.FromArgb(140, texto)) { DashStyle = DashStyle.Dot };
                using var pathF = UITheme.RutaRedondeada(rf, Math.Max(2, Radio - UITheme.S(4)));
                g.DrawPath(pf, pathF);
            }

            int pad = UITheme.S(10);
            var area = Rectangle.Inflate(rect, -pad, -UITheme.S(6));

            switch (_disposicion)
            {
                case DisposicionBoton.Izquierda:
                    {
                        int colIcono = UITheme.S(40);
                        bool soloIcono = Width < UITheme.S(110) && _icono.Length > 0;
                        if (soloIcono)
                        {
                            Pintura.Lineas(g, area, new[] { (_icono, Pintura.FuenteIcono(TamanoIcono + 2), texto) });
                        }
                        else
                        {
                            if (_icono.Length > 0)
                            {
                                var ri = new Rectangle(area.X, area.Y, colIcono, area.Height);
                                Pintura.Lineas(g, ri, new[] { (_icono, Pintura.FuenteIcono(TamanoIcono), texto) });
                            }
                            var rt = new Rectangle(area.X + (_icono.Length > 0 ? colIcono + UITheme.S(6) : 0), area.Y,
                                area.Width - (_icono.Length > 0 ? colIcono + UITheme.S(6) : 0), area.Height);
                            var lineas = new List<(string, Font, Color)> { (Text, Font, texto) };
                            if (!string.IsNullOrEmpty(_subtexto)) lineas.Add((_subtexto!, UITheme.FuenteChica, Color.FromArgb(200, texto)));
                            Pintura.Lineas(g, rt, lineas, izquierda: true);
                        }
                        break;
                    }
                case DisposicionBoton.Mosaico:
                    {
                        var lineas = new List<(string, Font, Color)>();
                        if (_icono.Length > 0) lineas.Add((_icono, Pintura.FuenteIcono(TamanoIcono), texto));
                        lineas.Add((Text, Font, texto));
                        if (!string.IsNullOrEmpty(_subtexto)) lineas.Add((_subtexto!, UITheme.FuenteChica, Color.FromArgb(210, texto)));
                        Pintura.Lineas(g, area, lineas);
                        break;
                    }
                default:
                    {
                        string principal = _icono.Length > 0 ? $"{_icono}  {Text}" : Text;
                        var lineas = new List<(string, Font, Color)> { (principal, Font, texto) };
                        if (!string.IsNullOrEmpty(_subtexto)) lineas.Add((_subtexto!, UITheme.FuenteChica, Color.FromArgb(220, texto)));
                        Pintura.Lineas(g, area, lineas);
                        break;
                    }
            }
        }
    }

    // =====================================================================
    // TARJETA DE PRODUCTO (estilo de los menús: título, descripción y pastilla de precio)
    // =====================================================================
    [DesignerCategory("Code")]
    public class TarjetaProducto : Control
    {
        private bool _hover, _presionado;
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Precio { get; set; } = "";
        public string? Insignia { get; set; }
        public Color ColorInsignia { get; set; } = UITheme.Verde;
        public string Icono { get; set; } = "";
        public object? Valor { get; set; }

        public TarjetaProducto()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Size = UITheme.S(214, 132);
            Margin = UITheme.P(6);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _presionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _presionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _presionado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Pintura.FondoEfectivo(Parent));

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fondo = !Enabled ? UITheme.Deshabilitado : (_presionado ? UITheme.RojoSuave : UITheme.Superficie);
            Color borde = !Enabled ? UITheme.Deshabilitado : (_hover || Focused ? UITheme.Rojo : UITheme.Borde);
            int radio = UITheme.S(16);

            using (var path = UITheme.RutaRedondeada(rect, radio))
            {
                using (var b = new SolidBrush(fondo)) g.FillPath(b, path);
                // Franja superior roja (detalle del menú de referencia)
                if (Enabled)
                {
                    var clip = g.Clip;
                    g.SetClip(path);
                    using (var bf = new SolidBrush(UITheme.Rojo)) g.FillRectangle(bf, 0, 0, Width, UITheme.S(6));
                    g.Clip = clip;
                }
                using var p = new Pen(borde, _hover ? UITheme.S(2) : 1);
                g.DrawPath(p, path);
            }

            int pad = UITheme.S(12);
            Color colorTitulo = Enabled ? UITheme.Texto : UITheme.TextoDeshabilitado;
            Color colorDesc = Enabled ? UITheme.TextoSuave : UITheme.TextoDeshabilitado;

            using var fTitulo = UITheme.Titulo(13f);
            int yTop = UITheme.S(14);
            int anchoTexto = Width - pad * 2;

            int altoPrecio = UITheme.S(30);
            int yPrecio = Height - pad - altoPrecio + UITheme.S(4);

            string titulo = string.IsNullOrEmpty(Icono) ? Titulo : $"{Icono} {Titulo}";
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.TextBoxControl;
            int altoTitulo = Math.Min(TextRenderer.MeasureText(g, titulo, fTitulo, new Size(anchoTexto, int.MaxValue), flags).Height,
                                      (int)(fTitulo.GetHeight(g) * 2) + 2);
            TextRenderer.DrawText(g, titulo, fTitulo, new Rectangle(pad, yTop, anchoTexto, altoTitulo), colorTitulo, flags);

            int yDesc = yTop + altoTitulo + UITheme.S(2);
            int altoDesc = Math.Max(0, yPrecio - yDesc - UITheme.S(4));
            if (altoDesc > 0 && !string.IsNullOrEmpty(Descripcion))
                TextRenderer.DrawText(g, Descripcion, UITheme.FuenteChica, new Rectangle(pad, yDesc, anchoTexto, altoDesc), colorDesc, flags);

            if (!string.IsNullOrEmpty(Precio))
            {
                using var fPrecio = new Font(UITheme.FamiliaBase, 11f, FontStyle.Bold);
                Pintura.Pastilla(g, Precio, fPrecio, new Point(pad, yPrecio),
                    Enabled ? UITheme.Ambar : UITheme.Deshabilitado, Enabled ? UITheme.Texto : UITheme.TextoDeshabilitado);
            }

            if (!string.IsNullOrEmpty(Insignia))
            {
                Color c = Enabled ? ColorInsignia : UITheme.RojoOscuro;
                Pintura.Pastilla(g, Insignia!, UITheme.FuenteEtiqueta, new Point(Width - pad, yPrecio + UITheme.S(3)), c, Color.White, alinearDerecha: true);
            }
        }
    }

    // =====================================================================
    // ENCABEZADO DE MÓDULO (equivalente a .vet-page-header, en rojo pizzería)
    // =====================================================================
    [DesignerCategory("Code")]
    public class EncabezadoModulo : Control
    {
        private string _titulo = "";
        private string _subtitulo = "";
        private string _icono = "";
        public string Titulo { get => _titulo; set { _titulo = value; Invalidate(); } }
        public string Subtitulo { get => _subtitulo; set { _subtitulo = value; Invalidate(); } }
        public string Icono { get => _icono; set { _icono = value; Invalidate(); } }

        public EncabezadoModulo(string titulo, string subtitulo, string icono)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _titulo = titulo;
            _subtitulo = subtitulo;
            _icono = icono;
            Dock = DockStyle.Top;
            Height = UITheme.S(96);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Pintura.FondoEfectivo(Parent));
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UITheme.RutaRedondeada(rect, UITheme.S(20));
            using (var brush = new LinearGradientBrush(rect, UITheme.RojoProfundo, UITheme.Rojo, LinearGradientMode.Horizontal))
                g.FillPath(brush, path);

            // Patrón de cuadros (como el menú de pizzería de referencia) en la esquina derecha
            var clip = g.Clip;
            g.SetClip(path);
            int lado = UITheme.S(22);
            using (var bCuadro = new SolidBrush(Color.FromArgb(38, 255, 255, 255)))
            {
                int columnas = 9;
                int x0 = Width - columnas * lado;
                for (int fila = 0; fila * lado < Height; fila++)
                    for (int col = 0; col < columnas; col++)
                        if ((fila + col) % 2 == 0)
                        {
                            int alpha = Math.Max(0, 255 - (columnas - col) * 28);
                            if (alpha <= 0) continue;
                            using var bb = new SolidBrush(Color.FromArgb(Math.Min(48, alpha / 5), 255, 255, 255));
                            g.FillRectangle(bb, x0 + col * lado, fila * lado, lado, lado);
                        }
            }
            using (var bCirculo = new SolidBrush(Color.FromArgb(40, UITheme.Ambar)))
                g.FillEllipse(bCirculo, Width - UITheme.S(260), -UITheme.S(90), UITheme.S(240), UITheme.S(240));
            g.Clip = clip;

            int pad = UITheme.S(22);
            int lado2 = UITheme.S(54);
            var rIcono = new Rectangle(pad, (Height - lado2) / 2, lado2, lado2);
            using (var pIcono = UITheme.RutaRedondeada(rIcono, UITheme.S(14)))
            using (var bIcono = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            using (var penIcono = new Pen(Color.FromArgb(60, 255, 255, 255)))
            {
                g.FillPath(bIcono, pIcono);
                g.DrawPath(penIcono, pIcono);
            }
            Pintura.Lineas(g, rIcono, new[] { (_icono, Pintura.FuenteIcono(20f), Color.White) });

            int xTexto = rIcono.Right + UITheme.S(16);
            var rTexto = new Rectangle(xTexto, UITheme.S(8), Width - xTexto - pad, Height - UITheme.S(16));
            using var fTitulo = UITheme.Titulo(22f);
            Pintura.Lineas(g, rTexto, new List<(string, Font, Color)>
            {
                (_titulo, fTitulo, Color.White),
                (_subtitulo, UITheme.Fuente(10.5f), Color.FromArgb(225, 255, 240, 230))
            }, izquierda: true);
        }
    }

    // =====================================================================
    // TARJETA DE INDICADOR (KPI)
    // =====================================================================
    [DesignerCategory("Code")]
    public class TarjetaKpi : Control
    {
        private string _valor = "—";
        public string Titulo { get; set; }
        public string Valor { get => _valor; set { _valor = value; Invalidate(); } }
        public string Icono { get; set; }
        public Color Acento { get; set; }

        public TarjetaKpi(string titulo, string icono, Color acento)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Titulo = titulo;
            Icono = icono;
            Acento = acento;
            Height = UITheme.S(100);
            Margin = UITheme.P(6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Pintura.FondoEfectivo(Parent));
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UITheme.RutaRedondeada(rect, UITheme.S(16)))
            {
                using (var b = new SolidBrush(UITheme.Superficie)) g.FillPath(b, path);
                using var p = new Pen(UITheme.Borde);
                g.DrawPath(p, path);
            }
            int pad = UITheme.S(16);
            int d = UITheme.S(48);
            var rc = new Rectangle(Width - pad - d, (Height - d) / 2, d, d);
            using (var bc = new SolidBrush(UITheme.Mezclar(Acento, Color.White, 0.82f))) g.FillEllipse(bc, rc);
            Pintura.Lineas(g, rc, new[] { (Icono, Pintura.FuenteIcono(16f), Acento) });

            var rt = new Rectangle(pad, UITheme.S(10), Width - pad * 3 - d, Height - UITheme.S(20));
            using var fValor = UITheme.Titulo(22f);
            Pintura.Lineas(g, rt, new List<(string, Font, Color)>
            {
                (Titulo.ToUpperInvariant(), UITheme.FuenteEtiqueta, UITheme.TextoSuave),
                (_valor, fValor, UITheme.Texto)
            }, izquierda: true);
        }
    }

    // =====================================================================
    // TABLA CON ESTADO VACÍO
    // =====================================================================
    [DesignerCategory("Code")]
    public class PanelTabla : PanelRedondeado
    {
        public DataGridView Grid { get; }
        private readonly Label _lblVacio;

        public PanelTabla()
        {
            Padding = UITheme.P(10);
            Grid = new DataGridView { Dock = DockStyle.Fill };
            UITheme.EstilizarGrid(Grid);
            _lblVacio = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UITheme.Fuente(12f),
                ForeColor = UITheme.TextoSuave,
                Visible = false
            };
            Controls.Add(Grid);
            Controls.Add(_lblVacio);
        }

        /// <summary>Asigna los datos y muestra un mensaje amable cuando no hay registros.</summary>
        public void Mostrar(object? datos, string mensajeVacio)
        {
            _lblVacio.Visible = false;
            Grid.Visible = true;
            Grid.DataSource = datos;
            bool vacio = Grid.Rows.Count == 0;
            _lblVacio.Text = "🍕\n\n" + mensajeVacio;
            _lblVacio.Visible = vacio;
            Grid.Visible = !vacio;
        }

        public DataGridViewRow? FilaSeleccionada => Grid.Visible ? Grid.CurrentRow : null;
    }

    // =====================================================================
    // GRUPO DE OPCIONES (botones tipo "segmented control")
    // =====================================================================
    [DesignerCategory("Code")]
    public class GrupoOpciones : FlowLayoutPanel
    {
        private readonly List<BotonRhino> _botones = new();
        public event EventHandler? ValorCambiado;
        public Size TamanoBoton { get; set; } = UITheme.S(150, 54);
        public object? Valor { get; private set; }
        public IReadOnlyList<BotonRhino> Botones => _botones;

        public GrupoOpciones()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = true;
            Margin = new Padding(0);
            Padding = new Padding(0);
        }

        public BotonRhino Agregar(string texto, object valor, string icono = "", string? subtexto = null)
        {
            var b = new BotonRhino(texto, EstiloBoton.Secundario, icono)
            {
                Size = TamanoBoton,
                Valor = valor,
                Subtexto = subtexto
            };
            b.Click += (_, _) => Seleccionar(valor, true);
            _botones.Add(b);
            Controls.Add(b);
            return b;
        }

        public void Limpiar()
        {
            var lista = _botones.ToList();
            _botones.Clear();
            Controls.Clear();
            foreach (var b in lista) b.Dispose();
            Valor = null;
        }

        public void Seleccionar(object? valor, bool notificar = false)
        {
            Valor = valor;
            foreach (var b in _botones) b.Seleccionado = Equals(b.Valor, valor);
            if (notificar) ValorCambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Utilidades de layout.</summary>
    public static class Diseno
    {
        /// <summary>
        /// Agrega un control Fill y varios controles Dock=Top en el orden visual indicado (de arriba hacia abajo).
        /// WinForms acomoda los controles acoplados en orden inverso al que se agregan, por eso se invierte aquí.
        /// </summary>
        public static void Apilar(Control padre, Control? relleno, params Control[] deArribaHaciaAbajo)
        {
            if (relleno != null)
            {
                relleno.Dock = DockStyle.Fill;
                padre.Controls.Add(relleno);
            }
            for (int i = deArribaHaciaAbajo.Length - 1; i >= 0; i--)
            {
                if (deArribaHaciaAbajo[i].Dock == DockStyle.None) deArribaHaciaAbajo[i].Dock = DockStyle.Top;
                padre.Controls.Add(deArribaHaciaAbajo[i]);
            }
        }

        /// <summary>Quita y destruye todos los controles hijos de forma segura (sin modificar la colección mientras se recorre).</summary>
        public static void LimpiarControles(Control padre)
        {
            var hijos = padre.Controls.Cast<Control>().ToList();
            padre.Controls.Clear();
            foreach (var c in hijos) c.Dispose();
        }

        /// <summary>Separador vertical transparente.</summary>
        public static Panel Espacio(int alto) => new Panel { Height = UITheme.S(alto), Dock = DockStyle.Top };

        /// <summary>Barra horizontal de botones que fluye a la siguiente línea si no cabe (responsiva).</summary>
        public static FlowLayoutPanel BarraBotones(params Control[] controles)
        {
            var f = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            f.Controls.AddRange(controles);
            return f;
        }
    }
}
