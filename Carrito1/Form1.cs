// ════════════════════════════════════════════════════════════
//  LA JEEPETA — Control PC v3
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
        private TextBox txtEmpujeIzq;
        private TextBox txtPpc, txtGiro;
        private TextBox txtPausaMs;

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
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.AutoScroll = true;
            CrearInterfaz();
        }

        // ════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN DE LA UI
        // ════════════════════════════════════════════════════════
        private void CrearInterfaz()
        {
            this.Text = "La Jeepeta — Control";
            this.BackColor = Color.FromArgb(245, 245, 245);

            // ── Barra superior ──────────────────────────────────
            Panel barraSuperior = new Panel
            {
                Size = new Size(this.ClientSize.Width, 80),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(145, 110, 10),
                Dock = DockStyle.Top
            };
            barraSuperior.Controls.Add(new Label
            {
                Text = "LA JEEPETA — CONTROL DE MOVIMIENTO",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(this.ClientSize.Width, 70),
                Location = new Point(0, 5),
                TextAlign = ContentAlignment.MiddleCenter
            });
            this.Controls.Add(barraSuperior);

            // ── Tarjeta Dibujo ──────────────────────────────────
            Panel tarjetaDibujo = CrearTarjeta(new Point(30, 100), new Size(660, 570));
            this.Controls.Add(tarjetaDibujo);

            tarjetaDibujo.Controls.Add(new Label
            {
                Text = "Área de dibujo",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            });

            pictureDibujo = new PictureBox
            {
                Location = new Point(20, 50),
                Size = new Size(620, 500),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            pictureDibujo.MouseDown += PictureDibujo_MouseDown;
            pictureDibujo.MouseUp += PictureDibujo_MouseUp;
            pictureDibujo.Paint += PictureDibujo_Paint;
            tarjetaDibujo.Controls.Add(pictureDibujo);

            // ── Tarjeta Configuración (columna derecha) ─────────
            // Con AutoScroll para ver todo el contenido
            Panel tarjetaConfig = CrearTarjeta(new Point(720, 100), new Size(560, 570));
            tarjetaConfig.AutoScroll = true;
            this.Controls.Add(tarjetaConfig);

            int y = 20; // cursor vertical

            // — Título —
            AgregarLabel(tarjetaConfig, "Configuración", 20, new Point(30, y));
            y += 55;

            // — Tabla de puntos —
            AgregarLabel(tarjetaConfig, "Puntos de Trayectoria", 12, new Point(30, y));
            y += 28;

            tablaCoordenadas = new DataGridView
            {
                Location = new Point(30, y),
                Size = new Size(500, 90),
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
            tablaCoordenadas.Columns[4].Name = "Distancia";
            tarjetaConfig.Controls.Add(tablaCoordenadas);
            y += 105;

            // — Separador visual —
            tarjetaConfig.Controls.Add(Separador(y)); y += 12;

            // — Unidad de Medida —
            AgregarLabel(tarjetaConfig, "Unidad de Medida", 12, new Point(30, y)); y += 24;

            btnCentimetros = CrearBoton("Centímetros", new Point(30, y), new Size(230, 36), Color.FromArgb(55, 180, 125));
            btnCentimetros.Click += (s, e) => SeleccionarUnidad("cm");
            tarjetaConfig.Controls.Add(btnCentimetros);

            btnMetros = CrearBoton("Metros", new Point(275, y), new Size(230, 36), Color.FromArgb(210, 210, 210));
            btnMetros.ForeColor = Color.Black;
            btnMetros.Click += (s, e) => SeleccionarUnidad("m");
            tarjetaConfig.Controls.Add(btnMetros);
            y += 48;

            // — Separador —
            tarjetaConfig.Controls.Add(Separador(y)); y += 12;

            // — Conexión ESP32 —
            AgregarLabel(tarjetaConfig, "Conexión USB — ESP32", 12, new Point(30, y)); y += 24;

            cmbPuertos = new ComboBox { Location = new Point(30, y), Size = new Size(130, 28), Font = new Font("Segoe UI", 10) };
            tarjetaConfig.Controls.Add(cmbPuertos);

            btnRefrescarPuertos = CrearBoton("↻", new Point(170, y - 2), new Size(38, 32), Color.FromArgb(80, 80, 80));
            btnRefrescarPuertos.Click += (s, e) => CargarPuertos();
            tarjetaConfig.Controls.Add(btnRefrescarPuertos);

            btnConectar = CrearBoton("Conectar", new Point(218, y - 2), new Size(115, 32), Color.FromArgb(35, 135, 210));
            btnConectar.Click += BtnConectar_Click;
            tarjetaConfig.Controls.Add(btnConectar);

            lblEstadoConexion = new Label
            {
                Text = "● No conectado",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Red,
                Location = new Point(342, y + 3),
                AutoSize = true
            };
            tarjetaConfig.Controls.Add(lblEstadoConexion);
            y += 42;

            // — Separador —
            tarjetaConfig.Controls.Add(Separador(y)); y += 12;

            // — Modo de operación —
            AgregarLabel(tarjetaConfig, "Modo de operación", 12, new Point(30, y)); y += 24;

            btnModoManual = CrearBoton("Modo Manual", new Point(30, y), new Size(230, 36), Color.FromArgb(80, 80, 80));
            btnModoManual.Click += BtnModoManual_Click;
            tarjetaConfig.Controls.Add(btnModoManual);

            btnModoAuto = CrearBoton("Modo Autónomo", new Point(275, y), new Size(230, 36), Color.FromArgb(80, 80, 80));
            btnModoAuto.Click += BtnModoAuto_Click;
            tarjetaConfig.Controls.Add(btnModoAuto);
            y += 44;

            lblModoActual = new Label
            {
                Text = "Modo actual: Ninguno",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(30, y),
                AutoSize = true
            };
            tarjetaConfig.Controls.Add(lblModoActual);
            y += 28;

            // — Separador —
            tarjetaConfig.Controls.Add(Separador(y)); y += 12;

            // ════════════════════════════════════════════════════
            //  PANEL: AJUSTE DE MOTORES
            //  ← Aquí es donde corriges que el carrito vaya chueco
            // ════════════════════════════════════════════════════
            Panel panelMotores = new Panel
            {
                Location = new Point(30, y),
                Size = new Size(500, 115),
                BackColor = Color.FromArgb(240, 248, 255),
                BorderStyle = BorderStyle.FixedSingle
            };
            tarjetaConfig.Controls.Add(panelMotores);

            panelMotores.Controls.Add(new Label
            {
                Text = "⚙ Ajuste de Motores",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 80, 150),
                Location = new Point(10, 8),
                AutoSize = true
            });

            // Fila 1: Speed L / Speed R / Kp
            int mx = 10, my = 28;
            panelMotores.Controls.Add(new Label { Text = "Izq (L)", Location = new Point(mx, my), AutoSize = true, Font = new Font("Segoe UI", 8) });
            panelMotores.Controls.Add(new Label { Text = "Der (R)", Location = new Point(mx + 80, my), AutoSize = true, Font = new Font("Segoe UI", 8) });
            panelMotores.Controls.Add(new Label { Text = "Kp", Location = new Point(mx + 180, my), AutoSize = true, Font = new Font("Segoe UI", 8) });

            my += 18;
            txtSpeedL = new TextBox { Location = new Point(mx, my), Size = new Size(60, 22), Text = "255", Font = new Font("Segoe UI", 10) };
            txtSpeedR = new TextBox { Location = new Point(mx + 80, my), Size = new Size(60, 22), Text = "225", Font = new Font("Segoe UI", 10) };
            txtKp = new TextBox { Location = new Point(mx + 180, my), Size = new Size(60, 22), Text = "1.5", Font = new Font("Segoe UI", 10) };
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

            my += 22;
            panelMotores.Controls.Add(new Label { Text = "Empuje Izq (giro L):", Location = new Point(mx, my + 3), AutoSize = true, Font = new Font("Segoe UI", 9) });
            txtEmpujeIzq = new TextBox { Location = new Point(mx + 155, my), Size = new Size(65, 26), Text = "230", Font = new Font("Segoe UI", 11) };
            panelMotores.Controls.Add(txtEmpujeIzq);
            panelMotores.Controls.Add(new Label
            {
                Text = "← solo durante giro izq",
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.Gray,
                Location = new Point(mx + 230, my + 5),
                AutoSize = true
            });

            // Botón aplicar motores (al lado de los textboxes)
            var btnAplicarMotores = CrearBoton("Aplicar", new Point(mx + 300, my - 2), new Size(80, 26), Color.FromArgb(35, 135, 210));
            btnAplicarMotores.Font = new Font("Segoe UI", 9, FontStyle.Bold);
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
                    MessageBox.Show("Valores inválidos. Usa números enteros para L/R y decimal para Kp.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            panelMotores.Controls.Add(btnAplicarMotores);

            y += 130;

            // — Separador —
            tarjetaConfig.Controls.Add(Separador(y)); y += 12;

            // ════════════════════════════════════════════════════
            //  PANEL: CALIBRACIÓN DE ENCODERS
            // ════════════════════════════════════════════════════
            Panel panelCal = new Panel
            {
                Location = new Point(30, y),
                Size = new Size(500, 100),
                BackColor = Color.FromArgb(255, 248, 235),
                BorderStyle = BorderStyle.FixedSingle
            };
            tarjetaConfig.Controls.Add(panelCal);

            panelCal.Controls.Add(new Label
            {
                Text = "📏 Calibración",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 80, 10),
                Location = new Point(10, 8),
                AutoSize = true
            });

            int cx = 10, cy = 28;
            panelCal.Controls.Add(new Label { Text = "pulsos/cm", Location = new Point(cx, cy), AutoSize = true, Font = new Font("Segoe UI", 8) });
            panelCal.Controls.Add(new Label { Text = "pulsos/90°", Location = new Point(cx + 100, cy), AutoSize = true, Font = new Font("Segoe UI", 8) });

            cy += 18;
            txtPpc = new TextBox { Location = new Point(cx, cy), Size = new Size(70, 22), Text = "20.0", Font = new Font("Segoe UI", 10) };
            txtGiro = new TextBox { Location = new Point(cx + 100, cy), Size = new Size(70, 22), Text = "180", Font = new Font("Segoe UI", 10) };
            panelCal.Controls.Add(txtPpc);
            panelCal.Controls.Add(txtGiro);

            var btnAplicarCfg = CrearBoton("Aplicar", new Point(cx + 300, cy - 2), new Size(80, 26), Color.FromArgb(55, 140, 80));
            btnAplicarCfg.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnAplicarCfg.Click += (s, e) =>
            {
                if (float.TryParse(txtPpc.Text.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float ppc) &&
                    int.TryParse(txtGiro.Text, out int g))
                {
                    EnviarComando($"CFG,{ppc:0.0},{g}");
                }
                else
                    MessageBox.Show("Valores inválidos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            panelCal.Controls.Add(btnAplicarCfg);

            cy += 40;
            var btnCalibrar = CrearBoton("🔧 Calibrar en pista", new Point(cx, cy), new Size(200, 32), Color.FromArgb(170, 100, 10));
            btnCalibrar.Click += async (s, e) =>
            {
                if (!VerificarConexion()) return;
                EnviarComando("CAL_START");
                await Task.Delay(100);
                MessageBox.Show(
                    "Mueve el carrito exactamente 100 cm hacia adelante y presiona OK.",
                    "Calibración en curso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                EnviarComando("CAL_STOP");
                MessageBox.Show(
                    "Revisa el Monitor Serial del ESP32 para ver el resultado (CAL_RESULT,izq,der).\n" +
                    "Divide el promedio entre 100 y escríbelo en 'pulsos/cm'.",
                    "Resultado de calibración",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            panelCal.Controls.Add(btnCalibrar);

            y += 115;

            // — Separador —
            tarjetaConfig.Controls.Add(Separador(y)); y += 12;

            // ════════════════════════════════════════════════════
            //  ACCIONES
            // ════════════════════════════════════════════════════
            AgregarLabel(tarjetaConfig, "Acciones", 12, new Point(30, y)); y += 24;

            btnEnviar = CrearBoton("▶ Ejecutar", new Point(30, y), new Size(160, 38), Color.FromArgb(35, 135, 210));
            btnEnviar.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnEnviar.Click += BtnEnviar_Click;
            tarjetaConfig.Controls.Add(btnEnviar);

            btnPausar = CrearBoton("⏸ Pausar", new Point(200, y), new Size(130, 38), Color.FromArgb(245, 190, 40));
            btnPausar.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnPausar.Click += (s, e) => EnviarComando("S");
            tarjetaConfig.Controls.Add(btnPausar);

            btnLimpiar = CrearBoton("✕ Limpiar", new Point(340, y), new Size(130, 38), Color.FromArgb(215, 55, 55));
            btnLimpiar.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnLimpiar.Click += BtnLimpiar_Click;
            tarjetaConfig.Controls.Add(btnLimpiar);

            CargarPuertos();
        }

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
            catch (Exception ex)
            {
                MessageBox.Show($"Error enviando comando:\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════
        //  MODOS
        // ════════════════════════════════════════════════════════
        private void BtnModoManual_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            lblModoActual.Text = "Modo actual: Manual";
            btnModoManual.BackColor = Color.FromArgb(55, 180, 125);
            btnModoAuto.BackColor = Color.FromArgb(80, 80, 80);
        }

        private void BtnModoAuto_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            lblModoActual.Text = "Modo actual: Autónomo";
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
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,R,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(1500);
                }
                else if (diferencia == 3)
                {
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,L,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(1500);
                }
                else if (diferencia == 2)
                {
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,R,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(1500);
                    tcs = new TaskCompletionSource<bool>();
                    EnviarComando("A,R,90,giro");
                    await Task.WhenAny(tcs.Task, Task.Delay(6000));
                    await Task.Delay(1500);
                }

                // Avanzar — timeout generoso (máx 30 s por tramo)
                tcs = new TaskCompletionSource<bool>();
                EnviarComando($"A,F,{distancias[i]:0.00},{unidadesPorPunto[i]}");
                await Task.WhenAny(tcs.Task, Task.Delay(30000));
                await Task.Delay(1500);

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
        //  CIERRE
        // ════════════════════════════════════════════════════════
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (esp32Conectado) EnviarComando("S");
            try { puertoSerial?.Close(); puertoSerial?.Dispose(); } catch { }
            base.OnFormClosing(e);
        }
    }
}