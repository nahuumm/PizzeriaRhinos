using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>Tablero de inicio: indicadores del día, accesos grandes (boceto pág. 2) y avisos del turno.</summary>
    [DesignerCategory("Code")]
    public class VistaDashboard : VistaBase
    {
        private readonly ReporteDAO _reporteDao = new();
        private readonly TarjetaKpi _kpiVentas, _kpiPedidos, _kpiPreparacion, _kpiRuta;
        private readonly Label _lblAvisos;
        private readonly EncabezadoModulo _encabezado;

        public VistaDashboard(FrmPrincipal shell) : base(shell)
        {
            _encabezado = new EncabezadoModulo($"¡Hola, {Sesion.NombreCorto}!", "", "👋");

            // ---------------- Indicadores ----------------
            _kpiVentas = new TarjetaKpi("Ventas de hoy", "💵", UITheme.Verde) { Dock = DockStyle.Fill };
            _kpiPedidos = new TarjetaKpi("Pedidos de hoy", "🧾", UITheme.Rojo) { Dock = DockStyle.Fill };
            _kpiPreparacion = new TarjetaKpi("En preparación", "🔥", UITheme.Ambar) { Dock = DockStyle.Fill };
            _kpiRuta = new TarjetaKpi("En ruta", "🛵", UITheme.Azul) { Dock = DockStyle.Fill };
            var filaKpi = new TableLayoutPanel { Height = UITheme.S(112), ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++) filaKpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            filaKpi.Controls.Add(_kpiVentas, 0, 0);
            filaKpi.Controls.Add(_kpiPedidos, 1, 0);
            filaKpi.Controls.Add(_kpiPreparacion, 2, 0);
            filaKpi.Controls.Add(_kpiRuta, 3, 0);

            // ---------------- Accesos grandes ----------------
            var accesos = new List<(string texto, string icono, string sub, string clave, EstiloBoton estilo)>
            {
                ("NUEVO PEDIDO", "🍕", "Domicilio o mostrador", "venta", EstiloBoton.Primario),
                ("PEDIDOS", "📋", "Seguimiento, despacho y comandas", "pedidos", EstiloBoton.Secundario),
                ("CLIENTES", "👥", "Alta, búsqueda e historial", "clientes", EstiloBoton.Secundario),
            };
            if (Sesion.EsAdmin)
            {
                accesos.Add(("INVENTARIO", "📦", "Bebidas, ingredientes y precios", "inventario", EstiloBoton.Secundario));
                accesos.Add(("EMPLEADOS", "🪪", "Cajeras, repartidores y accesos", "empleados", EstiloBoton.Secundario));
                accesos.Add(("REPORTES", "📊", "Corte de caja y liquidación", "reportes", EstiloBoton.Secundario));
            }

            int columnas = 3;
            int filas = (accesos.Count + columnas - 1) / columnas;
            var gridAccesos = new TableLayoutPanel { ColumnCount = columnas, RowCount = filas, Margin = new Padding(0) };
            for (int c = 0; c < columnas; c++) gridAccesos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
            for (int f = 0; f < filas; f++) gridAccesos.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / filas));
            for (int i = 0; i < accesos.Count; i++)
            {
                var a = accesos[i];
                var b = new BotonRhino(a.texto, a.estilo, a.icono)
                {
                    Disposicion = DisposicionBoton.Mosaico,
                    Subtexto = a.sub,
                    TamanoIcono = 34f,
                    Font = UITheme.Titulo(18f),
                    Dock = DockStyle.Fill,
                    Margin = UITheme.P(8),
                    Radio = UITheme.S(20)
                };
                string clave = a.clave;
                b.Click += (_, _) => Shell.Navegar(clave);
                gridAccesos.Controls.Add(b, i % columnas, i / columnas);
            }

            // ---------------- Avisos ----------------
            var cardAvisos = new PanelRedondeado { Padding = UITheme.P(18), Margin = UITheme.P(8) };
            var lblTituloAvisos = new Label { Text = "🔔  Avisos del turno", Font = UITheme.Titulo(16f), ForeColor = UITheme.Texto, AutoSize = false, Height = UITheme.S(36) };
            _lblAvisos = new Label { Font = UITheme.Fuente(11f), ForeColor = UITheme.Texto, AutoSize = false };
            var btnActualizar = new BotonRhino("Actualizar", EstiloBoton.Secundario, "⟳") { Dock = DockStyle.Bottom, Height = UITheme.S(50) };
            btnActualizar.Click += (_, _) => ErroresUi.Intentar(Shell, AlMostrar, "No se pudo actualizar");
            // Importante: el control Fill se agrega primero y los acoplados (Top/Bottom) después
            Diseno.Apilar(cardAvisos, _lblAvisos, lblTituloAvisos, Diseno.Espacio(6));
            cardAvisos.Controls.Add(btnActualizar);

            var cuerpo = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            gridAccesos.Dock = DockStyle.Fill;
            cardAvisos.Dock = DockStyle.Fill;
            cuerpo.Controls.Add(gridAccesos, 0, 0);
            cuerpo.Controls.Add(cardAvisos, 1, 0);

            Diseno.Apilar(this, cuerpo, _encabezado, Diseno.Espacio(14), filaKpi, Diseno.Espacio(8));
        }

        public override void AlMostrar()
        {
            _encabezado.Subtitulo = $"{DateTime.Now.ToString("dddd d 'de' MMMM 'de' yyyy", UITheme.Cultura)} · " +
                                    (Sesion.EsAdmin ? "Sesión de administrador" : "Turno de caja");
            ResumenDia r = _reporteDao.ObtenerResumenDelDia();
            _kpiVentas.Valor = UITheme.Dinero(r.VentasHoy);
            _kpiPedidos.Valor = r.PedidosHoy.ToString();
            _kpiPreparacion.Valor = r.EnPreparacion.ToString();
            _kpiRuta.Valor = r.EnRuta.ToString();

            var avisos = new List<string>();
            if (r.EnPreparacion > 0) avisos.Add($"🔥 {r.EnPreparacion} pedido(s) en preparación. Despacha los de domicilio desde 'Pedidos'.");
            if (r.EnRuta > 0) avisos.Add($"🛵 {r.EnRuta} pedido(s) en ruta. Márcalos como entregados al regresar el repartidor.");
            if (r.BebidasStockBajo > 0) avisos.Add($"🥤 Bebidas con 5 piezas o menos: {r.DetalleBebidasBajas}.");
            if (r.IngredientesAgotados > 0) avisos.Add($"🧂 Ingredientes marcados como NO HAY: {r.DetalleIngredientesAgotados}.");
            if (r.Cancelados > 0) avisos.Add($"✖ {r.Cancelados} pedido(s) cancelado(s) hoy.");
            if (avisos.Count == 0) avisos.Add("✔ Todo en orden. No hay pendientes por ahora.");
            _lblAvisos.Text = string.Join("\n\n", avisos);
        }
    }
}
