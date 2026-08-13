using System.ComponentModel;

namespace SysDiag.Models;

public class KeyValueRow
{
    [DisplayName("Dato")] public string Clave { get; set; } = "";
    [DisplayName("Valor")] public string Valor { get; set; } = "";

    public KeyValueRow() { }
    public KeyValueRow(string clave, object valor)
    {
        Clave = clave;
        Valor = valor?.ToString() ?? "";
    }
}
public class DiskRow
{
    [DisplayName("Unidad")] public string Unidad { get; set; } = "";
    [DisplayName("Etiqueta")] public string Etiqueta { get; set; } = "";
    [DisplayName("Tamaño")] public string Tamano { get; set; } = "";
    [DisplayName("Libre")] public string Libre { get; set; } = "";
    [DisplayName("Libre %")] public double LibrePct { get; set; }
}
public class MemoryRow
{
    [DisplayName("Ranura")] public string Ranura { get; set; } = "";
    [DisplayName("Capacidad")] public string Capacidad { get; set; } = "";
    [DisplayName("Velocidad")] public string Velocidad { get; set; } = "";
    [DisplayName("Fabricante")] public string Fabricante { get; set; } = "";
    [DisplayName("Nº de parte")] public string Parte { get; set; } = "";
}

public class GpuInfo
{
    [DisplayName("GPU")] public string Nombre { get; set; } = "";
    [DisplayName("Fabricante")] public string Fabricante { get; set; } = "";
    [DisplayName("Driver")] public string DriverVersion { get; set; } = "";
    [DisplayName("Fecha del driver")] public string DriverFecha { get; set; } = "";
    [DisplayName("Memoria dedicada")] public string MemoriaDedicada { get; set; } = "";
    /// <summary>"" si no se pudo medir: nunca se rellena con un número inventado.</summary>
    [DisplayName("Uso")] public string UsoPct { get; set; } = "";
}
