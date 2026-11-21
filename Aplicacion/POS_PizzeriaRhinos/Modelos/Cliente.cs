namespace POS_PizzeriaRhinos.Modelos
{
    public class Cliente
    {
        public int IdCliente { get; set; }
        public string Telefono { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Calle { get; set; } = string.Empty;
        public string NumeroCasa { get; set; } = string.Empty;
        public string Colonia { get; set; } = string.Empty;
        public string? EntreCalles { get; set; }
        public string? Referencias { get; set; }
    }
}
