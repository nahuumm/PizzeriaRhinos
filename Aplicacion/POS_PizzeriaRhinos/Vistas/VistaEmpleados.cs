using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Formularios;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Vistas
{
    /// <summary>CRUD de empleados (solo administrador).</summary>
    [DesignerCategory("Code")]
    public class VistaEmpleados : VistaBase
    {
        private readonly EmpleadoDAO _dao = new();
        private readonly PanelTabla _tabla;
        private readonly TextBox _txtBuscar;
        private readonly BotonRhino _btnEditar, _btnActivo, _btnEliminar;

        public VistaEmpleados(FrmPrincipal shell) : base(shell)
        {
            var encabezado = new EncabezadoModulo("Empleados", "Cajeras, repartidores y administradores. Sin cuentas para cocina.", "🪪");

            var btnNuevo = new BotonRhino("Nuevo empleado", EstiloBoton.Primario, "➕") { Size = UITheme.S(210, 60) };
            btnNuevo.Click += (_, _) => Nuevo();
            _btnEditar = new BotonRhino("Editar", EstiloBoton.Secundario, "✏") { Size = UITheme.S(150, 60) };
            _btnEditar.Click += (_, _) => Editar();
            _btnActivo = new BotonRhino("Activar / desactivar", EstiloBoton.Ambar, "⇄") { Size = UITheme.S(230, 60) };
            _btnActivo.Click += (_, _) => AlternarActivo();
            _btnEliminar = new BotonRhino("Eliminar", EstiloBoton.Secundario, "🗑") { Size = UITheme.S(150, 60) };
            _btnEliminar.Click += (_, _) => Eliminar();
            _txtBuscar = new TextBox { Width = UITheme.S(260), PlaceholderText = "Buscar por nombre, usuario o rol", Margin = UITheme.P(16, 12, 4, 4) };
            UITheme.EstilizarTextBox(_txtBuscar);
            _txtBuscar.TextChanged += (_, _) => ErroresUi.Intentar(Shell, Cargar);

            var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            barra.Controls.AddRange(new Control[] { btnNuevo, _btnEditar, _btnActivo, _btnEliminar, _txtBuscar });

            _tabla = new PanelTabla();
            _tabla.Grid.SelectionChanged += (_, _) => ActualizarBotones();
            _tabla.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Editar(); };

            var nota = new Label
            {
                Text = "ℹ Los empleados con pedidos no se pueden eliminar (se conserva quién capturó y entregó cada pedido); desactívalos.",
                Height = UITheme.S(34),
                Font = UITheme.FuenteChica,
                ForeColor = UITheme.TextoSuave,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Diseno.Apilar(this, _tabla, encabezado, Diseno.Espacio(12), barra, Diseno.Espacio(8), nota);
            ActualizarBotones();
        }

        public override void AlMostrar() => Cargar();

        private void Cargar()
        {
            int? sel = IdSeleccionado();
            _tabla.Mostrar(_dao.Listar(_txtBuscar.Text), "No hay empleados que coincidan con la búsqueda.");
            var g = _tabla.Grid;
            if (g.Columns.Contains("ID")) g.Columns["ID"].FillWeight = 35;
            if (g.Columns.Contains("Activo")) g.Columns["Activo"].FillWeight = 45;
            if (g.Columns.Contains("Pedidos")) g.Columns["Pedidos"].FillWeight = 45;
            if (sel.HasValue)
                foreach (DataGridViewRow r in g.Rows)
                    if (Convert.ToInt32(r.Cells["ID"].Value) == sel.Value) { g.CurrentCell = r.Cells["Nombre"]; break; }
            ActualizarBotones();
        }

        private int? IdSeleccionado()
        {
            var f = _tabla.FilaSeleccionada;
            return f == null ? null : Convert.ToInt32(f.Cells["ID"].Value);
        }

        private void ActualizarBotones()
        {
            bool hay = _tabla.FilaSeleccionada != null;
            _btnEditar.Enabled = hay;
            _btnActivo.Enabled = hay;
            _btnEliminar.Enabled = hay;
        }

        private void Nuevo()
        {
            using var frm = new FrmEmpleado(null);
            if (frm.ShowDialog(Shell) == DialogResult.OK)
            {
                ErroresUi.Intentar(Shell, Cargar);
                Dialogo.Exito(Shell, "El empleado quedó registrado.", "Empleado guardado");
            }
        }

        private void Editar()
        {
            int? id = IdSeleccionado();
            if (id == null) return;
            Empleado? emp = null;
            if (!ErroresUi.Intentar(Shell, () => emp = _dao.ObtenerPorId(id.Value)) || emp == null) return;
            using var frm = new FrmEmpleado(emp);
            if (frm.ShowDialog(Shell) == DialogResult.OK) ErroresUi.Intentar(Shell, Cargar);
        }

        private void AlternarActivo()
        {
            var f = _tabla.FilaSeleccionada;
            if (f == null) return;
            int id = Convert.ToInt32(f.Cells["ID"].Value);
            bool activo = Convert.ToBoolean(f.Cells["Activo"].Value);
            string nombre = Convert.ToString(f.Cells["Nombre"].Value) ?? "";
            string rol = Convert.ToString(f.Cells["Rol"].Value) ?? "";
            if (id == Sesion.Empleado?.IdEmpleado && activo)
            {
                Dialogo.Advertencia(Shell, "No puedes desactivar tu propio usuario mientras tienes la sesión abierta.");
                return;
            }
            if (activo && rol == "ADMIN")
            {
                int admins = 0;
                ErroresUi.Intentar(Shell, () => admins = _dao.ContarAdministradoresActivos());
                if (admins <= 1) { Dialogo.Advertencia(Shell, "Debe quedar al menos un administrador activo."); return; }
            }
            if (!Dialogo.Confirmar(Shell, activo ? "Desactivar empleado" : "Activar empleado",
                    activo ? $"{nombre} ya no podrá iniciar sesión ni aparecerá en la lista de repartidores. ¿Continuar?"
                           : $"¿Reactivar a {nombre}?",
                    activo ? "Sí, desactivar" : "Sí, activar", "Cancelar"))
                return;
            if (ErroresUi.Intentar(Shell, () => _dao.CambiarActivo(id, !activo)))
                ErroresUi.Intentar(Shell, Cargar);
        }

        private void Eliminar()
        {
            var f = _tabla.FilaSeleccionada;
            if (f == null) return;
            int id = Convert.ToInt32(f.Cells["ID"].Value);
            string nombre = Convert.ToString(f.Cells["Nombre"].Value) ?? "";
            int pedidos = Convert.ToInt32(f.Cells["Pedidos"].Value);
            if (id == Sesion.Empleado?.IdEmpleado)
            {
                Dialogo.Advertencia(Shell, "No puedes eliminar tu propio usuario.");
                return;
            }
            if (pedidos > 0)
            {
                Dialogo.Advertencia(Shell, $"{nombre} tiene {pedidos} pedido(s) asociados. Para conservar la trazabilidad del corte, desactívalo en lugar de eliminarlo.",
                    "No se puede eliminar");
                return;
            }
            if (!Dialogo.Confirmar(Shell, "Eliminar empleado", $"¿Eliminar definitivamente a {nombre}?", "Sí, eliminar", "Cancelar", peligro: true)) return;
            if (ErroresUi.Intentar(Shell, () => _dao.Eliminar(id), "No se pudo eliminar"))
                ErroresUi.Intentar(Shell, Cargar);
        }
    }
}
