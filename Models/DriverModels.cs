using System.ComponentModel;

namespace SysDiag.Models;

public class DriverRow
{
    [DisplayName("Dispositivo")] public string Dispositivo { get; set; } = "";
    [DisplayName("Categoría")] public string Categoria { get; set; } = "";
    [DisplayName("Fabricante")] public string Fabricante { get; set; } = "";
    [DisplayName("Versión")] public string Version { get; set; } = "";
    [DisplayName("Fecha")] public string Fecha { get; set; } = "";
    [DisplayName("Antigüedad")] public string Antiguedad { get; set; } = "";
    /// <summary>Instancia PnP del dispositivo, para abrir sus propiedades.</summary>
    [Browsable(false)] public string DeviceId { get; set; } = "";
    [Browsable(false)] public Severity Estado { get; set; }
}
public class DriverUpdateRow
{
    [DisplayName("Actualización")] public string Titulo { get; set; } = "";
    [DisplayName("Fabricante")] public string Fabricante { get; set; } = "";
    [DisplayName("Versión")] public string Version { get; set; } = "";
    [DisplayName("Fecha")] public string Fecha { get; set; } = "";
    [DisplayName("Tamaño")] public string Tamano { get; set; } = "";
    /// <summary>Identificador de la actualización en Windows Update.</summary>
    [Browsable(false)] public string UpdateId { get; set; } = "";
    [Browsable(false)] public int Indice { get; set; }
}
public class UpdateRow
{
    [DisplayName("Programa")] public string Nombre { get; set; } = "";
    [DisplayName("Id")] public string Id { get; set; } = "";
    [DisplayName("Instalada")] public string Actual { get; set; } = "";
    [DisplayName("Disponible")] public string Disponible { get; set; } = "";
}
