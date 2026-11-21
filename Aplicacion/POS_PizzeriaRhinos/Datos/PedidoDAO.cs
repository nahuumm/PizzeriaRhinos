using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Datos
{
    public class PedidoDAO
    {
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

        // Registra el pedido completo afectando PEDIDO, DETALLE_PEDIDO, DETALLE_MODIFICADOR y PRODUCTO_PRECIO (vía Trigger)
        public int RegistrarPedidoCompleto(int? idCliente, int idCajero, int? idRepartidor, string tipoServicio, List<ItemCarrito> items)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
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
                    cmdCab.Parameters.AddWithValue("@srv", tipoServicio);
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
                        cmdMod.Parameters.AddWithValue("@acc", mod.TipoAccion);
                        cmdMod.Parameters.AddWithValue("@mit", mod.MitadAplicada);
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
