namespace POS_PizzeriaRhinos.Modelos
{
    /// <summary>Indicadores del día para el tablero (Dashboard).</summary>
    public class ResumenDia
    {
        public decimal VentasHoy { get; set; }
        public int PedidosHoy { get; set; }
        public int EnPreparacion { get; set; }
        public int EnRuta { get; set; }
        public int Entregados { get; set; }
        public int Cancelados { get; set; }
        public int BebidasStockBajo { get; set; }
        public int IngredientesAgotados { get; set; }
        public string DetalleBebidasBajas { get; set; } = "";
        public string DetalleIngredientesAgotados { get; set; } = "";
    }

    /// <summary>Datos de cabecera de un pedido para comanda, ticket y detalle.</summary>
    public class CabeceraPedido
    {
        public int Folio { get; set; }
        public System.DateTime FechaHora { get; set; }
        public string TipoServicio { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Direccion { get; set; } = "";
        public string EntreCalles { get; set; } = "";
        public string Referencias { get; set; } = "";
        public string Cajera { get; set; } = "";
        public string Repartidor { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal Extras { get; set; }
        public decimal Total { get; set; }
    }
}
