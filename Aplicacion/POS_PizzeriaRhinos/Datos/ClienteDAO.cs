using System;
using System.Data;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Config;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Datos
{
    public class ClienteDAO
    {
        public Cliente? BuscarPorTelefono(string telefono)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "SELECT * FROM CLIENTE WHERE telefono = @tel LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tel", telefono.Trim());
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                return new Cliente
                {
                    IdCliente = rd.GetInt32("id_cliente"),
                    Telefono = rd.GetString("telefono"),
                    Nombre = rd.GetString("nombre"),
                    Calle = rd.GetString("calle"),
                    NumeroCasa = rd.GetString("numero_casa"),
                    Colonia = rd.GetString("colonia"),
                    EntreCalles = rd.IsDBNull(rd.GetOrdinal("entre_calles")) ? "" : rd.GetString("entre_calles"),
                    Referencias = rd.IsDBNull(rd.GetOrdinal("referencias")) ? "" : rd.GetString("referencias")
                };
            }
            return null;
        }

        public DataTable ListarTodos(string filtro = "")
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = @"SELECT id_cliente AS ID, telefono AS Teléfono, nombre AS Nombre, 
                                  CONCAT(calle, ' #', numero_casa, ', ', colonia) AS Dirección,
                                  entre_calles AS `Entre Calles`, referencias AS Referencias
                           FROM CLIENTE
                           WHERE telefono LIKE @f OR nombre LIKE @f OR colonia LIKE @f
                           ORDER BY id_cliente DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@f", $"%{filtro.Trim()}%");
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public int Guardar(Cliente c)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            if (c.IdCliente == 0)
            {
                string sql = @"INSERT INTO CLIENTE (telefono, nombre, calle, numero_casa, colonia, entre_calles, referencias)
                               VALUES (@tel, @nom, @calle, @num, @col, @entre, @ref);
                               SELECT LAST_INSERT_ID();";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@tel", c.Telefono.Trim());
                cmd.Parameters.AddWithValue("@nom", c.Nombre.Trim());
                cmd.Parameters.AddWithValue("@calle", c.Calle.Trim());
                cmd.Parameters.AddWithValue("@num", c.NumeroCasa.Trim());
                cmd.Parameters.AddWithValue("@col", c.Colonia.Trim());
                cmd.Parameters.AddWithValue("@entre", string.IsNullOrWhiteSpace(c.EntreCalles) ? DBNull.Value : c.EntreCalles.Trim());
                cmd.Parameters.AddWithValue("@ref", string.IsNullOrWhiteSpace(c.Referencias) ? DBNull.Value : c.Referencias.Trim());
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            else
            {
                string sql = @"UPDATE CLIENTE 
                               SET telefono = @tel, nombre = @nom, calle = @calle, numero_casa = @num, 
                                   colonia = @col, entre_calles = @entre, referencias = @ref
                               WHERE id_cliente = @id;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", c.IdCliente);
                cmd.Parameters.AddWithValue("@tel", c.Telefono.Trim());
                cmd.Parameters.AddWithValue("@nom", c.Nombre.Trim());
                cmd.Parameters.AddWithValue("@calle", c.Calle.Trim());
                cmd.Parameters.AddWithValue("@num", c.NumeroCasa.Trim());
                cmd.Parameters.AddWithValue("@col", c.Colonia.Trim());
                cmd.Parameters.AddWithValue("@entre", string.IsNullOrWhiteSpace(c.EntreCalles) ? DBNull.Value : c.EntreCalles.Trim());
                cmd.Parameters.AddWithValue("@ref", string.IsNullOrWhiteSpace(c.Referencias) ? DBNull.Value : c.Referencias.Trim());
                cmd.ExecuteNonQuery();
                return c.IdCliente;
            }
        }

        public void Eliminar(int idCliente)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = "DELETE FROM CLIENTE WHERE id_cliente = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idCliente);
            cmd.ExecuteNonQuery();
        }

        public DataTable ObtenerHistorialPedidos(int idCliente)
        {
            using var conn = ConexionMySQL.ObtenerConexion();
            conn.Open();
            string sql = @"SELECT folio AS Folio, fecha_hora AS Fecha, tipo_servicio AS Servicio, 
                                  estado AS Estado, total AS Total
                           FROM vw_resumen_pedidos_corte
                           WHERE folio IN (SELECT id_pedido FROM PEDIDO WHERE id_cliente = @id)
                           ORDER BY fecha_hora DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idCliente);
            using var da = new MySqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }
    }
}