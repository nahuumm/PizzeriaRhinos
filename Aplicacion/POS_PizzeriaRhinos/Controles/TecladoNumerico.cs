using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Controles
{
    /// <summary>Teclado numérico grande para montos, cantidades y teléfonos.</summary>
    [DesignerCategory("Code")]
    public class TecladoNumerico : Control
    {
        private readonly BotonRhino[] _teclas;
        private TextBox? _objetivo;
        public bool PermitirDecimal { get; set; } = true;

        public TecladoNumerico()
        {
            string[] etiquetas = { "7", "8", "9", "4", "5", "6", "1", "2", "3", ".", "0", "⌫" };
            _teclas = new BotonRhino[etiquetas.Length];
            for (int i = 0; i < etiquetas.Length; i++)
            {
                string t = etiquetas[i];
                var b = new BotonRhino(t, t == "⌫" ? EstiloBoton.Ambar : EstiloBoton.Secundario)
                {
                    TabStop = false,
                    Font = new Font(UITheme.FamiliaBase, 16f, FontStyle.Bold),
                    Margin = new Padding(0)
                };
                b.Click += (_, _) => Presionar(t);
                _teclas[i] = b;
                Controls.Add(b);
            }
            Size = UITheme.S(300, 300);
            Resize += (_, _) => Acomodar();
        }

        public void Vincular(TextBox tb)
        {
            _objetivo = tb;
            tb.Enter += (_, _) => _objetivo = tb;
        }

        private void Presionar(string t)
        {
            var tb = _objetivo;
            if (tb == null || tb.ReadOnly || !tb.Enabled) return;
            if (t == "⌫")
            {
                if (tb.SelectionLength > 0) tb.SelectedText = "";
                else if (tb.TextLength > 0)
                {
                    int pos = Math.Max(1, tb.SelectionStart);
                    tb.Text = tb.Text.Remove(pos - 1, 1);
                    tb.SelectionStart = pos - 1;
                }
            }
            else
            {
                if (t == "." && (!PermitirDecimal || tb.Text.Contains('.'))) return;
                if (tb.MaxLength > 0 && tb.TextLength >= tb.MaxLength && tb.SelectionLength == 0) return;
                tb.SelectedText = t;
            }
            tb.Focus();
        }

        private void Acomodar()
        {
            int gap = UITheme.S(8);
            int w = (Width - gap * 2) / 3;
            int h = (Height - gap * 3) / 4;
            for (int i = 0; i < _teclas.Length; i++)
            {
                int fila = i / 3, col = i % 3;
                _teclas[i].SetBounds(col * (w + gap), fila * (h + gap), w, h);
            }
            _teclas[9].Enabled = PermitirDecimal;
        }
    }
}
