using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>
    /// Alta / edición de cliente con teclado virtual QWERTY (Regla de negocio 1).
    /// Valida obligatorios, formato de teléfono (10 dígitos) y longitudes del esquema; el teléfono único lo garantiza MySQL (UNIQUE).
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmCliente : Form
    {
        private readonly ClienteDAO _clienteDao = new();
        private readonly int _idCliente;
        private readonly TextBox _txtTel, _txtNombre, _txtCalle, _txtNumero, _txtColonia, _txtEntre, _txtRef;
        private readonly Label _lblError;

        public Cliente? ClienteGuardado { get; private set; }

        public FrmCliente(Cliente? existente, string? telefonoInicial = null)
        {
            _idCliente = existente?.IdCliente ?? 0;
            bool edicion = _idCliente > 0;
            Text = edicion ? "Editar cliente" : "Nuevo cliente";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            var pantalla = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(UITheme.S(1080), pantalla.Width - 40), Math.Min(UITheme.S(860), pantalla.Height - 40));
            MinimumSize = UITheme.S(860, 680);

            var encabezado = new EncabezadoModulo(edicion ? "Editar cliente" : "Nuevo cliente",
                "Los campos con * son obligatorios. Usa el teclado en pantalla o el teclado físico.", edicion ? "✏" : "👤");

            // ---------------- Campos ----------------
            var tabla = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, Height = UITheme.S(4 * 84), Margin = new Padding(0) };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 4; i++) tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, UITheme.S(84)));

            TextBox Campo(string etiqueta, int col, int fila, int maxLen, bool nombrePropio, int span = 1)
            {
                var p = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(0, 0, 12, 0), Margin = new Padding(0) };
                var l = UITheme.CrearEtiqueta(etiqueta);
                l.AutoSize = false;
                l.Height = UITheme.S(26);
                var tb = new TextBox { MaxLength = maxLen, Tag = nombrePropio ? "nombre" : null };
                UITheme.EstilizarTextBox(tb);
                tb.Font = UITheme.Fuente(15f);
                Diseno.Apilar(p, null, l, tb);
                tabla.Controls.Add(p, col, fila);
                if (span > 1) tabla.SetColumnSpan(p, span);
                return tb;
            }

            _txtTel = Campo("TELÉFONO * (10 dígitos, único)", 0, 0, 15, false);
            _txtNombre = Campo("NOMBRE COMPLETO *", 1, 0, 100, true);
            _txtCalle = Campo("CALLE *", 0, 1, 100, true);
            _txtNumero = Campo("NÚMERO DE CASA *", 1, 1, 20, false);
            _txtColonia = Campo("COLONIA *", 0, 2, 80, true);
            _txtEntre = Campo("ENTRE CALLES", 1, 2, 120, true);
            _txtRef = Campo("REFERENCIAS VISUALES (color de casa, portón, vehículo…)", 0, 3, 250, false, span: 2);

            _txtTel.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

            _lblError = new Label { AutoSize = false, Height = UITheme.S(30), ForeColor = UITheme.Rojo, Font = UITheme.Fuente(11f, FontStyle.Bold) };

            var teclado = new TecladoVirtual();
            teclado.Vincular(_txtTel, _txtNombre, _txtCalle, _txtNumero, _txtColonia, _txtEntre, _txtRef);

            // ---------------- Botones ----------------
            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(78), Padding = UITheme.P(0, 12, 0, 0) };
            var btnCancelar = new BotonRhino("Cancelar", EstiloBoton.Secundario, "✕") { Dock = DockStyle.Left, Width = UITheme.S(190), DialogResult = DialogResult.Cancel };
            var btnGuardar = new BotonRhino(edicion ? "GUARDAR CAMBIOS" : "GUARDAR CLIENTE", EstiloBoton.Exito, "✔") { Dock = DockStyle.Right, Width = UITheme.S(300) };
            btnGuardar.Click += (_, _) => Guardar();
            barra.Controls.Add(btnCancelar);
            barra.Controls.Add(btnGuardar);
            CancelButton = btnCancelar;

            var card = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(20, 16, 20, 16) };
            Diseno.Apilar(card, teclado, tabla, _lblError);
            card.Controls.Add(barra);

            var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            Diseno.Apilar(cuerpo, card, encabezado, Diseno.Espacio(12));
            Controls.Add(cuerpo);

            // ---------------- Datos iniciales ----------------
            if (existente != null)
            {
                _txtTel.Text = existente.Telefono;
                _txtNombre.Text = existente.Nombre;
                _txtCalle.Text = existente.Calle;
                _txtNumero.Text = existente.NumeroCasa;
                _txtColonia.Text = existente.Colonia;
                _txtEntre.Text = existente.EntreCalles ?? "";
                _txtRef.Text = existente.Referencias ?? "";
            }
            else if (!string.IsNullOrWhiteSpace(telefonoInicial))
            {
                _txtTel.Text = new string(telefonoInicial.Where(char.IsDigit).ToArray());
            }

            Shown += (_, _) =>
            {
                var primero = string.IsNullOrEmpty(_txtTel.Text) ? _txtTel : _txtNombre;
                primero.Focus();
                primero.SelectionStart = primero.TextLength;
            };
        }

        private string? Validar(out TextBox? campo)
        {
            campo = null;
            string tel = _txtTel.Text.Trim();
            if (!Regex.IsMatch(tel, @"^\d{10}$")) { campo = _txtTel; return "El teléfono debe tener exactamente 10 dígitos (solo números)."; }
            if (_txtNombre.Text.Trim().Length < 3) { campo = _txtNombre; return "Escribe el nombre del cliente (mínimo 3 letras)."; }
            if (_txtCalle.Text.Trim().Length == 0) { campo = _txtCalle; return "La calle es obligatoria."; }
            if (_txtNumero.Text.Trim().Length == 0) { campo = _txtNumero; return "El número de casa es obligatorio."; }
            if (_txtColonia.Text.Trim().Length == 0) { campo = _txtColonia; return "La colonia es obligatoria."; }
            return null;
        }

        private void Guardar()
        {
            string? error = Validar(out var campo);
            if (error != null)
            {
                _lblError.Text = "⚠ " + error;
                campo?.Focus();
                return;
            }
            _lblError.Text = "";

            var c = new Cliente
            {
                IdCliente = _idCliente,
                Telefono = _txtTel.Text.Trim(),
                Nombre = _txtNombre.Text.Trim(),
                Calle = _txtCalle.Text.Trim(),
                NumeroCasa = _txtNumero.Text.Trim(),
                Colonia = _txtColonia.Text.Trim(),
                EntreCalles = _txtEntre.Text.Trim(),
                Referencias = _txtRef.Text.Trim()
            };

            try
            {
                c.IdCliente = _clienteDao.Guardar(c);
                ClienteGuardado = c;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                string msg = ErroresUi.Amigable(ex);
                if (ex is MySql.Data.MySqlClient.MySqlException m && m.Number == 1062)
                    msg = $"El teléfono {c.Telefono} ya pertenece a otro cliente. Búscalo en la lista de clientes.";
                _lblError.Text = "⚠ " + msg.Split('\n')[0];
                Dialogo.Error(this, msg, "No se pudo guardar el cliente");
            }
        }
    }

    /// <summary>Historial de pedidos de un cliente (botón "Historial" de la regla de negocio 1).</summary>
    [DesignerCategory("Code")]
    public class FrmHistorialCliente : Form
    {
        private readonly PanelTabla _tabla;

        private FrmHistorialCliente(Cliente cliente)
        {
            Text = $"Historial de {cliente.Nombre}";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            Size = UITheme.S(980, 640);

            var encabezado = new EncabezadoModulo($"Historial de {cliente.Nombre}",
                $"📞 {cliente.Telefono} · 📍 {cliente.Calle} #{cliente.NumeroCasa}, {cliente.Colonia}", "🕘");
            _tabla = new PanelTabla();
            _tabla.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) VerDetalle(); };

            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(74), Padding = UITheme.P(0, 12, 0, 0) };
            var btnCerrar = new BotonRhino("Cerrar", EstiloBoton.Secundario) { Dock = DockStyle.Left, Width = UITheme.S(170), DialogResult = DialogResult.Cancel };
            var btnDetalle = new BotonRhino("Ver detalle del pedido", EstiloBoton.Primario, "👁") { Dock = DockStyle.Right, Width = UITheme.S(280) };
            btnDetalle.Click += (_, _) => VerDetalle();
            barra.Controls.Add(btnCerrar);
            barra.Controls.Add(btnDetalle);
            CancelButton = btnCerrar;

            var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(18) };
            Diseno.Apilar(cuerpo, _tabla, encabezado, Diseno.Espacio(12));
            cuerpo.Controls.Add(barra);
            Controls.Add(cuerpo);

            Load += (_, _) => ErroresUi.Intentar(this, () =>
            {
                _tabla.Mostrar(new ClienteDAO().ObtenerHistorialPedidos(cliente.IdCliente), "Este cliente todavía no tiene pedidos registrados.");
                UITheme.FormatoMoneda(_tabla.Grid, "Total");
                if (_tabla.Grid.Columns.Contains("Fecha")) _tabla.Grid.Columns["Fecha"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                FormatoEstados.Aplicar(_tabla.Grid);
            }, "No se pudo cargar el historial");
        }

        private void VerDetalle()
        {
            var fila = _tabla.FilaSeleccionada;
            if (fila == null) return;
            FrmDetallePedido.Mostrar(this, Convert.ToInt32(fila.Cells["Folio"].Value));
        }

        public static void Mostrar(IWin32Window owner, Cliente cliente)
        {
            using var frm = new FrmHistorialCliente(cliente);
            frm.ShowDialog(owner);
        }
    }

    /// <summary>Pinta la columna "Estado" como pastillas de color en cualquier tabla de pedidos.</summary>
    public static class FormatoEstados
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView, string> _registrados = new();

        /// <summary>Registra la tabla una sola vez (se puede llamar en cada recarga sin duplicar el evento).</summary>
        public static void Aplicar(DataGridView dgv, string columna = "Estado")
        {
            if (_registrados.TryGetValue(dgv, out _)) return;
            _registrados.Add(dgv, columna);
            dgv.CellFormatting += Formatear;
        }

        private static void Formatear(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (sender is not DataGridView dgv || !_registrados.TryGetValue(dgv, out var columna)) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle == null) return;
            if (dgv.Columns[e.ColumnIndex].Name != columna || e.Value == null || e.Value == DBNull.Value) return;
            var (fondo, texto, etiqueta) = UITheme.ColorEstado(e.Value.ToString());
            e.CellStyle.BackColor = fondo;
            e.CellStyle.ForeColor = texto;
            e.CellStyle.SelectionBackColor = UITheme.Oscurecer(fondo, 0.06f);
            e.CellStyle.SelectionForeColor = texto;
            e.CellStyle.Font = UITheme.FuenteNegrita;
            e.Value = etiqueta;
            e.FormattingApplied = true;
        }
    }
}
