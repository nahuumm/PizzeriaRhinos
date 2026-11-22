using System;
using System.Data;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Datos
{
    /// <summary>
    /// Consultas de seguimiento y reportes (corte de caja, liquidación de repartidores, ventas por cajera,
    /// productos más vendidos). Usan la vista vw_resumen_pedidos_corte y consultas multitabla con GROUP BY.
    /// </summary>
    public class ReporteDAO
    {
        private static DataTable Llenar(MySqlCommand cmd)
        {
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        private static void Rango(MySqlCommand cmd, DateTime desde, DateTime hasta)
        {
            cmd.Parameters.AddWithValue("@desde", desde.Date);
            cmd.Parameters.AddWithValue("@hasta", hasta.Date.AddDays(1)); // exclusivo: incluye todo el día "hasta"
        }

        public ResumenDia ObtenerResumenDelDia()
        {
            var r = new ResumenDia();
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();

            const string sqlPedidos = @"SELECT
                    COALESCE(SUM(CASE WHEN estado <> 'CANCELADO' THEN total ELSE 0 END), 0) AS ventas,
                    COALESCE(SUM(CASE WHEN estado <> 'CANCELADO' THEN 1 ELSE 0 END), 0) AS pedidos,
                    COALESCE(SUM(CASE WHEN estado = 'EN_PREPARACION' THEN 1 ELSE 0 END), 0) AS prep,
                    COALESCE(SUM(CASE WHEN estado = 'EN_RUTA' THEN 1 ELSE 0 END), 0) AS ruta,
                    COALESCE(SUM(CASE WHEN estado = 'ENTREGADO' THEN 1 ELSE 0 END), 0) AS entregados,
                    COALESCE(SUM(CASE WHEN estado = 'CANCELADO' THEN 1 ELSE 0 END), 0) AS cancelados
                FROM PEDIDO
                WHERE fecha_hora >= CURDATE() AND fecha_hora < CURDATE() + INTERVAL 1 DAY;";
            using (var cmd = new MySqlCommand(sqlPedidos, conn))
            using (var rd = cmd.ExecuteReader())
            {
                if (rd.Read())
                {
                    r.VentasHoy = Convert.ToDecimal(rd["ventas"]);
                    r.PedidosHoy = Convert.ToInt32(rd["pedidos"]);
                    r.EnPreparacion = Convert.ToInt32(rd["prep"]);
                    r.EnRuta = Convert.ToInt32(rd["ruta"]);
                    r.Entregados = Convert.ToInt32(rd["entregados"]);
                    r.Cancelados = Convert.ToInt32(rd["cancelados"]);
                }
            }

            const string sqlBebidas = @"SELECT COUNT(*) AS n,
                       COALESCE(GROUP_CONCAT(CONCAT(p.nombre, ' ', t.nombre, ' (', pp.stock_actual, ')')
                                ORDER BY pp.stock_actual SEPARATOR ', '), '') AS detalle
                FROM PRODUCTO_PRECIO pp
                INNER JOIN PRODUCTO p ON pp.id_producto = p.id_producto
                INNER JOIN TAMANO_PRESENTACION t ON pp.id_tamano = t.id_tamano
                WHERE p.categoria = 'BEBIDA' AND pp.stock_actual IS NOT NULL AND pp.stock_actual <= 5;";
            using (var cmd = new MySqlCommand(sqlBebidas, conn))
            using (var rd = cmd.ExecuteReader())
            {
                if (rd.Read())
                {
                    r.BebidasStockBajo = Convert.ToInt32(rd["n"]);
                    r.DetalleBebidasBajas = Convert.ToString(rd["detalle"]) ?? "";
                }
            }

            const string sqlIng = @"SELECT COUNT(*) AS n, COALESCE(GROUP_CONCAT(nombre ORDER BY nombre SEPARATOR ', '), '') AS detalle
                                    FROM INGREDIENTE WHERE disponible = FALSE;";
            using (var cmd = new MySqlCommand(sqlIng, conn))
            using (var rd = cmd.ExecuteReader())
            {
                if (rd.Read())
                {
                    r.IngredientesAgotados = Convert.ToInt32(rd["n"]);
                    r.DetalleIngredientesAgotados = Convert.ToString(rd["detalle"]) ?? "";
                }
            }
            return r;
        }

        /// <summary>Pedidos (vista vw_resumen_pedidos_corte) con filtros de fecha, servicio, estado y búsqueda.</summary>
        /// <param name="estado">Un estado, "" para todos o "ACTIVOS" (en preparación + en ruta).</param>
        public DataTable ObtenerPedidos(DateTime desde, DateTime hasta, string servicio, string estado, string busqueda)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT folio AS Folio, fecha_hora AS Fecha, tipo_servicio AS Servicio, estado AS Estado,
                                        cliente AS Cliente, telefono AS Teléfono, cajero_responsable AS Cajera,
                                        repartidor_asignado AS Repartidor, subtotal AS Subtotal, total_extras AS Extras, total AS Total
                                 FROM vw_resumen_pedidos_corte
                                 WHERE fecha_hora >= @desde AND fecha_hora < @hasta
                                   AND (@srv = '' OR tipo_servicio = @srv)
                                   AND (@est = '' OR estado = @est OR (@est = 'ACTIVOS' AND estado IN ('EN_PREPARACION', 'EN_RUTA')))
                                   AND (@q = '' OR cliente LIKE CONCAT('%', @q, '%') OR telefono LIKE CONCAT('%', @q, '%')
                                        OR CAST(folio AS CHAR) = @q)
                                 ORDER BY folio DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            Rango(cmd, desde, hasta);
            cmd.Parameters.AddWithValue("@srv", servicio ?? "");
            cmd.Parameters.AddWithValue("@est", estado ?? "");
            cmd.Parameters.AddWithValue("@q", (busqueda ?? "").Trim());
            return Llenar(cmd);
        }

        public CabeceraPedido? ObtenerCabecera(int folio)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT v.folio, v.fecha_hora, v.tipo_servicio, v.estado, v.cliente, v.telefono,
                                        v.cajero_responsable, v.repartidor_asignado, v.subtotal, v.total_extras, v.total,
                                        c.calle, c.numero_casa, c.colonia, c.entre_calles, c.referencias
                                 FROM vw_resumen_pedidos_corte v
                                 INNER JOIN PEDIDO p ON p.id_pedido = v.folio
                                 LEFT JOIN CLIENTE c ON c.id_cliente = p.id_cliente
                                 WHERE v.folio = @f;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@f", folio);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;

            string Txt(string col) => rd.IsDBNull(rd.GetOrdinal(col)) ? "" : Convert.ToString(rd[col]) ?? "";
            string direccion = Txt("calle").Length > 0 ? $"{Txt("calle")} #{Txt("numero_casa")}, {Txt("colonia")}" : "";

            return new CabeceraPedido
            {
                Folio = Convert.ToInt32(rd["folio"]),
                FechaHora = Convert.ToDateTime(rd["fecha_hora"]),
                TipoServicio = Txt("tipo_servicio"),
                Estado = Txt("estado"),
                Cliente = Txt("cliente"),
                Telefono = Txt("telefono"),
                Direccion = direccion,
                EntreCalles = Txt("entre_calles"),
                Referencias = Txt("referencias"),
                Cajera = Txt("cajero_responsable"),
                Repartidor = Txt("repartidor_asignado"),
                Subtotal = Convert.ToDecimal(rd["subtotal"]),
                Extras = Convert.ToDecimal(rd["total_extras"]),
                Total = Convert.ToDecimal(rd["total"])
            };
        }

        /// <summary>Liquidación por repartidor: entregas realizadas y efectivo a entregar en el corte.</summary>
        public DataTable LiquidacionRepartidores(DateTime desde, DateTime hasta)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT e.nombre AS Repartidor,
                                        COALESCE(SUM(CASE WHEN p.estado = 'ENTREGADO' THEN 1 ELSE 0 END), 0) AS Entregas,
                                        COALESCE(SUM(CASE WHEN p.estado = 'EN_RUTA' THEN 1 ELSE 0 END), 0) AS `En ruta`,
                                        COALESCE(SUM(CASE WHEN p.estado = 'ENTREGADO' THEN p.total ELSE 0 END), 0) AS `Efectivo a liquidar`
                                 FROM EMPLEADO e
                                 LEFT JOIN PEDIDO p ON p.id_repartidor = e.id_empleado
                                                   AND p.fecha_hora >= @desde AND p.fecha_hora < @hasta
                                 WHERE e.rol = 'REPARTIDOR'
                                 GROUP BY e.id_empleado, e.nombre
                                 ORDER BY `Efectivo a liquidar` DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            Rango(cmd, desde, hasta);
            return Llenar(cmd);
        }

        public DataTable VentasPorCajera(DateTime desde, DateTime hasta)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT e.nombre AS Cajera, COUNT(p.id_pedido) AS Pedidos,
                                        COALESCE(SUM(p.total), 0) AS `Monto vendido`,
                                        COALESCE(AVG(p.total), 0) AS `Ticket promedio`
                                 FROM EMPLEADO e
                                 INNER JOIN PEDIDO p ON p.id_cajero = e.id_empleado
                                 WHERE p.estado <> 'CANCELADO' AND p.fecha_hora >= @desde AND p.fecha_hora < @hasta
                                 GROUP BY e.id_empleado, e.nombre
                                 ORDER BY `Monto vendido` DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            Rango(cmd, desde, hasta);
            return Llenar(cmd);
        }

        public DataTable VentasPorServicio(DateTime desde, DateTime hasta)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT tipo_servicio AS Servicio, COUNT(*) AS Pedidos, COALESCE(SUM(total), 0) AS Ingreso
                                 FROM PEDIDO
                                 WHERE estado <> 'CANCELADO' AND fecha_hora >= @desde AND fecha_hora < @hasta
                                 GROUP BY tipo_servicio;";
            using var cmd = new MySqlCommand(sql, conn);
            Rango(cmd, desde, hasta);
            return Llenar(cmd);
        }

        /// <summary>Productos más vendidos en el periodo (excluye pedidos cancelados).</summary>
        public DataTable ProductosMasVendidos(DateTime desde, DateTime hasta)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT pr.nombre AS Producto, pr.categoria AS Categoría,
                                        SUM(dp.cantidad) AS Unidades, SUM(dp.subtotal_linea) AS Importe
                                 FROM DETALLE_PEDIDO dp
                                 INNER JOIN PEDIDO p ON dp.id_pedido = p.id_pedido
                                 INNER JOIN PRODUCTO_PRECIO pp ON dp.id_producto_precio = pp.id_producto_precio
                                 INNER JOIN PRODUCTO pr ON pp.id_producto = pr.id_producto
                                 WHERE p.estado <> 'CANCELADO' AND p.fecha_hora >= @desde AND p.fecha_hora < @hasta
                                 GROUP BY pr.id_producto, pr.nombre, pr.categoria
                                 ORDER BY Unidades DESC, Importe DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            Rango(cmd, desde, hasta);
            return Llenar(cmd);
        }
    }
}
