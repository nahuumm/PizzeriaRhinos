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
        private Button btnHistorialRapido = null!;
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
        private Label lblKpiTotal = null!, lblKpiPrep = null!, lblKpiRuta = null!, lblKpiEntregados = null!;

        public FrmPrincipalPOS(Empleado empleado)
        {
            _empleado = empleado;
            Text = $"Pizzería Rhinos POS | {_empleado.Nombre} ({_empleado.Rol})";
            Size = new Size(1380, 860);
            MinimumSize = new Size(1240, 760);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = UITheme.BgBase;
            ForeColor = UITheme.TextPrimary;

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

            ConstruirBarraSuperior();

            Load += (_, _) =>
            {
                CargarRepartidores();
                ActualizarEstadoRepartidorPOS();
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
                Height = 72,
                BackColor = UITheme.GlassCard,
                Padding = new Padding(16, 10, 16, 10)
            };
            topBar.Paint += (_, e) =>
            {
                using var pen = new Pen(UITheme.GlassBorder, 1);
                e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
            };

            // Logo Oficial Rhinos Pizza en Barra Superior
            var pnlLogo = new Panel
            {
                Location = new Point(16, 10),
                Size = new Size(165, 52),
                BackColor = Color.White
            };
            UITheme.ApplyRoundedCorners(pnlLogo, 10);

            var picLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = UITheme.CargarLogoRhinos()
            };
            if (picLogo.Image == null)
            {
                pnlLogo.Controls.Add(new Label
                {
                    Text = "RHINOS POS",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = UITheme.AccentPrimary
                });
            }
            else
            {
                pnlLogo.Controls.Add(picLogo);
            }

            navPos = CrearBotonNav("1. Punto de Venta", 200, () => CambiarVista(0));
            navCli = CrearBotonNav("2. Clientes e Historial", 405, () => CambiarVista(1));
            navAdm = CrearBotonNav("3. Corte e Inventario", 620, () => CambiarVista(2));

            var lblSesion = new Label
            {
                Text = $"● {_empleado.Nombre}  [{_empleado.Rol}]",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = UITheme.AccentSuccess,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(topBar.Width - 320, 25)
            };

            topBar.Controls.AddRange(new Control[] { pnlLogo, navPos, navCli, navAdm, lblSesion });
            Controls.Add(topBar);
        }

        private Button CrearBotonNav(string texto, int x, Action onClick)
        {
            var btn = new Button
            {
                Text = texto,
                Location = new Point(x, 14),
                Size = new Size(195, 44)
            };
            UITheme.StyleButton(btn, UITheme.GlassElevated, 10, 10f);
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
            UITheme.StyleGrid(dgvMenu, 40); // Ajuste DBA: altura de encabezado 40px
            dgvMenu.CellFormatting += DgvMenu_CellFormatting;
            cardMenu.Controls.Add(dgvMenu);

            var spMenuCfg = new Panel { Dock = DockStyle.Bottom, Height = 10, BackColor = UITheme.BgBase };

            var cardConfig = new Panel { Dock = DockStyle.Bottom, Height = 355, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardConfig, 14);

            var lblCfgTitle = new Label
            {
                Text = "PERSONALIZACIÓN DE PARTIDA (MITADES, 2X1, INGREDIENTES Y FASE FINAL)",
                Location = new Point(16, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            };

            chkMitad2 = new CheckBox
            {
                Text = "Combinar Mitad 2:",
                Location = new Point(16, 38),
                AutoSize = true,
                ForeColor = UITheme.TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            cmbMitad2 = new ComboBox { Location = new Point(172, 35), Width = 225, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            UITheme.StyleComboBox(cmbMitad2);

            chkMitad2.CheckedChanged += (_, _) =>
            {
                cmbMitad2.Enabled = chkMitad2.Checked;
                cmbMitadMod.Items.Clear();
                if (chkMitad2.Checked)
                    cmbMitadMod.Items.AddRange(new[] { "COMPLETA", "MITAD_1", "MITAD_2" });
                else
                    cmbMitadMod.Items.Add("COMPLETA");
                cmbMitadMod.SelectedIndex = 0;
            };

            chkPromo2x1 = new CheckBox
            {
                Text = "Promo 2x1 (Med/Gde/XGde)",
                Location = new Point(415, 38),
                AutoSize = true,
                ForeColor = UITheme.AccentSuccess,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            cardConfig.Controls.Add(new Label { Text = "Masa:", Location = new Point(16, 76), AutoSize = true, ForeColor = UITheme.TextMuted });
            cmbMasa = new ComboBox { Location = new Point(65, 72), Width = 135, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMasa.Items.AddRange(new[] { "GRUESA", "DELGADA", "SARTEN" });
            cmbMasa.SelectedIndex = 0;
            UITheme.StyleComboBox(cmbMasa);

            chkOrillaQueso = new CheckBox { Text = "Orilla de Queso (+$)", Location = new Point(220, 74), AutoSize = true, ForeColor = UITheme.TextPrimary, Font = new Font("Segoe UI", 9.5f) };
            chkQuesoExtra = new CheckBox { Text = "Queso Extra (+$)", Location = new Point(415, 74), AutoSize = true, ForeColor = UITheme.TextPrimary, Font = new Font("Segoe UI", 9.5f) };

            // Fila responsiva de Modificadores (evita cualquier recorte en "+ Añadir" y "Limpiar")
            var tblModBar = new TableLayoutPanel
            {
                Location = new Point(16, 108),
                Size = new Size(cardConfig.Width - 32, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 6,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tblModBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86f));   // Etiqueta
            tblModBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));    // Combo Ingredientes
            tblModBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));   // CON / SIN
            tblModBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));    // COMPLETA / MITAD
            tblModBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));  // + Añadir
            tblModBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));  // Limpiar

            var lblIng = new Label
            {
                Text = "Ingrediente:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UITheme.TextMuted,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            cmbIngredientes = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 6, 4) };
            UITheme.StyleComboBox(cmbIngredientes);

            cmbAccionMod = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 6, 4) };
            cmbAccionMod.Items.AddRange(new[] { "CON", "SIN" });
            cmbAccionMod.SelectedIndex = 0;
            UITheme.StyleComboBox(cmbAccionMod);

            cmbMitadMod = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 6, 4) };
            cmbMitadMod.Items.Add("COMPLETA");
            cmbMitadMod.SelectedIndex = 0;
            UITheme.StyleComboBox(cmbMitadMod);

            var btnAddMod = new Button { Text = "+ Añadir", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 6, 1) };
            UITheme.StyleButton(btnAddMod, UITheme.AccentInfo, 8, 9.5f);
            btnAddMod.Click += BtnAddMod_Click;

            var btnClearMod = new Button { Text = "Limpiar", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 1) };
            UITheme.StyleButton(btnClearMod, Color.FromArgb(120, 45, 52), 8, 9.5f);
            btnClearMod.Click += (_, _) => { _modificadoresTemp.Clear(); lstModificadores.Items.Clear(); };

            tblModBar.Controls.Add(lblIng, 0, 0);
            tblModBar.Controls.Add(cmbIngredientes, 1, 0);
            tblModBar.Controls.Add(cmbAccionMod, 2, 0);
            tblModBar.Controls.Add(cmbMitadMod, 3, 0);
            tblModBar.Controls.Add(btnAddMod, 4, 0);
            tblModBar.Controls.Add(btnClearMod, 5, 0);

            lstModificadores = new ListBox
            {
                Location = new Point(16, 152),
                Size = new Size(cardConfig.Width - 32, 128),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = UITheme.GlassElevated,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };
            lstModificadores.DoubleClick += (_, _) =>
            {
                if (lstModificadores.SelectedIndex >= 0)
                {
                    int idx = lstModificadores.SelectedIndex;
                    _modificadoresTemp.RemoveAt(idx);
                    lstModificadores.Items.RemoveAt(idx);
                }
            };

            var btnAgregarCarrito = new Button
            {
                Text = "＋  AGREGAR PARTIDA AL TICKET",
                Location = new Point(16, 290),
                Size = new Size(cardConfig.Width - 32, 50),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            UITheme.StyleButton(btnAgregarCarrito, UITheme.AccentSuccess, 10, 11f);
            btnAgregarCarrito.Click += BtnAgregarCarrito_Click;

            cardConfig.Controls.AddRange(new Control[]
            {
                lblCfgTitle, chkMitad2, cmbMitad2, chkPromo2x1,
                cmbMasa, chkOrillaQueso, chkQuesoExtra,
                tblModBar, lstModificadores, btnAgregarCarrito
            });

            colIzq.Controls.Add(cardMenu);
            colIzq.Controls.Add(spMenuCfg);
            colIzq.Controls.Add(cardConfig);

            // --- COLUMNA DERECHA (Ticket Actual) ---
            var colDer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 12, 0, 0), BackColor = UITheme.BgBase };
            var cardTicket = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            UITheme.StyleGlassCard(cardTicket, 14);

            var pnlTicketHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
            pnlTicketHeader.Controls.Add(new Label
            {
                Text = "RESUMEN DEL PEDIDO ACTUAL",
                Location = new Point(0, 6),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            });

            // Ajuste DBA: Footer con lblTotalVenta SIEMPRE VISIBLE arriba del botón rojo "CONFIRMAR Y COBRAR PEDIDO"
            var pnlTicketFooter = new Panel { Dock = DockStyle.Bottom, Height = 148, BackColor = Color.Transparent };

            var pnlAccionesLinea = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
            var btnQuitarLinea = new Button { Text = "Quitar Línea", Location = new Point(0, 2), Size = new Size(145, 36) };
            UITheme.StyleButton(btnQuitarLinea, UITheme.GlassElevated, 8, 9.5f);
            btnQuitarLinea.Click += (_, _) =>
            {
                if (dgvCarrito.CurrentRow != null)
                {
                    _carrito.RemoveAt(dgvCarrito.CurrentRow.Index);
                    RefrescarCarrito();
                }
            };

            var btnVaciarTicket = new Button { Text = "Vaciar Ticket", Location = new Point(155, 2), Size = new Size(145, 36) };
            UITheme.StyleButton(btnVaciarTicket, Color.FromArgb(95, 48, 56), 8, 9.5f);
            btnVaciarTicket.Click += (_, _) =>
            {
                _carrito.Clear();
                _contadorGrupoPromo = 1;
                RefrescarCarrito();
            };
            pnlAccionesLinea.Controls.AddRange(new Control[] { btnQuitarLinea, btnVaciarTicket });

            var pnlTotalBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = UITheme.GlassElevated,
                Padding = new Padding(12, 0, 12, 0)
            };
            UITheme.ApplyRoundedCorners(pnlTotalBanner, 10);

            lblTotalVenta = new Label
            {
                Text = "TOTAL A COBRAR: $0.00",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 16.5f, FontStyle.Bold),
                ForeColor = UITheme.AccentAmber
            };
            pnlTotalBanner.Controls.Add(lblTotalVenta);

            var btnConfirmarPedido = new Button
            {
                Text = "CONFIRMAR Y COBRAR PEDIDO",
                Dock = DockStyle.Bottom,
                Height = 54
            };
            UITheme.StyleButton(btnConfirmarPedido, UITheme.AccentPrimary, 12, 11.5f);
            btnConfirmarPedido.Click += BtnConfirmarPedido_Click;

            pnlTicketFooter.Controls.Add(pnlTotalBanner);
            pnlTicketFooter.Controls.Add(pnlAccionesLinea);
            pnlTicketFooter.Controls.Add(btnConfirmarPedido);

            dgvCarrito = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleGrid(dgvCarrito, 38);

            cardTicket.Controls.Add(dgvCarrito);
            cardTicket.Controls.Add(pnlTicketFooter);
            cardTicket.Controls.Add(pnlTicketHeader);
            colDer.Controls.Add(cardTicket);

            splitPrincipal.Controls.Add(colIzq, 0, 0);
            splitPrincipal.Controls.Add(colDer, 1, 0);

            // --- TARJETA SUPERIOR (Cliente, Teclado QWERTY, Servicio, Repartidor y Categoría) ---
            var cardTop = new Panel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardTop, 14);

            cardTop.Controls.Add(new Label { Text = "TELÉFONO CLIENTE:", Location = new Point(16, 10), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            txtTelBuscar = new TextBox { Location = new Point(16, 32), Width = 140 };
            UITheme.StyleTextBox(txtTelBuscar);
            txtTelBuscar.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) BtnBuscarCli_Click(null, EventArgs.Empty); };

            var btnTecladoTel = new Button { Text = "⌨", Location = new Point(162, 31), Size = new Size(42, 34) };
            UITheme.StyleButton(btnTecladoTel, UITheme.GlassElevated, 8, 11f);
            btnTecladoTel.Click += (_, _) =>
            {
                using var kb = new FrmTecladoQwerty("Teléfono del Cliente", txtTelBuscar);
                kb.ShowDialog(this);
            };

            var btnBuscarCli = new Button { Text = "Buscar", Location = new Point(210, 31), Size = new Size(88, 34) };
            UITheme.StyleButton(btnBuscarCli, UITheme.AccentAmber, 8, 9.5f);
            btnBuscarCli.Click += BtnBuscarCli_Click;

            lblClienteInfo = new Label
            {
                Text = "Mostrador (Público General)",
                Location = new Point(308, 22),
                Size = new Size(340, 22),
                ForeColor = UITheme.TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            btnHistorialRapido = new Button
            {
                Text = "Ver Historial",
                Location = new Point(308, 46),
                Size = new Size(125, 28),
                Visible = false
            };
            UITheme.StyleButton(btnHistorialRapido, UITheme.AccentInfo, 8, 8.5f);
            btnHistorialRapido.Click += (_, _) =>
            {
                if (_clienteActual == null) return;
                CambiarVista(1);
                dgvHistorialCli.DataSource = _clienteDao.ObtenerHistorialPedidos(_clienteActual.IdCliente);
            };

            cardTop.Controls.Add(new Label { Text = "MODALIDAD:", Location = new Point(660, 10), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            cmbTipoServicio = new ComboBox { Location = new Point(660, 32), Width = 145, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTipoServicio.Items.AddRange(new[] { "MOSTRADOR", "DOMICILIO" });
            cmbTipoServicio.SelectedIndex = 0;
            cmbTipoServicio.SelectedIndexChanged += (_, _) =>
            {
                ActualizarEstadoRepartidorPOS();
                CargarMenu();
            };
            UITheme.StyleComboBox(cmbTipoServicio);

            cardTop.Controls.Add(new Label { Text = "REPARTIDOR:", Location = new Point(820, 10), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            cmbRepartidor = new ComboBox { Location = new Point(820, 32), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            UITheme.StyleComboBox(cmbRepartidor);

            cardTop.Controls.Add(new Label { Text = "CATEGORÍA MENÚ:", Location = new Point(1055, 10), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = UITheme.TextMuted });
            cmbCategoria = new ComboBox { Location = new Point(1055, 32), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCategoria.Items.AddRange(new[] { "TODAS", "PIZZA", "PIZZA_MOSTRADOR", "CALZONE", "COMPLEMENTO", "BEBIDA" });
            cmbCategoria.SelectedIndex = 0;
            cmbCategoria.SelectedIndexChanged += (_, _) => CargarMenu();
            UITheme.StyleComboBox(cmbCategoria);

            cardTop.Controls.AddRange(new Control[]
            {
                txtTelBuscar, btnTecladoTel, btnBuscarCli, lblClienteInfo, btnHistorialRapido,
                cmbTipoServicio, cmbRepartidor, cmbCategoria
            });

            parent.Controls.Add(splitPrincipal);
            parent.Controls.Add(cardTop);
        }

        private void ActualizarEstadoRepartidorPOS()
        {
            bool esDomicilio = cmbTipoServicio.SelectedItem?.ToString() == "DOMICILIO";
            cmbRepartidor.Enabled = esDomicilio;
        }

        private void DgvMenu_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || !dgvMenu.Columns.Contains("stock_actual")) return;
            var valStock = dgvMenu.Rows[e.RowIndex].Cells["stock_actual"].Value;
            if (valStock != null && valStock != DBNull.Value && Convert.ToInt32(valStock) == 0)
            {
                dgvMenu.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(240, 110, 110);
                dgvMenu.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(45, 28, 34);
            }
        }

        private void ConstruirVistaClientes(Panel parent)
        {
            var colForm = new Panel { Dock = DockStyle.Left, Width = 420, Padding = new Padding(0, 0, 12, 0), BackColor = UITheme.BgBase };
            var cardForm = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            UITheme.StyleGlassCard(cardForm, 14);

            int y = 14;
            void AddField(string label, out TextBox tb)
            {
                var l = new Label { Text = label, Location = new Point(16, y), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = UITheme.TextMuted };
                var box = new TextBox { Location = new Point(16, y + 20), Width = 328 };
                UITheme.StyleTextBox(box);

                var btnKb = new Button { Text = "⌨", Location = new Point(350, y + 19), Size = new Size(40, 30) };
                UITheme.StyleButton(btnKb, UITheme.GlassElevated, 6, 9.5f);
                btnKb.Click += (_, _) =>
                {
                    using var kb = new FrmTecladoQwerty(label.Replace("*", ""), box);
                    kb.ShowDialog(this);
                };

                cardForm.Controls.Add(l);
                cardForm.Controls.Add(box);
                cardForm.Controls.Add(btnKb);
                tb = box;
                y += 56;
            }

            AddField("TELÉFONO (ÚNICO)*", out txtCliTel);
            AddField("NOMBRE COMPLETO*", out txtCliNom);
            AddField("CALLE*", out txtCliCalle);
            AddField("NÚMERO CASA*", out txtCliNum);
            AddField("COLONIA*", out txtCliCol);
            AddField("ENTRE CALLES", out txtCliEntre);
            AddField("REFERENCIAS VISUALES", out txtCliRef);

            var btnGuardarCli = new Button { Text = "Guardar Cliente", Location = new Point(16, y + 8), Size = new Size(182, 44) };
            UITheme.StyleButton(btnGuardarCli, UITheme.AccentSuccess, 10, 10f);
            btnGuardarCli.Click += BtnGuardarCli_Click;

            var btnNuevoCli = new Button { Text = "Nuevo / Limpiar", Location = new Point(208, y + 8), Size = new Size(182, 44) };
            UITheme.StyleButton(btnNuevoCli, UITheme.GlassElevated, 10, 10f);
            btnNuevoCli.Click += (_, _) => LimpiarFormCliente();

            var btnEliminarCli = new Button { Text = "Eliminar Cliente", Location = new Point(16, y + 60), Size = new Size(374, 40) };
            UITheme.StyleButton(btnEliminarCli, UITheme.AccentPrimary, 10, 10f);
            btnEliminarCli.Click += BtnEliminarCli_Click;

            cardForm.Controls.AddRange(new Control[] { btnGuardarCli, btnNuevoCli, btnEliminarCli });
            colForm.Controls.Add(cardForm);

            var colLista = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.BgBase };
            var cardLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            UITheme.StyleGlassCard(cardLista, 14);

            cardLista.Controls.Add(new Label { Text = "BUSCAR CLIENTE (TELÉFONO / NOMBRE / COLONIA):", Location = new Point(16, 18), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = UITheme.AccentAmber });
            txtCliFiltro = new TextBox { Location = new Point(375, 14), Width = 300 };
            UITheme.StyleTextBox(txtCliFiltro);
            txtCliFiltro.TextChanged += (_, _) => CargarClientes();

            dgvClientes = new DataGridView { Location = new Point(16, 54), Size = new Size(860, 310), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            UITheme.StyleGrid(dgvClientes, 38);
            dgvClientes.CellClick += DgvClientes_CellClick;

            cardLista.Controls.Add(new Label { Text = "HISTORIAL DE PEDIDOS DEL CLIENTE SELECCIONADO", Location = new Point(16, 380), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = UITheme.AccentAmber });
            dgvHistorialCli = new DataGridView { Location = new Point(16, 408), Size = new Size(860, 270), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            UITheme.StyleGrid(dgvHistorialCli, 38);

            cardLista.Controls.AddRange(new Control[] { txtCliFiltro, dgvClientes, dgvHistorialCli });
            colLista.Controls.Add(cardLista);

            parent.Controls.Add(colLista);
            parent.Controls.Add(colForm);
        }

        private void ConstruirVistaAdmin(Panel parent)
        {
            // Tarjeta Superior: Filtros + Despachar a Ruta + Marcar Entregado + Ver Detalle / Imprimir Ticket
            var cardTop = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(12) };
            UITheme.StyleGlassCard(cardTop, 14);

            var flowAdmin = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 6, 4, 4)
            };

            flowAdmin.Controls.Add(new Label { Text = "Servicio:", AutoSize = true, ForeColor = UITheme.TextMuted, Margin = new Padding(0, 10, 4, 0) });
            cmbFiltroServicio = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 10, 0) };
            cmbFiltroServicio.Items.AddRange(new[] { "TODOS", "DOMICILIO", "MOSTRADOR" });
            cmbFiltroServicio.SelectedIndex = 0;
            UITheme.StyleComboBox(cmbFiltroServicio);

            flowAdmin.Controls.Add(new Label { Text = "Estado:", AutoSize = true, ForeColor = UITheme.TextMuted, Margin = new Padding(0, 10, 4, 0) });
            cmbFiltroEstado = new ComboBox { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 10, 0) };
            cmbFiltroEstado.Items.AddRange(new[] { "TODOS", "EN_PREPARACION", "EN_RUTA", "ENTREGADO", "CANCELADO" });
            cmbFiltroEstado.SelectedIndex = 0;
            UITheme.StyleComboBox(cmbFiltroEstado);

            var btnFiltrar = new Button { Text = "Actualizar Corte", Size = new Size(138, 40), Margin = new Padding(0, 2, 12, 0) };
            UITheme.StyleButton(btnFiltrar, UITheme.GlassElevated, 8, 9f);
            btnFiltrar.Click += (_, _) => CargarReporteCorte();

            flowAdmin.Controls.Add(new Label { Text = "Repartidor:", AutoSize = true, ForeColor = UITheme.TextMuted, Margin = new Padding(0, 10, 4, 0) });
            cmbRepartidorDespacho = new ComboBox { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 8, 0) };
            UITheme.StyleComboBox(cmbRepartidorDespacho);

            // Ajuste DBA: Botones de Flujo de Entrega juntos ("Despachar a Ruta" y "Marcar Entregado")
            var btnDespachar = new Button { Text = "Despachar a Ruta", Size = new Size(148, 40), Margin = new Padding(0, 2, 8, 0) };
            UITheme.StyleButton(btnDespachar, UITheme.AccentAmber, 8, 9f);
            btnDespachar.Click += BtnDespachar_Click;

            var btnEntregado = new Button { Text = "Marcar Entregado", Size = new Size(148, 40), Margin = new Padding(0, 2, 10, 0) };
            UITheme.StyleButton(btnEntregado, UITheme.AccentSuccess, 8, 9f);
            btnEntregado.Click += BtnMarcarEntregado_Click;

            // Ajuste DBA: Vista de Ticket / Comanda Imprimible
            var btnVerTicket = new Button { Text = "Ver Detalle / Imprimir Ticket", Size = new Size(205, 40), Margin = new Padding(0, 2, 0, 0) };
            UITheme.StyleButton(btnVerTicket, UITheme.AccentInfo, 8, 9f);
            btnVerTicket.Click += BtnVerTicket_Click;

            flowAdmin.Controls.AddRange(new Control[]
            {
                cmbFiltroServicio, cmbFiltroEstado, btnFiltrar,
                cmbRepartidorDespacho, btnDespachar, btnEntregado, btnVerTicket
            });
            cardTop.Controls.Add(flowAdmin);

            // Tarjetas KPI del Corte de Caja
            var spKpi = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = UITheme.BgBase };
            var tblKpis = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UITheme.BgBase
            };
            for (int i = 0; i < 4; i++) tblKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            Panel CrearTarjetaKpi(string titulo, out Label lblValor, Color accent)
            {
                var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(12, 8, 12, 8) };
                UITheme.StyleGlassCard(p, 12);
                var lt = new Label { Text = titulo, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = UITheme.TextMuted };
                lblValor = new Label { Text = "0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = accent };
                p.Controls.Add(lblValor);
                p.Controls.Add(lt);
                return p;
            }

            tblKpis.Controls.Add(CrearTarjetaKpi("VENTA TOTAL EN CORTE", out lblKpiTotal, UITheme.AccentAmber), 0, 0);
            tblKpis.Controls.Add(CrearTarjetaKpi("EN PREPARACIÓN", out lblKpiPrep, UITheme.AccentPrimary), 1, 0);
            tblKpis.Controls.Add(CrearTarjetaKpi("EN RUTA DE REPARTO", out lblKpiRuta, UITheme.AccentInfo), 2, 0);
            tblKpis.Controls.Add(CrearTarjetaKpi("PEDIDOS ENTREGADOS", out lblKpiEntregados, UITheme.AccentSuccess), 3, 0);

            var sp1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = UITheme.BgBase };
            var cardCorte = new Panel { Dock = DockStyle.Top, Height = 270, Padding = new Padding(12) };
            UITheme.StyleGlassCard(cardCorte, 14);
            dgvCorte = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleGrid(dgvCorte, 38);
            dgvCorte.DoubleClick += (_, _) => BtnVerTicket_Click(null, EventArgs.Empty);
            cardCorte.Controls.Add(dgvCorte);

            var sp2 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = UITheme.BgBase };
            var cardInv = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            UITheme.StyleGlassCard(cardInv, 14);

            cardInv.Controls.Add(new Label { Text = "CONTROL DE INGREDIENTES (SIN GRAMAJE) Y STOCK DE REFRESCOS", Location = new Point(16, 12), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = UITheme.AccentAmber });
            dgvIngredientesAdmin = new DataGridView { Location = new Point(16, 42), Size = new Size(580, 200), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
            UITheme.StyleGrid(dgvIngredientesAdmin, 38);

            var btnToggleIng = new Button { Text = "Activar / Desactivar Ingrediente Agotado", Location = new Point(620, 48), Size = new Size(310, 46) };
            UITheme.StyleButton(btnToggleIng, UITheme.AccentPrimary, 10, 10f);
            btnToggleIng.Click += BtnToggleIng_Click;

            cardInv.Controls.Add(new Label { Text = "Reabastecer Stock de Bebida seleccionada en Punto de Venta:", Location = new Point(620, 112), AutoSize = true, ForeColor = UITheme.TextMuted });
            numNuevoStock = new NumericUpDown { Location = new Point(620, 140), Width = 120, Minimum = 0, Maximum = 500, Value = 24, BackColor = UITheme.GlassElevated, ForeColor = UITheme.TextPrimary, Font = new Font("Segoe UI", 10.5f) };
            var btnActualizarStock = new Button { Text = "Guardar Stock", Location = new Point(752, 136), Size = new Size(178, 40) };
            UITheme.StyleButton(btnActualizarStock, UITheme.AccentSuccess, 8, 10f);
            btnActualizarStock.Click += BtnActualizarStock_Click;

            cardInv.Controls.AddRange(new Control[] { dgvIngredientesAdmin, btnToggleIng, numNuevoStock, btnActualizarStock });

            parent.Controls.Add(cardInv);
            parent.Controls.Add(sp2);
            parent.Controls.Add(cardCorte);
            parent.Controls.Add(sp1);
            parent.Controls.Add(tblKpis);
            parent.Controls.Add(spKpi);
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

            string[] ocultar = { "id_producto_precio", "id_producto", "id_tamano", "max_ingredientes_base", "variante" };
            foreach (var col in ocultar)
            {
                if (dgvMenu.Columns.Contains(col)) dgvMenu.Columns[col].Visible = false;
            }
            if (dgvMenu.Columns.Contains("producto"))
            {
                dgvMenu.Columns["producto"].HeaderText = "Producto / Especialidad";
                dgvMenu.Columns["producto"].FillWeight = 145;
            }
            if (dgvMenu.Columns.Contains("categoria")) dgvMenu.Columns["categoria"].HeaderText = "Categoría";
            if (dgvMenu.Columns.Contains("tamano_presentacion")) dgvMenu.Columns["tamano_presentacion"].HeaderText = "Tamaño";
            if (dgvMenu.Columns.Contains("aplica_promocion_2x1"))
            {
                dgvMenu.Columns["aplica_promocion_2x1"].HeaderText = "2x1";
                dgvMenu.Columns["aplica_promocion_2x1"].FillWeight = 45;
            }
            if (dgvMenu.Columns.Contains("precio"))
            {
                dgvMenu.Columns["precio"].HeaderText = "Precio ($)";
                dgvMenu.Columns["precio"].FillWeight = 70;
            }
            if (dgvMenu.Columns.Contains("stock_actual"))
            {
                dgvMenu.Columns["stock_actual"].HeaderText = "Stock";
                dgvMenu.Columns["stock_actual"].FillWeight = 55;
            }

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
                lblClienteInfo.Text = $"{_clienteActual.Nombre} • {_clienteActual.Calle} #{_clienteActual.NumeroCasa}, {_clienteActual.Colonia}";
                lblClienteInfo.ForeColor = UITheme.AccentSuccess;
                btnHistorialRapido.Visible = true;
                cmbTipoServicio.SelectedItem = "DOMICILIO";
            }
            else
            {
                btnHistorialRapido.Visible = false;
                lblClienteInfo.Text = "No registrado (Dar de alta en Clientes)";
                lblClienteInfo.ForeColor = UITheme.AccentPrimary;
                txtCliTel.Text = txtTelBuscar.Text.Trim();
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

            // Validar bloqueo de bebidas agotadas (Regla de Negocio #7)
            if (row.Cells["stock_actual"].Value != null && row.Cells["stock_actual"].Value != DBNull.Value)
            {
                int stock = Convert.ToInt32(row.Cells["stock_actual"].Value);
                if (stock <= 0)
                {
                    MessageBox.Show("Esta bebida está agotada (Stock = 0) en el refrigerador.", "Sin Existencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

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
            lblTotalVenta.Text = $"TOTAL A COBRAR: ${total:0.00}";
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
                _contadorGrupoPromo = 1;
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
                lblClienteInfo.Text = $"{c.Nombre} • {c.Calle} #{c.NumeroCasa}, {c.Colonia}";
                lblClienteInfo.ForeColor = UITheme.AccentSuccess;
                btnHistorialRapido.Visible = true;
                MessageBox.Show("Cliente guardado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LimpiarFormCliente();
                CargarClientes();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar cliente (¿teléfono duplicado?):\n{ex.Message}", "Error SGBD", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var dt = _pedidoDao.ObtenerReporteCorte(srv, est);
            dgvCorte.DataSource = dt;

            decimal totalVentas = 0m;
            int prep = 0, ruta = 0, entregados = 0;
            foreach (DataRow r in dt.Rows)
            {
                string estado = r["Estado"].ToString() ?? "";
                if (estado != "CANCELADO")
                    totalVentas += Convert.ToDecimal(r["Total"]);
                if (estado == "EN_PREPARACION") prep++;
                else if (estado == "EN_RUTA") ruta++;
                else if (estado == "ENTREGADO") entregados++;
            }

            lblKpiTotal.Text = $"${totalVentas:0.00}";
            lblKpiPrep.Text = prep.ToString();
            lblKpiRuta.Text = ruta.ToString();
            lblKpiEntregados.Text = entregados.ToString();
        }

        private void BtnDespachar_Click(object? sender, EventArgs e)
        {
            if (dgvCorte.CurrentRow == null || cmbRepartidorDespacho.SelectedValue == null)
            {
                MessageBox.Show("Selecciona un pedido de la tabla y un repartidor.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
                MessageBox.Show($"Error al asignar repartidor:\n{ex.Message}", "Validación SP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnMarcarEntregado_Click(object? sender, EventArgs e)
        {
            if (dgvCorte.CurrentRow == null)
            {
                MessageBox.Show("Selecciona un pedido en la tabla de corte para marcarlo como ENTREGADO.", "Selecciona un pedido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int folio = Convert.ToInt32(dgvCorte.CurrentRow.Cells["Folio"].Value);
            try
            {
                _pedidoDao.ActualizarEstadoPedido(folio, "ENTREGADO");
                MessageBox.Show($"Pedido #{folio} marcado como ENTREGADO.", "Ciclo de Entrega Completado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarReporteCorte();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo actualizar el estado:\n{ex.Message}", "Validación de Estado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnVerTicket_Click(object? sender, EventArgs e)
        {
            if (dgvCorte.CurrentRow == null)
            {
                MessageBox.Show("Selecciona un pedido en la tabla de corte para ver o imprimir su ticket.", "Selecciona un pedido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = dgvCorte.CurrentRow;
            int folio = Convert.ToInt32(row.Cells["Folio"].Value);
            string fecha = row.Cells["Fecha"].Value?.ToString() ?? "";
            string servicio = row.Cells["Servicio"].Value?.ToString() ?? "";
            string estado = row.Cells["Estado"].Value?.ToString() ?? "";
            string cliente = row.Cells["Cliente"].Value?.ToString() ?? "";
            string telefono = row.Cells["Teléfono"].Value?.ToString() ?? "";
            string cajera = row.Cells["Cajera"].Value?.ToString() ?? "";
            string repartidor = row.Cells["Repartidor"].Value?.ToString() ?? "";
            decimal subtotal = Convert.ToDecimal(row.Cells["Subtotal"].Value);
            decimal extras = Convert.ToDecimal(row.Cells["Extras"].Value);
            decimal total = Convert.ToDecimal(row.Cells["Total"].Value);

            try
            {
                using var frmTicket = new FrmTicketDetalle(
                    folio, fecha, servicio, estado, cliente, telefono,
                    cajera, repartidor, subtotal, extras, total);
                frmTicket.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el detalle del ticket:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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