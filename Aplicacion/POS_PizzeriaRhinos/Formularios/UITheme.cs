using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace POS_PizzeriaRhinos.Formularios
{
    public static class UITheme
    {
        // Paleta Moderna Pizzería Rhinos (Carbón Horno + Rojo Salsa + Ámbar Queso + Verde Albahaca)
        public static readonly Color BgBase = Color.FromArgb(18, 20, 28);           // Fondo principal oscuro
        public static readonly Color GlassCard = Color.FromArgb(28, 32, 46);        // Tarjeta nivel 1
        public static readonly Color GlassElevated = Color.FromArgb(38, 44, 62);    // Controles / tarjeta nivel 2
        public static readonly Color GlassBorder = Color.FromArgb(68, 78, 108);     // Borde sutil
        public static readonly Color AccentPrimary = Color.FromArgb(217, 56, 58);   // Rojo Pizzería Rhinos
        public static readonly Color AccentAmber = Color.FromArgb(255, 179, 0);     // Ámbar Queso Fundido
        public static readonly Color AccentSuccess = Color.FromArgb(46, 184, 114);  // Verde Albahaca
        public static readonly Color AccentInfo = Color.FromArgb(41, 148, 214);     // Azul Comanda / Info
        public static readonly Color TextPrimary = Color.FromArgb(245, 247, 250);
        public static readonly Color TextMuted = Color.FromArgb(168, 176, 198);

        public static Image? CargarLogoRhinos()
        {
            string[] posiblesRutas =
            {
                Path.Combine(AppContext.BaseDirectory, "Formularios", "Assets", "logo_rhinos.jpg"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "logo_rhinos.jpg"),
                Path.Combine(AppContext.BaseDirectory, "logo_rhinos.jpg")
            };

            foreach (var ruta in posiblesRutas)
            {
                if (File.Exists(ruta))
                {
                    try
                    {
                        using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read);
                        return Image.FromStream(fs);
                    }
                    catch
                    {
                        // Si el archivo está bloqueado o dañado, continúa con el respaldo visual
                    }
                }
            }
            return null;
        }

        public static void ApplyRoundedCorners(Control ctrl, int radius = 14)
        {
            void UpdateRegion()
            {
                if (ctrl.Width <= 0 || ctrl.Height <= 0) return;
                using var path = GetRoundedPath(new Rectangle(0, 0, ctrl.Width, ctrl.Height), radius);
                ctrl.Region = new Region(path);
            }
            ctrl.Resize += (_, _) => UpdateRegion();
            UpdateRegion();
        }

        public static void PaintGlassBorder(PaintEventArgs e, Control ctrl, int radius = 14, Color? borderColor = null)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, ctrl.Width - 1, ctrl.Height - 1);
            using var path = GetRoundedPath(rect, radius);
            using var pen = new Pen(borderColor ?? GlassBorder, 1.3f);
            e.Graphics.DrawPath(pen, path);
        }

        public static void StyleGlassCard(Panel pnl, int radius = 16, bool elevated = false)
        {
            pnl.BackColor = elevated ? GlassElevated : GlassCard;
            ApplyRoundedCorners(pnl, radius);
            pnl.Paint += (_, e) => PaintGlassBorder(e, pnl, radius);
        }

        public static void StyleButton(Button btn, Color bg, int radius = 10, float fontSize = 10f)
        {
            btn.BackColor = bg;
            btn.ForeColor = bg == AccentAmber ? Color.FromArgb(20, 22, 30) : Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            ApplyRoundedCorners(btn, radius);
        }

        public static void StyleTextBox(TextBox tb)
        {
            tb.BackColor = GlassElevated;
            tb.ForeColor = TextPrimary;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = new Font("Segoe UI", 10.5f);
        }

        public static void StyleComboBox(ComboBox cb)
        {
            cb.BackColor = GlassElevated;
            cb.ForeColor = TextPrimary;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = new Font("Segoe UI", 10f);
        }

        public static void StyleGrid(DataGridView dgv, int headerHeight = 40)
        {
            dgv.BackgroundColor = GlassCard;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = Color.FromArgb(46, 53, 74);
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.RowTemplate.Height = 34;

            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = headerHeight;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = GlassElevated;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = AccentAmber;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = GlassElevated;
            dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgv.DefaultCellStyle.BackColor = GlassCard;
            dgv.DefaultCellStyle.ForeColor = TextPrimary;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(72, 84, 120);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}