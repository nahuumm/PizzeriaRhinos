using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace POS_PizzeriaRhinos.Config
{
    public static class ConexionMySQL
    {
        private static readonly IConfigurationRoot Configuracion = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
            .Build();

        private static string ObtenerCadenaConexion()
        {
            string? cadena = Configuracion.GetConnectionString("PizzeriaRhinosDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                throw new InvalidOperationException(
                    "No se encontró la cadena de conexión 'ConnectionStrings:PizzeriaRhinosDB'. " +
                    "Configura 'Administrar secretos de usuario' (secrets.json) en Visual Studio.");
            }

            return cadena;
        }

        public static MySqlConnection ObtenerConexion()
        {
            return new MySqlConnection(ObtenerCadenaConexion());
        }

        public static bool ProbarConexion(out string mensaje)
        {
            try
            {
                using var conn = ObtenerConexion();
                conn.Open();
                mensaje = $"Conexión exitosa a '{conn.Database}' (MySQL {conn.ServerVersion}).";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = $"Error al conectar con MySQL: {ex.Message}";
                return false;
            }
        }
    }
}