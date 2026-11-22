using System;

namespace POS_PizzeriaRhinos.Modelos
{
    /// <summary>Empleado que tiene la sesión abierta (cajera o administrador en turno).</summary>
    public static class Sesion
    {
        public static Empleado? Empleado { get; private set; }

        public static bool EsAdmin => string.Equals(Empleado?.Rol, "ADMIN", StringComparison.OrdinalIgnoreCase);

        public static string NombreCorto
        {
            get
            {
                string n = Empleado?.Nombre ?? "";
                // "Cajera 1 - Ana López" → "Ana López"
                int i = n.IndexOf(" - ", StringComparison.Ordinal);
                return i >= 0 ? n[(i + 3)..] : n;
            }
        }

        public static void Iniciar(Empleado empleado) => Empleado = empleado;
        public static void Cerrar() => Empleado = null;
    }
}
