using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Datos
{
    /// <summary>
    /// Consultas de catálogo que necesita la interfaz (recetas, bebidas con stock, tarifas de fase final
    /// y administración de precios/disponibilidad).
    /// </summary>
    public class ProductoDAO
    {
        /// <summary>Ingredientes de la receta base de un producto (RECETA_PRODUCTO ⋈ INGREDIENTE).</summary>
        public DataTable ObtenerReceta(int idProducto)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT i.id_ingrediente, i.nombre, i.disponible
                                 FROM RECETA_PRODUCTO rp
                                 INNER JOIN INGREDIENTE i ON rp.id_ingrediente = i.id_ingrediente
                                 WHERE rp.id_producto = @id
                                 ORDER BY i.nombre;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idProducto);
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        /// <summary>Texto de la receta de cada producto (para mostrarlo en las tarjetas del menú).</summary>
        public Dictionary<int, string> ObtenerRecetasTexto()
        {
            var dic = new Dictionary<int, string>();
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT rp.id_producto, GROUP_CONCAT(i.nombre ORDER BY i.nombre SEPARATOR ', ') AS receta
                                 FROM RECETA_PRODUCTO rp
                                 INNER JOIN INGREDIENTE i ON rp.id_ingrediente = i.id_ingrediente
                                 GROUP BY rp.id_producto;";
            using var cmd = new MySqlCommand(sql, conn);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                dic[rd.GetInt32("id_producto")] = rd.IsDBNull(rd.GetOrdinal("receta")) ? "" : rd.GetString("receta");
            }
            return dic;
        }

        /// <summary>
        /// Bebidas con su stock, INCLUYENDO las agotadas (stock 0), para mostrar el botón deshabilitado
        /// (Regla de negocio 7). La vista vw_menu_pos_activo las oculta, por eso se consulta aparte.
        /// </summary>
        public DataTable ObtenerBebidasConStock()
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT pp.id_producto_precio, p.id_producto, p.nombre AS producto, p.categoria,
                                        t.id_tamano, t.nombre AS tamano_presentacion, t.aplica_promocion_2x1,
                                        pp.variante, pp.precio, pp.stock_actual, p.disponible
                                 FROM PRODUCTO_PRECIO pp
                                 INNER JOIN PRODUCTO p ON pp.id_producto = p.id_producto
                                 INNER JOIN TAMANO_PRESENTACION t ON pp.id_tamano = t.id_tamano
                                 WHERE p.categoria = 'BEBIDA'
                                 ORDER BY p.nombre, t.id_tamano;";
            using var da = new MySqlDataAdapter(sql, conn);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        /// <summary>Tarifas de fase final (ORILLA_QUESO, QUESO_EXTRA, MASA_SARTEN) para un tamaño.</summary>
        public Dictionary<string, decimal> ObtenerTarifasOpcionFinal(int idTamano)
        {
            var dic = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["ORILLA_QUESO"] = 0m,
                ["QUESO_EXTRA"] = 0m,
                ["MASA_SARTEN"] = 0m
            };
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "SELECT tipo_opcion, precio_adicional FROM TARIFA_OPCION_FINAL WHERE id_tamano = @tam;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tam", idTamano);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                dic[rd.GetString("tipo_opcion")] = rd.GetDecimal("precio_adicional");
            }
            return dic;
        }

        /// <summary>Catálogo completo para el administrador (incluye productos desactivados).</summary>
        public DataTable ListarCatalogoAdmin(string categoria = "", string filtro = "")
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT pp.id_producto_precio, p.id_producto,
                                        p.nombre AS Producto, p.categoria AS Categoría, t.nombre AS Tamaño,
                                        pp.variante AS Variante, pp.precio AS Precio, pp.stock_actual AS Stock,
                                        p.disponible AS Disponible
                                 FROM PRODUCTO_PRECIO pp
                                 INNER JOIN PRODUCTO p ON pp.id_producto = p.id_producto
                                 INNER JOIN TAMANO_PRESENTACION t ON pp.id_tamano = t.id_tamano
                                 WHERE (@cat = '' OR p.categoria = @cat)
                                   AND (@f = '' OR p.nombre LIKE CONCAT('%', @f, '%'))
                                 ORDER BY p.categoria, p.nombre, t.id_tamano;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat", categoria ?? "");
            cmd.Parameters.AddWithValue("@f", (filtro ?? "").Trim());
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            foreach (DataRow r in dt.Rows)
                r["Tamaño"] = POS_PizzeriaRhinos.Formularios.UITheme.NombreTamano(Convert.ToString(r["Categoría"]), Convert.ToString(r["Tamaño"]));
            return dt;
        }

        public void ActualizarPrecio(int idProductoPrecio, decimal precio)
        {
            if (precio < 0) throw new ArgumentOutOfRangeException(nameof(precio), "El precio no puede ser negativo.");
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "UPDATE PRODUCTO_PRECIO SET precio = @precio WHERE id_producto_precio = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@precio", precio);
            cmd.Parameters.AddWithValue("@id", idProductoPrecio);
            cmd.ExecuteNonQuery();
        }

        public void CambiarDisponibilidadProducto(int idProducto, bool disponible)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "UPDATE PRODUCTO SET disponible = @disp WHERE id_producto = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@disp", disponible);
            cmd.Parameters.AddWithValue("@id", idProducto);
            cmd.ExecuteNonQuery();
        }

        // =========================================================================
        // ALTA Y EDICIÓN DE PRODUCTOS (administrador)
        // =========================================================================

        /// <summary>Tamaños que se ofrecen al capturar precios según la categoría del producto.</summary>
        public List<PrecioTamano> ObtenerTamanosParaCategoria(string categoria)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = categoria switch
            {
                // Calzone: Chico, Mediano, Grande y Extra grande (mismos registros que Chica..Extragrande)
                "CALZONE" => @"SELECT id_tamano, nombre FROM TAMANO_PRESENTACION
                               WHERE UPPER(nombre) IN ('CHICA', 'MEDIANA', 'GRANDE', 'EXTRAGRANDE') ORDER BY id_tamano;",
                // Resto: los tamaños que ya usa esa categoría en el catálogo
                _ => @"SELECT DISTINCT t.id_tamano, t.nombre
                       FROM TAMANO_PRESENTACION t
                       INNER JOIN PRODUCTO_PRECIO pp ON pp.id_tamano = t.id_tamano
                       INNER JOIN PRODUCTO p ON p.id_producto = pp.id_producto
                       WHERE p.categoria = @cat AND pp.variante <> 'MOSTRADOR'
                       ORDER BY t.id_tamano;"
            };
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat", categoria);
            var lista = new List<PrecioTamano>();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                lista.Add(new PrecioTamano
                {
                    IdTamano = rd.GetInt32("id_tamano"),
                    NombreTamano = POS_PizzeriaRhinos.Formularios.UITheme.NombreTamano(categoria, rd.GetString("nombre"))
                });
            }
            return lista;
        }

        /// <summary>Carga un producto con sus precios (variante NORMAL) y su receta para editarlo.</summary>
        public ProductoEdicion? ObtenerProductoEdicion(int idProducto)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            ProductoEdicion? p = null;
            using (var cmd = new MySqlCommand("SELECT id_producto, nombre, categoria, max_ingredientes_base, disponible FROM PRODUCTO WHERE id_producto = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", idProducto);
                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    p = new ProductoEdicion
                    {
                        IdProducto = rd.GetInt32("id_producto"),
                        Nombre = rd.GetString("nombre"),
                        Categoria = rd.GetString("categoria"),
                        MaxIngredientesBase = rd.GetInt32("max_ingredientes_base"),
                        Disponible = rd.GetBoolean("disponible")
                    };
                }
            }
            if (p == null) return null;

            using (var cmd = new MySqlCommand(@"SELECT pp.id_tamano, t.nombre, pp.precio, pp.stock_actual
                                                FROM PRODUCTO_PRECIO pp INNER JOIN TAMANO_PRESENTACION t ON t.id_tamano = pp.id_tamano
                                                WHERE pp.id_producto = @id AND pp.variante = @var ORDER BY pp.id_tamano;", conn))
            {
                cmd.Parameters.AddWithValue("@id", idProducto);
                cmd.Parameters.AddWithValue("@var", p.Categoria == "PIZZA_MOSTRADOR" ? "MOSTRADOR" : "NORMAL");
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    p.Precios.Add(new PrecioTamano
                    {
                        IdTamano = rd.GetInt32("id_tamano"),
                        NombreTamano = POS_PizzeriaRhinos.Formularios.UITheme.NombreTamano(p.Categoria, rd.GetString("nombre")),
                        Precio = rd.GetDecimal("precio"),
                        Stock = rd.IsDBNull(rd.GetOrdinal("stock_actual")) ? null : rd.GetInt32("stock_actual")
                    });
                }
            }

            using (var cmd = new MySqlCommand("SELECT id_ingrediente FROM RECETA_PRODUCTO WHERE id_producto = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", idProducto);
                using var rd = cmd.ExecuteReader();
                while (rd.Read()) p.Receta.Add(rd.GetInt32("id_ingrediente"));
            }
            return p;
        }

        /// <summary>¿Ya existe otro producto con ese nombre en la misma categoría? (sin distinguir mayúsculas)</summary>
        public bool ExisteNombre(string nombre, string categoria, int idExcluir)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT COUNT(*) FROM PRODUCTO
                                 WHERE LOWER(TRIM(nombre)) = LOWER(TRIM(@nom)) AND categoria = @cat AND id_producto <> @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@nom", nombre);
            cmd.Parameters.AddWithValue("@cat", categoria);
            cmd.Parameters.AddWithValue("@id", idExcluir);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        /// <summary>Cambia solo el nombre de un producto (se refleja en POS, comandas, tickets y reportes).</summary>
        public void RenombrarProducto(int idProducto, string nombre)
        {
            nombre = (nombre ?? "").Trim();
            if (nombre.Length < 3 || nombre.Length > 80) throw new ArgumentException("El nombre debe tener entre 3 y 80 caracteres.");
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var cmd = new MySqlCommand("UPDATE PRODUCTO SET nombre = @nom WHERE id_producto = @id;", conn);
            cmd.Parameters.AddWithValue("@nom", nombre);
            cmd.Parameters.AddWithValue("@id", idProducto);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Da de alta o actualiza un producto completo en UNA transacción:
        /// PRODUCTO, sus precios por tamaño (PRODUCTO_PRECIO) y su receta (RECETA_PRODUCTO).
        /// Un tamaño que se deja sin precio se elimina, salvo que ya tenga ventas (FK RESTRICT): en ese caso
        /// se conserva y se devuelve un aviso.
        /// </summary>
        /// <returns>Id del producto y lista de avisos para mostrar al usuario.</returns>
        public (int idProducto, List<string> avisos) GuardarProducto(ProductoEdicion prod)
        {
            var avisos = new List<string>();
            string variante = prod.Categoria == "PIZZA_MOSTRADOR" ? "MOSTRADOR" : "NORMAL";
            bool conStock = prod.Categoria == "BEBIDA";
            bool conReceta = prod.Categoria is "PIZZA" or "CALZONE" or "PIZZA_MOSTRADOR";

            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                int id = prod.IdProducto;
                if (id == 0)
                {
                    const string sqlIns = @"INSERT INTO PRODUCTO (nombre, categoria, max_ingredientes_base, disponible)
                                            VALUES (@nom, @cat, @max, @disp); SELECT LAST_INSERT_ID();";
                    using var cmd = new MySqlCommand(sqlIns, conn, tx);
                    cmd.Parameters.AddWithValue("@nom", prod.Nombre.Trim());
                    cmd.Parameters.AddWithValue("@cat", prod.Categoria);
                    cmd.Parameters.AddWithValue("@max", prod.MaxIngredientesBase);
                    cmd.Parameters.AddWithValue("@disp", prod.Disponible);
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                else
                {
                    const string sqlUpd = "UPDATE PRODUCTO SET nombre = @nom, max_ingredientes_base = @max WHERE id_producto = @id;";
                    using var cmd = new MySqlCommand(sqlUpd, conn, tx);
                    cmd.Parameters.AddWithValue("@nom", prod.Nombre.Trim());
                    cmd.Parameters.AddWithValue("@max", prod.MaxIngredientesBase);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }

                // ---- Precios por tamaño ----
                var existentes = new Dictionary<int, (int idPp, int ventas, string nombre)>();
                using (var cmd = new MySqlCommand(@"SELECT pp.id_tamano, pp.id_producto_precio, t.nombre,
                                                           (SELECT COUNT(*) FROM DETALLE_PEDIDO dp WHERE dp.id_producto_precio = pp.id_producto_precio) AS ventas
                                                    FROM PRODUCTO_PRECIO pp INNER JOIN TAMANO_PRESENTACION t ON t.id_tamano = pp.id_tamano
                                                    WHERE pp.id_producto = @id AND pp.variante = @var;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@var", variante);
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                        existentes[rd.GetInt32("id_tamano")] = (rd.GetInt32("id_producto_precio"), Convert.ToInt32(rd["ventas"]),
                            POS_PizzeriaRhinos.Formularios.UITheme.NombreTamano(prod.Categoria, rd.GetString("nombre")));
                }

                foreach (var p in prod.Precios)
                {
                    if (existentes.TryGetValue(p.IdTamano, out var ex))
                    {
                        using var cmd = new MySqlCommand("UPDATE PRODUCTO_PRECIO SET precio = @precio WHERE id_producto_precio = @pp;", conn, tx);
                        cmd.Parameters.AddWithValue("@precio", p.Precio);
                        cmd.Parameters.AddWithValue("@pp", ex.idPp);
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        using var cmd = new MySqlCommand(@"INSERT INTO PRODUCTO_PRECIO (id_producto, id_tamano, variante, precio, stock_actual)
                                                           VALUES (@id, @tam, @var, @precio, @stock);", conn, tx);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@tam", p.IdTamano);
                        cmd.Parameters.AddWithValue("@var", variante);
                        cmd.Parameters.AddWithValue("@precio", p.Precio);
                        cmd.Parameters.AddWithValue("@stock", conStock ? (object)(p.Stock ?? 0) : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                var tamanosConPrecio = new HashSet<int>(prod.Precios.Select(x => x.IdTamano));
                foreach (var kv in existentes.Where(kv => !tamanosConPrecio.Contains(kv.Key)))
                {
                    if (kv.Value.ventas > 0)
                    {
                        avisos.Add($"El tamaño {kv.Value.nombre} ya tiene ventas registradas; se conservó su precio para no romper el historial. Si ya no se vende, desactiva el producto.");
                        continue;
                    }
                    using var cmd = new MySqlCommand("DELETE FROM PRODUCTO_PRECIO WHERE id_producto_precio = @pp;", conn, tx);
                    cmd.Parameters.AddWithValue("@pp", kv.Value.idPp);
                    cmd.ExecuteNonQuery();
                }

                // ---- Receta ----
                if (conReceta)
                {
                    using (var cmd = new MySqlCommand("DELETE FROM RECETA_PRODUCTO WHERE id_producto = @id;", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                    foreach (int idIng in prod.Receta.Distinct())
                    {
                        using var cmd = new MySqlCommand("INSERT INTO RECETA_PRODUCTO (id_producto, id_ingrediente) VALUES (@id, @ing);", conn, tx);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@ing", idIng);
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return (id, avisos);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
