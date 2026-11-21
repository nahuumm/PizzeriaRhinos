using System;
using MySql.Data.MySqlClient;

namespace POS_PizzeriaRhinos.Config
{
    public static class ConexionMySQL
    {
        // Cadena de conexión centralizada hacia la base de datos aislada pizzeria_rhinos
        private static readonly string CadenaConexion =
            "Server=127.0.0.1;Port=3306;Database=pizzeria_rhinos;Uid=root;Password=Rama2122;CharSet=utf8mb4;";

        public static MySqlConnection ObtenerConexion()
        {
            return new MySqlConnection(CadenaConexion);
        }

        public static bool ProbarConexion(out string mensaje)
        {
            try
            {
                using (var conn = ObtenerConexion())
                {
                    conn.Open();
                    mensaje = $"Conexión exitosa a '{conn.Database}' (MySQL {conn.ServerVersion}).";
                    return true;
                }
            }
            catch (Exception ex)
            {
                mensaje = $"Error al conectar con MySQL: {ex.Message}";
                return false;
            }
        }
    }
}