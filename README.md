# 🚗 La Jeepeta — Control de Vehículo Autónomo y Teleoperado (v2)

[![Build Status](https://img.shields.io/badge/Platform-ESP32%20%7C%20.NET%20Framework%204.7.2-blue.svg)](#)
[![Language](https://img.shields.io/badge/Language-C%2B%2B%20%7C%20C%23-green.svg)](#)
[![Libraries](https://img.shields.io/badge/Libraries-SharpDX%20%7C%2032feet.NET-orange.svg)](#)

**La Jeepeta** es un sistema robótico e integrado de control distribuido que combina una aplicación de escritorio de alto rendimiento (desarrollada en C# .NET Windows Forms) con un firmware reactivo de bajo nivel (desarrollada en C++ para ESP32). 

El sistema permite el guiado autónomo de precisión mediante odometría de lazo cerrado con control proporcional ($K_p$), la teleoperación inalámbrica en tiempo real utilizando un mando de Xbox (XInput), y un sistema dinámico de calibración en caliente sin necesidad de recompilar el código del microcontrolador.

---

## 🗺️ Vista General de la Arquitectura

El proyecto está diseñado bajo un modelo **Maestro-Esclavo (Client-Server)** separado por una capa de abstracción de hardware y comunicación inalámbrica:

```
┌─────────────────────────────────┐                 ┌─────────────────────────────────┐
│     PC - Cerebro Maestro        │                 │       ESP32 - Chasis Reactivo   │
│  (C# .NET Windows Forms UI)     │                 │        (C++ Firmware v2)        │
├─────────────────────────────────┤                 ├─────────────────────────────────┤
│ • Planificación de Rutas (GDI+) │  Serial / BT    │ • Controladores de Motores      │
│ • Captura de Mando Xbox         │ ──────────────> │ • Lectura de Encoders (ISR)     │
│ • Algoritmo de Calibración      │ <────────────── │ • Lazo Cerrado Proporcional     │
│ • Monitoreo de Telemetría       │   (Protocolo)   │ • Visualización LCD 16x2        │
└─────────────────────────────────┘                 └─────────────────────────────────┘
```

1. **Cerebro Central (PC):** Procesa la interfaz, renderiza el mapa del recorrido en una grilla a escala, interpreta el hardware de control y orquesta las secuencias de navegación autónoma enviando comandos secuenciales de alto nivel.
2. **Núcleo de Ejecución (ESP32):** Ejecuta tareas de tiempo crítico, gestiona las interrupciones de hardware generadas por los encoders, mantiene la trayectoria recta compensando la velocidad de los motores y maneja periféricos locales (LCD).

---

## ⚙️ Características Principales

* **🎮 Teleoperación de Alta Precisión (Mando Xbox):** Integración nativa a través de `SharpDX.XInput` con hilos dedicados para realizar *polling* rápido y mapeo suave de palancas y D-Pad hacia comandos de motor directos.
* **📍 Navegación Autónoma Euclidiana:** Permite trazar nodos y rutas complejas haciendo clic sobre un lienzo gráfico interactivo en la aplicación de escritorio. La PC calcula las distancias y los ángulos relativos, despachándolos al chasis por tramos.
* **📈 Lazo de Control Proporcional ($K_p$):** Algoritmo de corrección diferencial en tiempo real que compara los pulsos de los encoders izquierdo y derecho para mitigar desvíos físicos causados por asimetría en los motores o fricción de la pista.
* **🔧 Calibración Inteligente en Caliente:** Módulo interactivo que mide las constantes del entorno físico (`pulsosPorCm` y `pulsosGiro90`), permitiendo ajustar la escala matemática del robot sin interrumpir su ejecución ni requerir un nuevo flasheo de firmware.

---

## 🔌 Conectividad y Especificaciones de Hardware

### Mapas de Pines (Pinout del ESP32)

| Componente | Tipo de Pin | Pin ESP32 | Descripción Técnica |
| :--- | :--- | :--- | :--- |
| **IN1 (Motor Izquierdo)** | Salida PWM | `26` | Control de sentido de giro / velocidad |
| **IN2 (Motor Izquierdo)** | Salida PWM | `27` | Control de sentido de giro / velocidad |
| **IN3 (Motor Derecho)** | Salida PWM | `14` | Control de sentido de giro / velocidad |
| **IN4 (Motor Derecho)** | Salida PWM | `25` | Control de sentido de giro / velocidad |
| **SDA (Pantalla LCD)** | Bus I2C | `21` | Línea de datos del bus I2C |
| **SCL (Pantalla LCD)** | Bus I2C | `22` | Línea de reloj del bus I2C |
| **Encoder Izquierdo** | Entrada / ISR | `34` | Sensor óptico de ranuras (Interrupción de hardware) |
| **Encoder Derecho** | Entrada / ISR | `35` | Sensor óptico de ranuras (Interrupción de hardware) |

---

## 📡 Protocolo de Comunicación (API de Comandos)

El canal de comunicación (vía puerto serie cableado USB o Bluetooth Classic con el identificador `ESP32_CAR`) utiliza tramas formateadas en cadenas de caracteres claras (`ASCII`) terminadas en un carácter de salto de línea (`
`).

### 1. Comandos de Control y Configuración (PC ➔ ESP32)

* **`S`**
  * *Descripción:* Parada de emergencia. Detiene inmediatamente la marcha de todos los motores y pone el chasis en reposo seguro (`IDLE`).
* **`MODE,<MODO>`**
  * *Valores:* `MODE,MANUAL` o `MODE,AUTO`
  * *Descripción:* Cambia el estado interno y actualiza la visualización de la pantalla LCD.
* **`C,<VelIzq>,<VelDer>`**
  * *Ejemplo:* `C,255,225`
  * *Descripción:* Configura los límites de velocidad PWM base (rango `0-255`) para equilibrar las diferencias mecánicas de los motores de corriente continua.
* **`CFG,<PulsosPorCm>,<PulsosGiro90>`**
  * *Ejemplo:* `CFG,20.0,180`
  * *Descripción:* Transmite e inyecta dinámicamente las constantes de odometría calibradas.
* **`CAL_START` / `CAL_STOP`**
  * *Descripción:* Activa/Desactiva el modo de conteo de diagnóstico. Al detenerse, retorna las lecturas acumuladas en los contadores de los encoders.
* **`M,<DirIzq>,<PwmIzq>,<DirDer>,<PwmDer>`**
  * *Ejemplo:* `M,1,255,1,255`
  * *Descripción:* Comando directo para teleoperación manual (1 = Adelante, 0 = Reversa).
* **`A,<Dirección>,<Distancia>,<Unidad>`**
  * *Ejemplo:* `A,F,45.5,cm` o `A,L,90,giro`
  * *Descripción:* Comando de desplazamiento autónomo. Direcciones admitidas: `F` (Forward), `B` (Backward), `L` (Left), `R` (Right).

### 2. Respuestas y Telemetría (ESP32 ➔ PC)

* **`READY`**: Emitido en el `setup()` inicial para indicar que el microcontrolador está listo.
* **`RCV:<Trama>`**: Confirmación de eco para asegurar la integridad de la recepción.
* **`CAL,<PulsosIzq>,<PulsosDer>`**: Transmisión cíclica en tiempo real cada 200 ms durante el proceso de calibración.
* **`CAL_RESULT,<PulsosIzq>,<PulsosDer>`**: Reporte final de pulsos generados tras un desplazamiento de prueba.
* **`DONE`**: Bandera crítica emitida al completar un tramo autónomo de forma exitosa. La PC retiene el siguiente comando de la ruta hasta capturar esta señal.

---

## 🛠️ Estructura del Repositorio

```
├── Carrito1.slnx              # Solución global del entorno de desarrollo (Visual Studio)
├── Carrito1/                  # Proyecto de Estación de Control en C#
│   ├── Form1.cs               # Lógica de la UI, GDI+, Hilos de Xbox y Gestión Asíncrona
│   ├── Form1.Designer.cs      # Inicialización procedural de los controles del panel
│   ├── Program.cs             # Punto de entrada de la aplicación de escritorio
│   └── packages.config        # Dependencias NuGet (SharpDX, 32feet.NET)
└── Firmware/                  # Código fuente para el microcontrolador (C++ / Arduino)
    └── firmware_esp32.ino     # Gestión de FSM, interrupciones ISR de encoders y lazo Kp
```

---

## 🚀 Guía de Instalación y Despliegue

### Requisitos del Entorno
1. **Para la Aplicación PC:** Visual Studio 2022 con soporte para el desarrollo de escritorio de .NET e instalación de .NET Framework 4.7.2.
2. **Para el Firmware:** Visual Studio Code con PlatformIO o Arduino IDE (añadiendo el soporte de placas ESP32 v2.x).

### Paso 1: Flashear el ESP32
1. Abre el código del firmware en tu IDE de preferencia.
2. Instala las librerías requeridas si utilizas Arduino IDE de forma tradicional:
   * `LiquidCrystal_I2C` (por Frank de Brabander).
   * Soporte integrado de `BluetoothSerial`.
3. Conecta el ESP32 por USB, selecciona la placa correspondiente y el puerto COM, y sube el programa.

### Paso 2: Ejecutar la Estación de Control
1. Abre el archivo de solución `Carrito1.slnx` en Visual Studio.
2. Restaura los paquetes NuGet si es necesario (el entorno descargará automáticamente `SharpDX` e `InTheHand.Net.Personal`).
3. Compila el proyecto en modo `Debug` o `Release` y ejecuta la aplicación.

### Paso 3: Vinculación Inalámbrica
1. Enciende el chasis del coche para iniciar el ESP32.
2. Abre la configuración de Bluetooth en Windows, busca nuevos dispositivos y emparéjate con `ESP32_CAR`.
3. En la interfaz gráfica del programa C#, selecciona el puerto COM virtual asignado o utiliza el buscador automático de Bluetooth para abrir el canal de datos.

---

## 📐 Flujo Metódico de Calibración de Odometría

Para garantizar que un comando de `50 cm` o un giro de `90°` coincida exactamente con la superficie de la pista real, sigue este proceso integrado:

1. Coloca el robot en el punto de inicio de una cinta métrica física.
2. En la aplicación de la PC, presiona el botón **Iniciar Calibración** (`CAL_START`).
3. Empuja de manera manual el chasis en línea recta exactamente **100 cm**.
4. Presiona el botón **Finalizar Calibración** (`CAL_STOP`) en el cuadro de diálogo de la PC.
5. El sistema leerá el mensaje `CAL_RESULT` enviado por el ESP32 (por ejemplo, `2000` pulsos promedio).
6. La PC calcula automáticamente el valor óptimo: $	ext{Pulsos por cm} = rac{2000}{100} = 20.0$.
7. El programa enviará la nueva configuración al ESP32 mediante el comando `CFG`, guardando los parámetros operativos en vivo sin alterar el código fuente.

---

## 👥 Contribución y Desarrollo del Equipo
Este proyecto está estructurado modularmente para facilitar la colaboración paralela en el ciclo de desarrollo por sprints (adecuado para equipos de 4 ingenieros):
* **Módulo UI & Gráficos (C#):** Manejo del lienzo GDI+, lógica de renderizado y estructuras de datos de rutas.
* **Módulo de Periféricos & E/S (C#):** Integración asíncrona de XInput, captura de buffers seriales y conectividad Bluetooth.
* **Módulo de Control de Movimiento (ESP32):** Sintonización del lazo proporcional ($K_p$), lógica de motores PWM y máquinas de estado.
* **Módulo de Odometría & Sensores (ESP32):** Gestión de rutinas ISR, filtrado de ruido en encoders y actualización de telemetría LCD.
