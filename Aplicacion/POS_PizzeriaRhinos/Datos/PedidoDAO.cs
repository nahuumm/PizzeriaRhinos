using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Datos
{
    public class PedidoDAO
    {
        private static readonly HashSet<string> EstadosValidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "EN_PREPARACION",
            "EN_RUTA",
            "ENTREGADO",
            "CANCELADO"
        };

        public DataTable ObtenerMenuActivo(string categoria = "", string tipoServicio = "DOMICILIO")
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = @"SELECT id_producto_precio, id_producto, producto, categoria, max_ingredientes_base,
                                  id_tamano, tamano_presentacion, aplica_promocion_2x1, variante, precio, stock_actual
                           FROM vw_menu_pos_activo
                           WHERE (@cat = '' OR categoria = @cat)
                             AND (variante <> 'MOSTRADOR' OR @srv = 'MOSTRADOR')
                           ORDER BY categoria, producto, id_tamano;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat", categoria);
            cmd.Parameters.AddWithValue("@srv", tipoServicio);
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public DataTable ObtenerIngredientesDisponibles()
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "SELECT id_ingrediente, nombre, disponible FROM INGREDIENTE ORDER BY nombre;";
            using var da = new MySqlDataAdapter(sql, conn);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public decimal CalcularCostoExtraIngrediente(int idProducto, int idTamano, int idIngrediente, int previosAgregados)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "SELECT fn_calcular_costo_extra_ingrediente(@prod, @tam, @ing, @prev);";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@prod", idProducto);
            cmd.Parameters.AddWithValue("@tam", idTamano);
            cmd.Parameters.AddWithValue("@ing", idIngrediente);
            cmd.Parameters.AddWithValue("@prev", previosAgregados);
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }

        public decimal ObtenerTarifaOpcionFinal(int idTamano, string tipoOpcion)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "SELECT COALESCE(precio_adicional, 0) FROM TARIFA_OPCION_FINAL WHERE id_tamano = @tam AND tipo_opcion = @opc LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tam", idTamano);
            cmd.Parameters.AddWithValue("@opc", tipoOpcion);
            object? res = cmd.ExecuteScalar();
            return res == null ? 0m : Convert.ToDecimal(res);
        }

        // =========================================================================
        // 1. DESGLOSE DE MODIFICADORES Y MITADES EN HISTORIAL
        // =========================================================================
        public DataTable ObtenerDetallePedido(int idPedido)
        {
            if (idPedido <= 0)
                throw new ArgumentException("El folio del pedido debe ser mayor a cero.", nameof(idPedido));

            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();

            string sql = @"
                SELECT 
                    dp.id_detalle AS ID_Detalle,
                    dp.cantidad AS Cantidad,
                    CASE 
                        WHEN dp.id_producto_mitad2 IS NOT NULL 
                            THEN CONCAT('Mitad 1: ', p1.nombre, ' / Mitad 2: ', p2.nombre)
                        ELSE p1.nombre
                    END AS Producto_Especialidad,
                    tp.nombre AS Tamaño,
                    p1.categoria AS Categoria,
                    pp.variante AS Variante,
                    COALESCE(dp.tipo_masa, 'GRUESA') AS Masa,
                    CASE WHEN dp.con_orilla_queso THEN 'Sí' ELSE 'No' END AS Orilla_Queso,
                    CASE WHEN dp.con_queso_extra THEN 'Sí' ELSE 'No' END AS Queso_Extra,
                    CASE 
                        WHEN dp.es_promocion_2x1 THEN CONCAT('Sí (Grupo #', COALESCE(dp.grupo_promo, 0), ')')
                        ELSE 'No'
                    END AS Promo_2x1,
                    COALESCE(
                        GROUP_CONCAT(
                            CONCAT('[', dm.tipo_accion, '] ', ing.nombre, ' (', dm.mitad_aplicada, ', +$', FORMAT(dm.costo_aplicado, 2), ')')
                            ORDER BY dm.id_modificador ASC
                            SEPARATOR ' | '
                        ),
                        'Sin modificaciones'
                    ) AS Modificadores_Ingredientes,
                    dp.subtotal_linea AS Subtotal_Linea
                FROM DETALLE_PEDIDO dp
                INNER JOIN PRODUCTO_PRECIO pp ON dp.id_producto_precio = pp.id_producto_precio
                INNER JOIN PRODUCTO p1 ON pp.id_producto = p1.id_producto
                INNER JOIN TAMANO_PRESENTACION tp ON pp.id_tamano = tp.id_tamano
                LEFT JOIN PRODUCTO p2 ON dp.id_producto_mitad2 = p2.id_producto
                LEFT JOIN DETALLE_MODIFICADOR dm ON dp.id_detalle = dm.id_detalle
                LEFT JOIN INGREDIENTE ing ON dm.id_ingrediente = ing.id_ingrediente
                WHERE dp.id_pedido = @idPedido
                GROUP BY 
                    dp.id_detalle, dp.cantidad, dp.id_producto_mitad2, p1.nombre, p2.nombre,
                    tp.nombre, p1.categoria, pp.variante, dp.tipo_masa, dp.con_orilla_queso, dp.con_queso_extra,
                    dp.es_promocion_2x1, dp.grupo_promo, dp.subtotal_linea
                ORDER BY dp.id_detalle ASC;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idPedido", idPedido);
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        // =========================================================================
        // 2. CANCELACIÓN Y CAMBIO DE ESTADO (CON RESTAURACIÓN DE STOCK DE BEBIDAS)
        // =========================================================================
        public void ActualizarEstadoPedido(int idPedido, string nuevoEstado)
        {
            if (idPedido <= 0)
                throw new ArgumentException("El folio del pedido no es válido.", nameof(idPedido));

            if (string.IsNullOrWhiteSpace(nuevoEstado))
                throw new ArgumentException("Debes indicar el nuevo estado del pedido.", nameof(nuevoEstado));

            string estadoNormalizado = nuevoEstado.Trim().ToUpperInvariant();
            if (!EstadosValidos.Contains(estadoNormalizado))
            {
                throw new ArgumentException(
                    $"Estado '{nuevoEstado}' no válido. Valores permitidos: EN_PREPARACION, EN_RUTA, ENTREGADO, CANCELADO.",
                    nameof(nuevoEstado));
            }

            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                string? estadoActual = null;
                string? tipoServicio = null;
                int? idRepartidor = null;

                string sqlSelect = @"SELECT estado, tipo_servicio, id_repartidor 
                                     FROM PEDIDO 
                                     WHERE id_pedido = @id 
                                     FOR UPDATE;";
                using (var cmdSel = new MySqlCommand(sqlSelect, conn, tx))
                {
                    cmdSel.Parameters.AddWithValue("@id", idPedido);
                    using var rd = cmdSel.ExecuteReader();
                    if (rd.Read())
                    {
                        estadoActual = rd.GetString("estado").ToUpperInvariant();
                        tipoServicio = rd.GetString("tipo_servicio").ToUpperInvariant();
                        idRepartidor = rd.IsDBNull(rd.GetOrdinal("id_repartidor"))
                            ? null
                            : rd.GetInt32("id_repartidor");
                    }
                }

                if (estadoActual == null)
                    throw new InvalidOperationException($"El pedido con folio #{idPedido} no existe.");

                if (estadoActual == estadoNormalizado)
                    throw new InvalidOperationException($"El pedido #{idPedido} ya se encuentra en estado '{estadoNormalizado}'.");

                if (estadoActual == "CANCELADO")
                    throw new InvalidOperationException($"El pedido #{idPedido} ya está CANCELADO y no puede cambiar de estado.");

                if (estadoActual == "ENTREGADO")
                    throw new InvalidOperationException($"El pedido #{idPedido} ya fue ENTREGADO y no puede modificarse ni cancelarse.");

                if (estadoNormalizado == "EN_RUTA" && tipoServicio != "DOMICILIO")
                    throw new InvalidOperationException("Solo los pedidos a DOMICILIO pueden pasar al estado EN_RUTA.");

                if (estadoNormalizado == "EN_RUTA" && !idRepartidor.HasValue)
                    throw new InvalidOperationException("Asigna un repartidor antes de marcar el pedido como EN_RUTA.");

                string sqlUpdateEstado = "UPDATE PEDIDO SET estado = @estado WHERE id_pedido = @id;";
                using (var cmdUpd = new MySqlCommand(sqlUpdateEstado, conn, tx))
                {
                    cmdUpd.Parameters.AddWithValue("@estado", estadoNormalizado);
                    cmdUpd.Parameters.AddWithValue("@id", idPedido);
                    cmdUpd.ExecuteNonQuery();
                }

                if (estadoNormalizado == "CANCELADO")
                {
                    string sqlRestaurarStock = @"
                        UPDATE PRODUCTO_PRECIO pp
                        INNER JOIN (
                            SELECT id_producto_precio, SUM(cantidad) AS unidades_devueltas
                            FROM DETALLE_PEDIDO
                            WHERE id_pedido = @id
                            GROUP BY id_producto_precio
                        ) det ON pp.id_producto_precio = det.id_producto_precio
                        SET pp.stock_actual = pp.stock_actual + det.unidades_devueltas
                        WHERE pp.stock_actual IS NOT NULL;";

                    using var cmdStock = new MySqlCommand(sqlRestaurarStock, conn, tx);
                    cmdStock.Parameters.AddWithValue("@id", idPedido);
                    cmdStock.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =========================================================================
        // 3. VALIDACIONES DE NEGOCIO EXTRA (PIZZA_MOSTRADOR, MITADES, 2X1, SERVICIO)
        // =========================================================================
        public void ValidarPedidoNegocio(
            MySqlConnection conn,
            MySqlTransaction? tx,
            int? idCliente,
            int idCajero,
            int? idRepartidor,
            string tipoServicio,
            List<ItemCarrito> items)
        {
            if (items == null || items.Count == 0)
                throw new InvalidOperationException("El pedido debe contener al menos una partida.");

            string srv = (tipoServicio ?? string.Empty).Trim().ToUpperInvariant();
            if (srv != "DOMICILIO" && srv != "MOSTRADOR")
                throw new InvalidOperationException("Tipo de servicio inválido. Debe ser DOMICILIO o MOSTRADOR.");

            if (srv == "DOMICILIO" && !idCliente.HasValue)
                throw new InvalidOperationException("Regla de negocio: Todo pedido a DOMICILIO requiere un cliente registrado.");

            if (srv == "MOSTRADOR" && idRepartidor.HasValue)
                throw new InvalidOperationException("Regla de negocio: Un pedido de MOSTRADOR no debe tener repartidor asignado.");

            string sqlCajero = "SELECT rol FROM EMPLEADO WHERE id_empleado = @id AND activo = TRUE LIMIT 1;";
            using (var cmdCaj = new MySqlCommand(sqlCajero, conn, tx))
            {
                cmdCaj.Parameters.AddWithValue("@id", idCajero);
                string? rol = cmdCaj.ExecuteScalar()?.ToString();
                if (rol != "CAJERO" && rol != "ADMIN")
                    throw new InvalidOperationException("El empleado que registra el pedido no tiene rol activo de CAJERO o ADMIN.");
            }

            if (idRepartidor.HasValue)
            {
                string sqlRep = "SELECT rol FROM EMPLEADO WHERE id_empleado = @id AND activo = TRUE LIMIT 1;";
                using var cmdRep = new MySqlCommand(sqlRep, conn, tx);
                cmdRep.Parameters.AddWithValue("@id", idRepartidor.Value);
                string? rolRep = cmdRep.ExecuteScalar()?.ToString();
                if (rolRep != "REPARTIDOR")
                    throw new InvalidOperationException("El repartidor seleccionado no es válido o está inactivo.");
            }

            foreach (var item in items)
            {
                if (item.Cantidad <= 0)
                    throw new InvalidOperationException($"La cantidad para '{item.NombreProducto}' debe ser mayor a cero.");

                string sqlProdInfo = @"
                    SELECT p.id_producto, p.nombre, p.categoria, p.max_ingredientes_base, p.disponible,
                           pp.variante, t.aplica_promocion_2x1,
                           (SELECT COUNT(*) FROM RECETA_PRODUCTO rp WHERE rp.id_producto = p.id_producto) AS ingredientes_receta
                    FROM PRODUCTO_PRECIO pp
                    INNER JOIN PRODUCTO p ON pp.id_producto = p.id_producto
                    INNER JOIN TAMANO_PRESENTACION t ON pp.id_tamano = t.id_tamano
                    WHERE pp.id_producto_precio = @idPp
                    LIMIT 1;";

                string nombreBd;
                string categoriaBd;
                int maxIngBaseBd;
                bool disponibleBd;
                string varianteBd;
                bool aplica2x1Bd;
                int ingredientesRecetaBd;

                using (var cmdInfo = new MySqlCommand(sqlProdInfo, conn, tx))
                {
                    cmdInfo.Parameters.AddWithValue("@idPp", item.IdProductoPrecio);
                    using var rd = cmdInfo.ExecuteReader();
                    if (!rd.Read())
                        throw new InvalidOperationException($"La tarifa seleccionada (id_producto_precio={item.IdProductoPrecio}) no existe.");

                    nombreBd = rd.GetString("nombre");
                    categoriaBd = rd.GetString("categoria").ToUpperInvariant();
                    maxIngBaseBd = rd.GetInt32("max_ingredientes_base");
                    disponibleBd = rd.GetBoolean("disponible");
                    varianteBd = rd.GetString("variante").ToUpperInvariant();
                    aplica2x1Bd = rd.GetBoolean("aplica_promocion_2x1");
                    ingredientesRecetaBd = rd.GetInt32("ingredientes_receta");
                }

                if (!disponibleBd)
                    throw new InvalidOperationException($"El producto '{nombreBd}' se encuentra desactivado.");

                if (categoriaBd == "PIZZA_MOSTRADOR" || varianteBd == "MOSTRADOR")
                {
                    if (srv != "MOSTRADOR")
                    {
                        throw new InvalidOperationException(
                            $"Regla de negocio: '{nombreBd}' es exclusiva para pedidos en MOSTRADOR.");
                    }

                    if (item.IdProductoMitad2.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Regla de negocio: En PIZZA_MOSTRADOR ('{nombreBd}') no se permiten combinaciones de mitades (id_producto_mitad2 debe ser nulo).");
                    }

                    int conAgregados = item.Modificadores.Count(m =>
                        string.Equals(m.TipoAccion, "CON", StringComparison.OrdinalIgnoreCase));
                    int sinQuitados = item.Modificadores.Count(m =>
                        string.Equals(m.TipoAccion, "SIN", StringComparison.OrdinalIgnoreCase));

                    int totalIngredientesEfectivos = Math.Max(0, ingredientesRecetaBd - sinQuitados) + conAgregados;

                    if (maxIngBaseBd > 1 || ingredientesRecetaBd > 1 || totalIngredientesEfectivos > 1 || conAgregados > 0)
                    {
                        throw new InvalidOperationException(
                            $"Regla de negocio: En PIZZA_MOSTRADOR ('{nombreBd}') solo se permite 1 ingrediente base y no admite ingredientes adicionales.");
                    }
                }

                if (item.IdProductoMitad2.HasValue)
                {
                    if (categoriaBd != "PIZZA")
                    {
                        throw new InvalidOperationException(
                            "Regla de negocio: Solo las especialidades de categoría PIZZA permiten combinación mitad y mitad.");
                    }

                    string sqlMitad2 = "SELECT categoria, disponible FROM PRODUCTO WHERE id_producto = @m2 LIMIT 1;";
                    using var cmdM2 = new MySqlCommand(sqlMitad2, conn, tx);
                    cmdM2.Parameters.AddWithValue("@m2", item.IdProductoMitad2.Value);
                    using var rdM2 = cmdM2.ExecuteReader();
                    if (!rdM2.Read())
                        throw new InvalidOperationException("La especialidad seleccionada para Mitad 2 no existe.");

                    string catM2 = rdM2.GetString("categoria").ToUpperInvariant();
                    bool dispM2 = rdM2.GetBoolean("disponible");

                    if (!dispM2 || catM2 != "PIZZA")
                        throw new InvalidOperationException("La especialidad de Mitad 2 debe ser una PIZZA activa en el menú.");
                }
                else
                {
                    if (item.Modificadores.Any(m => string.Equals(m.MitadAplicada, "MITAD_2", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException(
                            $"No se puede aplicar un modificador a 'MITAD_2' en '{nombreBd}' porque la pizza no está dividida en mitades.");
                    }
                }

                if (item.EsPromocion2x1 && !aplica2x1Bd)
                {
                    throw new InvalidOperationException(
                        $"Regla de negocio: '{nombreBd}' no aplica para promoción 2x1 en el tamaño seleccionado.");
                }

                if (categoriaBd == "BEBIDA" || categoriaBd == "COMPLEMENTO")
                {
                    if (item.Modificadores.Count > 0 || item.ConOrillaQueso || item.ConQuesoExtra)
                    {
                        throw new InvalidOperationException(
                            $"El producto '{nombreBd}' ({categoriaBd}) no admite modificadores de ingredientes ni opciones de orilla/queso extra.");
                    }
                }
            }
        }

        public int RegistrarPedidoCompleto(int? idCliente, int idCajero, int? idRepartidor, string tipoServicio, List<ItemCarrito> items)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                ValidarPedidoNegocio(conn, tx, idCliente, idCajero, idRepartidor, tipoServicio, items);

                decimal subtotalTotal = 0m;
                decimal extrasTotal = 0m;
                foreach (var it in items)
                {
                    subtotalTotal += it.PrecioBase * it.Cantidad;
                    extrasTotal += it.CostoExtras * it.Cantidad;
                }
                decimal granTotal = subtotalTotal + extrasTotal;

                string sqlCab = @"INSERT INTO PEDIDO (id_cliente, id_cajero, id_repartidor, fecha_hora, tipo_servicio, subtotal, total_extras, total, estado)
                                  VALUES (@cli, @caj, @rep, NOW(), @srv, @sub, @ext, @tot, 'EN_PREPARACION');
                                  SELECT LAST_INSERT_ID();";
                int idPedido;
                using (var cmdCab = new MySqlCommand(sqlCab, conn, tx))
                {
                    cmdCab.Parameters.AddWithValue("@cli", idCliente.HasValue ? idCliente.Value : DBNull.Value);
                    cmdCab.Parameters.AddWithValue("@caj", idCajero);
                    cmdCab.Parameters.AddWithValue("@rep", idRepartidor.HasValue ? idRepartidor.Value : DBNull.Value);
                    cmdCab.Parameters.AddWithValue("@srv", tipoServicio.Trim().ToUpperInvariant());
                    cmdCab.Parameters.AddWithValue("@sub", subtotalTotal);
                    cmdCab.Parameters.AddWithValue("@ext", extrasTotal);
                    cmdCab.Parameters.AddWithValue("@tot", granTotal);
                    idPedido = Convert.ToInt32(cmdCab.ExecuteScalar());
                }

                foreach (var item in items)
                {
                    string sqlDet = @"INSERT INTO DETALLE_PEDIDO 
                                      (id_pedido, id_producto_precio, id_producto_mitad2, cantidad, tipo_masa, 
                                       con_orilla_queso, con_queso_extra, es_promocion_2x1, grupo_promo, subtotal_linea)
                                      VALUES (@ped, @pp, @m2, @cant, @masa, @orilla, @qextra, @p2x1, @gpromo, @subl);
                                      SELECT LAST_INSERT_ID();";
                    int idDetalle;
                    using (var cmdDet = new MySqlCommand(sqlDet, conn, tx))
                    {
                        cmdDet.Parameters.AddWithValue("@ped", idPedido);
                        cmdDet.Parameters.AddWithValue("@pp", item.IdProductoPrecio);
                        cmdDet.Parameters.AddWithValue("@m2", item.IdProductoMitad2.HasValue ? item.IdProductoMitad2.Value : DBNull.Value);
                        cmdDet.Parameters.AddWithValue("@cant", item.Cantidad);
                        cmdDet.Parameters.AddWithValue("@masa", item.TipoMasa);
                        cmdDet.Parameters.AddWithValue("@orilla", item.ConOrillaQueso);
                        cmdDet.Parameters.AddWithValue("@qextra", item.ConQuesoExtra);
                        cmdDet.Parameters.AddWithValue("@p2x1", item.EsPromocion2x1);
                        cmdDet.Parameters.AddWithValue("@gpromo", item.GrupoPromo.HasValue ? item.GrupoPromo.Value : DBNull.Value);
                        cmdDet.Parameters.AddWithValue("@subl", item.SubtotalLinea);
                        idDetalle = Convert.ToInt32(cmdDet.ExecuteScalar());
                    }

                    foreach (var mod in item.Modificadores)
                    {
                        string sqlMod = @"INSERT INTO DETALLE_MODIFICADOR (id_detalle, id_ingrediente, tipo_accion, mitad_aplicada, costo_aplicado)
                                          VALUES (@det, @ing, @acc, @mit, @costo);";
                        using var cmdMod = new MySqlCommand(sqlMod, conn, tx);
                        cmdMod.Parameters.AddWithValue("@det", idDetalle);
                        cmdMod.Parameters.AddWithValue("@ing", mod.IdIngrediente);
                        cmdMod.Parameters.AddWithValue("@acc", mod.TipoAccion.Trim().ToUpperInvariant());
                        cmdMod.Parameters.AddWithValue("@mit", mod.MitadAplicada.Trim().ToUpperInvariant());
                        cmdMod.Parameters.AddWithValue("@costo", mod.CostoAplicado);
                        cmdMod.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return idPedido;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public DataTable ObtenerReporteCorte(string filtroServicio = "", string filtroEstado = "")
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = @"SELECT folio AS Folio, fecha_hora AS Fecha, tipo_servicio AS Servicio, estado AS Estado,
                                  cliente AS Cliente, telefono AS Teléfono, cajero_responsable AS Cajera,
                                  repartidor_asignado AS Repartidor, subtotal AS Subtotal, total_extras AS Extras, total AS Total
                           FROM vw_resumen_pedidos_corte
                           WHERE (@srv = '' OR tipo_servicio = @srv)
                             AND (@est = '' OR estado = @est)
                           ORDER BY folio DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@srv", filtroServicio);
            cmd.Parameters.AddWithValue("@est", filtroEstado);
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public void AsignarRepartidorSP(int idPedido, int idRepartidor)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var cmd = new MySqlCommand("sp_asignar_repartidor_pedido", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("p_id_pedido", idPedido);
            cmd.Parameters.AddWithValue("p_id_repartidor", idRepartidor);
            cmd.ExecuteNonQuery();
        }

        public void ActualizarStockBebida(int idProductoPrecio, int nuevoStock)
        {
            if (nuevoStock < 0)
                throw new ArgumentOutOfRangeException(nameof(nuevoStock), "El stock no puede ser negativo.");

            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "UPDATE PRODUCTO_PRECIO SET stock_actual = @stk WHERE id_producto_precio = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@stk", nuevoStock);
            cmd.Parameters.AddWithValue("@id", idProductoPrecio);
            cmd.ExecuteNonQuery();
        }

        public void CambiarDisponibilidadIngrediente(int idIngrediente, bool disponible)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "UPDATE INGREDIENTE SET disponible = @disp WHERE id_ingrediente = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@disp", disponible);
            cmd.Parameters.AddWithValue("@id", idIngrediente);
            cmd.ExecuteNonQuery();
        }
    }
}