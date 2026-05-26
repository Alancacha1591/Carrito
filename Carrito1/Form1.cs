using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace Carrito1
{
    public partial class Form1 : Form
    {
        private PictureBox pictureDibujo;
        private Button btnCentimetros, btnMetros, btnLimpiar, btnEnviar, btnPausar;
        private Button btnConectar, btnRefrescarPuertos;
        private Button btnModoManual, btnModoAuto;
        private Button btnAdelante, btnAtras, btnIzquierda, btnDerecha, btnStop;
        private ComboBox cmbPuertos;
        private Label lblEstadoConexion, lblModoActual;
        private DataGridView tablaCoordenadas;

        private SerialPort puertoSerial;
        private bool esp32Conectado = false;
        private string modoActual = "NINGUNO";

        private List<Point> puntosPantalla = new List<Point>();
        private List<Point> coordenadas = new List<Point>();
        private List<double> distancias = new List<double>();
        private List<string> unidadesPorPunto = new List<string>();

        private Point? puntoOrigen = null;
        private string unidadSeleccionada = "cm";
        private bool dibujando = false;
        private Controller xbox;
        private System.Windows.Forms.Timer timerXbox;
        private string ultimoComando = "";
        Controller control = new Controller(UserIndex.One);



        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
          
            this.AutoScroll = true;
            CrearInterfaz();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void CrearInterfaz()
        {
            this.Text = "Movimiento por Control";
            this.Size = new Size(1400, 850);
            this.WindowState = FormWindowState.Maximized;
            this.AutoScroll = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 245, 245);

            Panel barraSuperior = new Panel();
            barraSuperior.Size = new Size(this.ClientSize.Width, 110);
            barraSuperior.Location = new Point(0, 0);
            barraSuperior.BackColor = Color.FromArgb(145, 110, 10);
            barraSuperior.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(barraSuperior);

            Label titulo = new Label();
            titulo.Text = "MOVIMIENTO POR CONTROL";
            titulo.Font = new Font("Segoe UI", 34, FontStyle.Bold);
            titulo.ForeColor = Color.White;
            titulo.AutoSize = false;
            titulo.Size = new Size(this.ClientSize.Width, 90);
            titulo.Location = new Point(0, 10);
            titulo.TextAlign = ContentAlignment.MiddleCenter;
            barraSuperior.Controls.Add(titulo);

            Panel tarjetaDibujo = CrearTarjeta(new Point(70, 150), new Size(650, 520));
            this.Controls.Add(tarjetaDibujo);

            Label lblPlano = new Label();
            lblPlano.Text = "Área de dibujo";
            lblPlano.Font = new Font("Segoe UI", 20, FontStyle.Bold);
            lblPlano.Location = new Point(30, 25);
            lblPlano.AutoSize = true;
            tarjetaDibujo.Controls.Add(lblPlano);

            pictureDibujo = new PictureBox();
            pictureDibujo.Location = new Point(30, 85);
            pictureDibujo.Size = new Size(590, 400);
            pictureDibujo.BackColor = Color.White;
            pictureDibujo.BorderStyle = BorderStyle.FixedSingle;
            pictureDibujo.MouseDown += PictureDibujo_MouseDown;
          //  pictureDibujo.MouseMove += PictureDibujo_MouseMove;
            pictureDibujo.MouseUp += PictureDibujo_MouseUp;
            pictureDibujo.Paint += PictureDibujo_Paint;
            tarjetaDibujo.Controls.Add(pictureDibujo);

            Panel tarjetaConfig = CrearTarjeta(new Point(780, 150), new Size(560, 760));
            this.Controls.Add(tarjetaConfig);

            Label lblConfig = new Label();
            lblConfig.Text = "Configuración";
            lblConfig.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            lblConfig.Location = new Point(35, 25);
            lblConfig.AutoSize = true;
            tarjetaConfig.Controls.Add(lblConfig);

            Label lblPuntos = new Label();
            lblPuntos.Text = "Puntos de Trayectoria";
            lblPuntos.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblPuntos.Location = new Point(35, 85);
            lblPuntos.AutoSize = true;
            tarjetaConfig.Controls.Add(lblPuntos);

            tablaCoordenadas = new DataGridView();
            tablaCoordenadas.Location = new Point(35, 120);
            tablaCoordenadas.Size = new Size(490, 140);
            tablaCoordenadas.AllowUserToAddRows = false;
            tablaCoordenadas.ReadOnly = true;
            tablaCoordenadas.RowHeadersVisible = false;
            tablaCoordenadas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            tablaCoordenadas.BackgroundColor = Color.White;
            tablaCoordenadas.ColumnCount = 5;
            tablaCoordenadas.Columns[0].Name = "Punto";
            tablaCoordenadas.Columns[1].Name = "Eje X";
            tablaCoordenadas.Columns[2].Name = "Eje Y";
            tablaCoordenadas.Columns[3].Name = "(X,Y)";
            tablaCoordenadas.Columns[4].Name = "Dist.";
            tarjetaConfig.Controls.Add(tablaCoordenadas);

            Label lblUnidad = new Label();
            lblUnidad.Text = "Unidad de Medida";
            lblUnidad.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblUnidad.Location = new Point(35, 280);
            lblUnidad.AutoSize = true;
            tarjetaConfig.Controls.Add(lblUnidad);

            btnCentimetros = CrearBoton("Centímetros", new Point(35, 315), new Size(230, 40), Color.FromArgb(55, 180, 125));
            btnCentimetros.Click += BtnCentimetros_Click;
            tarjetaConfig.Controls.Add(btnCentimetros);

            btnMetros = CrearBoton("Metros", new Point(290, 315), new Size(230, 40), Color.FromArgb(210, 210, 210));
            btnMetros.ForeColor = Color.Black;
            btnMetros.Click += BtnMetros_Click;
            tarjetaConfig.Controls.Add(btnMetros);

            Label lblConexion = new Label();
            lblConexion.Text = "Conexión Bluetooth ESP32";
            lblConexion.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblConexion.Location = new Point(35, 375);
            lblConexion.AutoSize = true;
            tarjetaConfig.Controls.Add(lblConexion);

            cmbPuertos = new ComboBox();
            cmbPuertos.Location = new Point(35, 415);
            cmbPuertos.Size = new Size(130, 30);
            tarjetaConfig.Controls.Add(cmbPuertos);

            btnRefrescarPuertos = CrearBoton("↻", new Point(175, 410), new Size(45, 35), Color.FromArgb(80, 80, 80));
            btnRefrescarPuertos.Click += BtnRefrescarPuertos_Click;
            tarjetaConfig.Controls.Add(btnRefrescarPuertos);

            btnConectar = CrearBoton("Conectar", new Point(235, 407), new Size(120, 40), Color.FromArgb(35, 135, 210));
            btnConectar.Click += BtnConectar_Click;
            tarjetaConfig.Controls.Add(btnConectar);

            lblEstadoConexion = new Label();
            lblEstadoConexion.Text = "ESP32: No conectado";
            lblEstadoConexion.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            lblEstadoConexion.ForeColor = Color.Red;
            lblEstadoConexion.Location = new Point(370, 418);
            lblEstadoConexion.AutoSize = true;
            tarjetaConfig.Controls.Add(lblEstadoConexion);

            btnModoManual = CrearBoton("Modo Manual", new Point(35, 465), new Size(230, 40), Color.FromArgb(80, 80, 80));
            btnModoManual.Click += BtnModoManual_Click;
            tarjetaConfig.Controls.Add(btnModoManual);

            btnModoAuto = CrearBoton("Modo Autónomo", new Point(290, 465), new Size(230, 40), Color.FromArgb(80, 80, 80));
            btnModoAuto.Click += BtnModoAuto_Click;
            tarjetaConfig.Controls.Add(btnModoAuto);

            lblModoActual = new Label();
            lblModoActual.Text = "Modo actual: Ninguno";
            lblModoActual.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblModoActual.Location = new Point(35, 515);
            lblModoActual.AutoSize = true;
            tarjetaConfig.Controls.Add(lblModoActual);

            Label lblManual = new Label();
            lblManual.Text = "Control Manual";
            lblManual.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblManual.Location = new Point(35, 545);
            lblManual.AutoSize = true;
            tarjetaConfig.Controls.Add(lblManual);

          /*  btnAdelante = CrearBoton("↑", new Point(235, 580), new Size(80, 45), Color.FromArgb(35, 135, 210));
            btnAdelante.Click += (s, e) => EnviarComando("F");
            tarjetaConfig.Controls.Add(btnAdelante);

            btnIzquierda = CrearBoton("←", new Point(145, 630), new Size(80, 45), Color.FromArgb(35, 135, 210));
            btnIzquierda.Click += (s, e) => EnviarComando("L");
            tarjetaConfig.Controls.Add(btnIzquierda);

            btnStop = CrearBoton("STOP", new Point(235, 630), new Size(80, 45), Color.FromArgb(215, 55, 55));
            btnStop.Click += (s, e) => EnviarComando("S");
            tarjetaConfig.Controls.Add(btnStop);

            btnDerecha = CrearBoton("→", new Point(325, 630), new Size(80, 45), Color.FromArgb(35, 135, 210));
            btnDerecha.Click += (s, e) => EnviarComando("R");
            tarjetaConfig.Controls.Add(btnDerecha);

            btnAtras = CrearBoton("↓", new Point(235, 680), new Size(80, 45), Color.FromArgb(35, 135, 210));
            btnAtras.Click += (s, e) => EnviarComando("B");
            tarjetaConfig.Controls.Add(btnAtras);

            */
            btnEnviar = CrearBoton("▶ Ejecutar", new Point(35, 585), new Size(135, 45), Color.FromArgb(35, 135, 210));
            btnEnviar.Click += BtnEnviar_Click;
            tarjetaConfig.Controls.Add(btnEnviar);

            btnPausar = CrearBoton("⏸ Pausar", new Point(190, 585), new Size(120, 45), Color.FromArgb(245, 190, 40));
            btnPausar.Click += (s, e) => EnviarComando("S");
            tarjetaConfig.Controls.Add(btnPausar);

            btnLimpiar = CrearBoton("↻ Reiniciar", new Point(330, 585), new Size(160, 45), Color.FromArgb(215, 55, 55));
            btnLimpiar.Click += BtnLimpiar_Click;
            tarjetaConfig.Controls.Add(btnLimpiar);

            
            CargarPuertos();
        }

        private void CargarPuertos()
        {
            cmbPuertos.Items.Clear();

            string[] puertos = SerialPort.GetPortNames();

            foreach (string puerto in puertos)
            {
                if (!cmbPuertos.Items.Contains(puerto))
                {
                    cmbPuertos.Items.Add(puerto);
                }
            }

            if (cmbPuertos.Items.Contains("COM4"))
            {
                cmbPuertos.SelectedItem = "COM4";
            }
            else if (cmbPuertos.Items.Count > 0)
            {
                cmbPuertos.SelectedIndex = 0;
            }
        }

        private void IniciarXbox()
        {
            xbox = new Controller(UserIndex.One);

            timerXbox = new System.Windows.Forms.Timer();
            timerXbox.Interval = 100;
            timerXbox.Tick += TimerXbox_Tick;
            timerXbox.Start();
        }

        private void TimerXbox_Tick(object sender, EventArgs e)
        {
            if (!esp32Conectado || puertoSerial == null || !puertoSerial.IsOpen)
                return;

            if (!xbox.IsConnected)
                return;

            var state = xbox.GetState();
            var buttons = state.Gamepad.Buttons;

            string comando = "S";

            if (buttons.HasFlag(GamepadButtonFlags.A))
                comando = "F";

            else if (buttons.HasFlag(GamepadButtonFlags.B))
                comando = "B";

            else if (buttons.HasFlag(GamepadButtonFlags.X))
                comando = "L";

            else if (buttons.HasFlag(GamepadButtonFlags.Y))
                comando = "R";

            if (comando != ultimoComando)
            {
                puertoSerial.Write(comando);
                ultimoComando = comando;
            }
        }


        private void BtnRefrescarPuertos_Click(object sender, EventArgs e)
        {
            CargarPuertos();
        }


        private void BtnConectar_Click(object sender, EventArgs e)
        {
            if (esp32Conectado)
            {
                try
                {
                    if (timerXbox != null)
                        timerXbox.Stop();

                    if (puertoSerial != null && puertoSerial.IsOpen)
                        puertoSerial.Close();
                }
                catch { }

                esp32Conectado = false;
                lblEstadoConexion.Text = "ESP32: No conectado";
                lblEstadoConexion.ForeColor = Color.Red;
                btnConectar.Text = "Conectar";
                return;
            }

            try
            {
                if (puertoSerial != null)
                {
                    if (puertoSerial.IsOpen)
                        puertoSerial.Close();

                    puertoSerial.Dispose();
                }

                puertoSerial = new SerialPort();
                puertoSerial.PortName = cmbPuertos.SelectedItem.ToString();
                puertoSerial.BaudRate = 115200;
                puertoSerial.DataBits = 8;
                puertoSerial.Parity = Parity.None;
                puertoSerial.StopBits = StopBits.One;
                puertoSerial.Handshake = Handshake.None;
                puertoSerial.DtrEnable = true;
                puertoSerial.RtsEnable = true;
                puertoSerial.ReadTimeout = 1000;
                puertoSerial.WriteTimeout = 1000;

                puertoSerial.Open();

                esp32Conectado = true;
                lblEstadoConexion.Text = "ESP32: Conectado";
                lblEstadoConexion.ForeColor = Color.Green;
                btnConectar.Text = "Desconectar";

                IniciarXbox();

                MessageBox.Show("ESP32 conectado por USB correctamente.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private void EnviarComando(string comando)
        {
            if (!esp32Conectado || puertoSerial == null || !puertoSerial.IsOpen)
            {
                MessageBox.Show("Primero conecta el ESP32.");
                return;
            }


            try
            {
                //MessageBox.Show("Mandando: " + comando);
                puertoSerial.Write(comando);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error enviando comando:\n" + ex.Message);
            }
        }

        private void BtnModoManual_Click(object sender, EventArgs e)
        {
            if (!esp32Conectado)
            {
                MessageBox.Show("Primero conecta el ESP32.");
                return;
            }

            modoActual = "MANUAL";
            
            lblModoActual.Text = "Modo actual: Manual";
            btnModoManual.BackColor = Color.FromArgb(55, 180, 125);
            btnModoAuto.BackColor = Color.FromArgb(80, 80, 80);
            
            puertoSerial.WriteLine("MODO MANUAL");
        }

        private void BtnModoAuto_Click(object sender, EventArgs e)
        {
            if (!esp32Conectado)
            {
                MessageBox.Show("Primero conecta el ESP32.");
                return;
            }

            modoActual = "AUTO";
            lblModoActual.Text = "Modo actual: Autónomo";
            btnModoAuto.BackColor = Color.FromArgb(55, 180, 125);
            btnModoManual.BackColor = Color.FromArgb(80, 80, 80);

            puertoSerial.WriteLine("MODO_AUTO");
        }

        private Panel CrearTarjeta(Point location, Size size)
        {
            Panel panel = new Panel();
            panel.Location = location;
            panel.Size = size;
            panel.BackColor = Color.White;
            return panel;
        }

        private Button CrearBoton(string texto, Point location, Size size, Color color)
        {
            Button btn = new Button();
            btn.Text = texto;
            btn.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btn.Location = location;
            btn.Size = size;
            btn.BackColor = color;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void PictureDibujo_MouseDown(object sender, MouseEventArgs e)
        {
            dibujando = true;
            AgregarPunto(e.Location);
        }

        private void PictureDibujo_MouseMove(object sender, MouseEventArgs e)
        {
            if (dibujando)
                AgregarPunto(e.Location);
        }

        private void PictureDibujo_MouseUp(object sender, MouseEventArgs e)
        {
            dibujando = false;
        }



        private void AgregarPunto(Point punto)
        {
            if (puntoOrigen == null)
                puntoOrigen = punto;

            int xPix = punto.X - puntoOrigen.Value.X;
            int yPix = puntoOrigen.Value.Y - punto.Y;

            // Coordenadas SIEMPRE en cuadritos, no en cm/m
            int xCuadro = (int)Math.Round(xPix / 35.0);
            int yCuadro = (int)Math.Round(yPix / 35.0);

            Point puntoUnidad = new Point(xCuadro, yCuadro);

            if (coordenadas.Count > 0)
            {
                Point ultimo = coordenadas[coordenadas.Count - 1];

                if (ultimo.X == puntoUnidad.X && ultimo.Y == puntoUnidad.Y)
                    return;
            }

            puntosPantalla.Add(punto);
            coordenadas.Add(puntoUnidad);

            double distancia = 0;

            if (coordenadas.Count > 1)
            {
                Point anterior = coordenadas[coordenadas.Count - 2];

                int dx = puntoUnidad.X - anterior.X;
                int dy = puntoUnidad.Y - anterior.Y;

                double distanciaEnCuadros = Math.Sqrt(dx * dx + dy * dy);

                if (unidadSeleccionada == "cm")
                    distancia = distanciaEnCuadros * 10;  // 1 cuadro = 10 cm
                else
                    distancia = distanciaEnCuadros * 1;   // 1 cuadro = 1 m
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

            using (Pen linea = new Pen(Color.FromArgb(45, 120, 170), 3))
            {
                for (int i = 0; i < puntosPantalla.Count; i++)
                {
                    Point p = puntosPantalla[i];

                    if (i > 0)
                        g.DrawLine(linea, puntosPantalla[i - 1], puntosPantalla[i]);

                    if (i == 0)
                    {
                        g.DrawEllipse(new Pen(Color.Green, 4), p.X - 10, p.Y - 10, 20, 20);
                        g.DrawString("ORIGEN (0,0)", new Font("Segoe UI", 11, FontStyle.Bold), Brushes.Black, p.X - 120, p.Y + 20);
                    }
                    else
                    {
                        g.FillEllipse(Brushes.Red, p.X - 5, p.Y - 5, 10, 10);
                    }
                }
            }
        }

        private void DibujarGrid(Graphics g)
        {
            int separacion = 35;

            using (Pen gridPen = new Pen(Color.FromArgb(190, 210, 215), 1))
            using (Pen ejePen = new Pen(Color.FromArgb(90, 150, 175), 2))
            {
                for (int x = 0; x < pictureDibujo.Width; x += separacion)
                    g.DrawLine(gridPen, x, 0, x, pictureDibujo.Height);

                for (int y = 0; y < pictureDibujo.Height; y += separacion)
                    g.DrawLine(gridPen, 0, y, pictureDibujo.Width, y);

                if (puntoOrigen != null)
                {
                    g.DrawLine(ejePen, puntoOrigen.Value.X, 0, puntoOrigen.Value.X, pictureDibujo.Height);
                    g.DrawLine(ejePen, 0, puntoOrigen.Value.Y, pictureDibujo.Width, puntoOrigen.Value.Y);
                }
            }
        }

        private void ActualizarTabla()
        {
            tablaCoordenadas.Rows.Clear();

            for (int i = 0; i < coordenadas.Count; i++)
            {
                Point p = coordenadas[i];

                tablaCoordenadas.Rows.Add(
                    i + 1,
                    p.X,
                    p.Y,
                    $"({p.X},{p.Y})",
                    $"{distancias[i]:0.00} {unidadesPorPunto[i]}"
                );
            }
        }

        private void BtnCentimetros_Click(object sender, EventArgs e)
        {
            unidadSeleccionada = "cm";
            btnCentimetros.BackColor = Color.FromArgb(55, 180, 125);
            btnCentimetros.ForeColor = Color.White;
            btnMetros.BackColor = Color.FromArgb(210, 210, 210);
            btnMetros.ForeColor = Color.Black;
        }

        private void BtnMetros_Click(object sender, EventArgs e)
        {
            unidadSeleccionada = "m";
            btnMetros.BackColor = Color.FromArgb(55, 180, 125);
            btnMetros.ForeColor = Color.White;
            btnCentimetros.BackColor = Color.FromArgb(210, 210, 210);
            btnCentimetros.ForeColor = Color.Black;
        }

        private void BtnLimpiar_Click(object sender, EventArgs e)
        {
            puntosPantalla.Clear();
            coordenadas.Clear();
            distancias.Clear();
            unidadesPorPunto.Clear();
            puntoOrigen = null;
            tablaCoordenadas.Rows.Clear();
            pictureDibujo.Invalidate();

            if (esp32Conectado)
                EnviarComando("S");
        }


        private async void BtnEnviar_Click(object sender, EventArgs e)
        {
            if (!esp32Conectado)
            {
                MessageBox.Show("Conecta ESP32");
                return;
            }

            if (coordenadas.Count < 2)
            {
                MessageBox.Show("Dibuja algo");
                return;
            }

            // 0 = Norte/arriba, 1 = Este/derecha, 2 = Sur/abajo, 3 = Oeste/izquierda
            int orientacionActual = 0;

            for (int i = 1; i < coordenadas.Count; i++)
            {
                Point anterior = coordenadas[i - 1];
                Point actual = coordenadas[i];

                int dx = actual.X - anterior.X;
                int dy = actual.Y - anterior.Y;

                int orientacionDeseada;

                if (Math.Abs(dx) > Math.Abs(dy))
                {
                    orientacionDeseada = dx > 0 ? 1 : 3;
                }
                else
                {
                    orientacionDeseada = dy > 0 ? 0 : 2;
                }

                int diferencia = (orientacionDeseada - orientacionActual + 4) % 4;

                if (diferencia == 1)
                {
                    puertoSerial.WriteLine("R");
                    await Task.Delay(900);
                }
                else if (diferencia == 3)
                {
                    puertoSerial.WriteLine("L");
                    await Task.Delay(900);
                }
                else if (diferencia == 2)
                {
                    puertoSerial.WriteLine("R");
                    await Task.Delay(900);

                    puertoSerial.WriteLine("R");
                    await Task.Delay(900);
                }

                double distancia = distancias[i];
                string unidad = unidadesPorPunto[i];

                puertoSerial.WriteLine($"F,{distancia:0.00},{unidad}");

                await Task.Delay(1000);

                orientacionActual = orientacionDeseada;
            }

            puertoSerial.WriteLine("S");
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (puertoSerial != null && puertoSerial.IsOpen)
                {
                    puertoSerial.Write("S");
                    puertoSerial.Close();
                }
            }
            catch { }

            base.OnFormClosing(e);
        }
    }
}