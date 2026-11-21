using System.Collections.Generic;
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
    }
}