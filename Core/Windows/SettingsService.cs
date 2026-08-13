using System;
using System.IO;
using System.Text.Json;
using SysDiag.Models;

namespace SysDiag.Core.Windows;

/// <summary>Carga y guarda la configuración en un JSON dentro de Documentos\SysDiag.</summary>
public static class SettingsService
{
    private static string Archivo => Path.Combine(AppEnv.OutputPath, "settings.json");

    public static AppSettings Cargar()
    {
        try
        {
            if (!File.Exists(Archivo)) return AppSettings.PorDefecto();

            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Archivo));
            return s ?? AppSettings.PorDefecto();
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo leer la configuración, se usan los valores por defecto: {ex.Message}", "WARN");
            return AppSettings.PorDefecto();
        }
    }

    public static void Guardar(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(AppEnv.OutputPath);
            File.WriteAllText(Archivo, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo guardar la configuración: {ex.Message}", "WARN");
        }
    }

    /// <summary>
    /// Aplica los valores cargados a los módulos que los usan. Se llama una
    /// vez al arrancar; los módulos ya exponían estos campos como estáticos
    /// ajustables, así que aplicar la configuración es solo asignarlos.
    /// </summary>
    public static void Aplicar(AppSettings s)
    {
        Performance.PerformanceModule.SampleSeconds = s.SampleSeconds;
        Network.NetworkModule.PingCount = s.PingCount;
        Diagnostics.StabilityModule.EventDays = s.EventDays;
        Diagnostics.StabilityModule.WheaDays = s.WheaDays;
        Diagnostics.Exporter.HistorialMaximo = s.HistorialMaximo;
        AppEnv.LogsMaximo = s.LogsMaximo;
    }
}
