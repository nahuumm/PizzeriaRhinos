using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace POS_PizzeriaRhinos.Formularios
{
    public static class UITheme
    {
        // Paleta Dark Minimalista + Glassmorphism
        public static readonly Color BgBase = Color.FromArgb(16, 18, 26);         // Fondo principal profundo
        public static readonly Color GlassCard = Color.FromArgb(26, 30, 44);      // Tarjeta translúcida nivel 1
        public static readonly Color GlassElevated = Color.FromArgb(34, 39, 56);  // Tarjeta elevada nivel 2
        public static readonly Color GlassBorder = Color.FromArgb(58, 66, 92);    // Borde sutil tipo cristal
        public static readonly Color AccentPrimary = Color.FromArgb(239, 83, 80); // Rojo Rhinos moderno
        public static readonly Color AccentAmber = Color.FromArgb(255, 167, 38);  // Ámbar cálido
        public static readonly Color AccentSuccess = Color.FromArgb(46, 184, 114);// Verde esmeralda
        public static readonly Color TextPrimary = Color.FromArgb(242, 244, 248);
        public static readonly Color TextMuted = Color.FromArgb(158, 166, 188);

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
            using var pen = new Pen(borderColor ?? GlassBorder, 1.2f);
            e.Graphics.DrawPath(pen, path);
        }

        public static void StyleGlassCard(Panel pnl, int radius = 16, bool elevated = false)
        {
            pnl.BackColor = elevated ? GlassElevated : GlassCard;
            ApplyRoundedCorners(pnl, radius);
            pnl.Paint += (_, e) => PaintGlassBorder(e, pnl, radius);
        }

        public static void StyleButton(Button btn, Color bg, int radius = 10)
        {
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            ApplyRoundedCorners(btn, radius);
        }

        public static void StyleTextBox(TextBox tb)
        {
            tb.BackColor = GlassElevated;
            tb.ForeColor = TextPrimary;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = new Font("Segoe UI", 9.5f);
        }

        public static void StyleComboBox(ComboBox cb)
        {
            cb.BackColor = GlassElevated;
            cb.ForeColor = TextPrimary;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = new Font("Segoe UI", 9.5f);
        }

        public static void StyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = GlassCard;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = Color.FromArgb(42, 48, 68);
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.RowTemplate.Height = 30;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = GlassElevated;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = AccentAmber;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = GlassElevated;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeight = 34;

            dgv.DefaultCellStyle.BackColor = GlassCard;
            dgv.DefaultCellStyle.ForeColor = TextPrimary;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(65, 76, 110);
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