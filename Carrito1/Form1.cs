// ════════════════════════════════════════════════════════════
//  LA JEEPETA — Control PC (Windows Forms + SharpDX)
//  Comunicación: Serial USB con ESP32
//  Protocolo:  "F,30,cm" | "B,1,m" | "R" | "L" | "S"
//              "MODO_MANUAL" | "MODO_AUTO"
// ════════════════════════════════════════════════════════════

using SharpDX.XInput;
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

        // ─── Serial / Estado ──────────────────────────────────
        private SerialPort puertoSerial;
        private bool esp32Conectado = false;
        private string modoActual = "NINGUNO";

        // ─── Trayectoria ──────────────────────────────────────
        private readonly List<Point> puntosPantalla = new List<Point>();
        private readonly List<Point> coordenadas = new List<Point>();
        private readonly List<double> distancias = new List<double>();
        private readonly List<string> unidadesPorPunto = new List<string>();

        private Point? puntoOrigen = null;
        private string unidadSeleccionada = "cm";
        private bool dibujando = false;

        // ─── Xbox control ─────────────────────────────────────────────
        private Controller xbox;
        private System.Windows.Forms.Timer timerXbox;
        private string ultimoComando = "";

        // Separación de cuadros del grid en píxeles (1 cuadro = 10 cm o 1 m)
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

        // ─── Construcción de la UI ────────────────────────────
        private void CrearInterfaz()
        {
            this.Text = "La Jeepeta — Control";
            this.BackColor = Color.FromArgb(245, 245, 245);

            // Barra superior
            Panel barraSuperior = new Panel
            {
                Size = new Size(this.ClientSize.Width, 110),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(145, 110, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            barraSuperior.Controls.Add(new Label
            {
                Text = "LA JEEPETA — CONTROL DE MOVIMIENTO",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(this.ClientSize.Width, 90),
                Location = new Point(0, 10),
                TextAlign = ContentAlignment.MiddleCenter
            });
            this.Controls.Add(barraSuperior);

            // ── Tarjeta Dibujo ──
            Panel tarjetaDibujo = CrearTarjeta(new Point(70, 150), new Size(650, 550));
            this.Controls.Add(tarjetaDibujo);

            tarjetaDibujo.Controls.Add(new Label
            {
                Text = "Área de dibujo",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(30, 20),
                AutoSize = true
            });

            pictureDibujo = new PictureBox
            {
                Location = new Point(30, 65),
                Size = new Size(590, 450),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            pictureDibujo.MouseDown += PictureDibujo_MouseDown;
            pictureDibujo.MouseUp += PictureDibujo_MouseUp;
            pictureDibujo.Paint += PictureDibujo_Paint;
            tarjetaDibujo.Controls.Add(pictureDibujo);

            // ── Tarjeta Configuración ──
            Panel tarjetaConfig = CrearTarjeta(new Point(780, 150), new Size(580, 760));
            this.Controls.Add(tarjetaConfig);

            AgregarLabel(tarjetaConfig, "Configuración", 24, new Point(35, 20));
            AgregarLabel(tarjetaConfig, "Puntos de Trayectoria", 14, new Point(35, 75));

            // Tabla
            tablaCoordenadas = new DataGridView
            {
                Location = new Point(35, 108),
                Size = new Size(510, 140),
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                ColumnCount = 5
            };
            tablaCoordenadas.Columns[0].Name = "Punto";
            tablaCoordenadas.Columns[1].Name = "X";
            tablaCoordenadas.Columns[2].Name = "Y";
            tablaCoordenadas.Columns[3].Name = "(X,Y)";
            tablaCoordenadas.Columns[4].Name = "Distancia";
            tarjetaConfig.Controls.Add(tablaCoordenadas);

            // Unidad
            AgregarLabel(tarjetaConfig, "Unidad de Medida", 14, new Point(35, 268));
            btnCentimetros = CrearBoton("Centímetros", new Point(35, 300), new Size(235, 40), Color.FromArgb(55, 180, 125));
            btnCentimetros.Click += (s, e) => SeleccionarUnidad("cm");
            tarjetaConfig.Controls.Add(btnCentimetros);

            btnMetros = CrearBoton("Metros", new Point(285, 300), new Size(235, 40), Color.FromArgb(210, 210, 210));
            btnMetros.ForeColor = Color.Black;
            btnMetros.Click += (s, e) => SeleccionarUnidad("m");
            tarjetaConfig.Controls.Add(btnMetros);

            // Conexión
            AgregarLabel(tarjetaConfig, "Conexión USB — ESP32", 14, new Point(35, 360));

            cmbPuertos = new ComboBox { Location = new Point(35, 398), Size = new Size(130, 30) };
            tarjetaConfig.Controls.Add(cmbPuertos);

            btnRefrescarPuertos = CrearBoton("↻", new Point(175, 394), new Size(45, 36), Color.FromArgb(80, 80, 80));
            btnRefrescarPuertos.Click += (s, e) => CargarPuertos();
            tarjetaConfig.Controls.Add(btnRefrescarPuertos);

            btnConectar = CrearBoton("Conectar", new Point(232, 392), new Size(130, 40), Color.FromArgb(35, 135, 210));
            btnConectar.Click += BtnConectar_Click;
            tarjetaConfig.Controls.Add(btnConectar);

            lblEstadoConexion = new Label
            {
                Text = "● No conectado",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Red,
                Location = new Point(375, 403),
                AutoSize = true
            };
            tarjetaConfig.Controls.Add(lblEstadoConexion);

            // Modos
            AgregarLabel(tarjetaConfig, "Modo de operación", 14, new Point(35, 450));

            btnModoManual = CrearBoton("Modo Manual", new Point(35, 482), new Size(235, 40), Color.FromArgb(80, 80, 80));
            btnModoManual.Click += BtnModoManual_Click;
            tarjetaConfig.Controls.Add(btnModoManual);

            btnModoAuto = CrearBoton("Modo Autónomo", new Point(285, 482), new Size(235, 40), Color.FromArgb(80, 80, 80));
            btnModoAuto.Click += BtnModoAuto_Click;
            tarjetaConfig.Controls.Add(btnModoAuto);

            lblModoActual = new Label
            {
                Text = "Modo actual: Ninguno",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(35, 532),
                AutoSize = true
            };
            tarjetaConfig.Controls.Add(lblModoActual);

            // Acciones
            AgregarLabel(tarjetaConfig, "Acciones", 14, new Point(35, 565));

            btnEnviar = CrearBoton("▶ Ejecutar", new Point(35, 597), new Size(150, 45), Color.FromArgb(35, 135, 210));
            btnEnviar.Click += BtnEnviar_Click;
            tarjetaConfig.Controls.Add(btnEnviar);

            btnPausar = CrearBoton("⏸ Pausar", new Point(200, 597), new Size(140, 45), Color.FromArgb(245, 190, 40));
            btnPausar.Click += (s, e) => EnviarComando("S");
            tarjetaConfig.Controls.Add(btnPausar);

            btnLimpiar = CrearBoton("↻ Limpiar", new Point(355, 597), new Size(170, 45), Color.FromArgb(215, 55, 55));
            btnLimpiar.Click += BtnLimpiar_Click;
            tarjetaConfig.Controls.Add(btnLimpiar);

            CargarPuertos();
        }

        // ─── Helpers UI ───────────────────────────────────────
        private Panel CrearTarjeta(Point location, Size size) =>
            new Panel { Location = location, Size = size, BackColor = Color.White };

        private Button CrearBoton(string texto, Point location, Size size, Color color) =>
            new Button
            {
                Text = texto,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
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

        // ─── Puertos ──────────────────────────────────────────
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

        // ─── Conexión ─────────────────────────────────────────
        private void BtnConectar_Click(object sender, EventArgs e)
        {
            if (esp32Conectado)
            {
                Desconectar();
                return;
            }

            if (cmbPuertos.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un puerto COM.", "Sin puerto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                puertoSerial.Open();

                esp32Conectado = true;
                lblEstadoConexion.Text = "● Conectado";
                lblEstadoConexion.ForeColor = Color.Green;
                btnConectar.Text = "Desconectar";

                IniciarXbox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Desconectar()
        {
            timerXbox?.Stop();
            try { puertoSerial?.Close(); } catch { }

            esp32Conectado = false;
            lblEstadoConexion.Text = "● No conectado";
            lblEstadoConexion.ForeColor = Color.Red;
            btnConectar.Text = "Conectar";
        }

        // ─── Xbox Controller ──────────────────────────────────
        private void IniciarXbox()
        {
            xbox = new Controller(UserIndex.One);
            timerXbox = new System.Windows.Forms.Timer { Interval = 100 };
            timerXbox.Tick += TimerXbox_Tick;
            timerXbox.Start();
        }

        private void TimerXbox_Tick(object sender, EventArgs e)
        {
            if (!esp32Conectado || !puertoSerial.IsOpen || !xbox.IsConnected) return;

            var buttons = xbox.GetState().Gamepad.Buttons;

            string comando = "S";
            if (buttons.HasFlag(GamepadButtonFlags.A)) comando = "F";
            else if (buttons.HasFlag(GamepadButtonFlags.B)) comando = "B";
            else if (buttons.HasFlag(GamepadButtonFlags.X)) comando = "L";
            else if (buttons.HasFlag(GamepadButtonFlags.Y)) comando = "R";

            if (comando != ultimoComando)
            {
                EnviarComando(comando);
                ultimoComando = comando;
            }
        }

        // ─── Envío de comandos ────────────────────────────────
        private void EnviarComando(string comando)
        {
            if (!esp32Conectado || puertoSerial == null || !puertoSerial.IsOpen) return;

            try { puertoSerial.WriteLine(comando); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enviando comando:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─── Modos ────────────────────────────────────────────
        private void BtnModoManual_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            modoActual = "MANUAL";
            lblModoActual.Text = "Modo actual: Manual";
            btnModoManual.BackColor = Color.FromArgb(55, 180, 125);
            btnModoAuto.BackColor = Color.FromArgb(80, 80, 80);
            EnviarComando("MODO_MANUAL");
        }

        private void BtnModoAuto_Click(object sender, EventArgs e)
        {
            if (!VerificarConexion()) return;
            modoActual = "AUTO";
            lblModoActual.Text = "Modo actual: Autónomo";
            btnModoAuto.BackColor = Color.FromArgb(55, 180, 125);
            btnModoManual.BackColor = Color.FromArgb(80, 80, 80);
            EnviarComando("MODO_AUTO");
        }

        private bool VerificarConexion()
        {
            if (esp32Conectado) return true;
            MessageBox.Show("Primero conecta el ESP32.", "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // ─── Unidades ─────────────────────────────────────────
        private void SeleccionarUnidad(string unidad)
        {
            unidadSeleccionada = unidad;
            bool esCm = unidad == "cm";
            btnCentimetros.BackColor = esCm ? Color.FromArgb(55, 180, 125) : Color.FromArgb(210, 210, 210);
            btnCentimetros.ForeColor = esCm ? Color.White : Color.Black;
            btnMetros.BackColor = esCm ? Color.FromArgb(210, 210, 210) : Color.FromArgb(55, 180, 125);
            btnMetros.ForeColor = esCm ? Color.Black : Color.White;
        }

        // ─── Limpiar ──────────────────────────────────────────
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

        // ─── Ejecutar trayectoria ─────────────────────────────
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
                switch (diferencia)
                {
                    case 1:
                        EnviarComando("R"); await Task.Delay(900);
                        break;
                    case 3:
                        EnviarComando("L"); await Task.Delay(900);
                        break;
                    case 2:
                        EnviarComando("R"); await Task.Delay(900);
                        EnviarComando("R"); await Task.Delay(900);
                        break;
                }

                // Avanzar
                string cmd = $"F,{distancias[i]:0.00},{unidadesPorPunto[i]}";
                EnviarComando(cmd);

                // Esperar estimado de recorrido (heurístico: 1 cm ≈ 80 ms)
                double cm = unidadesPorPunto[i] == "m" ? distancias[i] * 100 : distancias[i];
                int espera = Math.Max(500, (int)(cm * 80));
                await Task.Delay(espera);

                orientacionActual = orientacionDeseada;
            }

            EnviarComando("S");
            btnEnviar.Enabled = true;
        }

        // ─── Dibujo ───────────────────────────────────────────
        private void PictureDibujo_MouseDown(object sender, MouseEventArgs e)
        {
            dibujando = true;
            AgregarPunto(e.Location);
        }

        private void PictureDibujo_MouseUp(object sender, MouseEventArgs e) => dibujando = false;

        private void AgregarPunto(Point punto)
        {
            if (puntoOrigen == null) puntoOrigen = punto;

            int xPix = punto.X - puntoOrigen.Value.X;
            int yPix = puntoOrigen.Value.Y - punto.Y;

            Point puntoUnidad = new Point(
                (int)Math.Round(xPix / (double)GRID_PX),
                (int)Math.Round(yPix / (double)GRID_PX)
            );

            // Descartar duplicados consecutivos
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

        // ─── Tabla ────────────────────────────────────────────
        private void ActualizarTabla()
        {
            tablaCoordenadas.Rows.Clear();
            for (int i = 0; i < coordenadas.Count; i++)
            {
                Point p = coordenadas[i];
                tablaCoordenadas.Rows.Add(
                    i + 1,
                    p.X, p.Y,
                    $"({p.X},{p.Y})",
                    $"{distancias[i]:0.00} {unidadesPorPunto[i]}"
                );
            }
        }

        // ─── Cierre ───────────────────────────────────────────
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (esp32Conectado) EnviarComando("S");
            timerXbox?.Stop();
            try { puertoSerial?.Close(); puertoSerial?.Dispose(); } catch { }
            base.OnFormClosing(e);
        }
    }
}