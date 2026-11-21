using System.Collections.Generic;

namespace POS_PizzeriaRhinos.Modelos
{
    public class ItemCarrito
    {
        public int IdProductoPrecio { get; set; }
        public int IdProducto { get; set; }
        public int IdTamano { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public string TamanoNombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int? IdProductoMitad2 { get; set; }
        public string? NombreMitad2 { get; set; }
        public int Cantidad { get; set; } = 1;
        public string TipoMasa { get; set; } = "GRUESA"; // GRUESA, DELGADA, SARTEN
        public bool ConOrillaQueso { get; set; }
        public bool ConQuesoExtra { get; set; }
        public bool EsPromocion2x1 { get; set; }
        public int? GrupoPromo { get; set; }
        public decimal PrecioBase { get; set; }
        public decimal CostoExtras { get; set; }
        public decimal SubtotalLinea => (PrecioBase + CostoExtras) * Cantidad;
        public List<ModificadorItem> Modificadores { get; set; } = new();
    }

    public class ModificadorItem
    {
        public int IdIngrediente { get; set; }
        public string NombreIngrediente { get; set; } = string.Empty;
        public string TipoAccion { get; set; } = "CON"; // SIN o CON
        public string MitadAplicada { get; set; } = "COMPLETA"; // COMPLETA, MITAD_1, MITAD_2
        public decimal CostoAplicado { get; set; }
    }
}
