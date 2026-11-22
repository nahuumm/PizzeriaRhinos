using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using POS_PizzeriaRhinos.Controles;

namespace POS_PizzeriaRhinos.Formularios
{
    /// <summary>
    /// Pantalla de cobro (equivale a "Pagar cuenta" / "PAGOS" del boceto). Calcula el cambio para la cajera.
    /// Nota: el esquema de la BD no tiene entidad de pagos, por lo que el método y el cambio solo se imprimen en el ticket.
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmCobro : Form
    {
        private readonly decimal _total;
        private readonly TextBox _txtRecibido;
        private readonly Label _lblCambio;
        private readonly GrupoOpciones _metodo;
        private readonly BotonRhino _btnConfirmar;
        private readonly Panel _pnlEfectivo;

        public string MetodoPago { get; private set; } = "EFECTIVO";
        public decimal Recibido { get; private set; }
        public decimal Cambio { get; private set; }

        public FrmCobro(decimal total, string tipoServicio, string cliente, int partidas)
        {
            _total = total;
            Text = "Cobrar pedido";
            Icon = UITheme.IconoApp;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            ClientSize = UITheme.S(900, 640);

            // ---------------- Izquierda: resumen y método ----------------
            var izq = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(24) };
            var lblTitulo = new Label { Text = "💵  Cobrar pedido", Font = UITheme.Titulo(22f), ForeColor = UITheme.Texto, AutoSize = false, Height = UITheme.S(46) };
            var lblInfo = new Label
            {
                Text = $"{(tipoServicio == "DOMICILIO" ? "🛵 A domicilio" : "🏪 Mostrador")} · {cliente} · {partidas} partida(s)",
                Font = UITheme.Fuente(11f),
                ForeColor = UITheme.TextoSuave,
                AutoSize = false,
                Height = UITheme.S(30)
            };
            var lblTotalTit = UITheme.CrearEtiqueta("TOTAL A PAGAR");
            lblTotalTit.AutoSize = false;
            lblTotalTit.Height = UITheme.S(24);
            var lblTotal = new Label { Text = UITheme.Dinero(total), Font = UITheme.Titulo(40f), ForeColor = UITheme.Rojo, AutoSize = false, Height = UITheme.S(70) };

            var lblMetodo = UITheme.CrearEtiqueta("MÉTODO DE PAGO");
            lblMetodo.AutoSize = false;
            lblMetodo.Height = UITheme.S(26);
            _metodo = new GrupoOpciones { TamanoBoton = UITheme.S(150, 64), Dock = DockStyle.Top };
            _metodo.Agregar("Efectivo", "EFECTIVO", "💵");
            _metodo.Agregar("Tarjeta", "TARJETA", "💳");
            if (tipoServicio == "DOMICILIO") _metodo.Agregar("Al recibir", "CONTRA_ENTREGA", "🛵");
            _metodo.Seleccionar("EFECTIVO");
            _metodo.ValorCambiado += (_, _) => CambiarMetodo();

            // Efectivo: monto recibido + billetes rápidos + cambio
            _pnlEfectivo = new Panel { Height = UITheme.S(240) };
            var lblRecibido = UITheme.CrearEtiqueta("EFECTIVO RECIBIDO");
            lblRecibido.AutoSize = false;
            lblRecibido.Height = UITheme.S(26);
            _txtRecibido = new TextBox { MaxLength = 9, TextAlign = HorizontalAlignment.Right };
            UITheme.EstilizarTextBox(_txtRecibido);
            _txtRecibido.Font = UITheme.Fuente(22f, FontStyle.Bold);
            _txtRecibido.TextChanged += (_, _) => CalcularCambio();

            var rapidos = new FlowLayoutPanel { Height = UITheme.S(64), WrapContents = false };
            foreach (var monto in new[] { 0m, 200m, 500m, 1000m })
            {
                var b = new BotonRhino(monto == 0 ? "Exacto" : UITheme.Dinero(monto).Replace(".00", ""), EstiloBoton.Secundario)
                {
                    Size = UITheme.S(100, 54),
                    TabStop = false,
                    Font = UITheme.Fuente(11f, FontStyle.Bold)
                };
                decimal m = monto == 0 ? total : monto;
                b.Click += (_, _) => { _txtRecibido.Text = m.ToString("0.00", CultureInfo.InvariantCulture); _txtRecibido.Focus(); _txtRecibido.SelectionStart = _txtRecibido.TextLength; };
                rapidos.Controls.Add(b);
            }
            _lblCambio = new Label { Font = UITheme.Titulo(22f), ForeColor = UITheme.Verde, AutoSize = false, Height = UITheme.S(52) };
            Diseno.Apilar(_pnlEfectivo, null, lblRecibido, _txtRecibido, Diseno.Espacio(8), rapidos, _lblCambio);

            Diseno.Apilar(izq, null, lblTitulo, lblInfo, Diseno.Espacio(10), lblTotalTit, lblTotal, Diseno.Espacio(6),
                lblMetodo, _metodo, Diseno.Espacio(12), _pnlEfectivo);

            // ---------------- Derecha: teclado numérico ----------------
            var der = new Panel { Dock = DockStyle.Right, Width = UITheme.S(330), Padding = UITheme.P(16, 0, 0, 0) };
            var teclado = new TecladoNumerico { Dock = DockStyle.Fill, PermitirDecimal = true };
            teclado.Vincular(_txtRecibido);
            der.Controls.Add(teclado);

            // ---------------- Barra inferior ----------------
            var barra = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(84), Padding = UITheme.P(0, 16, 0, 0) };
            var btnCancelar = new BotonRhino("Regresar", EstiloBoton.Secundario, "◀") { Dock = DockStyle.Left, Width = UITheme.S(200), DialogResult = DialogResult.Cancel };
            _btnConfirmar = new BotonRhino("CONFIRMAR Y ENVIAR A COCINA", EstiloBoton.Exito, "✔") { Dock = DockStyle.Fill };
            _btnConfirmar.Click += (_, _) => Confirmar();
            barra.Controls.Add(_btnConfirmar);
            barra.Controls.Add(new Panel { Dock = DockStyle.Left, Width = UITheme.S(16) });
            barra.Controls.Add(btnCancelar);
            CancelButton = btnCancelar;
            AcceptButton = _btnConfirmar;

            var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(20) };
            var centro = new Panel { Dock = DockStyle.Fill };
            centro.Controls.Add(izq);
            centro.Controls.Add(der);
            cuerpo.Controls.Add(centro);
            cuerpo.Controls.Add(barra);
            Controls.Add(cuerpo);

            CalcularCambio();
            Shown += (_, _) => _txtRecibido.Focus();
        }

        private void CambiarMetodo()
        {
            MetodoPago = _metodo.Valor as string ?? "EFECTIVO";
            _pnlEfectivo.Visible = MetodoPago == "EFECTIVO";
            CalcularCambio();
        }

        private bool LeerRecibido(out decimal valor)
        {
            return decimal.TryParse(_txtRecibido.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
        }

        private void CalcularCambio()
        {
            if (MetodoPago != "EFECTIVO")
            {
                _btnConfirmar.Enabled = true;
                return;
            }
            if (!LeerRecibido(out decimal r) || _txtRecibido.Text.Trim().Length == 0)
            {
                _lblCambio.Text = "Captura el efectivo recibido";
                _lblCambio.ForeColor = UITheme.TextoSuave;
                _btnConfirmar.Enabled = false;
                return;
            }
            decimal cambio = r - _total;
            if (cambio < 0)
            {
                _lblCambio.Text = $"Faltan {UITheme.Dinero(-cambio)}";
                _lblCambio.ForeColor = UITheme.Rojo;
                _btnConfirmar.Enabled = false;
            }
            else
            {
                _lblCambio.Text = $"Cambio: {UITheme.Dinero(cambio)}";
                _lblCambio.ForeColor = UITheme.Verde;
                _btnConfirmar.Enabled = true;
            }
        }

        private void Confirmar()
        {
            if (MetodoPago == "EFECTIVO")
            {
                if (!LeerRecibido(out decimal r) || r < _total)
                {
                    Dialogo.Advertencia(this, "El efectivo recibido debe ser igual o mayor al total.");
                    return;
                }
                Recibido = r;
                Cambio = r - _total;
            }
            else
            {
                Recibido = _total;
                Cambio = 0m;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
