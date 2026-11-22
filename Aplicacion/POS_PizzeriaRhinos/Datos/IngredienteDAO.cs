using System;
using System.Data;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;

namespace POS_PizzeriaRhinos.Datos
{
    /// <summary>CRUD de INGREDIENTE (control manual del dueño, sin gramaje — Regla de negocio 8).</summary>
    public class IngredienteDAO
    {
        public DataTable Listar(string filtro = "")
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT i.id_ingrediente, i.nombre AS Ingrediente, i.disponible,
                                        (SELECT COUNT(*) FROM RECETA_PRODUCTO rp WHERE rp.id_ingrediente = i.id_ingrediente) AS `En recetas`
                                 FROM INGREDIENTE i
                                 WHERE (@f = '' OR i.nombre LIKE CONCAT('%', @f, '%'))
                                 ORDER BY i.nombre;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@f", (filtro ?? "").Trim());
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        /// <summary>
        /// Inserta o actualiza un ingrediente. Al insertar, también crea sus tarifas de ingrediente extra
        /// (PRECIO_INGREDIENTE_EXTRA) copiando la tarifa vigente por tamaño, para que la función
        /// fn_calcular_costo_extra_ingrediente pueda cobrar el 6.° ingrediente. Todo en una transacción.
        /// </summary>
        public int Guardar(int idIngrediente, string nombre, bool disponible)
        {
            nombre = (nombre ?? "").Trim();
            if (nombre.Length == 0) throw new ArgumentException("El nombre del ingrediente es obligatorio.");
            if (nombre.Length > 60) throw new ArgumentException("El nombre del ingrediente admite máximo 60 caracteres.");

            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            if (idIngrediente > 0)
            {
                const string sqlUpd = "UPDATE INGREDIENTE SET nombre = @nom, disponible = @disp WHERE id_ingrediente = @id;";
                using var cmd = new MySqlCommand(sqlUpd, conn);
                cmd.Parameters.AddWithValue("@nom", nombre);
                cmd.Parameters.AddWithValue("@disp", disponible);
                cmd.Parameters.AddWithValue("@id", idIngrediente);
                cmd.ExecuteNonQuery();
                return idIngrediente;
            }

            using var tx = conn.BeginTransaction();
            try
            {
                int nuevoId;
                const string sqlIns = "INSERT INTO INGREDIENTE (nombre, disponible) VALUES (@nom, @disp); SELECT LAST_INSERT_ID();";
                using (var cmd = new MySqlCommand(sqlIns, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@nom", nombre);
                    cmd.Parameters.AddWithValue("@disp", disponible);
                    nuevoId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                const string sqlTarifas = @"INSERT INTO PRECIO_INGREDIENTE_EXTRA (id_ingrediente, id_tamano, precio_extra)
                                            SELECT @id, id_tamano, MAX(precio_extra)
                                            FROM PRECIO_INGREDIENTE_EXTRA
                                            GROUP BY id_tamano;";
                using (var cmd = new MySqlCommand(sqlTarifas, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", nuevoId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return nuevoId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Elimina un ingrediente que no esté en recetas ni en pedidos. Primero borra sus tarifas de extra
        /// (dependientes) y luego el ingrediente; si tiene recetas o pedidos, MySQL rechaza la operación (FK RESTRICT)
        /// y se hace ROLLBACK.
        /// </summary>
        public void Eliminar(int idIngrediente)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                using (var cmd = new MySqlCommand("DELETE FROM PRECIO_INGREDIENTE_EXTRA WHERE id_ingrediente = @id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", idIngrediente);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("DELETE FROM INGREDIENTE WHERE id_ingrediente = @id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", idIngrediente);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void CambiarDisponibilidad(int idIngrediente, bool disponible)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "UPDATE INGREDIENTE SET disponible = @disp WHERE id_ingrediente = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@disp", disponible);
            cmd.Parameters.AddWithValue("@id", idIngrediente);
            cmd.ExecuteNonQuery();
        }
    }
}
