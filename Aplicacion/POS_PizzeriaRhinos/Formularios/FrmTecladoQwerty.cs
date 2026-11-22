using System;
using System.Drawing;
using System.Windows.Forms;

namespace POS_PizzeriaRhinos.Formularios
{
    public class FrmTecladoQwerty : Form
    {
        private readonly TextBox _destino;
        private readonly Label _lblPreview;

        public FrmTecladoQwerty(string tituloCampo, TextBox destino)
        {
            _destino = destino;
            Text = $"Teclado Virtual - {tituloCampo}";
            Size = new Size(860, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = UITheme.BgBase;
            ForeColor = UITheme.TextPrimary;

            _lblPreview = new Label
            {
                Text = _destino.Text,
                Location = new Point(20, 14),
                Size = new Size(804, 42),
                BackColor = UITheme.GlassElevated,
                ForeColor = UITheme.AccentAmber,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };
            UITheme.ApplyRoundedCorners(_lblPreview, 10);
            Controls.Add(_lblPreview);

            string[][] filas =
            {
                new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
                new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
                new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", "Ñ" },
                new[] { "Z", "X", "C", "V", "B", "N", "M", "#", "-", "." }
            };

            int y = 68;
            foreach (var fila in filas)
            {
                int x = 20;
                foreach (var tecla in fila)
                {
                    var btn = new Button
                    {
                        Text = tecla,
                        Location = new Point(x, y),
                        Size = new Size(74, 48)
                    };
                    UITheme.StyleButton(btn, UITheme.GlassElevated, 8, 11f);
                    btn.Click += (_, _) =>
                    {
                        _destino.Text += tecla;
                        _destino.SelectionStart = _destino.Text.Length;
                        _lblPreview.Text = _destino.Text;
                    };
                    Controls.Add(btn);
                    x += 81;
                }
                y += 55;
            }

            var btnEspacio = new Button { Text = "ESPACIO", Location = new Point(20, y), Size = new Size(340, 48) };
            UITheme.StyleButton(btnEspacio, UITheme.GlassCard, 10, 10f);
            btnEspacio.Click += (_, _) =>
            {
                _destino.Text += " ";
                _lblPreview.Text = _destino.Text;
            };

            var btnBorrar = new Button { Text = "⌫ BORRAR", Location = new Point(372, y), Size = new Size(210, 48) };
            UITheme.StyleButton(btnBorrar, UITheme.AccentPrimary, 10, 10f);
            btnBorrar.Click += (_, _) =>
            {
                if (_destino.Text.Length > 0)
                    _destino.Text = _destino.Text[..^1];
                _lblPreview.Text = _destino.Text;
            };

            var btnListo = new Button { Text = "✔ ACEPTAR", Location = new Point(594, y), Size = new Size(230, 48) };
            UITheme.StyleButton(btnListo, UITheme.AccentSuccess, 10, 10.5f);
            btnListo.Click += (_, _) => Close();

            Controls.AddRange(new Control[] { btnEspacio, btnBorrar, btnListo });
        }
    }
}