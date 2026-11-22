using System;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    public class FrmTicketDetalle : Form
    {
        private readonly int _folio;
        private readonly PedidoDAO _pedidoDao = new();
        private readonly ClienteDAO _clienteDao = new();
        private string _textoComanda = string.Empty;

        public FrmTicketDetalle(
            int folio,
            string fecha,
            string servicio,
            string estado,
            string clienteNombre,
            string telefono,
            string cajera,
            string repartidor,
            decimal subtotal,
            decimal extras,
            decimal total)
        {
            _folio = folio;
            Text = $"Ticket / Comanda de Cocina - Folio #{folio}";
            Size = new Size(640, 760);
            MinimumSize = new Size(580, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = UITheme.BgBase;
            ForeColor = UITheme.TextPrimary;

            // Obtener dirección completa si existe cliente registrado
            Cliente? cli = !string.IsNullOrWhiteSpace(telefono) && telefono != "N/A"
                ? _clienteDao.BuscarPorTelefono(telefono)
                : null;

            // Obtener desglose de partidas, mitades y modificadores SIN/CON desde PedidoDAO
            DataTable dtDetalle = _pedidoDao.ObtenerDetallePedido(folio);

            _textoComanda = ConstruirTextoTicket(
                folio, fecha, servicio, estado, clienteNombre, telefono, cli,
                cajera, repartidor, subtotal, extras, total, dtDetalle);

            // Cabecera con Logo
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = UITheme.GlassCard,
                Padding = new Padding(16, 10, 16, 10)
            };

            var pnlLogoBox = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(165, 58),
                BackColor = Color.White
            };
            UITheme.ApplyRoundedCorners(pnlLogoBox, 10);
            var pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = UITheme.CargarLogoRhinos()
            };
            pnlLogoBox.Controls.Add(pic);

            var lblTitulo = new Label
            {
                Text = $"COMANDA / TICKET FOLIO #{folio}",
                Location = new Point(195, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            };

            var lblSub = new Label
            {
                Text = $"Servicio: {servicio}   |   Estado: {estado}",
                Location = new Point(197, 46),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = UITheme.TextPrimary
            };

            pnlHeader.Controls.AddRange(new Control[] { pnlLogoBox, lblTitulo, lblSub });

            // Papel simulado estilo Ticket Térmico limpio y legible
            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 14, 18, 14),
                BackColor = UITheme.BgBase
            };

            var txtTicket = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(252, 250, 245), // Papel blanco cálido
                ForeColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                Text = _textoComanda
            };
            pnlBody.Controls.Add(txtTicket);

            // Barra inferior con botones grandes
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = UITheme.GlassCard,
                Padding = new Padding(18, 12, 18, 12)
            };

            var btnImprimir = new Button
            {
                Text = "🖨️  IMPRIMIR TICKET / COMANDA",
                Location = new Point(18, 12),
                Size = new Size(340, 48)
            };
            UITheme.StyleButton(btnImprimir, UITheme.AccentSuccess, 10, 10.5f);
            btnImprimir.Click += (_, _) => ImprimirTicket();

            var btnCerrar = new Button
            {
                Text = "Cerrar",
                Location = new Point(374, 12),
                Size = new Size(225, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            UITheme.StyleButton(btnCerrar, UITheme.AccentPrimary, 10, 10.5f);
            btnCerrar.Click += (_, _) => Close();

            pnlFooter.Controls.AddRange(new Control[] { btnImprimir, btnCerrar });

            Controls.Add(pnlBody);
            Controls.Add(pnlFooter);
            Controls.Add(pnlHeader);
        }

        private static string ConstruirTextoTicket(
            int folio,
            string fecha,
            string servicio,
            string estado,
            string clienteNombre,
            string telefono,
            Cliente? cli,
            string cajera,
            string repartidor,
            decimal subtotal,
            decimal extras,
            decimal total,
            DataTable dtDetalle)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==========================================================");
            sb.AppendLine("              PIZZERÍA RHINOS - ES RICA Y CRUJIENTE!");
            sb.AppendLine("                TICKET DE VENTA Y COMANDA DE COCINA");
            sb.AppendLine("==========================================================");
            sb.AppendLine($"FOLIO PEDIDO : #{folio}");
            sb.AppendLine($"FECHA / HORA : {fecha}");
            sb.AppendLine($"MODALIDAD    : {servicio}");
            sb.AppendLine($"ESTADO ACTUAL: {estado}");
            sb.AppendLine($"CAJERA       : {cajera}");
            sb.AppendLine($"REPARTIDOR   : {(string.IsNullOrWhiteSpace(repartidor) ? "N/A (Mostrador)" : repartidor)}");
            sb.AppendLine("----------------------------------------------------------");
            sb.AppendLine("DATOS DE ENTREGA / CLIENTE:");

            if (cli != null)
            {
                sb.AppendLine($"  Cliente    : {cli.Nombre}");
                sb.AppendLine($"  Teléfono   : {cli.Telefono}");
                sb.AppendLine($"  Dirección  : {cli.Calle} #{cli.NumeroCasa}, Col. {cli.Colonia}");
                if (!string.IsNullOrWhiteSpace(cli.EntreCalles))
                    sb.AppendLine($"  EntreCalles: {cli.EntreCalles}");
                if (!string.IsNullOrWhiteSpace(cli.Referencias))
                    sb.AppendLine($"  Referencias: {cli.Referencias}");
            }
            else
            {
                sb.AppendLine($"  Cliente    : {(string.IsNullOrWhiteSpace(clienteNombre) ? "Público General (Mostrador)" : clienteNombre)}");
                if (!string.IsNullOrWhiteSpace(telefono))
                    sb.AppendLine($"  Teléfono   : {telefono}");
            }

            sb.AppendLine("==========================================================");
            sb.AppendLine("DESGLOSE DE PARTIDAS (PIZZAS, MITADES Y MODIFICADORES):");
            sb.AppendLine("----------------------------------------------------------");

            int numLinea = 1;
            foreach (DataRow row in dtDetalle.Rows)
            {
                int cant = Convert.ToInt32(row["Cantidad"]);
                string prod = row["Producto_Especialidad"].ToString() ?? "";
                string tam = row["Tamaño"].ToString() ?? "";
                string masa = row["Masa"].ToString() ?? "GRUESA";
                string orilla = row["Orilla_Queso"].ToString() ?? "No";
                string qExtra = row["Queso_Extra"].ToString() ?? "No";
                string promo = row["Promo_2x1"].ToString() ?? "No";
                string mods = row["Modificadores_Ingredientes"].ToString() ?? "Sin modificaciones";
                decimal subLinea = Convert.ToDecimal(row["Subtotal_Linea"]);

                sb.AppendLine($"{numLinea}. [{cant}x] {prod} ({tam})");
                sb.AppendLine($"   Masa: {masa} | Orilla Queso: {orilla} | Queso Extra: {qExtra}");
                if (promo != "No")
                    sb.AppendLine($"   PROMOCIÓN: {promo}");
                sb.AppendLine($"   Modificadores (SIN/CON): {mods}");
                sb.AppendLine($"   Importe Línea: ${subLinea:0.00}");
                sb.AppendLine("----------------------------------------------------------");
                numLinea++;
            }

            sb.AppendLine($"SUBTOTAL BASE        : ${subtotal,10:0.00}");
            sb.AppendLine($"EXTRAS / OPCIONES    : ${extras,10:0.00}");
            sb.AppendLine("==========================================================");
            sb.AppendLine($"TOTAL A COBRAR       : ${total,10:0.00}");
            sb.AppendLine("==========================================================");
            sb.AppendLine("          ¡GRACIAS POR SU PREFERENCIA EN RHINOS!");
            return sb.ToString();
        }

        private void ImprimirTicket()
        {
            try
            {
                using var doc = new PrintDocument();
                doc.DocumentName = $"Ticket_Rhinos_Folio_{_folio}";
                doc.PrintPage += (_, e) =>
                {
                    if (e.Graphics == null) return;
                    using var font = new Font("Consolas", 9.5f);
                    e.Graphics.DrawString(_textoComanda, font, Brushes.Black, new RectangleF(30, 30, e.PageBounds.Width - 60, e.PageBounds.Height - 60));
                };

                using var preview = new PrintPreviewDialog
                {
                    Document = doc,
                    Width = 800,
                    Height = 650,
                    StartPosition = FormStartPosition.CenterParent
                };
                preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir la vista de impresión:\n{ex.Message}", "Impresión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}