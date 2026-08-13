using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace SysDiag.Models;

public class StorageRow
{
    [DisplayName("Unidad")] public string Nombre { get; set; } = "";
    [DisplayName("Tipo")] public string Tipo { get; set; } = "";
    [DisplayName("Salud")] public string Salud { get; set; } = "";
    [DisplayName("Firmware")] public string Firmware { get; set; } = "";
    [DisplayName("Horas encendido")] public string Horas { get; set; } = "";
    [DisplayName("Desgaste")] public string Desgaste { get; set; } = "";
    [DisplayName("Temp.")] public string Temperatura { get; set; } = "";
    [DisplayName("Errores no corregidos")] public string Errores { get; set; } = "";
    [Browsable(false)] public Severity Estado { get; set; }
}
public class CleanupRow
{
    [DisplayName("Ubicación")] public string Ubicacion { get; set; } = "";
    [DisplayName("Ruta")] public string Ruta { get; set; } = "";
    [DisplayName("Archivos")] public int Archivos { get; set; }
    [DisplayName("Ocupa")] public string Ocupa { get; set; } = "";
    [Browsable(false)] public long Bytes { get; set; }
    /// <summary>Archivos ya enumerados, para no recorrer el disco otra vez al borrar.</summary>
    [Browsable(false)] [JsonIgnore] public List<FileInfo> Items { get; set; } = new();
}
