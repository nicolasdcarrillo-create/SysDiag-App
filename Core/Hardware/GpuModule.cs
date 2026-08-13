using System;
using System.Linq;
using SysDiag.Models;

namespace SysDiag.Core.Hardware;

/// <summary>
/// Información de GPU sin depender de SDKs de fabricante (NVAPI/ADL, que no
/// están disponibles sin instalar el paquete correspondiente). Lo estático
/// (modelo, driver, memoria) viene de Win32_VideoController; el uso en vivo
/// viene del contador de rendimiento "GPU Engine" que trae Windows 10/11 de
/// fábrica — mismo dato que muestra el Administrador de tareas.
///
/// Si el contador no está disponible, el campo de uso queda vacío en vez de
/// completarse con un número que no se midió.
/// </summary>
public static class GpuModule
{
    public static void Run(DiagnosticReport r)
    {
        AppLog.Write("GPU", "STEP");

        var gpus = Wmi.Query("SELECT * FROM Win32_VideoController WHERE PNPDeviceID IS NOT NULL")
            .Select(g => new GpuInfo
            {
                Nombre = Wmi.Str(g, "Name"),
                Fabricante = Wmi.Str(g, "AdapterCompatibility"),
                DriverVersion = Wmi.Str(g, "DriverVersion"),
                DriverFecha = FormatearFechaWmi(Wmi.Str(g, "DriverDate")),
                // AdapterRAM reporta mal en GPUs modernas con más de 4 GB
                // (desborda el entero de 32 bits que usa WMI); se muestra
                // igual pero sin pretender que sea siempre exacto.
                MemoriaDedicada = FormatearMemoria(Wmi.Num(g, "AdapterRAM"))
            })
            .Where(g => !string.IsNullOrWhiteSpace(g.Nombre))
            .ToList();

        var usoTotal = LeerUsoPorContador();

        // El contador no distingue de qué adaptador viene cada muestra. Con
        // una sola GPU activa el total le pertenece sin ambigüedad; con dos
        // (equipo híbrido) no hay forma honesta de repartirlo, así que se
        // deja vacío en vez de adivinar a cuál asignarlo.
        if (gpus.Count == 1 && usoTotal.HasValue)
            gpus[0].UsoPct = $"{usoTotal.Value:0} %";

        foreach (var gpu in gpus)
            AppLog.Write($"{gpu.Nombre,-40} driver {gpu.DriverVersion} ({gpu.DriverFecha})  uso {(gpu.UsoPct == "" ? "n/d" : gpu.UsoPct)}");

        r.Gpus = gpus;

        if (gpus.Count == 0)
        {
            AppLog.Write("No se detectó ninguna GPU.", "WARN");
            return;
        }

        var integrada = gpus.FirstOrDefault(g => g.Nombre.Contains("Intel", StringComparison.OrdinalIgnoreCase)
                                               || g.Nombre.Contains("AMD Radeon Graphics", StringComparison.OrdinalIgnoreCase));
        var dedicada = gpus.FirstOrDefault(g => g != integrada);

        if (integrada != null && dedicada != null)
            r.Add(Severity.Ok, "GPU", $"Equipo con gráficos híbridos: {integrada.Nombre} + {dedicada.Nombre}.",
                "Si un juego usa la integrada por error, el rendimiento cae mucho. Revisa en Configuración ▸ Pantalla ▸ Gráficos que la app correcta use «Alto rendimiento».");
    }

    /// <summary>
    /// "GPU Engine" es una categoría de contador con una instancia por
    /// proceso y motor (3D, Copy, Video Decode...). Se suman las instancias
    /// del motor 3D por adaptador para aproximar el uso total de cada GPU.
    /// </summary>
    private static double? LeerUsoPorContador()
    {
        try
        {
            if (!System.Diagnostics.PerformanceCounterCategory.Exists("GPU Engine"))
                return null;

            var categoria = new System.Diagnostics.PerformanceCounterCategory("GPU Engine");
            var instancias = categoria.GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (instancias.Count == 0) return null;

            double total = 0;
            foreach (var inst in instancias)
            {
                try
                {
                    using var c = new System.Diagnostics.PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                    c.NextValue();
                    System.Threading.Thread.Sleep(50);
                    total += c.NextValue();
                }
                catch { /* la instancia desapareció entre enumerar y leer */ }
            }

            return Math.Min(total, 100);
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo leer el contador GPU Engine: {ex.Message}", "WARN");
            return null;
        }
    }

    private static string FormatearMemoria(double bytes)
    {
        if (bytes <= 0) return "n/d";
        return AppEnv.FormatBytes(bytes);
    }

    private static string FormatearFechaWmi(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8) return "desconocida";
        try
        {
            return $"{raw.Substring(0, 4)}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)}";
        }
        catch { return "desconocida"; }
    }
}
