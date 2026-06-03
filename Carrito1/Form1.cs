// ════════════════════════════════════════════════════════════
//  LA JEEPETA — Control PC v6
//  Windows Forms — Comunicación Serial USB con ESP32
//
//  PROTOCOLO ESP32:
//    S                → Detener
//    C,pwmL,pwmR      → Velocidad base motores
//    KP,1.5           → Constante corrección recta
//    CFG,ppc,giro     → pulsosPorCm, pulsosGiro90
//    CAL_START/STOP   → Calibración encoders
//    A,dir,dist,unid  → Movimiento autónomo
// ════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.XInput;

namespace Carrito1
{
    public partial class Form1 : Form
    {
        // ─── Controles UI ─────────────────────────────────────
        private PictureBox pictureDibujo;
        private Button btnCentimetros, btnMetros, btnLimpiar, btnEnviar, btnPausar;
        private Button btnConectar, btnRefrescarPuertos;
        private Button btnModoManual, btnModoAuto;
        private ComboBox cmbPuertos;
        private Label lblEstadoConexion, lblModoActual;
        private DataGridView tablaCoordenadas;

        // Ajuste motores (expuestos en UI)
        private TextBox txtSpeedL, txtSpeedR, txtKp;
        private TextBox txtEmpujeIzq, txtEmpujeDer;
        private TextBox txtPpc, txtGiro;
        private TextBox txtPausaMs;

        // ─── Xbox D-Pad ───────────────────────────────────────
        private Controller xboxController;
        private System.Windows.Forms.Timer timerXbox;
        private string ultimaDireccionXbox = "";   // evita re-enviar el mismo comando
        private bool modoManualActivo = false;

        // ─── Serial / Estado ──────────────────────────────────
        private SerialPort puertoSerial;
        private bool esp32Conectado = false;

        // ─── Trayectoria ──────────────────────────────────────
        private readonly List<Point> puntosPantalla = new List<Point>();
        private readonly List<Point> coordenadas = new List<Point>();
        private readonly List<double> distancias = new List<double>();
        private readonly List<string> unidadesPorPunto = new List<string>();

        private Point? puntoOrigen = null;
        private string unidadSeleccionada = "cm";

        // 1 cuadro del grid = 10 cm (o 1 m si unidad es metros)
        private const int GRID_PX = 35;

        // ════════════════════════════════════════════════════════
        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScroll = false;
            CrearInterfaz();
            IniciarXbox();
        }


        private bool motoresAplicados = false;
        private bool calibracionAplicada = false;


        private void VerificarHabilitar()
        {
            bool habilitar = motoresAplicados && calibracionAplicada;
            btnEnviar.Enabled = habilitar;
            btnPausar.Enabled = habilitar;
            btnModoManual.Enabled = habilitar;
            btnModoAuto.Enabled = habilitar;
            btnLimpiar.Enabled = habilitar;
        }

        // ════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN DE LA UI
        // ════════════════════════════════════════════════════════
        private void CrearInterfaz()
        {
            this.Text = "La Jeepeta — Control";
            this.BackColor = Color.FromArgb(245, 245, 245);

            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            int TOP = 65; // altura barra superior

            // ── Barra superior ────────────────────────────────────────
            Panel barra = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(W, TOP),
                BackColor = Color.FromArgb(145, 110, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            barra.Controls.Add(new Label
            {
                Text = "LA JEEPETA — CONTROL DE MOVIMIENTO",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            this.Controls.Add(barra);

            // Dimensiones de columnas
            int PAD = 18;
            int colW1 = (int)(W * 0.606);             // Retícula
            int colW2 = (int)(W * 0.35);             // Config izq
            int colW3 = (int)(W * 0.45) - PAD * 2;   // Config der (ajustable)
            int colH = H - TOP - PAD * 2;

            int x1 = PAD;
            int x2 = x1 + colW1 + PAD;
            int x3 = x2 + colW2 + PAD;
            int yTop = TOP + PAD;

            // ══════════════════════════════════════════════════════════
            //  COLUMNA 1 — Área de dibujo
            // ══════════════════════════════════════════════════════════
            Panel colDibujo = new Panel
            {
                Location = new Point(x1, yTop),
                Size = new Size(colW1, colH),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
            };
            this.Controls.Add(colDibujo);

            colDibujo.Controls.Add(new Label
            {
                Text = "Área de dibujo",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(8, 8),
                AutoSize = true
            });

            pictureDibujo = new PictureBox
            {
                Location = new Point(8, 36),
                Size = new Size(colW1 - 16, colH - 44),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
            };
            pictureDibujo.MouseDown += PictureDibujo_MouseDown;
            pictureDibujo.MouseUp += PictureDibujo_MouseUp;
            pictureDibujo.Paint += PictureDibujo_Paint;
            colDibujo.Controls.Add(pictureDibujo);

            // ══════════════════════════════════════════════════════════
            //  COLUMNA 2 — Configuración (Puntos / Unidad / Conexión / Modo)
            // ══════════════════════════════════════════════════════════
            Panel col2 = new Panel
            {
                Location = new Point(x2, yTop),
                Size = new Size(colW2, colH),
                BackColor = Color.FromArgb(245, 245, 245),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
            };
            this.Controls.Add(col2);

            int y = 0;

            AgregarLabel(col2, "Configuración", 14, new Point(0, y)); y += 38;

            // — Tabla de puntos —
            AgregarLabel(col2, "Puntos de Trayectoria", 10, new Point(0, y)); y += 22;
            tablaCoordenadas = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(colW2, 115),
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                ColumnCount = 5,
                Font = new Font("Segoe UI", 8)
            };
            tablaCoordenadas.Columns[0].Name = "Punto";
            tablaCoordenadas.Columns[1].Name = "X";
            tablaCoordenadas.Columns[2].Name = "Y";
            tablaCoordenadas.Columns[3].Name = "(X,Y)";
            tablaCoordenadas.Columns[4].Name = "Dist.";
            col2.Controls.Add(tablaCoordenadas);
            y += 124;

            col2.Controls.Add(Sep2(y, colW2)); y += 14;

            // — Unidad de Medida —
            AgregarLabel(col2, "Unidad de Medida", 10, new Point(0, y)); y += 22;
            int bw2 = (colW2 - 6) / 2;
            btnCentimetros = CrearBoton("Centímetros", new Point(0, y), new Size(bw2, 32), Color.FromArgb(55, 180, 125));
            btnCentimetros.Click += (s, e) => SeleccionarUnidad("cm");
            col2.Controls.Add(btnCentimetros);
            btnMetros = CrearBoton("Metros", new Point(bw2 + 6, y), new Size(bw2, 32), Color.FromArgb(210, 210, 210));
            btnMetros.ForeColor = Color.Black;
            btnMetros.Click += (s, e) => SeleccionarUnidad("m");
            col2.Controls.Add(btnMetros);
            y += 42;

            col2.Controls.Add(Sep2(y, colW2)); y += 14;

            // — Conexión —
            AgregarLabel(col2, "Conexión USB — ESP32", 10, new Point(0, y)); y += 22;
            cmbPuertos = new ComboBox { Location = new Point(0, y), Size = new Size(colW2 - 160, 26), Font = new Font("Segoe UI", 9) };
            col2.Controls.Add(cmbPuertos);
            btnRefrescarPuertos = CrearBoton("↻", new Point(colW2 - 155, y - 1), new Size(30, 28), Color.FromArgb(80, 80, 80));
            btnRefrescarPuertos.Click += (s, e) => CargarPuertos();
            col2.Controls.Add(btnRefrescarPuertos);
            btnConectar = CrearBoton("Conectar", new Point(colW2 - 120, y - 1), new Size(120, 28), Color.FromArgb(35, 135, 210));
            btnConectar.Click += BtnConectar_Click;
            col2.Controls.Add(btnConectar);
            y += 34;
            lblEstadoConexion = new Label
            {
                Text = "● No conectado",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Red,
                Location = new Point(0, y),
                AutoSize = true
            };
            col2.Controls.Add(lblEstadoConexion);
            y += 28;

            col2.Controls.Add(Sep2(y, colW2)); y += 14;

            // — Modo de operación —
            AgregarLabel(col2, "Modo de operación", 10, new Point(0, y)); y += 22;
            btnModoManual = CrearBoton("Modo Manual", new Point(0, y), new Size(bw2, 32), Color.FromArgb(80, 80, 80));
            btnModoManual.Click += BtnModoManual_Click;
            col2.Controls.Add(btnModoManual);
            btnModoAuto = CrearBoton("Modo Autónomo", new Point(bw2 + 6, y), new Size(bw2, 32), Color.FromArgb(80, 80, 80));
            btnModoAuto.Click += BtnModoAuto_Click;
            col2.Controls.Add(btnModoAuto);
            y += 40;
            lblModoActual = new Label
            {
                Text = "Modo actual: Ninguno",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(0, y),
                AutoSize = true
            };
            col2.Controls.Add(lblModoActual);

            // ══════════════════════════════════════════════════════════
            //  COLUMNA 3 — Motores / Calibración / Acciones
            // ══════════════════════════════════════════════════════════
            Panel col3 = new Panel
            {
                Location = new Point(x3, 143),
                Size = new Size(colW3, colH),
                BackColor = Color.FromArgb(245, 245, 245),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            this.Controls.Add(col3);

            int y3 = 0;
            int pw = colW3 - 4; // ancho interno de paneles

            // ── Panel Motores ─────────────────────────────────────────
            Panel panelMotores = new Panel
            {
                Location = new Point(0, y3),
                Size = new Size(pw, 180),
                BackColor = Color.FromArgb(240, 248, 255),
                BorderStyle = BorderStyle.FixedSingle
            };
            col3.Controls.Add(panelMotores);

            panelMotores.Controls.Add(new Label
            {
                Text = "⚙  Ajuste de Motores  (corrige si va chueco)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 80, 150),
                Location = new Point(10, 8),
                AutoSize = true
            });

            int mx = 10, my = 34;
            int col3w = (pw - 30) / 3;
            panelMotores.Controls.Add(new Label { Text = "Motor Izq (L)", Location = new Point(mx, my), AutoSize = true, Font = new Font("Segoe UI", 9) });
            panelMotores.Controls.Add(new Label { Text = "Motor Der (R)", Location = new Point(mx + col3w, my), AutoSize = true, Font = new Font("Segoe UI", 9) });
            panelMotores.Controls.Add(new Label { Text = "Kp corrección", Location = new Point(mx + col3w * 2, my), AutoSize = true, Font = new Font("Segoe UI", 9) });
            my += 20;
            txtSpeedL = new TextBox { Location = new Point(mx, my), Size = new Size(col3w - 10, 26), Text = "200", Font = new Font("Segoe UI", 11) };
            txtSpeedR = new TextBox { Location = new Point(mx + col3w, my), Size = new Size(col3w - 10, 26), Text = "225", Font = new Font("Segoe UI", 11) };
            txtKp = new TextBox { Location = new Point(mx + col3w * 2, my), Size = new Size(col3w - 10, 26), Text = "1.5", Font = new Font("Segoe UI", 11) };
            panelMotores.Controls.Add(txtSpeedL);
            panelMotores.Controls.Add(txtSpeedR);
            panelMotores.Controls.Add(txtKp);

            my += 30;
            panelMotores.Controls.Add(new Label
            {
                Text = "Va a la derecha → baja R  |  Va a la izquierda → baja L",
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.Gray,
                Location = new Point(mx, my),
                AutoSize = true
            });

            my += 20;
            int lw = (pw - 30) / 2 - 5;
            // Empuje izq
            panelMotores.Controls.Add(new Label { Text = "Empuje Izq (giro L):", Location = new Point(mx, my + 4), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(120, 60, 0) });
            txtEmpujeIzq = new TextBox { Location = new Point(mx + 145, my), Size = new Size(55, 26), Text = "150", Font = new Font("Segoe UI", 11) };
            panelMotores.Controls.Add(txtEmpujeIzq);
            // Empuje der
            panelMotores.Controls.Add(new Label { Text = "Empuje Der (giro R):", Location = new Point(mx + lw + 10, my + 4), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(120, 60, 0) });
            txtEmpujeDer = new TextBox { Location = new Point(mx + lw + 155, my), Size = new Size(55, 26), Text = "170", Font = new Font("Segoe UI", 11) };
            panelMotores.Controls.Add(txtEmpujeDer);

            my += 34;
            var btnAplicarMotores = CrearBoton("Aplicar Motores", new Point(mx, my), new Size(150, 30), Color.FromArgb(35, 135, 210));
            btnAplicarMotores.Click += (s, e) =>
            {
                if (int.TryParse(txtSpeedL.Text, out int sL) &&
                    int.TryParse(txtSpeedR.Text, out int sR) &&
                    float.TryParse(txtKp.Text.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float kp))
                {
                    EnviarComando($"C,{sL},{sR}");
                    EnviarComando($"KP,{kp:0.00}");
                }
                else
                    MessageBox.Show("Valores inválidos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                motoresAplicados = true;
                VerificarHabilitar();
            };
            panelMotores.Controls.Add(btnAplicarMotores);

            y3 += 190;

            // ── Panel Calibración ─────────────────────────────────────
            Panel panelCal = new Panel
            {
                Location = new Point(0, 190),
                Size = new Size(pw, 130),
                BackColor = Color.FromArgb(255, 248, 235),
                BorderStyle = BorderStyle.FixedSingle
            };
            col3.Controls.Add(panelCal);

            panelCal.Controls.Add(new Label
            {
                Text = "📏  Calibración de Encoders",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 80, 10),
                Location = new Point(10, 8),
                AutoSize = true
            });

            int cx = 10, cy = 34;
            panelCal.Controls.Add(new Label { Text = "pulsos / cm", Location = new Point(cx, cy), AutoSize = true, Font = new Font("Segoe UI", 9) });
            panelCal.Controls.Add(new Label { Text = "pulsos / 90°", Location = new Point(cx + 160, cy), AutoSize = true, Font = new Font("Segoe UI", 9) });
            cy += 20;
            txtPpc = new TextBox { Location = new Point(cx, cy), Size = new Size(75, 26), Text = "0.7", Font = new Font("Segoe UI", 11) };
            txtGiro = new TextBox { Location = new Point(cx + 160, cy), Size = new Size(75, 26), Text = "14", Font = new Font("Segoe UI", 11) };
            panelCal.Controls.Add(txtPpc);
            panelCal.Controls.Add(txtGiro);

            var btnAplicarCfg = CrearBoton("Aplicar", new Point(cx + 310, cy - 2), new Size(95, 30), Color.FromArgb(55, 140, 80));
            btnAplicarCfg.Click += (s, e) =>
            {
                if (float.TryParse(txtPpc.Text.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float ppc) &&
                    int.TryParse(txtGiro.Text, out int g))
                    EnviarComando($"CFG,{ppc:0.0},{g}");
                else
                    MessageBox.Show("Valores inválidos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                calibracionAplicada = true;
                VerificarHabilitar();
            };
            panelCal.Controls.Add(btnAplicarCfg);

            cy += 38;
            var btnCalibrar = CrearBoton("🔧 Calibrar en pista", new Point(cx, cy), new Size(185, 30), Color.FromArgb(170, 100, 10));
            btnCalibrar.Click += async (s, e) =>
            {
                if (!VerificarConexion()) return;
                EnviarComando("CAL_START");
                await Task.Delay(100);
                MessageBox.Show("Mueve el carrito exactamente 100 cm y presiona OK.",
                    "Calibración en curso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                EnviarComando("CAL_STOP");
                MessageBox.Show(
                    "Revisa el Monitor Serial (CAL_RESULT,izq,der).\nPromedio ÷ 100 = pulsos/cm.",
                    "Resultado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            panelCal.Controls.Add(btnCalibrar);

            y3 += 137;

            // ── Acciones ──────────────────────────────────────────────
            y3 += 10;
            col3.Controls.Add(new Label
            {
                Text = "Acciones",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(0, y3),
                AutoSize = true
            });
            y3 += 28;

            col3.Controls.Add(new Label { Text = "Pausa entre tramos:", Location = new Point(0, y3 + 5), AutoSize = true, Font = new Font("Segoe UI", 9) });
            txtPausaMs = new TextBox { Location = new Point(148, y3), Size = new Size(58, 26), Text = "1500", Font = new Font("Segoe UI", 11) };
            col3.Controls.Add(txtPausaMs);
            col3.Controls.Add(new Label { Text = "ms", Location = new Point(212, y3 + 5), AutoSize = true, Font = new Font("Segoe UI", 9) });
            y3 += 36;

            int bwa = (pw - 12) / 3;
            btnEnviar = CrearBoton("▶ Ejecutar", new Point(0, y3), new Size(bwa, 38), Color.FromArgb(35, 135, 210));
            btnPausar = CrearBoton("⏸ Pausar", new Point(bwa + 6, y3), new Size(bwa, 38), Color.FromArgb(245, 190, 40));
            btnLimpiar = CrearBoton("✕ Limpiar", new Point((bwa + 6) * 2, y3), new Size(bwa, 38), Color.FromArgb(215, 55, 55));
            btnEnviar.Click += BtnEnviar_Click;
            btnPausar.Click += (s, e) => EnviarComando("S");
            btnLimpiar.Click += BtnLimpiar_Click;
            col3.Controls.Add(btnEnviar);
            col3.Controls.Add(btnPausar);
            col3.Controls.Add(btnLimpiar);

            CargarPuertos();

            btnEnviar.Enabled = false;
            btnPausar.Enabled = false;
            btnModoManual.Enabled = false;
            btnModoAuto.Enabled = false;
            btnLimpiar.Enabled = false;
        }

        // Helper separador para col2 (sin padding izquierdo fijo)
        private Panel Sep2(int y, int w) =>
            new Panel { Location = new Point(0, y), Size = new Size(w, 1), BackColor = Color.FromArgb(220, 220, 220) };

        // ════════════════════════════════════════════════════════
        //  HELPERS UI
        // ════════════════════════════════════════════════════════
        private Panel CrearTarjeta(Point location, Size size) =>
            new Panel
            {
                Location = location,
                Size = size,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };

        private Button CrearBoton(string texto, Point location, Size size, Color color) =>
            new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = location,
                Size = size,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };

        private void AgregarLabel(Control parent, string texto, int fontSize, Point location)
        {
            parent.Controls.Add(new Label
            {
                Text = texto,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                Location = location,
                AutoSize = true
            });
        }

        // Línea separadora horizontal
        private Panel Separador(int y) =>
            new Panel
            {
                Location = new Point(30, y),
                Size = new Size(500, 1),
                BackColor = Color.FromArgb(220, 220, 220)
            };

        // ════════════════════════════════════════════════════════
        //  PUERTOS
        // ════════════════════════════════════════════════════════
        private void CargarPuertos()
        {
            cmbPuertos.Items.Clear();
            string[] puertos = SerialPort.GetPortNames();
            cmbPuertos.Items.AddRange(puertos);

            if (cmbPuertos.Items.Contains("COM4"))
                cmbPuertos.SelectedItem = "COM4";
            else if (cmbPuertos.Items.Count > 0)
                cmbPuertos.SelectedIndex = 0;
        }

        // ════════════════════════════════════════════════════════
        //  CONEXIÓN
        // ════════════════════════════════════════════════════════
        private void BtnConectar_Click(object sender, EventArgs e)
        {
            if (esp32Conectado) { Desconectar(); return; }

            if (cmbPuertos.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un puerto COM.", "Sin puerto",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                puertoSerial?.Dispose();
                puertoSerial = new SerialPort
                {
                    PortName = cmbPuertos.SelectedItem.ToString(),
                    BaudRate = 115200,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadTimeout = 2000,
                    WriteTimeout = 1000
                };
                puertoSerial.Open();

                esp32Conectado = true;
                lblEstadoConexion.Text = "● Conectado";
                lblEstadoConexion.ForeColor = Color.Green;
                btnConectar.Text = "Desconectar";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar:\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Desconectar()
        {
            try { puertoSerial?.Close(); } catch { }
            esp32Conectado = false;
            lblEstadoConexion.Text = "● No conectado";
            lblEstadoConexion.ForeColor = Color.Red;
            btnConectar.Text = "Conectar";
        }

        // ════════════════════════════════════════════════════════
        //  ENVÍO DE COMANDOS
        // ════════════════════════════════════════════════════════
        private void EnviarComando(string comando)
        {
            if (!esp32Conectado || puertoSerial == null || !puertoSerial.IsOpen) return;
            try { puertoSerial.WriteLine(comando); }
            catch { /* Puerto cerrado inesperadamente — ignorar silenciosamente */ }
        }

        // ════════════════════════════════════════════════════════
        //  MODOS
        // ════════════════════════════════════════════════════════
        private void BtnModoManual_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            modoManualActivo = true;
            EnviarComando("MODE,MANUAL");  // ← línea nueva
            lblModoActual.Text = "Modo actual: Manual (D-Pad activo)";
            btnModoManual.BackColor = Color.FromArgb(55, 180, 125);
            btnModoAuto.BackColor = Color.FromArgb(80, 80, 80);
            ultimaDireccionXbox = "";
        }

        private void BtnModoAuto_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            modoManualActivo = false;
            EnviarComando("S");
            EnviarComando("MODE,AUTO");    // ← línea nueva
            lblModoActual.Text = "Modo actual: Autonomo";
            btnModoAuto.BackColor = Color.FromArgb(55, 180, 125);
            btnModoManual.BackColor = Color.FromArgb(80, 80, 80);
        }

        private bool VerificarConexion()
        {
            if (esp32Conectado) return true;
            MessageBox.Show("Primero conecta el ESP32.", "Sin conexión",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // ════════════════════════════════════════════════════════
        //  UNIDADES
        // ════════════════════════════════════════════════════════
        private void SeleccionarUnidad(string unidad)
        {
            unidadSeleccionada = unidad;
            bool esCm = unidad == "cm";
            btnCentimetros.BackColor = esCm ? Color.FromArgb(55, 180, 125) : Color.FromArgb(210, 210, 210);
            btnCentimetros.ForeColor = esCm ? Color.White : Color.Black;
            btnMetros.BackColor = esCm ? Color.FromArgb(210, 210, 210) : Color.FromArgb(55, 180, 125);
            btnMetros.ForeColor = esCm ? Color.Black : Color.White;
        }

        // ════════════════════════════════════════════════════════
        //  LIMPIAR
        // ════════════════════════════════════════════════════════
        private void BtnLimpiar_Click(object sender, EventArgs e)
        {
            puntosPantalla.Clear();
            coordenadas.Clear();
            distancias.Clear();
            unidadesPorPunto.Clear();
            puntoOrigen = null;
            tablaCoordenadas.Rows.Clear();
            pictureDibujo.Invalidate();
            if (esp32Conectado) EnviarComando("S");
        }

        // ════════════════════════════════════════════════════════
        //  EJECUTAR TRAYECTORIA — Espera DONE del ESP32 en cada tramo
        // ════════════════════════════════════════════════════════
        private async void BtnEnviar_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            if (coordenadas.Count < 2)
            {
                MessageBox.Show("Dibuja al menos 2 puntos.", "Sin trayectoria",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnEnviar.Enabled = false;
            var tcs = new TaskCompletionSource<bool>();

            SerialDataReceivedEventHandler handler = null;
            handler = (s, ev) =>
            {
                try
                {
                    string linea = puertoSerial.ReadLine().Trim();
                    if (linea == "DONE") tcs.TrySetResult(true);
                }
                catch { }
            };
            puertoSerial.DataReceived += handler;

            // Pausa entre comandos (configurable desde la UI)
            int pausa = int.TryParse(txtPausaMs.Text, out int p) ? Math.Max(0, p) : 800;

            // Orientaciones: 0=Norte 1=Este 2=Sur 3=Oeste
            int orientacionActual = 0;

            for (int i = 1; i < coordenadas.Count; i++)
            {
                Point anterior = coordenadas[i - 1];
                Point actual = coordenadas[i];
                int dx = actual.X - anterior.X;
                int dy = actual.Y - anterior.Y;

                int orientacionDeseada = (Math.Abs(dx) > Math.Abs(dy))
                    ? (dx > 0 ? 1 : 3)
                    : (dy > 0 ? 0 : 2);

                int diferencia = (orientacionDeseada - orientacionActual + 4) % 4;

                // Girar según diferencia de orientación
                if (diferencia == 1)
                {
                    // Giro derecha: aplicar empuje extra al motor derecho
                    int sL2 = int.TryParse(txtSpeedL.Text, out int _sL2) ? _sL2 : 160;
                    int sR2 = int.TryParse(txtSpeedR.Text, out int _sR2) ? _sR2 : 200;
                    int empujeDer = int.TryParse(txtEmpujeDer.Text, out int _ed) ? _ed : 230;
                    EnviarComando($"C,{sL2},{empujeDer}");
                    await Task.Delay(50);
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,R,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(pausa);
                    EnviarComando($"C,{sL2},{sR2}");
                }
                else if (diferencia == 3)
                {
                    // Giro izquierda: aplicar empuje extra al motor izquierdo
                    int sL = int.TryParse(txtSpeedL.Text, out int _sL) ? _sL : 160;
                    int sR = int.TryParse(txtSpeedR.Text, out int _sR) ? _sR : 200;
                    int empujeIzq = int.TryParse(txtEmpujeIzq.Text, out int _e) ? _e : 230;

                    EnviarComando($"C,{empujeIzq},{sR}");   // ← sube L solo para este giro
                    await Task.Delay(50);                    // pequeña pausa para que llegue antes del A,L
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,L,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(pausa);
                    EnviarComando($"C,{sL},{sR}");           // ← restaura L a su valor normal
                }
                else if (diferencia == 2)
                {
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,R,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(pausa);   // ← pausa entre los dos giros
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,R,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(pausa);   // ← pausa tras el segundo giro
                }

                // Avanzar — timeout generoso (máx 30 s por tramo)
                tcs = new TaskCompletionSource<bool>();
                EnviarComando($"A,F,{distancias[i]:0.00},{unidadesPorPunto[i]}");
                await Task.WhenAny(tcs.Task, Task.Delay(30000));
                await Task.Delay(pausa);       // ← pausa tras avanzar

                orientacionActual = orientacionDeseada;
            }

            puertoSerial.DataReceived -= handler;
            EnviarComando("S");
            btnEnviar.Enabled = true;
            MessageBox.Show("Trayectoria completada.", "Listo",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════════
        //  DIBUJO
        // ════════════════════════════════════════════════════════
        private void PictureDibujo_MouseDown(object sender, MouseEventArgs e)
        {
            AgregarPunto(e.Location);
        }

        private void PictureDibujo_MouseUp(object sender, MouseEventArgs e) { }

        private void AgregarPunto(Point punto)
        {
            if (puntoOrigen == null) puntoOrigen = punto;

            int xPix = punto.X - puntoOrigen.Value.X;
            int yPix = puntoOrigen.Value.Y - punto.Y;

            Point puntoUnidad = new Point(
                (int)Math.Round(xPix / (double)GRID_PX),
                (int)Math.Round(yPix / (double)GRID_PX)
            );

            if (coordenadas.Count > 0 && coordenadas[coordenadas.Count - 1] == puntoUnidad)
                return;

            puntosPantalla.Add(punto);
            coordenadas.Add(puntoUnidad);

            double distancia = 0;
            if (coordenadas.Count > 1)
            {
                Point prev = coordenadas[coordenadas.Count - 2];
                int dx = puntoUnidad.X - prev.X;
                int dy = puntoUnidad.Y - prev.Y;
                double cuadros = Math.Sqrt(dx * dx + dy * dy);
                distancia = unidadSeleccionada == "cm" ? cuadros * 10 : cuadros;
            }

            distancias.Add(distancia);
            unidadesPorPunto.Add(unidadSeleccionada);

            ActualizarTabla();
            pictureDibujo.Invalidate();
        }

        private void PictureDibujo_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DibujarGrid(g);

            if (puntosPantalla.Count == 0) return;

            using (Pen lineaPen = new Pen(Color.FromArgb(45, 120, 170), 3))
            using (Font fuenteLabel = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                for (int i = 0; i < puntosPantalla.Count; i++)
                {
                    Point p = puntosPantalla[i];
                    if (i > 0) g.DrawLine(lineaPen, puntosPantalla[i - 1], p);

                    if (i == 0)
                    {
                        using (Pen verde = new Pen(Color.Green, 4))
                            g.DrawEllipse(verde, p.X - 10, p.Y - 10, 20, 20);
                        g.DrawString("ORIGEN (0,0)", fuenteLabel, Brushes.DarkGreen, p.X - 55, p.Y + 14);
                    }
                    else
                    {
                        g.FillEllipse(Brushes.Red, p.X - 5, p.Y - 5, 10, 10);
                        g.DrawString(i.ToString(), fuenteLabel, Brushes.DarkRed, p.X + 6, p.Y - 14);
                    }
                }
            }
        }

        private void DibujarGrid(Graphics g)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(190, 210, 215), 1))
            using (Pen ejePen = new Pen(Color.FromArgb(90, 150, 175), 2))
            {
                for (int x = 0; x < pictureDibujo.Width; x += GRID_PX)
                    g.DrawLine(gridPen, x, 0, x, pictureDibujo.Height);
                for (int y = 0; y < pictureDibujo.Height; y += GRID_PX)
                    g.DrawLine(gridPen, 0, y, pictureDibujo.Width, y);

                if (puntoOrigen != null)
                {
                    g.DrawLine(ejePen, puntoOrigen.Value.X, 0, puntoOrigen.Value.X, pictureDibujo.Height);
                    g.DrawLine(ejePen, 0, puntoOrigen.Value.Y, pictureDibujo.Width, puntoOrigen.Value.Y);
                }
            }
        }

        // ════════════════════════════════════════════════════════
        //  TABLA
        // ════════════════════════════════════════════════════════
        private void ActualizarTabla()
        {
            tablaCoordenadas.Rows.Clear();
            for (int i = 0; i < coordenadas.Count; i++)
            {
                Point p = coordenadas[i];
                tablaCoordenadas.Rows.Add(
                    i + 1, p.X, p.Y,
                    $"({p.X},{p.Y})",
                    $"{distancias[i]:0.00} {unidadesPorPunto[i]}"
                );
            }
        }


        // ════════════════════════════════════════════════════════
        //  XBOX D-PAD
        // ════════════════════════════════════════════════════════
        private void IniciarXbox()
        {
            xboxController = new Controller(UserIndex.One);

            timerXbox = new System.Windows.Forms.Timer { Interval = 50 }; // poll cada 50ms
            timerXbox.Tick += TimerXbox_Tick;
            timerXbox.Start();
        }

        private void TimerXbox_Tick(object sender, EventArgs e)
        {
            // Solo actuar en Modo Manual y con ESP32 conectado
            if (!modoManualActivo || !esp32Conectado) return;

            // Verificar si hay control conectado
            if (!xboxController.IsConnected)
            {
                if (ultimaDireccionXbox != "NOCONTROL")
                {
                    ultimaDireccionXbox = "NOCONTROL";
                    this.Invoke((Action)(() =>
                        lblModoActual.Text = "Modo actual: Manual (sin control Xbox)"));
                }
                return;
            }

            var state = xboxController.GetState();
            var dpad = state.Gamepad.Buttons;

            // Leer velocidades actuales de la UI
            int sL = int.TryParse(txtSpeedL.Text, out int _sL) ? _sL : 200;
            int sR = int.TryParse(txtSpeedR.Text, out int _sR) ? _sR : 200;

            string direccion;
            string comando;

            if ((dpad & GamepadButtonFlags.DPadUp) != 0)
            {
                direccion = "UP";
                comando = $"M,1,{sL},1,{sR}";   // Adelante
            }
            else if ((dpad & GamepadButtonFlags.DPadDown) != 0)
            {
                direccion = "DOWN";
                comando = $"M,0,{sL},0,{sR}";   // Atrás
            }
            else if ((dpad & GamepadButtonFlags.DPadLeft) != 0)
            {
                direccion = "LEFT";
                comando = $"M,0,{sL},1,{sR}";   // Giro izquierda (pivot)
            }
            else if ((dpad & GamepadButtonFlags.DPadRight) != 0)
            {
                direccion = "RIGHT";
                comando = $"M,1,{sL},0,{sR}";   // Giro derecha (pivot)
            }
            else
            {
                direccion = "STOP";
                comando = "S";
            }

            // Solo enviar si cambió la dirección (evita spam serial)
            if (direccion != ultimaDireccionXbox)
            {
                ultimaDireccionXbox = direccion;
                EnviarComando(comando);

                this.Invoke((Action)(() =>
                {
                    string icono;
                    switch (direccion)
                    {
                        case "UP":
                            icono = "↑ Adelante";
                            break;
                        case "DOWN":
                            icono = "↓ Atrás";
                            break;
                        case "LEFT":
                            icono = "← Izquierda";
                            break;
                        case "RIGHT":
                            icono = "→ Derecha";
                            break;
                        default:
                            icono = "■ Detenido";
                            break;
                    }
                    lblModoActual.Text = $"Modo actual: Manual — {icono}";
                }));
            }
        }

        // ════════════════════════════════════════════════════════
        //  CIERRE
        // ════════════════════════════════════════════════════════
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timerXbox?.Stop();
            if (esp32Conectado) EnviarComando("S");
            try { puertoSerial?.Close(); puertoSerial?.Dispose(); } catch { }
            base.OnFormClosing(e);
        }
    }
}