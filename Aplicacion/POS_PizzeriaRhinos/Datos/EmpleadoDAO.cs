using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Datos
{
    public class EmpleadoDAO
    {
        public Empleado? Autenticar(string usuario, string contrasena)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = @"SELECT id_empleado, nombre, rol, usuario, activo
                           FROM EMPLEADO
                           WHERE usuario = @usr AND contrasena = @pwd AND activo = TRUE
                             AND rol IN ('CAJERO', 'ADMIN') LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@usr", usuario.Trim());
            cmd.Parameters.AddWithValue("@pwd", contrasena);

            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                return new Empleado
                {
                    IdEmpleado = rd.GetInt32("id_empleado"),
                    Nombre = rd.GetString("nombre"),
                    Rol = rd.GetString("rol"),
                    Usuario = rd.GetString("usuario"),
                    Activo = rd.GetBoolean("activo")
                };
            }
            return null;
        }

        public List<Empleado> ObtenerRepartidoresActivos()
        {
            var lista = new List<Empleado>();
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "SELECT id_empleado, nombre, rol, activo FROM EMPLEADO WHERE rol = 'REPARTIDOR' AND activo = TRUE ORDER BY id_empleado;";
            using var cmd = new MySqlCommand(sql, conn);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                lista.Add(new Empleado
                {
                    IdEmpleado = rd.GetInt32("id_empleado"),
                    Nombre = rd.GetString("nombre"),
                    Rol = rd.GetString("rol"),
                    Activo = rd.GetBoolean("activo")
                });
            }
            return lista;
        }

        // =========================================================================
        // CRUD DE EMPLEADOS (módulo de administración)
        // =========================================================================
        public DataTable Listar(string filtro = "")
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = @"SELECT e.id_empleado AS ID, e.nombre AS Nombre, e.rol AS Rol,
                                        COALESCE(e.usuario, '— sin acceso —') AS Usuario, e.activo AS Activo,
                                        (SELECT COUNT(*) FROM PEDIDO p WHERE p.id_cajero = e.id_empleado OR p.id_repartidor = e.id_empleado) AS Pedidos
                                 FROM EMPLEADO e
                                 WHERE (@f = '' OR e.nombre LIKE CONCAT('%', @f, '%') OR e.usuario LIKE CONCAT('%', @f, '%') OR e.rol LIKE CONCAT('%', @f, '%'))
                                 ORDER BY e.activo DESC, e.rol, e.nombre;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@f", (filtro ?? "").Trim());
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public Empleado? ObtenerPorId(int idEmpleado)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "SELECT id_empleado, nombre, rol, usuario, activo FROM EMPLEADO WHERE id_empleado = @id LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idEmpleado);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;
            return new Empleado
            {
                IdEmpleado = rd.GetInt32("id_empleado"),
                Nombre = rd.GetString("nombre"),
                Rol = rd.GetString("rol"),
                Usuario = rd.IsDBNull(rd.GetOrdinal("usuario")) ? null : rd.GetString("usuario"),
                Activo = rd.GetBoolean("activo")
            };
        }

        /// <summary>
        /// Inserta o actualiza un empleado. Los repartidores no tienen usuario ni contraseña (no inician sesión).
        /// Al editar, si <paramref name="contrasenaNueva"/> viene vacía se conserva la contraseña actual.
        /// </summary>
        public int Guardar(Empleado e, string? contrasenaNueva)
        {
            string rol = (e.Rol ?? "").Trim().ToUpperInvariant();
            if (rol != "CAJERO" && rol != "REPARTIDOR" && rol != "ADMIN")
                throw new ArgumentException("El rol debe ser CAJERO, REPARTIDOR o ADMIN.");
            if (string.IsNullOrWhiteSpace(e.Nombre))
                throw new ArgumentException("El nombre del empleado es obligatorio.");

            bool conAcceso = rol != "REPARTIDOR";
            string? usuario = conAcceso ? e.Usuario?.Trim() : null;
            if (conAcceso && string.IsNullOrWhiteSpace(usuario))
                throw new ArgumentException("Las cajeras y el administrador necesitan un usuario para iniciar sesión.");
            if (conAcceso && e.IdEmpleado == 0 && string.IsNullOrWhiteSpace(contrasenaNueva))
                throw new ArgumentException("Asigna una contraseña al nuevo usuario.");

            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();

            if (e.IdEmpleado == 0)
            {
                const string sql = @"INSERT INTO EMPLEADO (nombre, rol, usuario, contrasena, activo)
                                     VALUES (@nom, @rol, @usr, @pwd, @act);
                                     SELECT LAST_INSERT_ID();";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nom", e.Nombre.Trim());
                cmd.Parameters.AddWithValue("@rol", rol);
                cmd.Parameters.AddWithValue("@usr", (object?)usuario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pwd", conAcceso ? contrasenaNueva! : DBNull.Value);
                cmd.Parameters.AddWithValue("@act", e.Activo);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            else
            {
                // Si cambia a REPARTIDOR se le retira el acceso; si se envía contraseña nueva se actualiza.
                string sql = @"UPDATE EMPLEADO
                               SET nombre = @nom, rol = @rol, usuario = @usr, activo = @act" +
                             (!conAcceso ? ", contrasena = NULL" : (!string.IsNullOrWhiteSpace(contrasenaNueva) ? ", contrasena = @pwd" : "")) +
                             " WHERE id_empleado = @id;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", e.IdEmpleado);
                cmd.Parameters.AddWithValue("@nom", e.Nombre.Trim());
                cmd.Parameters.AddWithValue("@rol", rol);
                cmd.Parameters.AddWithValue("@usr", (object?)usuario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@act", e.Activo);
                if (conAcceso && !string.IsNullOrWhiteSpace(contrasenaNueva))
                    cmd.Parameters.AddWithValue("@pwd", contrasenaNueva);
                cmd.ExecuteNonQuery();
                return e.IdEmpleado;
            }
        }

        public void CambiarActivo(int idEmpleado, bool activo)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "UPDATE EMPLEADO SET activo = @act WHERE id_empleado = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@act", activo);
            cmd.Parameters.AddWithValue("@id", idEmpleado);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Elimina un empleado sin pedidos. Si tiene pedidos, la FK (ON DELETE RESTRICT) lo impide.</summary>
        public void Eliminar(int idEmpleado)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            const string sql = "DELETE FROM EMPLEADO WHERE id_empleado = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idEmpleado);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Número de administradores activos (para no dejar el sistema sin administrador).</summary>
        public int ContarAdministradoresActivos()
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            using var cmd = new MySqlCommand("SELECT COUNT(*) FROM EMPLEADO WHERE rol = 'ADMIN' AND activo = TRUE;", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
