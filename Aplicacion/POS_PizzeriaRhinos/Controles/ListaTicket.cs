using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Controles
{
    /// <summary>
    /// Lista del ticket con desplazamiento vertical por píxel (a diferencia del DataGridView, que salta por filas
    /// completas y no deja ver una partida más alta que el área visible). Cada partida calcula su alto según
    /// las líneas de ingredientes. Se puede desplazar con la rueda del ratón, la barra o arrastrando con el dedo.
    /// </summary>
    [DesignerCategory("Code")]
    public class ListaTicket : Panel
    {
        public static int AnchoCantidad => UITheme.S(58);
        public static int AnchoImporte => UITheme.S(104);

        private readonly List<FilaTicket> _filas = new();
        private int _seleccionado = -1;
        private bool _acomodando;

        public event EventHandler? SeleccionCambiada;
        public int IndiceSeleccionado => _seleccionado;
        public int Cantidad => _filas.Count;

        public ListaTicket()
        {
            AutoScroll = true;
            BackColor = UITheme.Superficie;
            DoubleBuffered = true;
            // Sin barra horizontal: las filas siempre miden lo mismo que el área visible
            HorizontalScroll.Maximum = 0;
            HorizontalScroll.Visible = false;
            HorizontalScroll.Enabled = false;
        }

        public void Cargar(IList<(string cantidad, string descripcion, string importe)> partidas, int? seleccionar)
        {
            SuspendLayout();
            var viejas = _filas.ToList();
            _filas.Clear();
            Controls.Clear();
            foreach (var f in viejas) f.Dispose();

            for (int i = 0; i < partidas.Count; i++)
            {
                var fila = new FilaTicket(this, i, partidas[i].cantidad, partidas[i].descripcion, partidas[i].importe);
                _filas.Add(fila);
                Controls.Add(fila);
            }
            AutoScrollPosition = new Point(0, 0);
            Acomodar();
            ResumeLayout(true);

            _seleccionado = -1;
            if (_filas.Count > 0)
                Seleccionar(Math.Clamp(seleccionar ?? _filas.Count - 1, 0, _filas.Count - 1));
            else
                SeleccionCambiada?.Invoke(this, EventArgs.Empty);
        }

        public void Seleccionar(int indice)
        {
            if (indice < 0 || indice >= _filas.Count) return;
            _seleccionado = indice;
            for (int i = 0; i < _filas.Count; i++) _filas[i].Seleccionada = i == indice;
            ScrollControlIntoView(_filas[indice]);
            SeleccionCambiada?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Coloca las filas una debajo de otra con el ancho visible y su alto según el texto.</summary>
        private void Acomodar()
        {
            if (_acomodando) return;
            _acomodando = true;
            try
            {
                // Se repite si al acomodar aparece/desaparece la barra vertical (cambia el ancho útil),
                // así las filas nunca quedan más anchas que el área visible y no sale barra horizontal.
                for (int intento = 0; intento < 3; intento++)
                {
                    int ancho = ClientSize.Width;
                    int y = AutoScrollPosition.Y;
                    foreach (var f in _filas)
                    {
                        int alto = f.CalcularAlto(ancho);
                        f.SetBounds(0, y, ancho, alto);
                        y += alto;
                    }
                    AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y);
                    if (ClientSize.Width == ancho) break;
                }
            }
            finally
            {
                _acomodando = false;
            }
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            Acomodar(); // al aparecer la barra vertical cambia el ancho útil
        }

        /// <summary>Desplaza la lista (rueda del ratón o arrastre con el dedo desde una fila).</summary>
        internal void Desplazar(int pixeles)
        {
            int actual = -AutoScrollPosition.Y;
            int maximo = Math.Max(0, DisplayRectangle.Height - ClientSize.Height);
            AutoScrollPosition = new Point(0, Math.Clamp(actual + pixeles, 0, maximo));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Desplazar(-e.Delta / 120 * UITheme.S(48));
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        /// <summary>Estado vacío: mensaje cuando todavía no hay productos en el ticket.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_filas.Count > 0) return;
            using var f = new Font(UITheme.FamiliaBase, 11.5f);
            TextRenderer.DrawText(e.Graphics, "🍕\n\nEl ticket está vacío.\nAgrega pizzas, complementos o bebidas.", f, ClientRectangle, UITheme.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_filas.Count == 0) Invalidate();
        }
    }

    /// <summary>Una partida del ticket: cantidad, descripción multilínea e importe.</summary>
    [DesignerCategory("Code")]
    public class FilaTicket : Control
    {
        private readonly ListaTicket _lista;
        private readonly int _indice;
        private readonly string _cantidad, _descripcion, _importe;
        private bool _seleccionada;
        private Point _inicioArrastre;
        private bool _arrastrando;

        private static readonly Font FuenteCant = new Font(UITheme.FamiliaBase, 12f, FontStyle.Bold);
        private static readonly Font FuenteNombre = new Font(UITheme.FamiliaBase, 10.5f, FontStyle.Bold);
        private static readonly Font FuenteDetalle = new Font(UITheme.FamiliaBase, 9.5f);
        private static readonly Font FuenteImporte = new Font(UITheme.FamiliaBase, 11f, FontStyle.Bold);
        private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        public FilaTicket(ListaTicket lista, int indice, string cantidad, string descripcion, string importe)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _lista = lista;
            _indice = indice;
            _cantidad = cantidad;
            _descripcion = descripcion ?? "";
            _importe = importe;
            Cursor = Cursors.Hand;
        }

        public bool Seleccionada
        {
            get => _seleccionada;
            set { _seleccionada = value; Invalidate(); }
        }

        private (string nombre, string detalle) Partes()
        {
            int i = _descripcion.IndexOf('\n');
            return i < 0 ? (_descripcion, "") : (_descripcion[..i], _descripcion[(i + 1)..].Replace("\n   ", "\n").TrimStart());
        }

        private int AnchoTexto(int anchoFila) => Math.Max(UITheme.S(60), anchoFila - ListaTicket.AnchoCantidad - ListaTicket.AnchoImporte - UITheme.S(8));

        /// <summary>Alto necesario para mostrar todas las líneas sin recortar.</summary>
        public int CalcularAlto(int anchoFila)
        {
            var (nombre, detalle) = Partes();
            int w = AnchoTexto(anchoFila);
            int alto = TextRenderer.MeasureText(nombre, FuenteNombre, new Size(w, int.MaxValue), Flags).Height;
            if (detalle.Length > 0)
                alto += UITheme.S(2) + TextRenderer.MeasureText(detalle, FuenteDetalle, new Size(w, int.MaxValue), Flags).Height;
            return Math.Max(UITheme.S(50), alto + UITheme.S(20));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color fondo = _seleccionada ? UITheme.RojoSuave : (_indice % 2 == 0 ? UITheme.Superficie : UITheme.SuperficieAlt);
            g.Clear(fondo);
            if (_seleccionada)
                using (var b = new SolidBrush(UITheme.Rojo)) g.FillRectangle(b, 0, 0, UITheme.S(4), Height);
            using (var p = new Pen(UITheme.Borde)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);

            int top = UITheme.S(10);
            TextRenderer.DrawText(g, _cantidad, FuenteCant, new Rectangle(0, top, ListaTicket.AnchoCantidad, UITheme.S(26)), UITheme.Texto,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

            var (nombre, detalle) = Partes();
            int x = ListaTicket.AnchoCantidad;
            int w = AnchoTexto(Width);
            int hNombre = TextRenderer.MeasureText(g, nombre, FuenteNombre, new Size(w, int.MaxValue), Flags).Height;
            TextRenderer.DrawText(g, nombre, FuenteNombre, new Rectangle(x, top, w, hNombre), UITheme.Texto, Flags);
            if (detalle.Length > 0)
            {
                int yDet = top + hNombre + UITheme.S(2);
                TextRenderer.DrawText(g, detalle, FuenteDetalle, new Rectangle(x, yDet, w, Math.Max(0, Height - yDet)), UITheme.TextoSuave, Flags);
            }

            TextRenderer.DrawText(g, _importe, FuenteImporte,
                new Rectangle(Width - ListaTicket.AnchoImporte, top, ListaTicket.AnchoImporte - UITheme.S(10), UITheme.S(26)), UITheme.Texto,
                TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }

        // ---- Selección y desplazamiento táctil (arrastrar para mover la lista) ----
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _inicioArrastre = Cursor.Position;
            _arrastrando = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button != MouseButtons.Left) return;
            int dy = Cursor.Position.Y - _inicioArrastre.Y;
            if (!_arrastrando && Math.Abs(dy) < UITheme.S(8)) return;
            _arrastrando = true;
            _lista.Desplazar(-dy);
            _inicioArrastre = Cursor.Position;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_arrastrando && e.Button == MouseButtons.Left) _lista.Seleccionar(_indice);
            _arrastrando = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // La rueda sobre una fila mueve la lista completa
            _lista.Desplazar(-e.Delta / 120 * UITheme.S(48));
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }
    }

    /// <summary>Encabezado fijo de columnas del ticket (Cant. | Producto | Importe).</summary>
    [DesignerCategory("Code")]
    public class EncabezadoTicket : Control
    {
        public EncabezadoTicket()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = UITheme.S(40);
            Dock = DockStyle.Top;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var fondo = Color.FromArgb(255, 241, 224);
            g.Clear(fondo);
            using var f = new Font(UITheme.FamiliaBase, 10.5f, FontStyle.Bold);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, "Cant.", f, new Rectangle(0, 0, ListaTicket.AnchoCantidad, Height), UITheme.RojoOscuro, flags | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "Producto", f, new Rectangle(ListaTicket.AnchoCantidad, 0, Width - ListaTicket.AnchoCantidad - ListaTicket.AnchoImporte, Height), UITheme.RojoOscuro, flags);
            TextRenderer.DrawText(g, "Importe", f, new Rectangle(Width - ListaTicket.AnchoImporte, 0, ListaTicket.AnchoImporte - UITheme.S(10), Height), UITheme.RojoOscuro, flags | TextFormatFlags.Right);
        }
    }
}
