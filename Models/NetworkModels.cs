using System.ComponentModel;

namespace SysDiag.Models;

public class LatencyResult
{
    [DisplayName("Destino")] public string Destino { get; set; } = "";
    [DisplayName("Host")] public string Host { get; set; } = "";
    [DisplayName("Enviados")] public int Enviados { get; set; }
    [DisplayName("Perdidos")] public int Perdidos { get; set; }
    [DisplayName("Pérdida %")] public double PerdidaPct { get; set; }
    [DisplayName("Mín (ms)")] public double Min { get; set; }
    [DisplayName("Media (ms)")] public double Media { get; set; }
    [DisplayName("Máx (ms)")] public double Max { get; set; }
    [DisplayName("Jitter (ms)")] public double Jitter { get; set; }
    [Browsable(false)] public Severity Estado { get; set; }
}
public class TraceHop
{
    [DisplayName("Salto")] public int Hop { get; set; }
    [DisplayName("Dirección")] public string Direccion { get; set; } = "";
    [DisplayName("Nombre")] public string Nombre { get; set; } = "";
    [DisplayName("Media (ms)")] public string Media { get; set; } = "";
    [Browsable(false)] public Severity Estado { get; set; }
}
public class WifiNetworkRow
{
    [DisplayName("Red")] public string Ssid { get; set; } = "";
    [DisplayName("Canal")] public int Canal { get; set; }
    [DisplayName("Banda")] public string Banda { get; set; } = "";
    [DisplayName("Señal")] public string Senal { get; set; } = "";
    [Browsable(false)] public int SenalPct { get; set; }
}
