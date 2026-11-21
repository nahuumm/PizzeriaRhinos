namespace POS_PizzeriaRhinos.Modelos
{
    public class Empleado
    {
        public int IdEmpleado { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty; // CAJERO, REPARTIDOR, ADMIN
        public string? Usuario { get; set; }
        public bool Activo { get; set; }
    }
}