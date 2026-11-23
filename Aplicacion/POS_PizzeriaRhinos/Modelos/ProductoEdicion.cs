using System.Collections.Generic;

namespace POS_PizzeriaRhinos.Modelos
{
    /// <summary>Precio (y stock, en bebidas) de un producto en un tamaño o presentación.</summary>
    public class PrecioTamano
    {
        public int IdTamano { get; set; }
        public string NombreTamano { get; set; } = "";
        public decimal Precio { get; set; }
        public int? Stock { get; set; }
    }

    /// <summary>Datos para dar de alta o editar un producto del menú (PRODUCTO + PRODUCTO_PRECIO + RECETA_PRODUCTO).</summary>
    public class ProductoEdicion
    {
        public int IdProducto { get; set; }
        public string Nombre { get; set; } = "";
        public string Categoria { get; set; } = "PIZZA";
        public int MaxIngredientesBase { get; set; } = 5;
        public bool Disponible { get; set; } = true;
        public List<int> Receta { get; set; } = new();
        public List<PrecioTamano> Precios { get; set; } = new();
    }
}
