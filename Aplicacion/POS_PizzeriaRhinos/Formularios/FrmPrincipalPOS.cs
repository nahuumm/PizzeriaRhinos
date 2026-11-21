using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Datos;
using POS_PizzeriaRhinos.Modelos;

namespace POS_PizzeriaRhinos.Formularios
{
    public class FrmPrincipalPOS : Form
    {
        private readonly Empleado _empleado;
        private readonly ClienteDAO _clienteDao = new();
        private readonly PedidoDAO _pedidoDao = new();
        private readonly EmpleadoDAO _empleadoDao = new();

        private Cliente? _clienteActual;
        private readonly List<ItemCarrito> _carrito = new();
        private readonly List<ModificadorItem> _modificadoresTemp = new();
        private int _contadorGrupoPromo = 1;

        // Controles POS
        private TextBox txtTelBuscar = null!;
        private Label lblClienteInfo = null!;
        private ComboBox cmbTipoServicio = null!;
        private ComboBox cmbRepartidor = null!;
        private ComboBox cmbCategoria = null!;
        private DataGridView dgvMenu = null!;
        private CheckBox chkMitad2 = null!;
        private ComboBox cmbMitad2 = null!;
        private ComboBox cmbMasa = null!;
        private CheckBox chkOrillaQueso = null!;
        private CheckBox chkQuesoExtra = null!;
        private CheckBox chkPromo2x1 = null!;
        private ComboBox cmbIngredientes = null!;
        private ComboBox cmbAccionMod = null!;
        private ComboBox cmbMitadMod = null!;
        private ListBox lstModificadores = null!;
        private DataGridView dgvCarrito = null!;
        private Label lblTotalVenta = null!;

        // Controles CRUD Clientes
        private TextBox txtCliFiltro = null!, txtCliTel = null!, txtCliNom = null!, txtCliCalle = null!, txtCliNum = null!, txtCliCol = null!, txtCliEntre = null!, txtCliRef = null!;
        private DataGridView dgvClientes = null!, dgvHistorialCli = null!;
        private int _idClienteEditando = 0;

        // Controles Corte / Inventario
        private DataGridView dgvCorte = null!, dgvIngredientesAdmin = null!;
        private ComboBox cmbFiltroServicio = null!, cmbFiltroEstado = null!, cmbRepartidorDespacho = null!;
        private NumericUpDown numNuevoStock = null!;

        public FrmPrincipalPOS(Empleado empleado)
        {
            _empleado = empleado;
            Text = $"Pizzería Rhinos POS | Sesión: {_empleado.Nombre} ({_empleado.Rol})";
            Size = new Size(1260, 760);
            StartPosition = FormStartPosition.CenterScreen;

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
            var tabPos = new TabPage("1. Punto de Venta (POS)");
            var tabClientes = new TabPage("2. Clientes e Historial (CRUD)");
            var tabAdmin = new TabPage("3. Corte, Reparto e Inventario");

            ConstruirTabPOS(tabPos);
            ConstruirTabClientes(tabClientes);
            ConstruirTabAdmin(tabAdmin);

            tabs.TabPages.AddRange(new[] { tabPos, tabClientes, tabAdmin });
            Controls.Add(tabs);

            Load += (_, _) =>
            {
                CargarRepartidores();
                CargarMenu();
                CargarIngredientes();
                CargarClientes();
                CargarReporteCorte();
            };
        }

        private void ConstruirTabPOS(TabPage tab)
        {
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 85, BackColor = Color.FromArgb(245, 245, 248), Padding = new Padding(10) };
            pnlTop.Controls.Add(new Label { Text = "Tel. Cliente:", Location = new Point(12, 14), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
            txtTelBuscar = new TextBox { Location = new Point(95, 11), Width = 140 };
            var btnBuscarCli = new Button { Text = "Buscar Tel.", Location = new Point(242, 10), Width = 95, Height = 26 };
            btnBuscarCli.Click += BtnBuscarCli_Click;

            lblClienteInfo = new Label { Text = "Cliente: Venta de Mostrador (Público General)", Location = new Point(350, 14), AutoSize = true, ForeColor = Color.DarkBlue, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

            pnlTop.Controls.Add(new Label { Text = "Servicio:", Location = new Point(12, 48), AutoSize = true });
            cmbTipoServicio = new ComboBox { Location = new Point(95, 45), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTipoServicio.Items.AddRange(new[] { "MOSTRADOR", "DOMICILIO" });
            cmbTipoServicio.SelectedIndex = 0;
            cmbTipoServicio.SelectedIndexChanged += (_, _) => CargarMenu();

            pnlTop.Controls.Add(new Label { Text = "Repartidor:", Location = new Point(250, 48), AutoSize = true });
            cmbRepartidor = new ComboBox { Location = new Point(325, 45), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

            pnlTop.Controls.Add(new Label { Text = "Categoría:", Location = new Point(565, 48), AutoSize = true });
            cmbCategoria = new ComboBox { Location = new Point(635, 45), Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCategoria.Items.AddRange(new[] { "TODAS", "PIZZA", "PIZZA_MOSTRADOR", "CALZONE", "COMPLEMENTO", "BEBIDA" });
            cmbCategoria.SelectedIndex = 0;
            cmbCategoria.SelectedIndexChanged += (_, _) => CargarMenu();

            pnlTop.Controls.AddRange(new Control[] { txtTelBuscar, btnBuscarCli, lblClienteInfo, cmbTipoServicio, cmbRepartidor, cmbCategoria });

            // Panel Izquierdo: Catálogo y Configurador de Pizza
            var pnlIzq = new Panel { Dock = DockStyle.Left, Width = 640, Padding = new Padding(10) };
            dgvMenu = new DataGridView { Location = new Point(10, 10), Size = new Size(620, 230), ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

            var grpConfig = new GroupBox { Text = "Configuración de Pizza (Mitades, Modificadores SIN/CON y Fase Final)", Location = new Point(10, 248), Size = new Size(620, 355) };

            chkMitad2 = new CheckBox { Text = "Mitad 2:", Location = new Point(12, 26), AutoSize = true };
            cmbMitad2 = new ComboBox { Location = new Point(90, 23), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            chkMitad2.CheckedChanged += (_, _) => cmbMitad2.Enabled = chkMitad2.Checked;

            chkPromo2x1 = new CheckBox { Text = "Aplica Promo 2x1 (Med/Gde/XGde)", Location = new Point(320, 26), AutoSize = true };

            grpConfig.Controls.Add(new Label { Text = "Masa:", Location = new Point(12, 60), AutoSize = true });
            cmbMasa = new ComboBox { Location = new Point(90, 57), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMasa.Items.AddRange(new[] { "GRUESA", "DELGADA", "SARTEN" });
            cmbMasa.SelectedIndex = 0;

            chkOrillaQueso = new CheckBox { Text = "Orilla de Queso (+$)", Location = new Point(240, 59), AutoSize = true };
            chkQuesoExtra = new CheckBox { Text = "Queso Extra (+$)", Location = new Point(410, 59), AutoSize = true };

            // Modificadores SIN / CON
            grpConfig.Controls.Add(new Label { Text = "Ingrediente:", Location = new Point(12, 98), AutoSize = true });
            cmbIngredientes = new ComboBox { Location = new Point(90, 95), Width = 175, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAccionMod = new ComboBox { Location = new Point(272, 95), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAccionMod.Items.AddRange(new[] { "CON", "SIN" });
            cmbAccionMod.SelectedIndex = 0;

            cmbMitadMod = new ComboBox { Location = new Point(348, 95), Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMitadMod.Items.AddRange(new[] { "COMPLETA", "MITAD_1", "MITAD_2" });
            cmbMitadMod.SelectedIndex = 0;

            var btnAddMod = new Button { Text = "+ Ajuste", Location = new Point(460, 94), Width = 75, Height = 26 };
            btnAddMod.Click += BtnAddMod_Click;
            var btnClearMod = new Button { Text = "Limpiar", Location = new Point(540, 94), Width = 68, Height = 26 };
            btnClearMod.Click += (_, _) => { _modificadoresTemp.Clear(); lstModificadores.Items.Clear(); };

            lstModificadores = new ListBox { Location = new Point(12, 130), Size = new Size(596, 155) };

            var btnAgregarCarrito = new Button
            {
                Text = "AGREGAR PARTIDA AL PEDIDO",
                Location = new Point(12, 295),
                Size = new Size(596, 44),
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnAgregarCarrito.Click += BtnAgregarCarrito_Click;

            grpConfig.Controls.AddRange(new Control[] { chkMitad2, cmbMitad2, chkPromo2x1, cmbMasa, chkOrillaQueso, chkQuesoExtra, cmbIngredientes, cmbAccionMod, cmbMitadMod, btnAddMod, btnClearMod, lstModificadores, btnAgregarCarrito });
            pnlIzq.Controls.AddRange(new Control[] { dgvMenu, grpConfig });

            // Panel Derecho: Carrito y Cobro
            var pnlDer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            dgvCarrito = new DataGridView { Location = new Point(10, 10), Size = new Size(565, 450), ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

            var btnQuitarLinea = new Button { Text = "Quitar Seleccionado", Location = new Point(10, 470), Size = new Size(165, 34) };
            btnQuitarLinea.Click += (_, _) =>
            {
                if (dgvCarrito.CurrentRow != null)
                {
                    _carrito.RemoveAt(dgvCarrito.CurrentRow.Index);
                    RefrescarCarrito();
                }
            };

            lblTotalVenta = new Label { Text = "TOTAL: $0.00", Location = new Point(280, 472), AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.DarkRed };

            var btnConfirmarPedido = new Button
            {
                Text = "CONFIRMAR Y REGISTRAR PEDIDO EN SGBD",
                Location = new Point(10, 520),
                Size = new Size(565, 55),
                BackColor = Color.FromArgb(211, 47, 47),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnConfirmarPedido.Click += BtnConfirmarPedido_Click;

            pnlDer.Controls.AddRange(new Control[] { dgvCarrito, btnQuitarLinea, lblTotalVenta, btnConfirmarPedido });

            tab.Controls.Add(pnlDer);
            tab.Controls.Add(pnlIzq);
            tab.Controls.Add(pnlTop);
        }

        private void ConstruirTabClientes(TabPage tab)
        {
            var pnlForm = new Panel { Dock = DockStyle.Left, Width = 380, Padding = new Padding(12), BackColor = Color.FromArgb(248, 248, 250) };
            int y = 15;
            Label AddField(string label, out TextBox tb)
            {
                var l = new Label { Text = label, Location = new Point(12, y), AutoSize = true };
                tb = new TextBox { Location = new Point(12, y + 20), Width = 345 };
                pnlForm.Controls.Add(l);
                pnlForm.Controls.Add(tb);
                y += 52;
                return l;
            }

            AddField("Teléfono (Único)*:", out txtCliTel);
            AddField("Nombre Completo*:", out txtCliNom);
            AddField("Calle*:", out txtCliCalle);
            AddField("Número Casa*:", out txtCliNum);
            AddField("Colonia*:", out txtCliCol);
            AddField("Entre Calles:", out txtCliEntre);
            AddField("Referencias Visuales:", out txtCliRef);

            var btnGuardarCli = new Button { Text = "Guardar Cliente", Location = new Point(12, y + 5), Size = new Size(165, 36), BackColor = Color.FromArgb(25, 118, 210), ForeColor = Color.White };
            btnGuardarCli.Click += BtnGuardarCli_Click;

            var btnNuevoCli = new Button { Text = "Limpiar / Nuevo", Location = new Point(192, y + 5), Size = new Size(165, 36) };
            btnNuevoCli.Click += (_, _) => LimpiarFormCliente();

            var btnEliminarCli = new Button { Text = "Eliminar Cliente", Location = new Point(12, y + 48), Size = new Size(345, 32), BackColor = Color.Firebrick, ForeColor = Color.White };
            btnEliminarCli.Click += BtnEliminarCli_Click;

            pnlForm.Controls.AddRange(new Control[] { btnGuardarCli, btnNuevoCli, btnEliminarCli });

            var pnlLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            pnlLista.Controls.Add(new Label { Text = "Buscar por teléfono, nombre o colonia:", Location = new Point(12, 14), AutoSize = true });
            txtCliFiltro = new TextBox { Location = new Point(250, 11), Width = 250 };
            txtCliFiltro.TextChanged += (_, _) => CargarClientes();

            dgvClientes = new DataGridView { Location = new Point(12, 45), Size = new Size(820, 320), ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgvClientes.CellClick += DgvClientes_CellClick;

            pnlLista.Controls.Add(new Label { Text = "Historial de Pedidos del Cliente Seleccionado:", Location = new Point(12, 378), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });
            dgvHistorialCli = new DataGridView { Location = new Point(12, 405), Size = new Size(820, 240), ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

            pnlLista.Controls.AddRange(new Control[] { txtCliFiltro, dgvClientes, dgvHistorialCli });
            tab.Controls.Add(pnlLista);
            tab.Controls.Add(pnlForm);
        }

        private void ConstruirTabAdmin(TabPage tab)
        {
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(10), BackColor = Color.FromArgb(245, 245, 248) };
            pnlTop.Controls.Add(new Label { Text = "Servicio:", Location = new Point(12, 16), AutoSize = true });
            cmbFiltroServicio = new ComboBox { Location = new Point(75, 13), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFiltroServicio.Items.AddRange(new[] { "TODOS", "DOMICILIO", "MOSTRADOR" });
            cmbFiltroServicio.SelectedIndex = 0;

            pnlTop.Controls.Add(new Label { Text = "Estado:", Location = new Point(220, 16), AutoSize = true });
            cmbFiltroEstado = new ComboBox { Location = new Point(275, 13), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFiltroEstado.Items.AddRange(new[] { "TODOS", "EN_PREPARACION", "EN_RUTA", "ENTREGADO", "CANCELADO" });
            cmbFiltroEstado.SelectedIndex = 0;

            var btnFiltrar = new Button { Text = "Consultar Corte", Location = new Point(440, 12), Width = 130, Height = 27 };
            btnFiltrar.Click += (_, _) => CargarReporteCorte();

            pnlTop.Controls.Add(new Label { Text = "Asignar Repartidor (SP):", Location = new Point(600, 16), AutoSize = true });
            cmbRepartidorDespacho = new ComboBox { Location = new Point(755, 13), Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };

            var btnDespachar = new Button { Text = "Despachar a Ruta", Location = new Point(955, 12), Width = 140, Height = 27, BackColor = Color.DarkOrange, ForeColor = Color.White };
            btnDespachar.Click += BtnDespachar_Click;

            pnlTop.Controls.AddRange(new Control[] { cmbFiltroServicio, cmbFiltroEstado, btnFiltrar, cmbRepartidorDespacho, btnDespachar });

            dgvCorte = new DataGridView { Dock = DockStyle.Top, Height = 330, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

            var pnlBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            pnlBottom.Controls.Add(new Label { Text = "Control de Inventario: Disponibilidad de Ingredientes (Sin Gramaje) y Stock de Bebidas", Location = new Point(12, 8), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });
            dgvIngredientesAdmin = new DataGridView { Location = new Point(12, 35), Size = new Size(560, 210), ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

            var btnToggleIng = new Button { Text = "Activar / Desactivar Ingrediente Agotado", Location = new Point(590, 45), Size = new Size(260, 38) };
            btnToggleIng.Click += BtnToggleIng_Click;

            pnlBottom.Controls.Add(new Label { Text = "Reabastecer Stock Bebida Seleccionada en POS:", Location = new Point(590, 105), AutoSize = true });
            numNuevoStock = new NumericUpDown { Location = new Point(590, 130), Width = 100, Minimum = 0, Maximum = 500, Value = 24 };
            var btnActualizarStock = new Button { Text = "Actualizar Stock", Location = new Point(700, 128), Size = new Size(150, 28) };
            btnActualizarStock.Click += BtnActualizarStock_Click;

            pnlBottom.Controls.AddRange(new Control[] { dgvIngredientesAdmin, btnToggleIng, numNuevoStock, btnActualizarStock });

            tab.Controls.Add(pnlBottom);
            tab.Controls.Add(dgvCorte);
            tab.Controls.Add(pnlTop);
        }

        // --- LÓGICA OPERATIVA ---
        private void CargarRepartidores()
        {
            var reps = _empleadoDao.ObtenerRepartidoresActivos();
            cmbRepartidor.DataSource = new List<Empleado>(reps);
            cmbRepartidor.DisplayMember = "Nombre";
            cmbRepartidor.ValueMember = "IdEmpleado";

            cmbRepartidorDespacho.DataSource = new List<Empleado>(reps);
            cmbRepartidorDespacho.DisplayMember = "Nombre";
            cmbRepartidorDespacho.ValueMember = "IdEmpleado";
        }

        private void CargarMenu()
        {
            string cat = cmbCategoria.SelectedItem?.ToString() == "TODAS" ? "" : cmbCategoria.SelectedItem?.ToString() ?? "";
            string srv = cmbTipoServicio.SelectedItem?.ToString() ?? "MOSTRADOR";
            var dt = _pedidoDao.ObtenerMenuActivo(cat, srv);
            dgvMenu.DataSource = dt;

            // Cargar especialidades para Mitad 2
            var dtPizzas = _pedidoDao.ObtenerMenuActivo("PIZZA", srv);
            var especialidades = dtPizzas.DefaultView.ToTable(true, "id_producto", "producto");
            cmbMitad2.DataSource = especialidades;
            cmbMitad2.DisplayMember = "producto";
            cmbMitad2.ValueMember = "id_producto";
        }

        private void CargarIngredientes()
        {
            var dt = _pedidoDao.ObtenerIngredientesDisponibles();
            dgvIngredientesAdmin.DataSource = dt;

            var disponibles = dt.Select("disponible = TRUE");
            var dtDisp = disponibles.Length > 0 ? disponibles.CopyToDataTable() : dt.Clone();
            cmbIngredientes.DataSource = dtDisp;
            cmbIngredientes.DisplayMember = "nombre";
            cmbIngredientes.ValueMember = "id_ingrediente";
        }

        private void BtnBuscarCli_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTelBuscar.Text)) return;
            _clienteActual = _clienteDao.BuscarPorTelefono(txtTelBuscar.Text);
            if (_clienteActual != null)
            {
                lblClienteInfo.Text = $"Cliente: {_clienteActual.Nombre} | Dir: {_clienteActual.Calle} #{_clienteActual.NumeroCasa}, {_clienteActual.Colonia}";
                cmbTipoServicio.SelectedItem = "DOMICILIO";
            }
            else
            {
                lblClienteInfo.Text = "Cliente no registrado. Regístralo en la pestaña '2. Clientes e Historial'.";
                MessageBox.Show("Teléfono no encontrado. Puedes darlo de alta en la pestaña '2. Clientes e Historial'.", "Cliente Nuevo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnAddMod_Click(object? sender, EventArgs e)
        {
            if (dgvMenu.CurrentRow == null || cmbIngredientes.SelectedValue == null) return;
            int idProd = Convert.ToInt32(dgvMenu.CurrentRow.Cells["id_producto"].Value);
            int idTam = Convert.ToInt32(dgvMenu.CurrentRow.Cells["id_tamano"].Value);
            int idIng = Convert.ToInt32(cmbIngredientes.SelectedValue);
            string nomIng = cmbIngredientes.Text;
            string accion = cmbAccionMod.Text;
            string mitad = cmbMitadMod.Text;

            decimal costo = 0m;
            if (accion == "CON")
            {
                int previosCon = _modificadoresTemp.Count(m => m.TipoAccion == "CON");
                costo = _pedidoDao.CalcularCostoExtraIngrediente(idProd, idTam, idIng, previosCon);
            }

            _modificadoresTemp.Add(new ModificadorItem
            {
                IdIngrediente = idIng,
                NombreIngrediente = nomIng,
                TipoAccion = accion,
                MitadAplicada = mitad,
                CostoAplicado = costo
            });
            lstModificadores.Items.Add($"{accion} {nomIng} ({mitad}) -> +${costo:0.00}");
        }

        private void BtnAgregarCarrito_Click(object? sender, EventArgs e)
        {
            if (dgvMenu.CurrentRow == null) return;
            var row = dgvMenu.CurrentRow;

            int idPp = Convert.ToInt32(row.Cells["id_producto_precio"].Value);
            int idProd = Convert.ToInt32(row.Cells["id_producto"].Value);
            int idTam = Convert.ToInt32(row.Cells["id_tamano"].Value);
            string nomProd = row.Cells["producto"].Value.ToString()!;
            string nomTam = row.Cells["tamano_presentacion"].Value.ToString()!;
            string cat = row.Cells["categoria"].Value.ToString()!;
            bool aplica2x1 = Convert.ToBoolean(row.Cells["aplica_promocion_2x1"].Value);
            decimal precioBase = Convert.ToDecimal(row.Cells["precio"].Value);

            if (chkPromo2x1.Checked && !aplica2x1)
            {
                MessageBox.Show("Regla de negocio: La promoción 2x1 aplica únicamente en tamaños Mediana, Grande y Extragrande.", "Promoción no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal extras = _modificadoresTemp.Sum(m => m.CostoAplicado);
            if (cmbMasa.Text == "SARTEN") extras += _pedidoDao.ObtenerTarifaOpcionFinal(idTam, "MASA_SARTEN");
            if (chkOrillaQueso.Checked) extras += _pedidoDao.ObtenerTarifaOpcionFinal(idTam, "ORILLA_QUESO");
            if (chkQuesoExtra.Checked) extras += _pedidoDao.ObtenerTarifaOpcionFinal(idTam, "QUESO_EXTRA");

            bool esSegundoDel2x1 = chkPromo2x1.Checked && _carrito.Count(c => c.EsPromocion2x1) % 2 == 1;
            if (esSegundoDel2x1) precioBase = 0m; // Segunda pizza del par 2x1 sin costo base, solo cobra sus extras

            var item = new ItemCarrito
            {
                IdProductoPrecio = idPp,
                IdProducto = idProd,
                IdTamano = idTam,
                NombreProducto = nomProd,
                TamanoNombre = nomTam,
                Categoria = cat,
                IdProductoMitad2 = chkMitad2.Checked ? Convert.ToInt32(cmbMitad2.SelectedValue) : null,
                NombreMitad2 = chkMitad2.Checked ? cmbMitad2.Text : null,
                Cantidad = 1,
                TipoMasa = cmbMasa.Text,
                ConOrillaQueso = chkOrillaQueso.Checked,
                ConQuesoExtra = chkQuesoExtra.Checked,
                EsPromocion2x1 = chkPromo2x1.Checked,
                GrupoPromo = chkPromo2x1.Checked ? _contadorGrupoPromo : null,
                PrecioBase = precioBase,
                CostoExtras = extras,
                Modificadores = new List<ModificadorItem>(_modificadoresTemp)
            };

            if (esSegundoDel2x1) _contadorGrupoPromo++;

            _carrito.Add(item);
            _modificadoresTemp.Clear();
            lstModificadores.Items.Clear();
            chkMitad2.Checked = false;
            chkOrillaQueso.Checked = false;
            chkQuesoExtra.Checked = false;
            RefrescarCarrito();
        }

        private void RefrescarCarrito()
        {
            dgvCarrito.DataSource = _carrito.Select(c => new
            {
                Producto = c.NombreMitad2 != null ? $"{c.NombreProducto} / {c.NombreMitad2}" : c.NombreProducto,
                Tamaño = c.TamanoNombre,
                Masa = c.TipoMasa,
                Promo2x1 = c.EsPromocion2x1 ? $"Sí (#{c.GrupoPromo})" : "No",
                Base = c.PrecioBase,
                Extras = c.CostoExtras,
                Subtotal = c.SubtotalLinea
            }).ToList();

            decimal total = _carrito.Sum(c => c.SubtotalLinea);
            lblTotalVenta.Text = $"TOTAL: ${total:0.00}";
        }

        private void BtnConfirmarPedido_Click(object? sender, EventArgs e)
        {
            if (_carrito.Count == 0)
            {
                MessageBox.Show("Agrega al menos un producto al pedido.", "Pedido vacío", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string srv = cmbTipoServicio.SelectedItem?.ToString() ?? "MOSTRADOR";
            if (srv == "DOMICILIO" && _clienteActual == null)
            {
                MessageBox.Show("Para pedidos a DOMICILIO debes buscar o registrar primero al cliente por teléfono.", "Cliente requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int? idRep = srv == "DOMICILIO" && cmbRepartidor.SelectedValue != null ? Convert.ToInt32(cmbRepartidor.SelectedValue) : null;

            try
            {
                int folio = _pedidoDao.RegistrarPedidoCompleto(_clienteActual?.IdCliente, _empleado.IdEmpleado, idRep, srv, _carrito);
                MessageBox.Show($"Pedido registrado correctamente con Folio #{folio}.\nCajera: {_empleado.Nombre}", "Venta Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _carrito.Clear();
                RefrescarCarrito();
                CargarMenu(); // Refresca stock de bebidas descontado por el Trigger
                CargarReporteCorte();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo registrar el pedido:\n{ex.Message}", "Error de Integridad / SGBD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- CRUD CLIENTES ---
        private void CargarClientes() => dgvClientes.DataSource = _clienteDao.ListarTodos(txtCliFiltro.Text);

        private void DgvClientes_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var r = dgvClientes.Rows[e.RowIndex];
            _idClienteEditando = Convert.ToInt32(r.Cells["ID"].Value);
            var cli = _clienteDao.BuscarPorTelefono(r.Cells["Teléfono"].Value.ToString()!);
            if (cli != null)
            {
                txtCliTel.Text = cli.Telefono;
                txtCliNom.Text = cli.Nombre;
                txtCliCalle.Text = cli.Calle;
                txtCliNum.Text = cli.NumeroCasa;
                txtCliCol.Text = cli.Colonia;
                txtCliEntre.Text = cli.EntreCalles;
                txtCliRef.Text = cli.Referencias;
                dgvHistorialCli.DataSource = _clienteDao.ObtenerHistorialPedidos(cli.IdCliente);
            }
        }

        private void BtnGuardarCli_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCliTel.Text) || string.IsNullOrWhiteSpace(txtCliNom.Text) ||
                string.IsNullOrWhiteSpace(txtCliCalle.Text) || string.IsNullOrWhiteSpace(txtCliNum.Text) || string.IsNullOrWhiteSpace(txtCliCol.Text))
            {
                MessageBox.Show("Completa todos los campos obligatorios (*).", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var c = new Cliente
                {
                    IdCliente = _idClienteEditando,
                    Telefono = txtCliTel.Text,
                    Nombre = txtCliNom.Text,
                    Calle = txtCliCalle.Text,
                    NumeroCasa = txtCliNum.Text,
                    Colonia = txtCliCol.Text,
                    EntreCalles = txtCliEntre.Text,
                    Referencias = txtCliRef.Text
                };
                int id = _clienteDao.Guardar(c);
                c.IdCliente = id;
                _clienteActual = c;
                lblClienteInfo.Text = $"Cliente: {c.Nombre} | Dir: {c.Calle} #{c.NumeroCasa}, {c.Colonia}";
                MessageBox.Show("Cliente guardado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LimpiarFormCliente();
                CargarClientes();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar cliente (¿teléfono duplicado?): {ex.Message}", "Error SGBD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEliminarCli_Click(object? sender, EventArgs e)
        {
            if (_idClienteEditando == 0) return;
            try
            {
                _clienteDao.Eliminar(_idClienteEditando);
                MessageBox.Show("Cliente eliminado.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LimpiarFormCliente();
                CargarClientes();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se puede eliminar un cliente con pedidos registrados (Integridad Referencial):\n{ex.Message}", "Restricción FK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LimpiarFormCliente()
        {
            _idClienteEditando = 0;
            txtCliTel.Clear(); txtCliNom.Clear(); txtCliCalle.Clear(); txtCliNum.Clear(); txtCliCol.Clear(); txtCliEntre.Clear(); txtCliRef.Clear();
        }

        // --- CORTE E INVENTARIO ---
        private void CargarReporteCorte()
        {
            string srv = cmbFiltroServicio.SelectedItem?.ToString() == "TODOS" ? "" : cmbFiltroServicio.SelectedItem?.ToString() ?? "";
            string est = cmbFiltroEstado.SelectedItem?.ToString() == "TODOS" ? "" : cmbFiltroEstado.SelectedItem?.ToString() ?? "";
            dgvCorte.DataSource = _pedidoDao.ObtenerReporteCorte(srv, est);
        }

        private void BtnDespachar_Click(object? sender, EventArgs e)
        {
            if (dgvCorte.CurrentRow == null || cmbRepartidorDespacho.SelectedValue == null) return;
            int folio = Convert.ToInt32(dgvCorte.CurrentRow.Cells["Folio"].Value);
            int idRep = Convert.ToInt32(cmbRepartidorDespacho.SelectedValue);
            try
            {
                _pedidoDao.AsignarRepartidorSP(folio, idRep);
                MessageBox.Show($"Pedido #{folio} asignado y marcado EN_RUTA.", "Despacho exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarReporteCorte();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al asignar repartidor: {ex.Message}", "Validación SP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnToggleIng_Click(object? sender, EventArgs e)
        {
            if (dgvIngredientesAdmin.CurrentRow == null) return;
            int idIng = Convert.ToInt32(dgvIngredientesAdmin.CurrentRow.Cells["id_ingrediente"].Value);
            bool actual = Convert.ToBoolean(dgvIngredientesAdmin.CurrentRow.Cells["disponible"].Value);
            _pedidoDao.CambiarDisponibilidadIngrediente(idIng, !actual);
            CargarIngredientes();
        }

        private void BtnActualizarStock_Click(object? sender, EventArgs e)
        {
            if (dgvMenu.CurrentRow == null || dgvMenu.CurrentRow.Cells["stock_actual"].Value == DBNull.Value)
            {
                MessageBox.Show("Selecciona una bebida en la pestaña 1 (POS) para actualizar su stock.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int idPp = Convert.ToInt32(dgvMenu.CurrentRow.Cells["id_producto_precio"].Value);
            _pedidoDao.ActualizarStockBebida(idPp, (int)numNuevoStock.Value);
            CargarMenu();
            MessageBox.Show("Stock actualizado en refrigerador.", "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
