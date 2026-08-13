using System;
using System.Collections.Generic;
using System.Linq;
using SysDiag.Core;

namespace SysDiag.Models;

public enum Severity
{
    Ok,
    Warn,
    Bad
}
public class Finding
{
    public Severity Severity { get; set; }
    public string Area { get; set; } = "";
    public string Message { get; set; } = "";
    public string Action { get; set; } = "";
    /// <summary>Qué módulo lo generó. Lo usa la UI para fusionar corridas parciales sin duplicar.</summary>
    public string Modulo { get; set; } = "";

    /// <summary>
    /// Acción concreta que corrige este hallazgo, si existe una que sea segura
    /// y reversible. Muchos hallazgos no la tienen a propósito: un disco con
    /// errores o un jitter del proveedor no se arreglan desde el equipo, y
    /// ofrecer un botón ahí sería mentirle al usuario.
    /// </summary>
    public string AccionId { get; set; } = "";
    public string AccionTexto { get; set; } = "";

    public string Etiqueta => Severity switch
    {
        Severity.Bad => "Crítico",
        Severity.Warn => "Atención",
        _ => "Correcto"
    };
}
/// <summary>Contenedor de todo lo que produce una ejecución.</summary>
public class DiagnosticReport
{
    public DateTime Inicio { get; set; } = DateTime.Now;
    public string Equipo { get; set; } = Environment.MachineName;

    public List<Finding> Hallazgos { get; } = new();
    public List<KeyValueRow> Sistema { get; set; } = new();
    public List<DiskRow> Discos { get; set; } = new();
    public List<MemoryRow> Memoria { get; set; } = new();
    public List<KeyValueRow> WiFi { get; set; } = new();
    public List<LatencyResult> Red { get; set; } = new();
    public List<KeyValueRow> RendimientoResumen { get; set; } = new();
    public List<ProcessRow> TopCpu { get; set; } = new();
    public List<ProcessRow> TopRam { get; set; } = new();
    public List<KeyValueRow> Termicas { get; set; } = new();
    public List<EventSummaryRow> EventosResumen { get; set; } = new();
    public List<EventRow> EventosDetalle { get; set; } = new();
    public List<EventRow> Whea { get; set; } = new();
    public List<DumpRow> Minidumps { get; set; } = new();
    public List<TraceHop> Traceroute { get; set; } = new();
    public string TracerouteDestino { get; set; } = "";
    public List<KeyValueRow> Bateria { get; set; } = new();
    public List<CleanupRow> Limpieza { get; set; } = new();
    public string EspacioLiberado { get; set; } = "";
    public List<DriverRow> Drivers { get; set; } = new();
    public List<SecurityCheckRow> Seguridad { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
    public List<UpdateRow> Actualizaciones { get; set; } = new();
    public List<DriverUpdateRow> DriversDisponibles { get; set; } = new();
    public List<StorageRow> Almacenamiento { get; set; } = new();
    public List<StartupRow> Arranque { get; set; } = new();
    public List<ServiceRow> Servicios { get; set; } = new();
    public List<ProgramRow> Programas { get; set; } = new();
    public List<WifiNetworkRow> RedesCercanas { get; set; } = new();
    public int Puntaje { get; set; } = -1;

    public bool TieneDatosRelevantes() =>
        Hallazgos.Count > 0 || Sistema.Count > 0 || Discos.Count > 0 || Memoria.Count > 0 ||
        WiFi.Count > 0 || Red.Count > 0 || RendimientoResumen.Count > 0 || TopCpu.Count > 0 ||
        TopRam.Count > 0 || Termicas.Count > 0 || EventosResumen.Count > 0 || EventosDetalle.Count > 0 ||
        Whea.Count > 0 || Minidumps.Count > 0 || Traceroute.Count > 0 || Bateria.Count > 0 ||
        Limpieza.Count > 0 || Drivers.Count > 0 || Seguridad.Count > 0 || Gpus.Count > 0 ||
        Actualizaciones.Count > 0 || DriversDisponibles.Count > 0 || Almacenamiento.Count > 0 ||
        Arranque.Count > 0 || Servicios.Count > 0 || Programas.Count > 0 || RedesCercanas.Count > 0;

    public string ResumenEstado()
    {
        if (TieneDatosRelevantes())
            return Hallazgos.Count == 0
                ? "La comprobación se completó, pero no se detectaron problemas relevantes en los datos disponibles."
                : "La comprobación se completó con los datos disponibles del equipo.";

        var faltantes = ModulosFaltantes();
        var lista = faltantes.Count > 0 ? $" Módulos pendientes: {string.Join(", ", faltantes.Take(3))}." : "";
        var bloqueoWmi = Wmi.LastAccessDenied
            ? " El sistema respondió con acceso denegado a WMI o al registro; repetir como administrador suele completar los módulos faltantes."
            : "";

        if (!AppEnv.IsAdmin)
            return $"No se obtuvieron datos útiles. Repite la comprobación como administrador para completar WMI, registro y contadores del sistema.{lista}{bloqueoWmi}";

        return $"No se obtuvieron datos útiles. Es posible que el equipo no exponga esa información o que la consulta fallara en ese momento.{lista}{bloqueoWmi}";
    }

    public List<string> ModulosConDatos()
    {
        var modulos = new List<string>();
        if (Red.Count > 0) modulos.Add("Red y latencia");
        if (RendimientoResumen.Count > 0 || TopCpu.Count > 0 || TopRam.Count > 0) modulos.Add("Rendimiento");
        if (Termicas.Count > 0 || Bateria.Count > 0 || Gpus.Count > 0) modulos.Add("Térmicas y energía");
        if (Almacenamiento.Count > 0 || Discos.Count > 0 || Memoria.Count > 0) modulos.Add("Almacenamiento");
        if (EventosResumen.Count > 0 || Whea.Count > 0 || Minidumps.Count > 0 || EventosDetalle.Count > 0) modulos.Add("Estabilidad");
        if (Seguridad.Count > 0) modulos.Add("Seguridad");
        if (Drivers.Count > 0 || DriversDisponibles.Count > 0) modulos.Add("Drivers");
        if (Actualizaciones.Count > 0) modulos.Add("Actualizaciones");
        if (Limpieza.Count > 0) modulos.Add("Limpieza");
        if (Arranque.Count > 0 || Servicios.Count > 0 || Programas.Count > 0) modulos.Add("Arranque y software");
        if (Sistema.Count > 0) modulos.Add("Equipo");
        return modulos;
    }

    public List<string> ModulosFaltantes()
    {
        var todos = new List<string>
        {
            "Red y latencia",
            "Rendimiento",
            "Térmicas y energía",
            "Almacenamiento",
            "Estabilidad",
            "Seguridad",
            "Drivers",
            "Actualizaciones",
            "Limpieza",
            "Arranque y software"
        };

        var existentes = new HashSet<string>(ModulosConDatos(), StringComparer.OrdinalIgnoreCase);
        return todos.Where(m => !existentes.Contains(m)).ToList();
    }

    public void Add(Severity severity, string area, string message, string action = "",
                    string accionId = "")
    {
        var finding = new Finding
        {
            Severity = severity,
            Area = area,
            Message = message,
            Action = action,
            AccionId = accionId
        };

        if (!ContainsFinding(finding))
            Hallazgos.Add(finding);
    }

    private static bool SameFinding(Finding a, Finding b)
    {
        if (ReferenceEquals(a, b)) return true;
        return string.Equals(a.Area, b.Area, StringComparison.OrdinalIgnoreCase)
            && a.Severity == b.Severity
            && string.Equals(a.Message, b.Message, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Modulo, b.Modulo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.AccionId, b.AccionId, StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsFinding(Finding candidate)
    {
        return Hallazgos.Any(existing => SameFinding(existing, candidate));
    }

    /// <summary>
    /// Copia los datos de otro reporte (de un módulo que sí corrió) sin tocar los
    /// campos que ese módulo no toca. Así, correr un módulo suelto no borra lo
    /// que ya se sabía de los demás. El inventario (Sistema/Discos/Memoria) se
    /// maneja aparte porque todos los módulos lo refrescan de paso.
    /// </summary>
    public void MergeFrom(DiagnosticReport other)
    {
        if (other.WiFi.Count > 0) WiFi = other.WiFi;
        if (other.Red.Count > 0) Red = other.Red;
        if (other.Traceroute.Count > 0) { Traceroute = other.Traceroute; TracerouteDestino = other.TracerouteDestino; }
        if (other.RendimientoResumen.Count > 0) RendimientoResumen = other.RendimientoResumen;
        if (other.TopCpu.Count > 0) TopCpu = other.TopCpu;
        if (other.TopRam.Count > 0) TopRam = other.TopRam;
        if (other.Termicas.Count > 0) Termicas = other.Termicas;
        if (other.Bateria.Count > 0) Bateria = other.Bateria;
        if (other.EventosResumen.Count > 0) EventosResumen = other.EventosResumen;
        if (other.EventosDetalle.Count > 0) EventosDetalle = other.EventosDetalle;
        if (other.Whea.Count > 0) Whea = other.Whea;
        if (other.Minidumps.Count > 0) Minidumps = other.Minidumps;
        if (other.Limpieza.Count > 0) { Limpieza = other.Limpieza; EspacioLiberado = other.EspacioLiberado; }
        if (other.Drivers.Count > 0) Drivers = other.Drivers;
        if (other.Seguridad.Count > 0) Seguridad = other.Seguridad;
        if (other.Gpus.Count > 0) Gpus = other.Gpus;
        if (other.Actualizaciones.Count > 0) Actualizaciones = other.Actualizaciones;
        if (other.DriversDisponibles.Count > 0) DriversDisponibles = other.DriversDisponibles;
        if (other.Almacenamiento.Count > 0) Almacenamiento = other.Almacenamiento;
        if (other.Arranque.Count > 0) Arranque = other.Arranque;
        if (other.Servicios.Count > 0) Servicios = other.Servicios;
        if (other.Programas.Count > 0) Programas = other.Programas;
        if (other.RedesCercanas.Count > 0) RedesCercanas = other.RedesCercanas;

        foreach (var hallazgo in other.Hallazgos)
        {
            if (!ContainsFinding(hallazgo))
                Hallazgos.Add(hallazgo);
        }
    }
}
