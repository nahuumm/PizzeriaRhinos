using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using POS_PizzeriaRhinos.Controles;

namespace POS_PizzeriaRhinos.Formularios
{
    public enum TipoMensaje { Info, Exito, Advertencia, Error, Pregunta }

    /// <summary>
    /// Ventana de mensaje con el diseño de la aplicación (reemplaza a MessageBox):
    /// mensajes de confirmación, error, advertencia y éxito con botones grandes.
    /// </summary>
    [DesignerCategory("Code")]
    public class FrmMensaje : Form
    {
        private readonly TipoMensaje _tipo;

        public FrmMensaje(string titulo, string mensaje, TipoMensaje tipo, string textoSi, string? textoNo, bool siEsPeligro = false)
        {
            _tipo = tipo;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = UITheme.Superficie;
            KeyPreview = true;
            DoubleBuffered = true;
            Text = titulo;

            int ancho = UITheme.S(540);
            int pad = UITheme.S(30);
            int anchoTexto = ancho - pad * 2;

            var fuenteMensaje = UITheme.Fuente(12f);
            int altoMensaje = TextRenderer.MeasureText(mensaje, fuenteMensaje, new Size(anchoTexto, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + UITheme.S(6);
            altoMensaje = Math.Min(altoMensaje, UITheme.S(340));

            var lblTitulo = new Label
            {
                Text = titulo,
                Font = UITheme.Titulo(19f),
                ForeColor = UITheme.Texto,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Bounds = new Rectangle(pad, UITheme.S(108), anchoTexto, UITheme.S(40))
            };
            var lblMensaje = new Label
            {
                Text = mensaje,
                Font = fuenteMensaje,
                ForeColor = UITheme.TextoSuave,
                TextAlign = ContentAlignment.TopCenter,
                AutoSize = false,
                AutoEllipsis = true,
                Bounds = new Rectangle(pad, UITheme.S(154), anchoTexto, altoMensaje)
            };

            int yBotones = lblMensaje.Bottom + UITheme.S(22);
            int altoBoton = UITheme.S(58);

            var btnSi = new BotonRhino(textoSi, siEsPeligro ? EstiloBoton.Peligro :
                (tipo == TipoMensaje.Exito ? EstiloBoton.Exito : EstiloBoton.Primario))
            {
                DialogResult = DialogResult.OK
            };
            Controls.Add(lblTitulo);
            Controls.Add(lblMensaje);
            Controls.Add(btnSi);
            AcceptButton = btnSi;

            if (textoNo != null)
            {
                var btnNo = new BotonRhino(textoNo, EstiloBoton.Secundario) { DialogResult = DialogResult.Cancel };
                int w = (anchoTexto - UITheme.S(12)) / 2;
                btnNo.Bounds = new Rectangle(pad, yBotones, w, altoBoton);
                btnSi.Bounds = new Rectangle(pad + w + UITheme.S(12), yBotones, w, altoBoton);
                Controls.Add(btnNo);
                CancelButton = btnNo;
            }
            else
            {
                btnSi.Bounds = new Rectangle(pad, yBotones, anchoTexto, altoBoton);
                CancelButton = btnSi;
            }

            ClientSize = new Size(ancho, yBotones + altoBoton + pad);
            Load += (_, _) =>
            {
                using var path = UITheme.RutaRedondeada(new Rectangle(0, 0, Width, Height), UITheme.S(18));
                Region = new Region(path);
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            (Color color, string icono) = _tipo switch
            {
                TipoMensaje.Exito => (UITheme.Verde, "✔"),
                TipoMensaje.Advertencia => (UITheme.Ambar, "⚠"),
                TipoMensaje.Error => (UITheme.Rojo, "✖"),
                TipoMensaje.Pregunta => (UITheme.Rojo, "?"),
                _ => (UITheme.Azul, "ℹ")
            };
            using (var b = new SolidBrush(color)) g.FillRectangle(b, 0, 0, Width, UITheme.S(8));
            int d = UITheme.S(72);
            var rc = new Rectangle((Width - d) / 2, UITheme.S(28), d, d);
            using (var bc = new SolidBrush(UITheme.Mezclar(color, Color.White, 0.82f))) g.FillEllipse(bc, rc);
            using (var f = new Font(UITheme.FamiliaBase, 26f, FontStyle.Bold))
                TextRenderer.DrawText(g, icono, f, rc, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using var pen = new Pen(UITheme.Borde, 2);
            using var path = UITheme.RutaRedondeada(new Rectangle(1, 1, Width - 3, Height - 3), UITheme.S(18));
            g.DrawPath(pen, path);
        }
    }

    /// <summary>Atajos para mostrar mensajes con el diseño de la aplicación.</summary>
    public static class Dialogo
    {
        public static void Info(IWin32Window? owner, string mensaje, string titulo = "Información")
            => Mostrar(owner, titulo, mensaje, TipoMensaje.Info, "Entendido", null);

        public static void Exito(IWin32Window? owner, string mensaje, string titulo = "¡Listo!")
            => Mostrar(owner, titulo, mensaje, TipoMensaje.Exito, "Aceptar", null);

        public static void Advertencia(IWin32Window? owner, string mensaje, string titulo = "Revisa los datos")
            => Mostrar(owner, titulo, mensaje, TipoMensaje.Advertencia, "Entendido", null);

        public static void Error(IWin32Window? owner, string mensaje, string titulo = "Ocurrió un error")
            => Mostrar(owner, titulo, mensaje, TipoMensaje.Error, "Cerrar", null);

        public static bool Confirmar(IWin32Window? owner, string titulo, string mensaje,
            string textoSi = "Sí, continuar", string textoNo = "No, regresar", bool peligro = false)
            => Mostrar(owner, titulo, mensaje, TipoMensaje.Pregunta, textoSi, textoNo, peligro) == DialogResult.OK;

        private static DialogResult Mostrar(IWin32Window? owner, string titulo, string mensaje, TipoMensaje tipo,
            string textoSi, string? textoNo, bool peligro = false)
        {
            using var frm = new FrmMensaje(titulo, mensaje, tipo, textoSi, textoNo, peligro);
            if (owner == null) frm.StartPosition = FormStartPosition.CenterScreen;
            return owner != null ? frm.ShowDialog(owner) : frm.ShowDialog();
        }
    }

    /// <summary>Traduce excepciones técnicas a mensajes entendibles para la cajera / administrador.</summary>
    public static class ErroresUi
    {
        public static string Amigable(Exception ex)
        {
            if (ex is MySqlException m)
            {
                switch (m.Number)
                {
                    case 1062:
                        return "Ya existe un registro con ese dato único (por ejemplo, el teléfono o el usuario ya están registrados).";
                    case 1451:
                        return "No se puede eliminar porque tiene información relacionada (pedidos, recetas o modificadores). " +
                               "Para conservar el historial, desactívalo en lugar de eliminarlo.";
                    case 1452:
                        return "El registro relacionado ya no existe (integridad referencial). Actualiza la pantalla e inténtalo de nuevo.";
                    case 3819:
                        return "Un dato no cumple una regla de la base de datos (restricción CHECK).\n\n" + m.Message;
                    case 1644:
                        return m.Message; // SIGNAL SQLSTATE '45000' lanzado por un trigger o procedimiento
                    case 1042:
                    case 0:
                        return "No hay conexión con el servidor MySQL.\nVerifica que el servicio MySQL80 esté iniciado y que la " +
                               "cadena de conexión en 'Administrar secretos de usuario' sea correcta.";
                    case 1045:
                        return "El usuario o la contraseña de MySQL configurados en los secretos de usuario son incorrectos.";
                    case 1049:
                        return "La base de datos 'pizzeria_rhinos' no existe. Ejecuta los scripts de la carpeta Sql (01 al 04).";
                    default:
                        return $"Error de base de datos ({m.Number}):\n{m.Message}";
                }
            }
            return ex.Message;
        }

        /// <summary>Ejecuta una acción mostrando un mensaje amable si falla.</summary>
        public static bool Intentar(IWin32Window? owner, Action accion, string titulo = "No se pudo completar la operación")
        {
            try
            {
                accion();
                return true;
            }
            catch (Exception ex)
            {
                Dialogo.Error(owner, Amigable(ex), titulo);
                return false;
            }
        }
    }

    public enum ModoEntrada { Texto, Decimal, Entero }

    /// <summary>Ventana pequeña para capturar un solo dato (nombre, precio, cantidad) con teclado en pantalla.</summary>
    [DesignerCategory("Code")]
    public class FrmEntrada : Form
    {
        private readonly TextBox _txt;
        private readonly ModoEntrada _modo;
        private readonly decimal _min, _max;
        public string ValorTexto => _txt.Text.Trim();
        public decimal ValorNumero { get; private set; }

        public FrmEntrada(string titulo, string etiqueta, string valorInicial, ModoEntrada modo,
            int maxLongitud = 60, decimal minimo = 0, decimal maximo = 99999)
        {
            _modo = modo;
            _min = minimo;
            _max = maximo;
            Text = titulo;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = UITheme.Fondo;
            Font = UITheme.FuenteNormal;
            Icon = UITheme.IconoApp;

            bool texto = modo == ModoEntrada.Texto;
            ClientSize = texto ? UITheme.S(820, 560) : UITheme.S(420, 600);

            var card = new PanelRedondeado { Dock = DockStyle.Fill, Padding = UITheme.P(20) };
            var contenedor = new Panel { Dock = DockStyle.Fill, Padding = UITheme.P(16) };
            contenedor.Controls.Add(card);
            Controls.Add(contenedor);

            var lblTitulo = new Label { Text = titulo, Font = UITheme.Titulo(18f), ForeColor = UITheme.Texto, AutoSize = false, Height = UITheme.S(38) };
            var lblEtiqueta = UITheme.CrearEtiqueta(etiqueta.ToUpperInvariant());
            lblEtiqueta.AutoSize = false;
            lblEtiqueta.Height = UITheme.S(26);
            _txt = new TextBox { Text = valorInicial, MaxLength = maxLongitud };
            UITheme.EstilizarTextBox(_txt);
            _txt.Font = UITheme.Fuente(16f);
            if (!texto) _txt.TextAlign = HorizontalAlignment.Right;

            var btnAceptar = new BotonRhino("Guardar", EstiloBoton.Exito, "✔") { Dock = DockStyle.Right, Width = UITheme.S(180) };
            var btnCancelar = new BotonRhino("Cancelar", EstiloBoton.Secundario) { Dock = DockStyle.Left, Width = UITheme.S(160), DialogResult = DialogResult.Cancel };
            btnAceptar.Click += (_, _) => Aceptar();
            AcceptButton = btnAceptar;
            CancelButton = btnCancelar;

            Control teclado;
            if (texto)
            {
                var tv = new TecladoVirtual();
                tv.Vincular(_txt);
                teclado = tv;
            }
            else
            {
                var tn = new TecladoNumerico { PermitirDecimal = modo == ModoEntrada.Decimal };
                tn.Vincular(_txt);
                teclado = tn;
            }

            var barraAbajo = new Panel { Dock = DockStyle.Bottom, Height = UITheme.S(60) };
            barraAbajo.Controls.Add(btnAceptar);
            barraAbajo.Controls.Add(btnCancelar);

            // Abajo: botones; arriba: título, etiqueta y cuadro de texto; el teclado ocupa el resto
            var espacioAbajo = Diseno.Espacio(12);
            espacioAbajo.Dock = DockStyle.Bottom;
            Diseno.Apilar(card, teclado, lblTitulo, lblEtiqueta, _txt, Diseno.Espacio(12));
            card.Controls.Add(espacioAbajo);
            card.Controls.Add(barraAbajo);

            Shown += (_, _) => { _txt.Focus(); _txt.SelectAll(); };
        }

        private void Aceptar()
        {
            string v = _txt.Text.Trim();
            if (_modo == ModoEntrada.Texto)
            {
                if (v.Length == 0)
                {
                    Dialogo.Advertencia(this, "El campo no puede quedar vacío.");
                    return;
                }
            }
            else
            {
                if (!decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) &&
                    !decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, out n))
                {
                    Dialogo.Advertencia(this, "Escribe un número válido.");
                    return;
                }
                if (_modo == ModoEntrada.Entero && n != Math.Floor(n))
                {
                    Dialogo.Advertencia(this, "Escribe un número entero (sin decimales).");
                    return;
                }
                if (n < _min || n > _max)
                {
                    Dialogo.Advertencia(this, $"El valor debe estar entre {_min:0.##} y {_max:0.##}.");
                    return;
                }
                ValorNumero = n;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
