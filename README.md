# SysDiag 5.7

[![Compilar y probar](https://github.com/nicolasdcarrillo-create/SysDiag-App/actions/workflows/build.yml/badge.svg)](https://github.com/nicolasdcarrillo-create/SysDiag-App/actions/workflows/build.yml) [![Validar fixture](https://github.com/nicolasdcarrillo-create/SysDiag-App/actions/workflows/validate-fixture.yml/badge.svg)](https://github.com/nicolasdcarrillo-create/SysDiag-App/actions/workflows/validate-fixture.yml)

Aplicación de escritorio para Windows que diagnostica red, rendimiento, térmicas y
estabilidad, limpia temporales y aplica optimizaciones reversibles. Interfaz gráfica
en WPF, registro de actividad e informe HTML exportable.

## Cómo obtener el .exe

El proyecto se entrega como código fuente. Para compilarlo:

1. Descomprime la carpeta `SysDiag` donde quieras.
2. Doble clic en **`build.bat`**.
3. El ejecutable queda en `publish\SysDiag.exe`.

No hace falta instalar nada a mano primero. `build.bat` revisa si el equipo tiene el
SDK de .NET 8; si no lo tiene, lo descarga e instala solo —en una carpeta propia
(`%LOCALAPPDATA%\SysDiag\dotnet-sdk`), sin pedir permisos de administrador y sin tocar
ninguna instalación de .NET que ya exista en el sistema (si detecta una que sirve, la
usa directamente en vez de descargar otra). Las siguientes veces que compiles reutiliza
esa copia y no vuelve a descargar nada.

Los paquetes NuGet y los componentes de escritorio de Windows (WinForms) también se
descargan solos durante la compilación; eso ya funcionaba así antes, no hacía falta
tocarlo.

Lo único que sigue haciendo falta es conexión a internet la primera vez (el SDK pesa
unos 200 MB). Si compilas en una red que filtre el tráfico saliente y la descarga
automática falla, el script termina con el enlace para instalarlo a mano:
<https://dotnet.microsoft.com/download/dotnet/8.0>.

El `.exe` resultante es **autocontenido**: pesa entre 60 y 90 MB porque lleva dentro el
runtime de .NET, y por eso funciona en cualquier Windows 10 u 11 de 64 bits aunque no
tenga nada instalado. Se puede copiar a un pendrive y ejecutar en otro equipo.

Si prefieres un archivo pequeño (unos 300 KB) a cambio de exigir .NET 8 instalado en la
máquina destino, edita `SysDiag.csproj` y cambia `<SelfContained>true</SelfContained>`
por `false`.

## Estructura

```
SysDiag/
├─ SysDiag.csproj
├─ App.xaml / App.xaml.cs        Arranque, cultura, instancia única, captura de errores
│
├─ Models/                       DTOs puros, sin lógica. Namespace SysDiag.Models
│  ├─ DiagnosticModels.cs        Severity, Finding, DiagnosticReport (+ MergeFrom)
│  ├─ HardwareModels.cs          KeyValueRow, DiskRow, MemoryRow, GpuInfo
│  ├─ NetworkModels.cs           LatencyResult, TraceHop, WifiNetworkRow
│  ├─ StorageModels.cs           StorageRow, CleanupRow
│  ├─ DriverModels.cs            DriverRow, DriverUpdateRow, UpdateRow
│  ├─ SecurityModels.cs          SecurityCheckRow
│  ├─ ProcessModels.cs / EventModels.cs / SoftwareModels.cs
│
├─ Core/                         Recolección de datos. Un namespace por subcarpeta
│  ├─ AppEnv.cs, ComWorker.cs, WmiHelper.cs     infraestructura transversal (SysDiag.Core)
│  ├─ Hardware/                  SystemModule, ThermalModule, GpuModule
│  ├─ Performance/                PerformanceModule
│  ├─ Network/                   NetworkModule (latencia, Wi-Fi, canales, traceroute)
│  ├─ Storage/                   StorageModule (SMART), CleanupModule
│  ├─ Security/                  SecurityModule (Defender, Firewall, BitLocker, TPM, Secure Boot, UAC)
│  ├─ Drivers/                   DriverModule, DriverUpdateModule, DriverVerifier
│  ├─ Windows/                   OptimizeModule, StartupModule, UpdateModule (winget)
│  └─ Diagnostics/               HealthScore, Remediation, ReportBuilder, Exporter, StabilityModule
│
├─ Services/                     Contrato hacia la UI. Namespace SysDiag.Services
│  ├─ Interfaces.cs               IDiagnosticService y las 8 interfaces de dominio
│  ├─ HardwareService, NetworkService, SecurityService, DriverService, StorageService
│  ├─ PerformanceService, StabilityService, StartupService   (sin interfaz dedicada)
│  └─ ScanService, RepairService, ReportService, HistoryService
│
├─ Diagnostics/                  Motor de reglas. Namespace SysDiag.Diagnostics
│  ├─ DiagnosticRule.cs           Interfaz IDiagnosticRule
│  ├─ DiagnosticEngine.cs         Corre las reglas registradas sobre un reporte ya recolectado
│  └─ CpuRules.cs, MemoryRules.cs Ejemplos reales, ya conectados (ver nota abajo)
│
└─ Ui/                            Capa visual en WPF
   ├─ Theme.xaml                  Tokens de color y tipografía, plantillas de control
   ├─ MainWindow.xaml             Ventana con chrome propio
   ├─ MainViewModel.cs            Estado observable y orquestación de sesión
   ├─ Charts.cs / Converters.cs
   ├─ CleanupWindow.xaml / OptimizeWindow.xaml / Dialog.xaml
```

## Módulos

| Módulo | Qué mide | Admin |
|---|---|---|
| Diagnóstico completo | Todos los de abajo en una pasada | recomendado |
| Red y latencia | RTT, jitter y pérdida contra router, internet y chat regional de Riot (LAS/LAN); señal, banda y canal Wi-Fi; traceroute hacia el destino más relevante | no |
| Rendimiento | CPU por proceso con muestreo real, RAM, cola de disco | no |
| Térmicas y energía | Temperatura ACPI, frecuencia actual vs. nominal, throttling, plan de energía, desgaste de batería (capacidad de diseño vs. actual) | no |
| Estabilidad | Kernel-Power 41, BugCheck, WHEA (catálogo general + escaneo dedicado por proveedor), errores de disco, minidumps | recomendado |
| Limpieza | Calcula, confirma y borra temporales reportando lo liberado | parcial |
| Drivers | Inventario de drivers con antigüedad, foco en almacenamiento/chipset/red | no |
| Optimizar | DNS, reparación de WLAN, plan de energía, reinicio de pila TCP/IP | sí |
| Restaurar | Deshace la última optimización desde el respaldo | sí |

### Sobre el módulo de Drivers

Es de **solo lectura**: audita versión y fecha de cada driver vía WMI y nada más.
A propósito no descarga ni instala nada — es exactamente la superficie que explotan
las herramientas tipo "driver updater" (bajan de espejos no verificados y a veces
instalan la versión equivocada). Para actualizar, la app te lleva con un clic a dos
canales oficiales: Windows Update ▸ Actualizaciones opcionales, o la página de soporte
del fabricante para tu modelo exacto.

La pestaña **Resumen** muestra el estado como tarjetas (dashboard): estado general, equipo,
latencia de referencia, CPU/RAM, desgaste de batería y conteo de errores WHEA/eventos
críticos — se arma sola con lo que haya en el último diagnóstico corrido.

La app arranca sin pedir UAC. Cuando eliges un módulo que lo necesita, ofrece reiniciarse
elevada; también hay un enlace permanente en la cabecera.

## Salidas

Todo en `Documentos\SysDiag\`:

- `informe_AAAAMMDD_HHmm.html` — informe con los hallazgos ordenados por severidad
- `logs\sysdiag_*.log` — registro completo de cada sesión
- `estado-previo.json` — respaldo para restaurar

## Release y packaging (5.7)

La parte de packaging ya no es solo "compila y sube .exe": ahora el workflow valida
que el bundle publicado tenga ejecutable, dependencias y estructura de release
correcta, y luego empaqueta la carpeta `publish` en un `.zip` para descarga.

El gate real en CI hace esto:

1. `dotnet publish` en Release.
2. `Tools/validate_release.ps1` confirma que existe `SysDiag.exe` y los artefactos
   críticos del runtime.
3. Se genera un ZIP con toda la carpeta publicada.
4. Se suben ambos artefactos (`.zip` y `SysDiag.exe`) como artefactos de la corrida.

Además, hay un workflow de release real para GitHub: `.github/workflows/release.yml`
que se dispara con etiquetas tipo `vX.Y.Z` y crea un release con notas automáticas y
la versión empaquetada lista para descargar.

Esto hace que el pipeline sea útil como validación de release y no solo de build.

## CI (5.7)

`.github/workflows/build.yml` compila el proyecto completo, ejecuta la suite de
pruebas de `Tools/IntegrationTests`, valida el bundle de release y deja el `.exe`
/`.zip` como artefactos descargables para cada corrida — en una máquina Windows
real que administra GitHub, no la tuya ni la mía. Se dispara en cada `push` a
`main`, en cada pull request y también a mano desde la pestaña Actions.

El pipeline también valida la fixture de diagnóstico y deja un primer gate de
calidad real para el repositorio antes de publicar artefactos.

## Tests (5.7)

La suite de pruebas está en `Tools/IntegrationTests/` y cubre la lógica pura
verificada contra escenarios reales: cálculo del puntaje, fusión parcial de
reportes, no duplicación de hallazgos y validación del fixture generado desde la
ejecución real del diagnóstico.

Se ejecuta con:

```powershell
dotnet test "Tools/IntegrationTests/IntegrationTests.csproj" --verbosity minimal
```

También se integra en el solution y en el workflow de CI para que cada cambio
quede validado automáticamente en Windows.

## Sobre la arquitectura (5.0)

La versión 5.0 reestructura el proyecto en capas físicas separadas:
`Models/` (datos puros) → `Core/` (recolección, un namespace por dominio de hardware)
→ `Services/` (el contrato hacia la UI, con interfaces) → `Diagnostics/` (motor de
reglas) → `Ui/` (WPF).

Dos decisiones que vale la pena explicar:

- **El motor de reglas convive con las reglas existentes, no las reemplaza.**
  `DiagnosticEngine` corre reglas registradas (`CpuRules`, `MemoryRules` por ahora)
  sobre el reporte ya recolectado. Portar automáticamente los ~40 hallazgos que ya
  generan los módulos de `Core/` a clases de regla individuales habría sido un
  refactor grande sin beneficio funcional — esa lógica ya está probada donde está.
  Los dos ejemplos SÍ reemplazaron su versión inline (se retiró de
  `PerformanceModule` para no duplicar el hallazgo), como muestra real del patrón
  para lo que se agregue de aquí en más.
- **Los namespaces de `Core/` no calzan 1:1 con dónde vive `AppEnv`/`ComWorker`/`Wmi`.**
  Quedaron en la raíz de `SysDiag.Core` porque son infraestructura transversal
  (registro, WMI, hilo COM) usada por todos los dominios; C# hace visibles los
  namespaces padre a los hijos sin necesidad de `using`, así que
  `SysDiag.Core.Network` ve `AppEnv` sin declarar nada extra.

Dos módulos nuevos, ambos reales — nada de datos de ejemplo:

- **Seguridad** (`Core/Security/SecurityModule.cs`): Defender vía
  `MSFT_MpComputerStatus`, Firewall vía `netsh`, BitLocker y TPM vía WMI, Secure
  Boot vía registro, UAC vía registro. Todo de solo lectura.
- **GPU** (`Core/Hardware/GpuModule.cs`): modelo y driver por
  `Win32_VideoController`, uso real por el contador de rendimiento "GPU Engine"
  que trae Windows de fábrica — mismo dato que muestra el Administrador de
  tareas. Si el contador no está disponible, el campo queda vacío en vez de
  completarse con un número inventado.

## Sobre la interfaz

La capa visual está en **WPF**, no en WinForms. WinForms dibuja con GDI+ en píxeles
fijos y sus controles nativos (`TabControl`, `ComboBox`, `ListView`, `ProgressBar`,
barras de desplazamiento, barra de título) no aceptan tema: en modo oscuro quedaban
islas blancas imposibles de corregir. WPF es vectorial, escala solo por DPI y permite
retemplar cualquier control. Viene incluido en el SDK de .NET 8, así que no agrega
ninguna dependencia de NuGet.

El motor `Core/` no cambió ni una línea con la migración: esa separación estricta entre
medición y presentación fue justamente lo que la hizo barata.

Decisiones de diseño:

- **Paleta azul-pizarra**, no negro puro, con acento índigo `#6C7BF7`. La profundidad
  viene de la elevación de superficie, no de bordes marcados.
- **Tres roles tipográficos**: Bahnschrift (un DIN, la letra del dibujo técnico) para
  cifras y rótulos, Segoe UI Variable para texto corrido, y Cascadia Mono con cifras
  tabulares para todo dato numérico, así las columnas no bailan.
- **La regla de escala**: cada métrica del Resumen lleva debajo una serie de marcas que
  se llenan según su magnitud, como la escala de un instrumento. Es el elemento que da
  identidad a la interfaz y codifica lo que la aplicación hace: medir.
- **Barra de título propia**: la del sistema no se puede tematizar.
- **Diálogos propios**: `MessageBox` se dibuja en claro y rompe el conjunto.
- **La navegación es una lista con selección**, no botones sueltos: el módulo activo
  queda marcado sin estado que sincronizar a mano.

## Decisiones técnicas

- **CPU medida por diferencia.** Se toman dos lecturas de `TotalProcessorTime` separadas
  por N segundos y se normaliza por número de núcleos. Leer el acumulado del proceso,
  como hace la mayoría de los scripts, solo premia a los procesos más antiguos.
- **Sin `Get-Counter` ni contadores por nombre.** Los nombres de contador vienen traducidos
  en un Windows en español y rompen el código. Se usan clases CIM, que son estables.
- **La configuración automática de WLAN nunca se desactiva.** Si se detecta apagada, se
  ofrece encenderla: dejarla así impide reconectarse solo a las redes guardadas.
- **Todo cambio es reversible.** Antes de optimizar se serializa el estado a JSON.
- **El reinicio de la pila TCP/IP exige doble confirmación** y advierte de que borra IP fija,
  DNS personalizados y configuración de VPN.
- **Jitter en vez de solo ping medio.** Es la métrica que explica los tirones en juego.

## Nota sobre antivirus

Un ejecutable recién compilado y sin firma digital puede activar SmartScreen la primera
vez ("Windows protegió su PC" → *Más información* → *Ejecutar de todas formas"). Es normal
en binarios propios. Para distribuirlo a terceros haría falta un certificado de firma de
código.

## Siguientes pasos posibles

- Temperatura por núcleo integrando `LibreHardwareMonitorLib` (requiere driver).
- Historial entre ejecuciones para comparar antes y después de un cambio.
- Exportación a CSV/JSON para graficar tendencias.
- Monitor en vivo de latencia mientras juegas, con gráfico en tiempo real.

## Fixtures para desarrollo y CI

Se incluye un diagnóstico real que sirve como fixture para pruebas de integración y para desarrollar la UI sin ejecutar WMI/COM. Está en:

- Tools\fixtures\diagnostico_fixture.json

Para validar localmente que el fixture contiene hallazgos relevantes, ejecutar (PowerShell):

PS> .\Tools\validate_fixture.ps1

El script devuelve código de salida 0 si la comprobación pasa. Usar este archivo como entrada para pruebas o para poblar vistas en desarrollo. Si se desea integrar pruebas xUnit en el repo, se puede añadir un proyecto de tests, pero puede requerir ajustar las propiedades de ensamblado del proyecto principal para evitar duplicidad de atributos en la compilación (GenerateAssemblyInfo).

