using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>
    /// Alta / edición de empleados. Regla 2: solo existen CAJERO, REPARTIDOR y ADMIN (sin cocineros);
    /// los repartidores no tienen usuario ni contraseña porque no inician sesión.
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmEmpleado : Form
    {
        private readonly EmpleadoDAO _dao = new();
        private readonly int _id;
        private readonly TextBox _txtNombre, _txtUsuario, _txtPwd, _txtPwd2;
        private readonly GrupoOpciones _grpRol, _grpActivo;
        private readonly Panel _pnlAcceso;
        private readonly Label _lblError, _lblPwdAyuda;

        public FrmEmpleado(Empleado? existente)
        {
            _id = existente?.IdEmpleado ?? 0;
            bool edicion = _id > 0;
            Text = edicion ? "Editar empleado" : "Nuevo empleado";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            ClientSize = UITheme.S(760, 830);

            var encabezado = new EncabezadoModulo(edicion ? "Editar empleado" : "Nuevo empleado",
                "Cajeras y administrador inician sesión; los repartidores solo se asignan a pedidos.", "🪪");

            Label Etiqueta(string t)
            {
                var l = UITheme.CrearEtiqueta(t);
                l.AutoSize = false;
                l.Height = UITheme.S(26);
                return l;
            }
            TextBox Caja(int max, bool pwd = false)
            {
                var tb = new TextBox { MaxLength = max, UseSystemPasswordChar = pwd };
                UITheme.EstilizarTextBox(tb);
                tb.Font = UITheme.Fuente(15f);
                return tb;
            }

            _txtNombre = Caja(100);
            _grpRol = new GrupoOpciones { TamanoBoton = UITheme.S(200, 58), Dock = DockStyle.Top };
            _grpRol.Agregar("Cajera", "CAJERO", "💵");
            _grpRol.Agregar("Repartidor", "REPARTIDOR", "🛵");
            _grpRol.Agregar("Administrador", "ADMIN", "👔");
            _grpRol.ValorCambiado += (_, _) => ActualizarAcceso();

            _grpActivo = new GrupoOpciones { TamanoBoton = UITheme.S(200, 54), Dock = DockStyle.Top };
            _grpActivo.Agregar("Activo", true, "✔");
            _grpActivo.Agregar("Inactivo", false, "⏸");

            _txtUsuario = Caja(30);
            _txtPwd = Caja(60, true);
            _txtPwd2 = Caja(60, true);
            _lblPwdAyuda = new Label { Font = UITheme.FuenteChica, ForeColor = UITheme.TextoSuave, Height = UITheme.S(24) };
            _pnlAcceso = new Panel { Height = UITheme.S(26 + 40 + 12 + 26 + 40 + 8 + 26 + 40 + 26 + 8) };
            Diseno.Apilar(_pnlAcceso, null, Etiqueta("USUARIO (3 a 30 letras, números, punto o guion bajo)"), _txtUsuario, Diseno.Espacio(12),
                Etiqueta("CONTRASEÑA"), _txtPwd, Diseno.Espacio(8), Etiqueta("CONFIRMAR CONTRASEÑA"), _txtPwd2, _lblPwdAyuda);

            _lblError = new Label { Height = UITheme.S(30), ForeColor = UITheme.Rojo, Font = UITheme.Fuente(11f, FontStyle.Bold) };

            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(74), Padding = UITheme.P(0, 12, 0, 0) };
            var btnCancelar = new BotonRhino("Cancelar", EstiloBoton.Secundario, "✕") { Dock = DockStyle.Left, Width = UITheme.S(170), DialogResult = DialogResult.Cancel };
            var btnGuardar = new BotonRhino("GUARDAR", EstiloBoton.Exito, "✔") { Dock = DockStyle.Right, Width = UITheme.S(240) };
            btnGuardar.Click += (_, _) => Guardar();
            barra.Controls.Add(btnCancelar);
            barra.Controls.Add(btnGuardar);
            CancelButton = btnCancelar;
            AcceptButton = btnGuardar;

            var card = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(20, 14, 20, 14) };
            Diseno.Apilar(card, null, Etiqueta("NOMBRE COMPLETO *"), _txtNombre, Diseno.Espacio(10), Etiqueta("ROL *"), _grpRol,
                Diseno.Espacio(6), Etiqueta("ESTADO"), _grpActivo, Diseno.Espacio(10), _pnlAcceso, _lblError);
            card.Controls.Add(barra);

            var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            Diseno.Apilar(cuerpo, card, encabezado, Diseno.Espacio(12));
            Controls.Add(cuerpo);

            if (existente != null)
            {
                _txtNombre.Text = existente.Nombre;
                _txtUsuario.Text = existente.Usuario ?? "";
                _grpRol.Seleccionar(existente.Rol.ToUpperInvariant());
                _grpActivo.Seleccionar(existente.Activo);
            }
            else
            {
                _grpRol.Seleccionar("CAJERO");
                _grpActivo.Seleccionar(true);
            }
            ActualizarAcceso();
            Shown += (_, _) => _txtNombre.Focus();
        }

        private string Rol => _grpRol.Valor as string ?? "CAJERO";

        private void ActualizarAcceso()
        {
            bool conAcceso = Rol != "REPARTIDOR";
            _pnlAcceso.Visible = conAcceso;
            _lblPwdAyuda.Text = _id > 0 ? "Deja la contraseña vacía para conservar la actual." : "Mínimo 6 caracteres.";
        }

        private void Guardar()
        {
            _lblError.Text = "";
            string nombre = _txtNombre.Text.Trim();
            string usuario = _txtUsuario.Text.Trim();
            bool conAcceso = Rol != "REPARTIDOR";

            string? error = null;
            Control? foco = null;
            if (nombre.Length < 3) { error = "Escribe el nombre completo (mínimo 3 letras)."; foco = _txtNombre; }
            else if (conAcceso && !Regex.IsMatch(usuario, @"^[A-Za-z0-9._]{3,30}$")) { error = "El usuario debe tener de 3 a 30 caracteres (letras, números, punto o guion bajo)."; foco = _txtUsuario; }
            else if (conAcceso && (_id == 0 || _txtPwd.Text.Length > 0) && _txtPwd.Text.Length < 6) { error = "La contraseña debe tener al menos 6 caracteres."; foco = _txtPwd; }
            else if (conAcceso && _txtPwd.Text != _txtPwd2.Text) { error = "Las contraseñas no coinciden."; foco = _txtPwd2; }

            bool activo = _grpActivo.Valor is bool b && b;
            if (error == null && _id > 0 && _id == Sesion.Empleado?.IdEmpleado)
            {
                if (!activo) error = "No puedes desactivar tu propio usuario mientras tienes la sesión abierta.";
                else if (Rol != "ADMIN") error = "No puedes quitarte el rol de administrador mientras tienes la sesión abierta.";
            }
            if (error != null)
            {
                _lblError.Text = "⚠ " + error;
                foco?.Focus();
                return;
            }

            var emp = new Empleado
            {
                IdEmpleado = _id,
                Nombre = nombre,
                Rol = Rol,
                Usuario = conAcceso ? usuario : null,
                Activo = activo
            };
            try
            {
                _dao.Guardar(emp, conAcceso && _txtPwd.Text.Length > 0 ? _txtPwd.Text : null);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                string msg = ex is MySql.Data.MySqlClient.MySqlException m && m.Number == 1062
                    ? $"El usuario \"{usuario}\" ya está en uso. Elige otro."
                    : ErroresUi.Amigable(ex);
                _lblError.Text = "⚠ " + msg.Split('\n')[0];
            }
        }
    }
}
