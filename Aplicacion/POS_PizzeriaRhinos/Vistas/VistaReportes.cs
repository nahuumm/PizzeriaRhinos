using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>
    /// Corte de caja y reportes del administrador (consultas multitabla, vista vw_resumen_pedidos_corte,
    /// GROUP BY / agregados). Incluye "lo más vendido" con la clasificación del boceto (mucho / más o menos / poco).
    /// </summary>
    [DesignerCategory("Code")]
    public class VistaReportes : VistaBase
    {
        private readonly ReporteDAO _dao = new();
        private readonly GrupoOpciones _grpRango, _grpServicio, _grpReporte;
        private readonly DateTimePicker _dtpDesde, _dtpHasta;
        private readonly TarjetaKpi _kpiTotal, _kpiPedidos, _kpiPromedio, _kpiCancelados;
        private readonly PanelTabla _tabla;
        private readonly Label _lblTituloReporte;
        private DataTable? _actual;
        private bool _cambiandoFechas;

        public VistaReportes(FrmPrincipal shell) : base(shell)
        {
            var encabezado = new EncabezadoModulo("Corte de caja y reportes", "Cierre del turno, liquidación de repartidores y productos más vendidos.", "📊");

            // ---------------- Periodo ----------------
            _grpRango = new GrupoOpciones { TamanoBoton = UITheme.S(120, 50) };
            _grpRango.Agregar("Hoy", "HOY");
            _grpRango.Agregar("Ayer", "AYER");
            _grpRango.Agregar("7 días", "SEMANA");
            _grpRango.Agregar("Este mes", "MES");
            _grpRango.Seleccionar("HOY");
            _grpRango.ValorCambiado += (_, _) => AplicarRango();

            _dtpDesde = new DateTimePicker { Width = UITheme.S(170), Margin = UITheme.P(12, 10, 4, 4) };
            _dtpHasta = new DateTimePicker { Width = UITheme.S(170), Margin = UITheme.P(4, 10, 4, 4) };
            UITheme.EstilizarFecha(_dtpDesde);
            UITheme.EstilizarFecha(_dtpHasta);
            _dtpDesde.ValueChanged += (_, _) => FechaManual();
            _dtpHasta.ValueChanged += (_, _) => FechaManual();
            var lblA = new Label { Text = "a", AutoSize = true, Margin = UITheme.P(2, 18, 2, 4), ForeColor = UITheme.TextoSuave };

            _grpServicio = new GrupoOpciones { TamanoBoton = UITheme.S(124, 50) };
            _grpServicio.Agregar("Todos", "");
            _grpServicio.Agregar("Domicilio", "DOMICILIO", "🛵");
            _grpServicio.Agregar("Mostrador", "MOSTRADOR", "🏪");
            _grpServicio.Seleccionar("");
            _grpServicio.ValorCambiado += (_, _) => Generar();

            var btnGenerar = new BotonRhino("Generar", EstiloBoton.Primario, "⟳") { Size = UITheme.S(140, 50) };
            btnGenerar.Click += (_, _) => Generar();
            var btnExportar = new BotonRhino("Exportar CSV", EstiloBoton.Secundario, "📄") { Size = UITheme.S(170, 50) };
            btnExportar.Click += (_, _) => Exportar();

            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            filtros.Controls.AddRange(new Control[]
            {
                _grpRango, _dtpDesde, lblA, _dtpHasta, new Panel { Width = UITheme.S(12), Height = 1 }, _grpServicio, btnGenerar, btnExportar
            });

            // ---------------- Indicadores ----------------
            _kpiTotal = new TarjetaKpi("Total vendido", "💵", UITheme.Verde) { Dock = DockStyle.Fill };
            _kpiPedidos = new TarjetaKpi("Pedidos cobrados", "🧾", UITheme.Rojo) { Dock = DockStyle.Fill };
            _kpiPromedio = new TarjetaKpi("Ticket promedio", "📈", UITheme.Azul) { Dock = DockStyle.Fill };
            _kpiCancelados = new TarjetaKpi("Cancelados", "✖", UITheme.RojoOscuro) { Dock = DockStyle.Fill };
            var filaKpi = new TableLayoutPanel { Height = UITheme.S(110), ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++) filaKpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            filaKpi.Controls.Add(_kpiTotal, 0, 0);
            filaKpi.Controls.Add(_kpiPedidos, 1, 0);
            filaKpi.Controls.Add(_kpiPromedio, 2, 0);
            filaKpi.Controls.Add(_kpiCancelados, 3, 0);

            // ---------------- Selector de reporte ----------------
            _grpReporte = new GrupoOpciones { TamanoBoton = UITheme.S(210, 54), Dock = DockStyle.Top };
            _grpReporte.Agregar("Corte de caja", "CORTE", "🧾");
            _grpReporte.Agregar("Liquidación repartidores", "LIQ", "🛵");
            _grpReporte.Agregar("Ventas por cajera", "CAJ", "💁");
            _grpReporte.Agregar("Por modalidad", "SRV", "🏪");
            _grpReporte.Agregar("Lo más vendido", "TOP", "🔥");
            _grpReporte.Seleccionar("CORTE");
            _grpReporte.ValorCambiado += (_, _) => Generar();

            _lblTituloReporte = new Label { Height = UITheme.S(36), Font = UITheme.Titulo(15f), ForeColor = UITheme.Texto, TextAlign = ContentAlignment.MiddleLeft };
            _tabla = new PanelTabla();
            _tabla.Grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && (_grpReporte.Valor as string) == "CORTE" && _tabla.Grid.Columns.Contains("Folio"))
                    FrmDetallePedido.Mostrar(Shell, Convert.ToInt32(_tabla.Grid.Rows[e.RowIndex].Cells["Folio"].Value));
            };

            Diseno.Apilar(this, _tabla, encabezado, Diseno.Espacio(12), filtros, Diseno.Espacio(6), filaKpi, Diseno.Espacio(6), _grpReporte, _lblTituloReporte);
            AplicarRango(false);
        }

        public override void AlMostrar() => CargarDatos();

        // ---------------------------------------------------------------
        // PERIODO
        // ---------------------------------------------------------------
        private void AplicarRango(bool generar = true)
        {
            DateTime hoy = DateTime.Today;
            (DateTime d, DateTime h) = (_grpRango.Valor as string) switch
            {
                "AYER" => (hoy.AddDays(-1), hoy.AddDays(-1)),
                "SEMANA" => (hoy.AddDays(-6), hoy),
                "MES" => (new DateTime(hoy.Year, hoy.Month, 1), hoy),
                _ => (hoy, hoy)
            };
            _cambiandoFechas = true;
            _dtpDesde.Value = d;
            _dtpHasta.Value = h;
            _cambiandoFechas = false;
            if (generar) Generar();
        }

        private void FechaManual()
        {
            if (_cambiandoFechas) return;
            _grpRango.Seleccionar(null);
        }

        // ---------------------------------------------------------------
        // DATOS
        // ---------------------------------------------------------------
        private void Generar() => ErroresUi.Intentar(Shell, CargarDatos, "No se pudo generar el reporte");

        private void CargarDatos()
        {
            DateTime desde = _dtpDesde.Value.Date, hasta = _dtpHasta.Value.Date;
            if (hasta < desde)
            {
                Dialogo.Advertencia(Shell, "La fecha final no puede ser anterior a la inicial.", "Periodo inválido");
                return;
            }
            string srv = _grpServicio.Valor as string ?? "";
            string periodo = desde == hasta ? desde.ToString("dd/MM/yyyy") : $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

            // Indicadores del corte (siempre se calculan con los pedidos del periodo)
            var corte = _dao.ObtenerPedidos(desde, hasta, srv, "", "");
            decimal total = 0; int cobrados = 0, cancelados = 0;
            foreach (DataRow r in corte.Rows)
            {
                if (Convert.ToString(r["Estado"]) == "CANCELADO") cancelados++;
                else { cobrados++; total += Convert.ToDecimal(r["Total"]); }
            }
            _kpiTotal.Valor = UITheme.Dinero(total);
            _kpiPedidos.Valor = cobrados.ToString();
            _kpiPromedio.Valor = cobrados == 0 ? UITheme.Dinero(0) : UITheme.Dinero(Math.Round(total / cobrados, 2));
            _kpiCancelados.Valor = cancelados.ToString();

            string reporte = _grpReporte.Valor as string ?? "CORTE";
            DataTable dt;
            string vacio = $"Sin movimientos del {periodo}.";
            switch (reporte)
            {
                case "LIQ":
                    dt = _dao.LiquidacionRepartidores(desde, hasta);
                    _lblTituloReporte.Text = $"Liquidación de repartidores · {periodo}  (efectivo de pedidos ENTREGADOS)";
                    break;
                case "CAJ":
                    dt = _dao.VentasPorCajera(desde, hasta);
                    _lblTituloReporte.Text = $"Ventas por cajera · {periodo}";
                    break;
                case "SRV":
                    dt = _dao.VentasPorServicio(desde, hasta);
                    _lblTituloReporte.Text = $"Ventas por modalidad · {periodo}";
                    break;
                case "TOP":
                    dt = _dao.ProductosMasVendidos(desde, hasta);
                    AgregarNivelVenta(dt);
                    _lblTituloReporte.Text = $"Lo más vendido · {periodo}";
                    break;
                default:
                    dt = corte;
                    _lblTituloReporte.Text = $"Corte de caja · {periodo} · {corte.Rows.Count} pedido(s)  (doble clic para ver el detalle)";
                    break;
            }

            _actual = dt;
            _tabla.Mostrar(dt, vacio);
            var g = _tabla.Grid;
            UITheme.FormatoMoneda(g, "Subtotal", "Extras", "Total", "Efectivo a liquidar", "Monto vendido", "Ticket promedio", "Ingreso", "Importe");
            if (g.Columns.Contains("Fecha")) g.Columns["Fecha"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            FormatoEstados.Aplicar(g);
        }

        /// <summary>Clasifica los productos en tercios por unidades vendidas (boceto: se vendió mucho / más o menos / poco).</summary>
        private static void AgregarNivelVenta(DataTable dt)
        {
            dt.Columns.Add("Nivel de venta", typeof(string));
            int n = dt.Rows.Count;
            for (int i = 0; i < n; i++)
            {
                double posicion = n == 1 ? 0 : i / (double)n;
                dt.Rows[i]["Nivel de venta"] = posicion < 1 / 3.0 ? "✔ Se vendió mucho" : posicion < 2 / 3.0 ? "◐ Se vendió más o menos" : "✖ Se vendió poco";
            }
        }

        // ---------------------------------------------------------------
        // EXPORTAR
        // ---------------------------------------------------------------
        private void Exportar()
        {
            if (_actual == null || _actual.Rows.Count == 0)
            {
                Dialogo.Info(Shell, "No hay datos para exportar en el reporte actual.");
                return;
            }
            using var sfd = new SaveFileDialog
            {
                Filter = "Archivo CSV (*.csv)|*.csv",
                FileName = $"Rhinos_{_grpReporte.Valor}_{_dtpDesde.Value:yyyyMMdd}_{_dtpHasta.Value:yyyyMMdd}.csv"
            };
            if (sfd.ShowDialog(Shell) != DialogResult.OK) return;

            ErroresUi.Intentar(Shell, () =>
            {
                var sb = new StringBuilder();
                var columnas = _actual.Columns.Cast<DataColumn>().ToList();
                sb.AppendLine(string.Join(",", columnas.Select(c => Csv(c.ColumnName))));
                foreach (DataRow r in _actual.Rows)
                    sb.AppendLine(string.Join(",", columnas.Select(c => Csv(Convert.ToString(r[c], System.Globalization.CultureInfo.InvariantCulture) ?? ""))));
                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                Dialogo.Exito(Shell, $"Se guardó el archivo:\n{sfd.FileName}", "Reporte exportado");
            }, "No se pudo exportar");
        }

        private static string Csv(string v) => v.Contains(',') || v.Contains('"') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
