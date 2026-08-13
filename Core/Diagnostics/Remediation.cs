using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SysDiag.Core.Windows;
using SysDiag.Models;

namespace SysDiag.Core.Diagnostics;

/// <summary>
/// Reparaciones de un clic.
///
/// Solo entran aquí acciones que cumplen tres condiciones: son seguras, son
/// reversibles y de verdad corrigen el hallazgo. Un hallazgo sin acción no es
/// una carencia: un SSD con errores no corregidos, jitter del proveedor o un
/// driver que el fabricante no ha publicado no se arreglan con un botón, y
/// fingir que sí es peor que no ofrecer nada.
/// </summary>
public static class Remediation
{
    public record Accion(string Id, string Titulo, string Descripcion, bool RequiereAdmin);

    private static readonly Dictionary<string, Accion> Catalogo = new()
    {
        ["wlan-autoconfig"] = new("wlan-autoconfig", "Rehabilitar Wi-Fi automático",
            "Vuelve a activar la configuración automática de WLAN para que el equipo se reconecte solo a las redes guardadas.", true),

        ["flush-dns"] = new("flush-dns", "Vaciar caché DNS",
            "Fuerza a resolver de nuevo los nombres de dominio. Sin riesgo.", false),

        ["wifi-power"] = new("wifi-power", "Quitar ahorro de energía del Wi-Fi",
            "Pone la radio en máximo rendimiento cuando el equipo está enchufado. Es causa habitual de picos de ping.", true),

        ["limpiar-temp"] = new("limpiar-temp", "Liberar espacio",
            "Borra archivos temporales que Windows y las aplicaciones regeneran solos.", false),

        ["buscar-drivers"] = new("buscar-drivers", "Buscar driver en Windows Update",
            "Consulta si Microsoft publica una versión más nueva, firmada y validada para este hardware.", false),

        ["abrir-inicio"] = new("abrir-inicio", "Abrir programas de inicio",
            "Abre el Administrador de tareas en la pestaña Inicio para desactivar lo que no necesites.", false),

        ["plan-energia"] = new("plan-energia", "Cambiar plan de energía",
            "Pasa el equipo a alto rendimiento. Reversible desde «Restaurar estado».", true),

        ["sfc"] = new("sfc", "Reparar archivos de sistema",
            "Ejecuta sfc /scannow en una consola visible: comprueba y repara archivos de Windows dañados.", true),

        ["reset-wu"] = new("reset-wu", "Reiniciar componentes de Windows Update",
            "Detiene los servicios de Windows Update, renombra su caché local (no la borra) y los vuelve a iniciar. Windows la reconstruye sola. Es el arreglo estándar de Microsoft para fallos de búsqueda persistentes.", true),

        ["punto-restauracion"] = new("punto-restauracion", "Crear punto de restauración",
            "Guarda un punto al que Windows puede volver completo si algo sale mal más adelante. No cambia nada del sistema ahora mismo.", true)
    };

    /// <summary>
    /// Detiene wuauserv/BITS, renombra la caché local de Windows Update
    /// (no la borra: queda con sufijo .bak, por si algo saliera mal) y
    /// reinicia los servicios. Windows regenera las carpetas solo.
    /// </summary>
    private static string ReiniciarWindowsUpdate()
    {
        // Punto de restauración real antes de tocar la carpeta: si algo sale
        // mal, Windows entero vuelve al estado de ahora, no solo esta carpeta.
        // Si no se pudo crear (Protección del sistema desactivada, algo
        // común de fábrica en SSD), se avisa y se sigue con el respaldo por
        // renombrado de siempre como red de seguridad mínima.
        var punto = RestorePointModule.Crear("Antes de reiniciar Windows Update");
        AppLog.Write(punto.Exito ? punto.Mensaje : $"Sin punto de restauración: {punto.Mensaje}",
            punto.Exito ? "OK" : "WARN");

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string sello = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        AppEnv.RunConsole("net", "stop wuauserv");
        AppEnv.RunConsole("net", "stop bits");

        int movidas = 0;
        foreach (var carpeta in new[] { "SoftwareDistribution" })
        {
            string ruta = System.IO.Path.Combine(windows, carpeta);
            if (!System.IO.Directory.Exists(ruta)) continue;
            try
            {
                System.IO.Directory.Move(ruta, $"{ruta}.bak_{sello}");
                movidas++;
            }
            catch (Exception ex)
            {
                AppLog.Write($"No se pudo renombrar {ruta}: {ex.Message}", "WARN");
            }
        }

        AppEnv.RunConsole("net", "start bits");
        AppEnv.RunConsole("net", "start wuauserv");

        string prefijoPunto = punto.Exito
            ? "Se creó un punto de restauración antes de este cambio.\n\n"
            : "No se pudo crear un punto de restauración (ver Registro para el detalle); se continuó igual.\n\n";

        return prefijoPunto + (movidas > 0
            ? "Componentes de Windows Update reiniciados. Vuelve a buscar drivers; si el error persiste, puede ser un corte temporal en los servidores de Microsoft."
            : "Los servicios se reiniciaron, pero no había caché que renombrar.");
    }

    public static Accion Obtener(string id) =>
        string.IsNullOrEmpty(id) ? null : Catalogo.GetValueOrDefault(id);

    /// <summary>Ejecuta la reparación y devuelve el resultado en texto.</summary>
    public static string Ejecutar(string id, DiagnosticReport r)
    {
        var accion = Obtener(id);
        if (accion == null) return "Esa reparación no existe.";

        if (accion.RequiereAdmin && !AppEnv.IsAdmin)
            return "Esta reparación necesita que SysDiag se ejecute como administrador.";

        AppLog.Write($"Reparación: {accion.Titulo}", "STEP");

        try
        {
            switch (id)
            {
                case "flush-dns":
                    AppEnv.RunConsole("ipconfig", "/flushdns");
                    return "Caché DNS vaciada.";

                case "wlan-autoconfig":
                    OptimizeModule.SaveState();
                    foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                                 .Where(x => x.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211))
                    {
                        AppEnv.RunConsole("netsh", $"wlan set autoconfig enabled=yes interface=\"{ni.Name}\"");
                    }
                    return "Configuración automática de WLAN rehabilitada.";

                case "wifi-power":
                    OptimizeModule.SaveState();
                    var opts = new OptimizeModule.Options
                    {
                        FlushDns = false,
                        FlushArp = false,
                        FixWlanAutoconfig = false,
                        WifiMaxPerformance = true
                    };
                    OptimizeModule.Run(r, opts);
                    return "Adaptador inalámbrico en máximo rendimiento. Se revierte desde «Restaurar estado».";

                case "plan-energia":
                    OptimizeModule.SaveState();
                    OptimizeModule.Run(r, new OptimizeModule.Options
                    {
                        FlushDns = false,
                        FlushArp = false,
                        FixWlanAutoconfig = false,
                        HighPerformancePlan = true
                    });
                    return "Plan de energía en alto rendimiento. Se revierte desde «Restaurar estado».";

                case "abrir-inicio":
                    Process.Start(new ProcessStartInfo("taskmgr.exe", "/7 /startup") { UseShellExecute = true });
                    return "Administrador de tareas abierto en la pestaña Inicio.";

                case "reset-wu":
                    return ReiniciarWindowsUpdate();

                case "punto-restauracion":
                    var manual = RestorePointModule.Crear("Punto manual desde SysDiag");
                    return manual.Mensaje;

                case "sfc":
                    // En consola visible y a propósito: la comprobación tarda
                    // varios minutos y conviene que el usuario vea el avance.
                    Process.Start(new ProcessStartInfo("cmd.exe", "/k sfc /scannow")
                    { UseShellExecute = true, Verb = "runas" });
                    return "Comprobación de archivos de sistema lanzada en una consola aparte.";

                default:
                    return "Esa reparación se maneja desde su propio módulo.";
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"La reparación falló: {ex.Message}", "ERROR");
            return $"No se pudo aplicar: {ex.Message}";
        }
    }
}
