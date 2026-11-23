using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Impresion
{
    /// <summary>
    /// Genera la comanda de cocina (sin precios) y el ticket del cliente en formato de impresora térmica de 80 mm,
    /// con vista previa. La cocina no usa el sistema: su única interacción es la comanda impresa (nota operativa del análisis).
    /// </summary>
    public static class ImpresorTickets
    {
        public enum TipoDocumento { Comanda, Ticket, Ambos }

        private const int AnchoCaracteres = 40;          // caracteres por renglón en Consolas 8.5 pt
        private const int AnchoPapel = 315;              // 80 mm en centésimas de pulgada
        private const int Margen = 12;

        private enum TipoLinea { Texto, Separador, Corte, Logo }

        private sealed record Linea(TipoLinea Tipo, string Texto = "", float Tamano = 8.5f, bool Negrita = false, bool Centrado = false);

        public static void VistaPrevia(IWin32Window owner, CabeceraPedido cab, DataTable detalle, TipoDocumento tipo,
            string? metodoPago = null, decimal recibido = 0m, decimal cambio = 0m)
        {
            var lineas = new List<Linea>();
            if (tipo != TipoDocumento.Ticket) lineas.AddRange(Comanda(cab, detalle));
            if (tipo == TipoDocumento.Ambos) lineas.Add(new Linea(TipoLinea.Corte));
            if (tipo != TipoDocumento.Comanda) lineas.AddRange(Ticket(cab, detalle, metodoPago, recibido, cambio));

            int alto = Margen * 2 + (int)Math.Ceiling(lineas.Sum(x => AltoLinea(x))) + 20;

            using var doc = new PrintDocument { DocumentName = $"Rhinos Pizza - Pedido {cab.Folio}" };
            doc.DefaultPageSettings.PaperSize = new PaperSize("Ticket 80 mm", AnchoPapel, Math.Max(alto, 300));
            doc.DefaultPageSettings.Margins = new Margins(Margen, Margen, Margen, Margen);
            doc.PrintPage += (_, e) => Dibujar(e, lineas);

            using var vista = new PrintPreviewDialog
            {
                Document = doc,
                Text = $"Vista previa · Pedido #{cab.Folio}",
                StartPosition = FormStartPosition.CenterParent,
                Size = UITheme.S(560, 860),
                UseAntiAlias = true,
                ShowIcon = false
            };
            vista.PrintPreviewControl.Zoom = 1.2;
            try
            {
                vista.ShowDialog(owner);
            }
            catch (InvalidPrinterException)
            {
                Dialogo.Advertencia(owner, "No hay una impresora instalada en Windows. Instala la impresora de tickets o 'Microsoft Print to PDF'.",
                    "Sin impresora");
            }
        }

        // =====================================================================
        // CONTENIDO
        // =====================================================================
        private static bool EsPizza(DataRow r)
        {
            string cat = r.Table.Columns.Contains("Categoria") ? Convert.ToString(r["Categoria"]) ?? "" : "";
            return cat == "PIZZA" || cat == "PIZZA_MOSTRADOR";
        }

        private static string Tamano(DataRow r)
        {
            string cat = r.Table.Columns.Contains("Categoria") ? Convert.ToString(r["Categoria"]) ?? "" : "";
            return UITheme.NombreTamano(cat, Convert.ToString(r["Tamaño"]));
        }

        private static IEnumerable<string> Modificadores(DataRow r, bool conCosto)
        {
            string mods = Convert.ToString(r["Modificadores_Ingredientes"]) ?? "";
            if (mods.Length == 0 || mods == "Sin modificaciones") yield break;
            foreach (var m in mods.Split(" | "))
            {
                // Formato del DAO: "[CON] Piña (COMPLETA, +$0.00)"
                string texto = m.Replace("[CON]", "CON").Replace("[SIN]", "SIN")
                                .Replace("(COMPLETA", "(toda").Replace("(MITAD_1", "(mitad 1").Replace("(MITAD_2", "(mitad 2");
                if (!conCosto || texto.Contains("+$0.00"))
                    texto = Regex.Replace(texto, @",\s*\+\$[\d,\.]+\)", ")");
                yield return texto;
            }
        }

        private static List<Linea> Comanda(CabeceraPedido cab, DataTable det)
        {
            var l = new List<Linea>
            {
                new(TipoLinea.Texto, "*** COMANDA DE COCINA ***", 10f, true, true),
                new(TipoLinea.Texto, $"FOLIO #{cab.Folio}", 18f, true, true),
                new(TipoLinea.Texto, $"{cab.TipoServicio} · {cab.FechaHora:dd/MM/yyyy HH:mm}", 10f, true, true),
                new(TipoLinea.Separador)
            };
            foreach (DataRow r in det.Rows)
            {
                l.AddRange(Envolver($"{r["Cantidad"]} x {r["Producto_Especialidad"]}", 10f, true));
                l.AddRange(Envolver($"   Tamaño: {Tamano(r)}"));
                if (EsPizza(r))
                {
                    l.AddRange(Envolver($"   Masa: {r["Masa"]}"));
                    if (Convert.ToString(r["Orilla_Queso"]) == "Sí") l.AddRange(Envolver("   + ORILLA DE QUESO", 8.5f, true));
                }
                if (Convert.ToString(r["Queso_Extra"]) == "Sí") l.AddRange(Envolver("   + QUESO EXTRA", 8.5f, true));
                foreach (var m in Modificadores(r, false)) l.AddRange(Envolver("   " + m, 8.5f, true));
                string promo = Convert.ToString(r["Promo_2x1"]) ?? "No";
                if (promo != "No") l.AddRange(Envolver($"   PROMO 2x1 {promo.Replace("Sí ", "")}"));
                l.Add(new Linea(TipoLinea.Texto, ""));
            }
            l.Add(new Linea(TipoLinea.Separador));
            l.AddRange(Envolver($"Cajera: {cab.Cajera}"));
            if (cab.TipoServicio == "DOMICILIO") l.AddRange(Envolver($"Repartidor: {cab.Repartidor}"));
            return l;
        }

        private static List<Linea> Ticket(CabeceraPedido cab, DataTable det, string? metodo, decimal recibido, decimal cambio)
        {
            var l = new List<Linea>
            {
                new(TipoLinea.Logo),
                new(TipoLinea.Texto, "¡Es rica y crujiente!", 9f, true, true),
                new(TipoLinea.Texto, "TICKET DE VENTA", 11f, true, true),
                new(TipoLinea.Separador)
            };
            l.AddRange(Envolver($"Folio: #{cab.Folio}"));
            l.AddRange(Envolver($"Fecha: {cab.FechaHora:dd/MM/yyyy HH:mm}"));
            l.AddRange(Envolver($"Atendió: {cab.Cajera}"));
            l.AddRange(Envolver($"Servicio: {(cab.TipoServicio == "DOMICILIO" ? "A domicilio" : "Mostrador")}"));
            if (cab.TipoServicio == "DOMICILIO")
            {
                l.AddRange(Envolver($"Cliente: {cab.Cliente}"));
                l.AddRange(Envolver($"Tel: {cab.Telefono}"));
                if (cab.Direccion.Length > 0) l.AddRange(Envolver($"Dir: {cab.Direccion}"));
                if (cab.EntreCalles.Length > 0) l.AddRange(Envolver($"Entre: {cab.EntreCalles}"));
                if (cab.Referencias.Length > 0) l.AddRange(Envolver($"Ref: {cab.Referencias}"));
                l.AddRange(Envolver($"Repartidor: {cab.Repartidor}"));
            }
            l.Add(new Linea(TipoLinea.Separador));

            foreach (DataRow r in det.Rows)
            {
                decimal importe = Convert.ToDecimal(r["Subtotal_Linea"]);
                l.AddRange(DosColumnas($"{r["Cantidad"]} {r["Producto_Especialidad"]}", Dinero(importe), true));
                l.AddRange(Envolver($"   {Tamano(r)}" + (EsPizza(r) ? $" · Masa {Convert.ToString(r["Masa"])?.ToLowerInvariant()}" : "")));
                if (EsPizza(r) && Convert.ToString(r["Orilla_Queso"]) == "Sí") l.AddRange(Envolver("   Orilla de queso"));
                if (Convert.ToString(r["Queso_Extra"]) == "Sí") l.AddRange(Envolver("   Queso extra"));
                foreach (var m in Modificadores(r, true)) l.AddRange(Envolver("   " + m));
                string promo = Convert.ToString(r["Promo_2x1"]) ?? "No";
                if (promo != "No") l.AddRange(Envolver($"   Promo 2x1 {promo.Replace("Sí ", "")}"));
            }
            l.Add(new Linea(TipoLinea.Separador));
            l.AddRange(DosColumnas("Subtotal", Dinero(cab.Subtotal)));
            l.AddRange(DosColumnas("Extras", Dinero(cab.Extras)));
            l.Add(new Linea(TipoLinea.Texto, $"TOTAL {Dinero(cab.Total)}", 14f, true, true));
            if (!string.IsNullOrEmpty(metodo))
            {
                string nombreMetodo = metodo switch { "TARJETA" => "Tarjeta", "CONTRA_ENTREGA" => "Paga al recibir", _ => "Efectivo" };
                l.AddRange(DosColumnas("Forma de pago", nombreMetodo));
                if (metodo == "EFECTIVO")
                {
                    l.AddRange(DosColumnas("Recibido", Dinero(recibido)));
                    l.AddRange(DosColumnas("Cambio", Dinero(cambio)));
                }
            }
            l.Add(new Linea(TipoLinea.Separador));
            l.Add(new Linea(TipoLinea.Texto, "¡Gracias por su preferencia!", 9f, true, true));
            return l;
        }

        private static string Dinero(decimal v) => v.ToString("C2", UITheme.Cultura);

        private static IEnumerable<Linea> Envolver(string texto, float tamano = 8.5f, bool negrita = false)
        {
            int ancho = tamano > 9f ? (int)(AnchoCaracteres * 8.5f / tamano) : AnchoCaracteres;
            foreach (var parte in Partir(texto, ancho))
                yield return new Linea(TipoLinea.Texto, parte, tamano, negrita);
        }

        /// <summary>Texto a la izquierda e importe alineado a la derecha (fuente monoespaciada).</summary>
        private static IEnumerable<Linea> DosColumnas(string izquierda, string derecha, bool negrita = false)
        {
            int libre = AnchoCaracteres - derecha.Length - 1;
            var partes = Partir(izquierda, libre).ToList();
            for (int i = 0; i < partes.Count; i++)
            {
                string t = i == 0 ? partes[i].PadRight(libre) + " " + derecha : partes[i];
                yield return new Linea(TipoLinea.Texto, t, 8.5f, negrita);
            }
        }

        private static IEnumerable<string> Partir(string texto, int ancho)
        {
            if (string.IsNullOrEmpty(texto)) { yield return ""; yield break; }
            string sangria = new string(texto.TakeWhile(c => c == ' ').ToArray());
            var palabras = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string actual = sangria;
            foreach (var p in palabras)
            {
                string candidato = actual.Trim().Length == 0 ? sangria + p : actual + " " + p;
                if (candidato.Length > ancho && actual.Trim().Length > 0)
                {
                    yield return actual;
                    actual = sangria + "  " + p;
                }
                else
                {
                    actual = candidato;
                }
                while (actual.Length > ancho)
                {
                    yield return actual[..ancho];
                    actual = sangria + "  " + actual[ancho..];
                }
            }
            if (actual.Length > 0) yield return actual;
        }

        // =====================================================================
        // DIBUJO
        // =====================================================================
        private static float AltoLinea(Linea l) => l.Tipo switch
        {
            TipoLinea.Separador => 14f,
            TipoLinea.Corte => 40f,
            TipoLinea.Logo => UITheme.Logo != null ? (AnchoPapel - Margen * 2) * UITheme.Logo.Height / (float)UITheme.Logo.Width + 8f : 30f,
            _ => l.Tamano * 1.45f / 72f * 100f
        };

        private static void Dibujar(PrintPageEventArgs e, List<Linea> lineas)
        {
            var g = e.Graphics!;
            // Se dibuja con GDI+ (texto Unicode), no con bytes ESC/POS, por lo que acentos, ñ, ¿ y ¡ salen tal cual
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float x = e.MarginBounds.Left;
            float ancho = e.MarginBounds.Width;
            float y = e.MarginBounds.Top;
            using var pen = new Pen(Color.Black, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };

            foreach (var l in lineas)
            {
                float alto = AltoLinea(l);
                switch (l.Tipo)
                {
                    case TipoLinea.Separador:
                        g.DrawLine(pen, x, y + alto / 2, x + ancho, y + alto / 2);
                        break;
                    case TipoLinea.Corte:
                        using (var f = new Font("Consolas", 7f))
                        {
                            var fmt = new StringFormat { Alignment = StringAlignment.Center };
                            g.DrawString("- - - - - -  cortar aquí  - - - - - -", f, Brushes.Black, new RectangleF(x, y + 12, ancho, 20), fmt);
                        }
                        break;
                    case TipoLinea.Logo:
                        if (UITheme.Logo != null)
                            g.DrawImage(UITheme.Logo, x, y, ancho, alto - 8f);
                        else
                            using (var f = new Font("Segoe UI", 16f, FontStyle.Bold))
                                g.DrawString("RHINOS PIZZA", f, Brushes.Black, new RectangleF(x, y, ancho, alto), new StringFormat { Alignment = StringAlignment.Center });
                        break;
                    default:
                        {
                            bool mono = !l.Centrado;
                            using var f = new Font(mono ? "Consolas" : "Segoe UI", l.Tamano, l.Negrita ? FontStyle.Bold : FontStyle.Regular);
                            var fmt = new StringFormat
                            {
                                Alignment = l.Centrado ? StringAlignment.Center : StringAlignment.Near,
                                Trimming = StringTrimming.None,
                                FormatFlags = StringFormatFlags.NoWrap
                            };
                            g.DrawString(l.Texto, f, Brushes.Black, new RectangleF(x, y, ancho, alto + 4), fmt);
                            break;
                        }
                }
                y += alto;
            }
            e.HasMorePages = false;
        }
    }
}
