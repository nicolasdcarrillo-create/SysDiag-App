using System;
using System.Collections.Generic;

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

    public void Add(Severity severity, string area, string message, string action = "",
                    string accionId = "")
    {
        Hallazgos.Add(new Finding
        {
            Severity = severity,
            Area = area,
            Message = message,
            Action = action,
            AccionId = accionId
        });
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
        Hallazgos.AddRange(other.Hallazgos);
    }
}
