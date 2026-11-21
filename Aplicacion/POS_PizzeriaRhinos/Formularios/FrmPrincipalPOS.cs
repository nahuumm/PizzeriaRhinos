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

        // Vistas principales
        private Panel viewPos = null!, viewClientes = null!, viewAdmin = null!;
        private Button navPos = null!, navCli = null!, navAdm = null!;

        // Controles POS
        private TextBox txtTelBuscar = null!;
        private Label lblClienteInfo = null!;
        private ComboBox cmbTipoServicio = null!, cmbRepartidor = null!, cmbCategoria = null!;
        private DataGridView dgvMenu = null!;
        private CheckBox chkMitad2 = null!, chkOrillaQueso = null!, chkQuesoExtra = null!, chkPromo2x1 = null!;
        private ComboBox cmbMitad2 = null!, cmbMasa = null!, cmbIngredientes = null!, cmbAccionMod = null!, cmbMitadMod = null!;
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
            Text = $"Pizzería Rhinos POS | {_empleado.Nombre} ({_empleado.Rol})";
            Size = new Size(1340, 820);
            MinimumSize = new Size(1240, 760);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = UITheme.BgBase;
            ForeColor = UITheme.TextPrimary;

            // 1. Primero crear el contenedor central (Dock.Fill)
            var contenedor = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 16, 16),
                BackColor = UITheme.BgBase
            };

            viewPos = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.BgBase };
            viewClientes = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.BgBase, Visible = false };
            viewAdmin = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.BgBase, Visible = false };

            ConstruirVistaPOS(viewPos);
            ConstruirVistaClientes(viewClientes);
            ConstruirVistaAdmin(viewAdmin);

            contenedor.Controls.AddRange(new Control[] { viewPos, viewClientes, viewAdmin });
            Controls.Add(contenedor);

            // 2. Después agregar la barra superior (Dock.Top) para que WinForms respete su altura exacta sin tapar nada
            ConstruirBarraSuperior();

            Load += (_, _) =>
            {
                CargarRepartidores();
                CargarMenu();
                CargarIngredientes();
                CargarClientes();
                CargarReporteCorte();
                CambiarVista(0);
            };
        }

        private void ConstruirBarraSuperior()
        {
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = UITheme.GlassCard,
                Padding = new Padding(18, 10, 18, 10)
            };
            topBar.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.GlassBorder, 1);
                e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
            };

            var lblBrand = new Label
            {
                Text = "RHINOS POS",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber,
                Location = new Point(20, 17),
                AutoSize = true
            };

            navPos = CrearBotonNav("1. Punto de Venta", 185, () => CambiarVista(0));
            navCli = CrearBotonNav("2. Clientes e Historial", 375, () => CambiarVista(1));
            navAdm = CrearBotonNav("3. Corte e Inventario", 575, () => CambiarVista(2));

            var lblSesion = new Label
            {
                Text = $"● {_empleado.Nombre}  [{_empleado.Rol}]",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = UITheme.AccentSuccess,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(topBar.Width - 310, 21)
            };

            topBar.Controls.AddRange(new Control[] { lblBrand, navPos, navCli, navAdm, lblSesion });
            Controls.Add(topBar);
        }

        private Button CrearBotonNav(string texto, int x, Action onClick)
        {
            var btn = new Button
            {
                Text = texto,
                Location = new Point(x, 13),
                Size = new Size(180, 38)
            };
            UITheme.StyleButton(btn, UITheme.GlassElevated, 10);
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private void CambiarVista(int index)
        {
            viewPos.Visible = index == 0;
            viewClientes.Visible = index == 1;
            viewAdmin.Visible = index == 2;

            navPos.BackColor = index == 0 ? UITheme.AccentPrimary : UITheme.GlassElevated;
            navCli.BackColor = index == 1 ? UITheme.AccentPrimary : UITheme.GlassElevated;
            navAdm.BackColor = index == 2 ? UITheme.AccentPrimary : UITheme.GlassElevated;
        }

        private void ConstruirVistaPOS(Panel parent)
        {
            // Layout principal: Columna Izquierda (55%) y Columna Derecha (45%)
            var splitPrincipal = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UITheme.BgBase
            };
            splitPrincipal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
            splitPrincipal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));

            // --- COLUMNA IZQUIERDA (Catálogo + Configurador) ---
            var colIzq = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 8, 0), BackColor = UITheme.BgBase };

            var cardMenu = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            UITheme.StyleGlassCard(cardMenu, 14);
            dgvMenu = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleGrid(dgvMenu);
            cardMenu.Controls.Add(dgvMenu);

            var spMenuCfg = new Panel { Dock = DockStyle.Bottom, Height = 12, BackColor = UITheme.BgBase };

            var cardConfig = new Panel { Dock = DockStyle.Bottom, Height = 335, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardConfig, 14);

            var lblCfgTitle = new Label
            {
                Text = "PERSONALIZACIÓN DE PARTIDA (MITADES, 2X1, INGREDIENTES Y EXTRAS)",
                Location = new Point(16, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            };

            chkMitad2 = new CheckBox { Text = "Combinar Mitad 2:", Location = new Point(16, 40), AutoSize = true, ForeColor = UITheme.TextPrimary };
            cmbMitad2 = new ComboBox { Location = new Point(160, 37), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            cmbMitad2.BackColor = UITheme.GlassElevated;
            cmbMitad2.ForeColor = UITheme.TextPrimary;
            chkMitad2.CheckedChanged += (_, _) => cmbMitad2.Enabled = chkMitad2.Checked;

            chkPromo2x1 = new CheckBox
            {
                Text = "Promo 2x1 (Med/Gde/XGde)",
                Location = new Point(415, 40),
                AutoSize = true,
                ForeColor = UITheme.AccentSuccess,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            cardConfig.Controls.Add(new Label { Text = "Masa:", Location = new Point(16, 76), AutoSize = true, ForeColor = UITheme.TextMuted });
            cmbMasa = new ComboBox { Location = new Point(65, 73), Width = 135, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMasa.Items.AddRange(new[] { "GRUESA", "DELGADA", "SARTEN" });
            cmbMasa.SelectedIndex = 0;
            cmbMasa.BackColor = UITheme.GlassElevated;
            cmbMasa.ForeColor = UITheme.TextPrimary;

            chkOrillaQueso = new CheckBox { Text = "Orilla de Queso (+$)", Location = new Point(225, 75), AutoSize = true, ForeColor = UITheme.TextPrimary };
            chkQuesoExtra = new CheckBox { Text = "Queso Extra (+$)", Location = new Point(415, 75), AutoSize = true, ForeColor = UITheme.TextPrimary };

            cardConfig.Controls.Add(new Label { Text = "Ingrediente:", Location = new Point(16, 112), AutoSize = true, ForeColor = UITheme.TextMuted });
            cmbIngredientes = new ComboBox { Location = new Point(100, 109), Width = 195, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbIngredientes.BackColor = UITheme.GlassElevated;
            cmbIngredientes.ForeColor = UITheme.TextPrimary;

            cmbAccionMod = new ComboBox { Location = new Point(303, 109), Width = 72, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAccionMod.Items.AddRange(new[] { "CON", "SIN" });
            cmbAccionMod.SelectedIndex = 0;
            cmbAccionMod.BackColor = UITheme.GlassElevated;
            cmbAccionMod.ForeColor = UITheme.TextPrimary;

            cmbMitadMod = new ComboBox { Location = new Point(383, 109), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMitadMod.Items.AddRange(new[] { "COMPLETA", "MITAD_1", "MITAD_2" });
            cmbMitadMod.SelectedIndex = 0;
            cmbMitadMod.BackColor = UITheme.GlassElevated;
            cmbMitadMod.ForeColor = UITheme.TextPrimary;

            var btnAddMod = new Button { Text = "+ Añadir", Location = new Point(502, 108), Size = new Size(80, 27) };
            UITheme.StyleButton(btnAddMod, UITheme.GlassElevated, 8);
            btnAddMod.Click += BtnAddMod_Click;

            var btnClearMod = new Button { Text = "Limpiar", Location = new Point(588, 108), Size = new Size(72, 27) };
            UITheme.StyleButton(btnClearMod, Color.FromArgb(80, 44, 50), 8);
            btnClearMod.Click += (_, _) => { _modificadoresTemp.Clear(); lstModificadores.Items.Clear(); };

            lstModificadores = new ListBox
            {
                Location = new Point(16, 144),
                Size = new Size(644, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = UITheme.GlassElevated,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };

            var btnAgregarCarrito = new Button
            {
                Text = "＋  AGREGAR PARTIDA AL TICKET",
                Location = new Point(16, 276),
                Size = new Size(644, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            UITheme.StyleButton(btnAgregarCarrito, UITheme.AccentSuccess, 10);
            btnAgregarCarrito.Click += BtnAgregarCarrito_Click;

            cardConfig.Controls.AddRange(new Control[] { lblCfgTitle, chkMitad2, cmbMitad2, chkPromo2x1, cmbMasa, chkOrillaQueso, chkQuesoExtra, cmbIngredientes, cmbAccionMod, cmbMitadMod, btnAddMod, btnClearMod, lstModificadores, btnAgregarCarrito });

            colIzq.Controls.Add(cardMenu);
            colIzq.Controls.Add(spMenuCfg);
            colIzq.Controls.Add(cardConfig);

            // --- COLUMNA DERECHA (Ticket Actual) ---
            var colDer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 12, 0, 0), BackColor = UITheme.BgBase };
            var cardTicket = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            UITheme.StyleGlassCard(cardTicket, 14);

            var pnlTicketHeader = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
            pnlTicketHeader.Controls.Add(new Label
            {
                Text = "RESUMEN DEL PEDIDO ACTUAL",
                Location = new Point(0, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            });

            var pnlTicketFooter = new Panel { Dock = DockStyle.Bottom, Height = 112, BackColor = Color.Transparent };

            var btnQuitarLinea = new Button { Text = "Quitar Línea", Location = new Point(0, 10), Size = new Size(140, 36) };
            UITheme.StyleButton(btnQuitarLinea, UITheme.GlassElevated, 8);
            btnQuitarLinea.Click += (_, _) =>
            {
                if (dgvCarrito.CurrentRow != null)
                {
                    _carrito.RemoveAt(dgvCarrito.CurrentRow.Index);
                    RefrescarCarrito();
                }
            };

            lblTotalVenta = new Label
            {
                Text = "TOTAL: $0.00",
                Location = new Point(220, 10),
                Size = new Size(320, 36),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            };

            var btnConfirmarPedido = new Button
            {
                Text = "CONFIRMAR Y COBRAR PEDIDO",
                Dock = DockStyle.Bottom,
                Height = 52
            };
            UITheme.StyleButton(btnConfirmarPedido, UITheme.AccentPrimary, 12);
            btnConfirmarPedido.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnConfirmarPedido.Click += BtnConfirmarPedido_Click;

            pnlTicketFooter.Controls.AddRange(new Control[] { btnQuitarLinea, lblTotalVenta, btnConfirmarPedido });

            dgvCarrito = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleGrid(dgvCarrito);

            cardTicket.Controls.Add(dgvCarrito);
            cardTicket.Controls.Add(pnlTicketFooter);
            cardTicket.Controls.Add(pnlTicketHeader);
            colDer.Controls.Add(cardTicket);

            splitPrincipal.Controls.Add(colIzq, 0, 0);
            splitPrincipal.Controls.Add(colDer, 1, 0);

            // --- TARJETA SUPERIOR (Cliente, Servicio, Repartidor y Categoría) ---
            var cardTop = new Panel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardTop, 14);

            cardTop.Controls.Add(new Label { Text = "TELÉFONO CLIENTE:", Location = new Point(16, 12), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            txtTelBuscar = new TextBox { Location = new Point(16, 33), Width = 145 };
            UITheme.StyleTextBox(txtTelBuscar);

            var btnBuscarCli = new Button { Text = "Buscar", Location = new Point(168, 32), Size = new Size(82, 28) };
            UITheme.StyleButton(btnBuscarCli, UITheme.AccentAmber, 8);
            btnBuscarCli.ForeColor = Color.Black;
            btnBuscarCli.Click += BtnBuscarCli_Click;

            lblClienteInfo = new Label
            {
                Text = "Mostrador (Público General)",
                Location = new Point(265, 35),
                AutoSize = true,
                ForeColor = UITheme.TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            cardTop.Controls.Add(new Label { Text = "MODALIDAD:", Location = new Point(660, 12), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            cmbTipoServicio = new ComboBox { Location = new Point(660, 33), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTipoServicio.Items.AddRange(new[] { "MOSTRADOR", "DOMICILIO" });
            cmbTipoServicio.SelectedIndex = 0;
            cmbTipoServicio.SelectedIndexChanged += (_, _) => CargarMenu();
            cmbTipoServicio.BackColor = UITheme.GlassElevated;
            cmbTipoServicio.ForeColor = UITheme.TextPrimary;

            cardTop.Controls.Add(new Label { Text = "REPARTIDOR:", Location = new Point(815, 12), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            cmbRepartidor = new ComboBox { Location = new Point(815, 33), Width = 225, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRepartidor.BackColor = UITheme.GlassElevated;
            cmbRepartidor.ForeColor = UITheme.TextPrimary;

            cardTop.Controls.Add(new Label { Text = "CATEGORÍA MENÚ:", Location = new Point(1055, 12), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            cmbCategoria = new ComboBox { Location = new Point(1055, 33), Width = 195, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCategoria.Items.AddRange(new[] { "TODAS", "PIZZA", "PIZZA_MOSTRADOR", "CALZONE", "COMPLEMENTO", "BEBIDA" });
            cmbCategoria.SelectedIndex = 0;
            cmbCategoria.SelectedIndexChanged += (_, _) => CargarMenu();
            cmbCategoria.BackColor = UITheme.GlassElevated;
            cmbCategoria.ForeColor = UITheme.TextPrimary;

            cardTop.Controls.AddRange(new Control[] { txtTelBuscar, btnBuscarCli, lblClienteInfo, cmbTipoServicio, cmbRepartidor, cmbCategoria });

            // Orden de acoplamiento correcto: primero Fill, al final Top
            parent.Controls.Add(splitPrincipal);
            parent.Controls.Add(cardTop);
        }

        private void ConstruirVistaClientes(Panel parent)
        {
            var colForm = new Panel { Dock = DockStyle.Left, Width = 390, Padding = new Padding(0, 0, 12, 0), BackColor = UITheme.BgBase };
            var cardForm = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            UITheme.StyleGlassCard(cardForm, 14);

            int y = 16;
            void AddField(string label, out TextBox tb)
            {
                var l = new Label { Text = label, Location = new Point(16, y), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = UITheme.TextMuted };
                tb = new TextBox { Location = new Point(16, y + 20), Width = 340 };
                UITheme.StyleTextBox(tb);
                cardForm.Controls.Add(l);
                cardForm.Controls.Add(tb);
                y += 54;
            }

            AddField("TELÉFONO (ÚNICO)*", out txtCliTel);
            AddField("NOMBRE COMPLETO*", out txtCliNom);
            AddField("CALLE*", out txtCliCalle);
            AddField("NÚMERO CASA*", out txtCliNum);
            AddField("COLONIA*", out txtCliCol);
            AddField("ENTRE CALLES", out txtCliEntre);
            AddField("REFERENCIAS VISUALES", out txtCliRef);

            var btnGuardarCli = new Button { Text = "Guardar Cliente", Location = new Point(16, y + 8), Size = new Size(165, 38) };
            UITheme.StyleButton(btnGuardarCli, UITheme.AccentSuccess, 10);
            btnGuardarCli.Click += BtnGuardarCli_Click;

            var btnNuevoCli = new Button { Text = "Nuevo / Limpiar", Location = new Point(191, y + 8), Size = new Size(165, 38) };
            UITheme.StyleButton(btnNuevoCli, UITheme.GlassElevated, 10);
            btnNuevoCli.Click += (_, _) => LimpiarFormCliente();

            var btnEliminarCli = new Button { Text = "Eliminar Cliente", Location = new Point(16, y + 54), Size = new Size(340, 34) };
            UITheme.StyleButton(btnEliminarCli, UITheme.AccentPrimary, 10);
            btnEliminarCli.Click += BtnEliminarCli_Click;

            cardForm.Controls.AddRange(new Control[] { btnGuardarCli, btnNuevoCli, btnEliminarCli });
            colForm.Controls.Add(cardForm);

            var colLista = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.BgBase };
            var cardLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            UITheme.StyleGlassCard(cardLista, 14);

            cardLista.Controls.Add(new Label { Text = "BUSCAR CLIENTE (TELÉFONO / NOMBRE / COLONIA):", Location = new Point(16, 18), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = UITheme.AccentAmber });
            txtCliFiltro = new TextBox { Location = new Point(355, 14), Width = 280 };
            UITheme.StyleTextBox(txtCliFiltro);
            txtCliFiltro.TextChanged += (_, _) => CargarClientes();

            dgvClientes = new DataGridView { Location = new Point(16, 52), Size = new Size(860, 320), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            UITheme.StyleGrid(dgvClientes);
            dgvClientes.CellClick += DgvClientes_CellClick;

            cardLista.Controls.Add(new Label { Text = "HISTORIAL DE PEDIDOS DEL CLIENTE SELECCIONADO", Location = new Point(16, 390), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = UITheme.AccentAmber });
            dgvHistorialCli = new DataGridView { Location = new Point(16, 418), Size = new Size(860, 260), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            UITheme.StyleGrid(dgvHistorialCli);

            cardLista.Controls.AddRange(new Control[] { txtCliFiltro, dgvClientes, dgvHistorialCli });
            colLista.Controls.Add(cardLista);

            parent.Controls.Add(colLista);
            parent.Controls.Add(colForm);
        }

        private void ConstruirVistaAdmin(Panel parent)
        {
            var cardTop = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardTop, 14);

            cardTop.Controls.Add(new Label { Text = "Servicio:", Location = new Point(16, 20), AutoSize = true, ForeColor = UITheme.TextMuted });
            cmbFiltroServicio = new ComboBox { Location = new Point(80, 17), Width = 135, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFiltroServicio.Items.AddRange(new[] { "TODOS", "DOMICILIO", "MOSTRADOR" });
            cmbFiltroServicio.SelectedIndex = 0;
            cmbFiltroServicio.BackColor = UITheme.GlassElevated;
            cmbFiltroServicio.ForeColor = UITheme.TextPrimary;

            cardTop.Controls.Add(new Label { Text = "Estado:", Location = new Point(230, 20), AutoSize = true, ForeColor = UITheme.TextMuted });
            cmbFiltroEstado = new ComboBox { Location = new Point(285, 17), Width = 155, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFiltroEstado.Items.AddRange(new[] { "TODOS", "EN_PREPARACION", "EN_RUTA", "ENTREGADO", "CANCELADO" });
            cmbFiltroEstado.SelectedIndex = 0;
            cmbFiltroEstado.BackColor = UITheme.GlassElevated;
            cmbFiltroEstado.ForeColor = UITheme.TextPrimary;

            var btnFiltrar = new Button { Text = "Actualizar Corte", Location = new Point(455, 15), Size = new Size(135, 32) };
            UITheme.StyleButton(btnFiltrar, UITheme.GlassElevated, 8);
            btnFiltrar.Click += (_, _) => CargarReporteCorte();

            cardTop.Controls.Add(new Label { Text = "Repartidor (SP):", Location = new Point(625, 20), AutoSize = true, ForeColor = UITheme.TextMuted });
            cmbRepartidorDespacho = new ComboBox { Location = new Point(735, 17), Width = 205, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRepartidorDespacho.BackColor = UITheme.GlassElevated;
            cmbRepartidorDespacho.ForeColor = UITheme.TextPrimary;

            var btnDespachar = new Button { Text = "Despachar a Ruta", Location = new Point(950, 15), Size = new Size(155, 32) };
            UITheme.StyleButton(btnDespachar, UITheme.AccentAmber, 8);
            btnDespachar.ForeColor = Color.Black;
            btnDespachar.Click += BtnDespachar_Click;

            cardTop.Controls.AddRange(new Control[] { cmbFiltroServicio, cmbFiltroEstado, btnFiltrar, cmbRepartidorDespacho, btnDespachar });

            var sp1 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = UITheme.BgBase };
            var cardCorte = new Panel { Dock = DockStyle.Top, Height = 320, Padding = new Padding(12) };
            UITheme.StyleGlassCard(cardCorte, 14);
            dgvCorte = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleGrid(dgvCorte);
            cardCorte.Controls.Add(dgvCorte);

            var sp2 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = UITheme.BgBase };
            var cardInv = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardInv, 14);

            cardInv.Controls.Add(new Label { Text = "CONTROL DE INGREDIENTES (SIN GRAMAJE) Y STOCK DE REFRESCOS", Location = new Point(16, 12), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = UITheme.AccentAmber });
            dgvIngredientesAdmin = new DataGridView { Location = new Point(16, 40), Size = new Size(580, 210), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
            UITheme.StyleGrid(dgvIngredientesAdmin);

            var btnToggleIng = new Button { Text = "Activar / Desactivar Ingrediente Agotado", Location = new Point(620, 48), Size = new Size(280, 40) };
            UITheme.StyleButton(btnToggleIng, UITheme.AccentPrimary, 10);
            btnToggleIng.Click += BtnToggleIng_Click;

            cardInv.Controls.Add(new Label { Text = "Reabastecer Stock de Bebida seleccionada en Punto de Venta:", Location = new Point(620, 110), AutoSize = true, ForeColor = UITheme.TextMuted });
            numNuevoStock = new NumericUpDown { Location = new Point(620, 138), Width = 110, Minimum = 0, Maximum = 500, Value = 24, BackColor = UITheme.GlassElevated, ForeColor = UITheme.TextPrimary };
            var btnActualizarStock = new Button { Text = "Guardar Stock", Location = new Point(740, 135), Size = new Size(160, 32) };
            UITheme.StyleButton(btnActualizarStock, UITheme.AccentSuccess, 8);
            btnActualizarStock.Click += BtnActualizarStock_Click;

            cardInv.Controls.AddRange(new Control[] { dgvIngredientesAdmin, btnToggleIng, numNuevoStock, btnActualizarStock });

            parent.Controls.Add(cardInv);
            parent.Controls.Add(sp2);
            parent.Controls.Add(cardCorte);
            parent.Controls.Add(sp1);
            parent.Controls.Add(cardTop);
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

            // Ocultar columnas técnicas internas para que la tabla se vea limpia y legible
            string[] ocultar = { "id_producto_precio", "id_producto", "id_tamano", "max_ingredientes_base", "variante" };
            foreach (var col in ocultar)
            {
                if (dgvMenu.Columns.Contains(col)) dgvMenu.Columns[col].Visible = false;
            }
            if (dgvMenu.Columns.Contains("producto")) dgvMenu.Columns["producto"].HeaderText = "Producto / Especialidad";
            if (dgvMenu.Columns.Contains("categoria")) dgvMenu.Columns["categoria"].HeaderText = "Categoría";
            if (dgvMenu.Columns.Contains("tamano_presentacion")) dgvMenu.Columns["tamano_presentacion"].HeaderText = "Tamaño";
            if (dgvMenu.Columns.Contains("aplica_promocion_2x1")) dgvMenu.Columns["aplica_promocion_2x1"].HeaderText = "2x1";
            if (dgvMenu.Columns.Contains("precio")) dgvMenu.Columns["precio"].HeaderText = "Precio ($)";
            if (dgvMenu.Columns.Contains("stock_actual")) dgvMenu.Columns["stock_actual"].HeaderText = "Stock";

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
                lblClienteInfo.Text = $"{_clienteActual.Nombre}  •  {_clienteActual.Calle} #{_clienteActual.NumeroCasa}, {_clienteActual.Colonia}";
                lblClienteInfo.ForeColor = UITheme.AccentSuccess;
                cmbTipoServicio.SelectedItem = "DOMICILIO";
            }
            else
            {
                lblClienteInfo.Text = "No registrado (Dar de alta en Clientes)";
                lblClienteInfo.ForeColor = UITheme.AccentPrimary;
                MessageBox.Show("Teléfono no encontrado. Puedes registrarlo en la pestaña '2. Clientes e Historial'.", "Cliente Nuevo", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            lstModificadores.Items.Add($"[{accion}] {nomIng,-20} ({mitad,-8})  +${costo:0.00}");
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
            if (esSegundoDel2x1) precioBase = 0m;

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
                CargarMenu();
                CargarReporteCorte();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo registrar el pedido:\n{ex.Message}", "Error de Integridad / SGBD", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
                lblClienteInfo.Text = $"{c.Nombre}  •  {c.Calle} #{c.NumeroCasa}, {c.Colonia}";
                lblClienteInfo.ForeColor = UITheme.AccentSuccess;
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
                MessageBox.Show("Selecciona una bebida en la vista 1 (Punto de Venta) para actualizar su stock.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int idPp = Convert.ToInt32(dgvMenu.CurrentRow.Cells["id_producto_precio"].Value);
            _pedidoDao.ActualizarStockBebida(idPp, (int)numNuevoStock.Value);
            CargarMenu();
            MessageBox.Show("Stock actualizado en refrigerador.", "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}