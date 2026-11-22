using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;

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
    }
}
