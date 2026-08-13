using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using SysDiag.Models;

namespace SysDiag.Core.Drivers;

/// <summary>
/// Búsqueda, descarga e instalación de drivers a través del Agente de Windows
/// Update: el mismo motor detrás de «Actualizaciones opcionales».
///
/// Se eligió este canal y no la descarga directa desde sitios de fabricantes
/// porque aquí Microsoft ya hizo dos cosas que un descargador casero no puede:
/// verificar la firma WHQL del paquete y comprobar que el driver corresponde a
/// los IDs de hardware reales de este equipo. Instalar un driver que no calza
/// —sobre todo de almacenamiento— puede dejar el sistema sin arrancar.
///
/// Se usa enlace tardío (COM por ProgID) para no arrastrar una referencia a
/// WUApiLib, que complicaría la compilación en un solo ejecutable.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DriverUpdateModule
{
    // Todo el trabajo COM pasa por este hilo. Los resultados de una búsqueda
    // son punteros válidos solo dentro del apartamento donde nacieron, así que
    // buscar e instalar tienen que ocurrir en el mismo.
    private static readonly ComWorker Worker = new("SysDiag.WindowsUpdate");

    private static dynamic _sesion;
    private static dynamic _resultado;

    /// <summary>Último error real, para poder explicarlo en pantalla.</summary>
    public static string UltimoError { get; private set; } = "";

    public static List<DriverUpdateRow> Buscar(DiagnosticReport r)
    {
        AppLog.Write("Buscando drivers en Windows Update", "STEP");
        UltimoError = "";

        var filas = Worker.Run(() => BuscarInterno(r));
        r.DriversDisponibles = filas;
        return filas;
    }

    // Servicio de catálogo (DCat) desde donde Windows publica los drivers
    // opcionales. Es el mismo origen que alimenta «Actualizaciones opcionales ▸
    // Actualizaciones de controladores»; el canal estándar de Windows Update
    // no los devuelve, y por eso una búsqueda normal sale siempre en cero.
    private const string ServicioDrivers = "855E8A7C-ECB4-4CA3-B045-1DFA50104289";

    private static List<DriverUpdateRow> BuscarInterno(DiagnosticReport r)
    {
        var filas = new List<DriverUpdateRow>();

        var tipo = Type.GetTypeFromProgID("Microsoft.Update.Session");
        if (tipo == null)
        {
            UltimoError = "El Agente de Windows Update no está disponible en este equipo.";
            AppLog.Write(UltimoError, "ERROR");
            return filas;
        }

        RevisarDirectivas(r);

        _sesion = Activator.CreateInstance(tipo);

        bool dcatDisponible = PrepararServicioDrivers();

        // Se intentan varias vías porque cuál funciona depende de la edición de
        // Windows y de las directivas aplicadas. La primera es la que devuelve
        // los drivers opcionales; las otras quedan como respaldo.
        var estrategias = new List<(string Nombre, Action<dynamic> Configurar)>();

        if (dcatDisponible)
            estrategias.Add(("catálogo de drivers (DCat)", b =>
            {
                b.ServerSelection = 3;               // ssOthers
                b.ServiceID = ServicioDrivers;
            }));

        estrategias.Add(("Windows Update", b => { b.ServerSelection = 2; }));
        estrategias.Add(("Microsoft Update", b =>
        {
            b.ServerSelection = 3;
            b.ServiceID = "7971f918-a847-4430-9279-4a52d1efe18d";
        }));
        estrategias.Add(("origen configurado del equipo", b => { }));

        foreach (var (nombre, configurar) in estrategias)
        {
            try
            {
                AppLog.Write($"Consultando {nombre}...");

                dynamic buscador = _sesion.CreateUpdateSearcher();
                try { buscador.Online = true; } catch { }
                configurar(buscador);

                _resultado = buscador.Search("Type='Driver' and IsInstalled=0 and IsHidden=0");

                int total = _resultado.Updates.Count;
                AppLog.Write($"  {nombre}: {total} resultado(s)");

                if (total == 0) continue;

                for (int i = 0; i < total; i++)
                {
                    dynamic u = _resultado.Updates.Item(i);

                    double bytes = 0;
                    try { bytes = Convert.ToDouble(u.MaxDownloadSize); } catch { }

                    string fecha = "";
                    try { fecha = ((DateTime)u.LastDeploymentChangeTime).ToString("yyyy-MM-dd"); } catch { }

                    string fabricante = "";
                    try { fabricante = u.DriverManufacturer ?? ""; } catch { }

                    string version = "";
                    try { version = u.DriverVerVersion ?? ""; } catch { }

                    filas.Add(new DriverUpdateRow
                    {
                        Titulo = u.Title,
                        Fabricante = fabricante,
                        Version = version,
                        Fecha = fecha,
                        Tamano = bytes > 0 ? AppEnv.FormatBytes(bytes) : "n/d",
                        UpdateId = SafeId(u),
                        Indice = i
                    });

                    AppLog.Write($"  {u.Title}");
                }

                AppLog.Write($"Drivers disponibles ({nombre}): {filas.Count}", "WARN");

                r.Add(Severity.Warn, "Drivers", $"{filas.Count} driver(s) con versión más reciente disponible.",
                    "Vienen firmados por Microsoft y validados contra el hardware de este equipo. Instálalos desde el botón «Instalar seleccionado».");

                return filas;
            }
            catch (Exception ex)
            {
                AppLog.Write($"  {nombre}: {ex.Message}", "WARN");
                UltimoError = Explicar(ex);
            }
        }

        if (string.IsNullOrEmpty(UltimoError))
        {
            UltimoError = "Ninguno de los orígenes de Windows Update ofrece drivers nuevos para este equipo. " +
                          "Puede que ya estén todos al día, o que el fabricante publique versiones que Microsoft no distribuye.";
            r.Add(Severity.Ok, "Drivers", "Windows Update no ofrece drivers nuevos para este equipo.",
                "Si un driver sigue apareciendo viejo en la tabla «Drivers», búscalo en la página de soporte del fabricante.");
        }
        else
        {
            r.Add(Severity.Warn, "Drivers", "No se pudo consultar el catálogo de drivers.", UltimoError,
                "reset-wu");
        }

        return filas;
    }

    /// <summary>
    /// Registra el servicio de catálogo si hace falta y deja constancia de qué
    /// orígenes conoce el equipo.
    ///
    /// Sin este paso, fijar ServiceID sobre un servicio no registrado hace
    /// fallar la búsqueda, y el usuario ve «cero drivers» sin saber que la
    /// consulta nunca llegó a salir.
    /// </summary>
    private static bool PrepararServicioDrivers()
    {
        try
        {
            dynamic gestor = Activator.CreateInstance(
                Type.GetTypeFromProgID("Microsoft.Update.ServiceManager"));

            bool registrado = false;
            AppLog.Write("Orígenes de actualización registrados:");

            for (int i = 0; i < gestor.Services.Count; i++)
            {
                dynamic svc = gestor.Services.Item(i);
                string id = "";
                try { id = svc.ServiceID; } catch { }

                AppLog.Write($"  {svc.Name}  [{id}]");
                if (string.Equals(id, ServicioDrivers, StringComparison.OrdinalIgnoreCase))
                    registrado = true;
            }

            if (registrado)
            {
                AppLog.Write("El catálogo de drivers ya está registrado.", "OK");
                return true;
            }

            if (!AppEnv.IsAdmin)
            {
                AppLog.Write("El catálogo de drivers no está registrado y registrarlo requiere administrador.", "WARN");
                return false;
            }

            // 2 = asfAllowOnlineRegistration
            AppLog.Write("Registrando el catálogo de drivers...");
            gestor.AddService2(ServicioDrivers, 2, "");
            AppLog.Write("Catálogo de drivers registrado.", "OK");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo preparar el catálogo de drivers: {ex.Message}", "WARN");
            return false;
        }
    }

    /// <summary>
    /// Comprueba las directivas que bloquean la entrega de drivers. En
    /// ediciones LTSC, IoT y Enterprise suelen venir puestas de fábrica, y sin
    /// revisarlas el usuario ve «cero drivers» sin saber que la búsqueda nunca
    /// tuvo posibilidad de devolver algo.
    /// </summary>
    private static void RevisarDirectivas(DiagnosticReport r)
    {
        try
        {
            using var wu = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");

            if (wu?.GetValue("ExcludeWUDriversInQualityUpdate") is int excl && excl == 1)
            {
                AppLog.Write("Directiva activa: ExcludeWUDriversInQualityUpdate = 1", "WARN");
                r.Add(Severity.Warn, "Drivers", "Una directiva del sistema excluye los drivers de Windows Update.",
                    "Está activa «No incluir controladores con las actualizaciones de Windows». Se cambia en gpedit.msc ▸ Configuración del equipo ▸ Plantillas administrativas ▸ Componentes de Windows ▸ Windows Update, o borrando el valor ExcludeWUDriversInQualityUpdate del registro.");
            }

            using var dm = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata");

            if (dm?.GetValue("PreventDeviceMetadataFromNetwork") is int prev && prev == 1)
                AppLog.Write("Directiva activa: PreventDeviceMetadataFromNetwork = 1", "WARN");

            using var ds = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching");

            if (ds?.GetValue("SearchOrderConfig") is int orden && orden == 0)
            {
                AppLog.Write("Directiva activa: SearchOrderConfig = 0 (no buscar en Windows Update)", "WARN");
                r.Add(Severity.Warn, "Drivers", "El equipo tiene desactivada la búsqueda de drivers en Windows Update.",
                    "En Configuración avanzada del sistema ▸ Hardware ▸ Configuración de instalación de dispositivos, elige que Windows descargue software de dispositivos automáticamente.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudieron leer las directivas de driver: {ex.Message}", "WARN");
        }
    }

    /// <summary>
    /// Traduce el código de error de Windows Update a algo accionable. Sin esto
    /// el usuario solo ve «falló» y no tiene por dónde seguir.
    /// </summary>
    private static string Explicar(Exception ex)
    {
        int hr = ex.HResult;
        string codigo = $"0x{hr:X8}";

        string causa = (uint)hr switch
        {
            0x8024402C => "No hay conexión con el servidor de Windows Update. Revisa la red o el proxy.",
            0x80240438 => "Una directiva del sistema bloquea la búsqueda de drivers. Es habitual en ediciones LTSC o Enterprise con Windows Update administrado por directiva de grupo.",
            0x8024002E => "Windows Update está restringido por directiva («Acceso a todas las características de Windows Update»).",
            0x8024802A => "El servicio de localización de Microsoft devolvió un error temporal (503, servidor no disponible). No es un problema de este equipo: suele resolverse solo reintentando en unos minutos. Si persiste, reiniciar los componentes de Windows Update también ayuda.",
            0x80070422 => "El servicio Windows Update está detenido o deshabilitado. Actívalo en Servicios (wuauserv).",
            0x80244022 => "El servidor de actualizaciones rechazó la petición o no está disponible ahora mismo.",
            0x80240024 => "No hay actualizaciones aplicables a este equipo.",
            _ => "Comprueba que el servicio Windows Update (wuauserv) esté en ejecución y que haya conexión a internet."
        };

        return $"{causa}  [código {codigo}]";
    }

    /// <summary>
    /// Descarga e instala las actualizaciones indicadas. Requiere privilegios de
    /// administrador; devuelve un resumen legible de lo ocurrido.
    /// </summary>
    public static string Instalar(IEnumerable<DriverUpdateRow> seleccion, Action<string> progreso = null)
    {
        if (!AppEnv.IsAdmin)
            return "Instalar drivers requiere ejecutar SysDiag como administrador.";

        var elegidos = seleccion?.ToList() ?? new List<DriverUpdateRow>();
        if (elegidos.Count == 0) return "No se seleccionó ninguna actualización.";
        if (_resultado == null) return "Vuelve a buscar antes de instalar.";

        return Worker.Run(() => InstalarInterno(elegidos, progreso));
    }

    private static string InstalarInterno(List<DriverUpdateRow> elegidos, Action<string> progreso)
    {
        // Un punto de restauración por driver instalado sería ruidoso; se
        // crea uno solo cubriendo el lote completo de esta instalación.
        progreso?.Invoke("Creando punto de restauración...");
        var punto = SysDiag.Core.Windows.RestorePointModule.Crear(
            elegidos.Count == 1 ? $"Antes de instalar {elegidos[0].Titulo}" : $"Antes de instalar {elegidos.Count} drivers");
        AppLog.Write(punto.Exito ? punto.Mensaje : $"Sin punto de restauración: {punto.Mensaje}",
            punto.Exito ? "OK" : "WARN");

        try
        {
            var tipoCol = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
            dynamic aDescargar = Activator.CreateInstance(tipoCol);

            foreach (var fila in elegidos)
            {
                dynamic u = _resultado.Updates.Item(fila.Indice);

                // Las licencias hay que aceptarlas explícitamente; sin esto la
                // instalación falla en silencio.
                try
                {
                    if (u.EulaAccepted == false) u.AcceptEula();
                }
                catch { }

                aDescargar.Add(u);
            }

            dynamic sesion = _sesion ?? Activator.CreateInstance(
                Type.GetTypeFromProgID("Microsoft.Update.Session"));

            progreso?.Invoke("Descargando...");
            AppLog.Write($"Descargando {elegidos.Count} driver(s)...", "STEP");

            dynamic descargador = sesion.CreateUpdateDownloader();
            descargador.Updates = aDescargar;
            dynamic resDescarga = descargador.Download();

            // 2 = correcto, 3 = correcto con errores.
            int codigoDescarga = Convert.ToInt32(resDescarga.ResultCode);
            if (codigoDescarga != 2 && codigoDescarga != 3)
            {
                AppLog.Write($"La descarga terminó con código {codigoDescarga}.", "ERROR");
                return "No se pudo descargar. Revisa el registro para el detalle.";
            }

            dynamic aInstalar = Activator.CreateInstance(tipoCol);
            for (int i = 0; i < aDescargar.Count; i++)
            {
                dynamic u = aDescargar.Item(i);
                if (u.IsDownloaded) aInstalar.Add(u);
            }

            if (aInstalar.Count == 0) return "Ningún paquete quedó descargado correctamente.";

            progreso?.Invoke("Instalando...");
            AppLog.Write($"Instalando {aInstalar.Count} driver(s)...", "STEP");

            dynamic instalador = sesion.CreateUpdateInstaller();
            instalador.Updates = aInstalar;
            dynamic resInstalar = instalador.Install();

            int codigo = Convert.ToInt32(resInstalar.ResultCode);
            bool reinicio = false;
            try { reinicio = resInstalar.RebootRequired; } catch { }

            string estado = codigo switch
            {
                2 => "Instalación completada.",
                3 => "Instalación completada con advertencias.",
                4 => "La instalación falló.",
                5 => "La instalación fue cancelada.",
                _ => $"La instalación terminó con código {codigo}."
            };

            AppLog.Write(estado, codigo == 2 ? "OK" : "WARN");

            estado = (punto.Exito
                ? "Se creó un punto de restauración antes de instalar. "
                : "No se pudo crear un punto de restauración (ver Registro); se instaló igual. ") + estado;

            if (reinicio)
            {
                AppLog.Write("Hay que REINICIAR el equipo para aplicar los drivers.", "WARN");
                estado += "\n\nHay que reiniciar el equipo para que los drivers queden activos.";
            }

            return estado;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Error al instalar drivers: {ex.Message}", "ERROR");
            return "No se pudo completar la instalación.\n\n" + Explicar(ex);
        }
    }

    /// <summary>
    /// Abre las propiedades del dispositivo en el Administrador de dispositivos,
    /// donde está la pestaña Controlador con «Actualizar» y «Revertir». Es la
    /// vía para un driver puntual cuando el fabricante publica algo más nuevo
    /// que Windows Update todavía no distribuye.
    /// </summary>
    public static void AbrirPropiedades(string deviceId)
    {
        try
        {
            string args = string.IsNullOrWhiteSpace(deviceId)
                ? "devmgr.dll DeviceProperties_RunDLL"
                : $"devmgr.dll DeviceProperties_RunDLL /DeviceID \"{deviceId}\"";

            Process.Start(new ProcessStartInfo("rundll32.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo abrir las propiedades del dispositivo: {ex.Message}", "ERROR");
        }
    }

    private static string SafeId(dynamic u)
    {
        try { return u.Identity.UpdateID; }
        catch { return ""; }
    }
}
