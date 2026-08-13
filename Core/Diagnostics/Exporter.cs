using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SysDiag.Models;

namespace SysDiag.Core.Diagnostics;

/// <summary>
/// Exporta las tablas a CSV y JSON, y guarda cada diagnóstico en el historial.
/// El historial es lo que permite responder «¿esto mejoró o empeoró?», que es
/// la pregunta que ninguna medición aislada puede contestar.
/// </summary>
public static class Exporter
{
    public static string HistorialPath { get; } = Path.Combine(AppEnv.OutputPath, "historial");

    /// <summary>Vuelca una tabla a CSV respetando los encabezados de la vista.</summary>
    public static string ToCsv(string nombre, IList filas)
    {
        if (filas == null || filas.Count == 0) throw new InvalidOperationException("La tabla está vacía.");

        var tipo = filas[0].GetType();
        var props = TypeDescriptor.GetProperties(tipo)
            .Cast<PropertyDescriptor>()
            .Where(p => p.IsBrowsable)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", props.Select(p => Escapar(p.DisplayName))));

        foreach (var fila in filas)
            sb.AppendLine(string.Join(";", props.Select(p => Escapar(p.GetValue(fila)?.ToString() ?? ""))));

        Directory.CreateDirectory(AppEnv.OutputPath);
        string archivo = Path.Combine(AppEnv.OutputPath,
            $"{Sanear(nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.csv");

        // BOM para que Excel en español abra el archivo como UTF-8 sin romper acentos.
        File.WriteAllText(archivo, sb.ToString(), new UTF8Encoding(true));
        AppLog.Write($"Exportado: {archivo}", "OK");
        return archivo;
    }

    public static string ToJson(DiagnosticReport r)
    {
        Directory.CreateDirectory(AppEnv.OutputPath);
        string archivo = Path.Combine(AppEnv.OutputPath, $"diagnostico_{r.Inicio:yyyyMMdd_HHmm}.json");

        File.WriteAllText(archivo, Serializar(r), new UTF8Encoding(false));
        AppLog.Write($"Exportado: {archivo}", "OK");
        return archivo;
    }

    /// <summary>Guarda una copia en el historial para poder comparar más adelante.</summary>
    /// <summary>Cuántos diagnósticos conservar; SettingsService lo ajusta desde la configuración del usuario.</summary>
    public static int HistorialMaximo = 60;

    public static void Archivar(DiagnosticReport r)
    {
        try
        {
            Directory.CreateDirectory(HistorialPath);
            string archivo = Path.Combine(HistorialPath, $"{r.Inicio:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(archivo, Serializar(r), new UTF8Encoding(false));

            // El historial es para ver tendencias, no para acumular sin límite.
            var viejos = new DirectoryInfo(HistorialPath)
                .GetFiles("*.json")
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(HistorialMaximo);

            foreach (var f in viejos)
            {
                try { f.Delete(); } catch { }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo archivar el diagnóstico: {ex.Message}", "WARN");
        }
    }

    /// <summary>Puntaje del diagnóstico anterior, para mostrar la tendencia.</summary>
    public static int PuntajeAnterior(DateTime actual)
    {
        try
        {
            if (!Directory.Exists(HistorialPath)) return -1;

            var previo = new DirectoryInfo(HistorialPath)
                .GetFiles("*.json")
                .Where(f => f.LastWriteTime < actual.AddSeconds(-5))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (previo == null) return -1;

            using var doc = JsonDocument.Parse(File.ReadAllText(previo.FullName));
            return doc.RootElement.TryGetProperty("Puntaje", out var p) ? p.GetInt32() : -1;
        }
        catch
        {
            return -1;
        }
    }

    public class EntradaHistorial
    {
        public DateTime Fecha;
        public int Puntaje;
        public string Archivo = "";
    }

    /// <summary>Lista los diagnósticos guardados, más recientes primero, para la vista de Historial.</summary>
    public static List<EntradaHistorial> Listar(int maximo = 100)
    {
        var lista = new List<EntradaHistorial>();
        try
        {
            if (!Directory.Exists(HistorialPath)) return lista;

            foreach (var f in new DirectoryInfo(HistorialPath)
                         .GetFiles("*.json")
                         .OrderByDescending(x => x.LastWriteTime)
                         .Take(maximo))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(f.FullName));
                    int puntaje = doc.RootElement.TryGetProperty("Puntaje", out var p) ? p.GetInt32() : -1;
                    DateTime fecha = doc.RootElement.TryGetProperty("Inicio", out var i) && i.TryGetDateTime(out var d)
                        ? d : f.LastWriteTime;

                    lista.Add(new EntradaHistorial { Fecha = fecha, Puntaje = puntaje, Archivo = f.FullName });
                }
                catch { /* archivo del historial corrupto: se omite */ }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo listar el historial: {ex.Message}", "WARN");
        }
        return lista;
    }

    /// <summary>Carga un diagnóstico archivado completo, para revisar sus hallazgos y tablas.</summary>
    public static DiagnosticReport Cargar(string archivo)
    {
        var opciones = new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Deserialize<DiagnosticReport>(File.ReadAllText(archivo), opciones);
    }

    /// <summary>Serie de puntajes archivados, para dibujar la tendencia.</summary>
    public static List<(DateTime Fecha, int Puntaje)> Historial(int maximo = 30)
    {
        var serie = new List<(DateTime, int)>();
        try
        {
            if (!Directory.Exists(HistorialPath)) return serie;

            foreach (var f in new DirectoryInfo(HistorialPath)
                         .GetFiles("*.json")
                         .OrderByDescending(x => x.LastWriteTime)
                         .Take(maximo))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(f.FullName));
                    if (!doc.RootElement.TryGetProperty("Puntaje", out var p)) continue;

                    int puntaje = p.GetInt32();
                    if (puntaje < 0) continue;

                    DateTime fecha = doc.RootElement.TryGetProperty("Inicio", out var i)
                        && i.TryGetDateTime(out var d) ? d : f.LastWriteTime;

                    serie.Add((fecha, puntaje));
                }
                catch { /* archivo del historial corrupto: se omite */ }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo leer el historial: {ex.Message}", "WARN");
        }
        return serie;
    }

    private static string Serializar(DiagnosticReport r) =>
        JsonSerializer.Serialize(r, new JsonSerializerOptions
        {
            WriteIndented = true,
            // Los FileInfo de la limpieza no son serializables ni interesan aquí.
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });

    private static string Escapar(string valor)
    {
        valor = (valor ?? "").Replace("\"", "\"\"");
        return valor.Contains(';') || valor.Contains('"') || valor.Contains('\n')
            ? $"\"{valor}\""
            : valor;
    }

    private static string Sanear(string nombre)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
        return nombre.Replace(' ', '_');
    }
}
