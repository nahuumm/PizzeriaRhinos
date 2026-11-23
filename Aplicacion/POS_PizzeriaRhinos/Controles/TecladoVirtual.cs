using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Controles
{
    /// <summary>
    /// Teclado virtual QWERTY en pantalla (Regla de negocio 1 del análisis: la captura de clientes nuevos
    /// se realiza mediante teclado virtual QWERTY). Escribe en el último TextBox vinculado que tuvo el foco.
    /// </summary>
    [DesignerCategory("Code")]
    public class TecladoVirtual : PanelRedondeado
    {
        private static readonly string[][] Filas =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
            new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
            new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", "Ñ" },
            new[] { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "-" }
        };

        private readonly List<List<BotonRhino>> _teclas = new();
        private readonly BotonRhino _btnMayus, _btnEspacio, _btnBorrar, _btnLimpiar, _btnNumero, _btnSiguiente;
        private bool _mayusculas = true;
        private TextBox? _objetivo;
        private readonly List<TextBox> _vinculados = new();

        public TecladoVirtual()
        {
            BackColor = UITheme.SuperficieAlt;
            Padding = UITheme.P(8);
            Height = UITheme.S(300);

            foreach (var fila in Filas)
            {
                var lista = new List<BotonRhino>();
                foreach (var t in fila)
                {
                    var b = CrearTecla(t, EstiloBoton.Secundario);
                    string valor = t;
                    b.Click += (_, _) => Escribir(_mayusculas ? valor : valor.ToLowerInvariant());
                    lista.Add(b);
                }
                _teclas.Add(lista);
            }

            _btnMayus = CrearTecla("⇧ Mayús", EstiloBoton.Secundario);
            _btnMayus.Seleccionado = true;
            _btnMayus.Click += (_, _) => CambiarMayusculas(!_mayusculas);

            _btnNumero = CrearTecla("#", EstiloBoton.Secundario);
            _btnNumero.Click += (_, _) => Escribir("#");

            _btnEspacio = CrearTecla("Espacio", EstiloBoton.Secundario);
            _btnEspacio.Click += (_, _) => Escribir(" ");

            _btnBorrar = CrearTecla("⌫ Borrar", EstiloBoton.Ambar);
            _btnBorrar.Click += (_, _) => Retroceso();

            _btnLimpiar = CrearTecla("Limpiar", EstiloBoton.Secundario);
            _btnLimpiar.Click += (_, _) => { if (_objetivo != null) { _objetivo.Clear(); _objetivo.Focus(); } };

            _btnSiguiente = CrearTecla("Siguiente ▶", EstiloBoton.Oscuro);
            _btnSiguiente.Click += (_, _) => SiguienteCampo();

            Resize += (_, _) => Acomodar();
        }

        private BotonRhino CrearTecla(string texto, EstiloBoton estilo)
        {
            var b = new BotonRhino(texto, estilo)
            {
                TabStop = false,
                Radio = UITheme.S(10),
                Font = new Font(UITheme.FamiliaBase, 13f, FontStyle.Bold),
                Margin = new Padding(0)
            };
            Controls.Add(b);
            return b;
        }

        /// <summary>Vincula los cuadros de texto en los que escribirá el teclado (en orden de captura).</summary>
        public void Vincular(params TextBox[] cuadros)
        {
            foreach (var tb in cuadros)
            {
                _vinculados.Add(tb);
                tb.Enter += (_, _) => _objetivo = tb;
            }
            if (_objetivo == null && cuadros.Length > 0) _objetivo = cuadros[0];
        }

        /// <summary>Olvida los cuadros vinculados (cuando el formulario reconstruye sus campos).</summary>
        public void LimpiarVinculos()
        {
            _vinculados.Clear();
            _objetivo = null;
        }

        private void CambiarMayusculas(bool valor)
        {
            _mayusculas = valor;
            _btnMayus.Seleccionado = valor;
            for (int f = 1; f < _teclas.Count; f++)
                for (int i = 0; i < _teclas[f].Count; i++)
                {
                    string t = Filas[f][i];
                    _teclas[f][i].Text = valor ? t : t.ToLowerInvariant();
                }
        }

        private void Escribir(string texto)
        {
            var tb = _objetivo;
            if (tb == null || !tb.Enabled || tb.ReadOnly) return;
            if (tb.MaxLength > 0 && tb.TextLength - tb.SelectionLength + texto.Length > tb.MaxLength) return;
            tb.SelectedText = texto;
            tb.Focus();
            // Mayúscula tipo celular: después de una letra regresa a minúsculas;
            // en campos marcados como "nombre" vuelve a mayúscula después de un espacio.
            if (_mayusculas && char.IsLetter(texto[0])) CambiarMayusculas(false);
            else if (texto == " " && !_mayusculas && tb.Tag as string == "nombre") CambiarMayusculas(true);
        }

        private void Retroceso()
        {
            var tb = _objetivo;
            if (tb == null || !tb.Enabled || tb.ReadOnly) return;
            if (tb.SelectionLength > 0)
            {
                tb.SelectedText = "";
            }
            else if (tb.SelectionStart > 0)
            {
                int pos = tb.SelectionStart;
                tb.Text = tb.Text.Remove(pos - 1, 1);
                tb.SelectionStart = pos - 1;
            }
            tb.Focus();
        }

        private void SiguienteCampo()
        {
            if (_vinculados.Count == 0) return;
            int i = _objetivo == null ? -1 : _vinculados.IndexOf(_objetivo);
            for (int k = 1; k <= _vinculados.Count; k++)
            {
                var tb = _vinculados[(i + k) % _vinculados.Count];
                if (tb.Enabled && tb.Visible)
                {
                    _objetivo = tb;
                    tb.Focus();
                    tb.SelectionStart = tb.TextLength;
                    CambiarMayusculas(tb.TextLength == 0);
                    return;
                }
            }
        }

        private void Acomodar()
        {
            var area = DisplayRectangle;
            if (area.Width <= 0 || area.Height <= 0) return;
            int gap = UITheme.S(6);
            int filas = Filas.Length + 1;
            int altoTecla = (area.Height - gap * (filas - 1)) / filas;
            int y = area.Y;

            for (int f = 0; f < _teclas.Count; f++)
            {
                int n = _teclas[f].Count;
                int ancho = (area.Width - gap * (n - 1)) / n;
                int x = area.X;
                for (int i = 0; i < n; i++)
                {
                    _teclas[f][i].SetBounds(x, y, ancho, altoTecla);
                    x += ancho + gap;
                }
                y += altoTecla + gap;
            }

            // Fila inferior: Mayús | # | Espacio (ancho) | Borrar | Limpiar | Siguiente
            var inferiores = new (BotonRhino b, int peso)[]
            {
                (_btnMayus, 2), (_btnNumero, 1), (_btnEspacio, 5), (_btnBorrar, 2), (_btnLimpiar, 2), (_btnSiguiente, 2)
            };
            int pesoTotal = 0;
            foreach (var t in inferiores) pesoTotal += t.peso;
            int disponible = area.Width - gap * (inferiores.Length - 1);
            int xx = area.X;
            for (int i = 0; i < inferiores.Length; i++)
            {
                int w = i == inferiores.Length - 1 ? area.Right - xx : disponible * inferiores[i].peso / pesoTotal;
                inferiores[i].b.SetBounds(xx, y, w, altoTecla);
                xx += w + gap;
            }
        }
    }
}
