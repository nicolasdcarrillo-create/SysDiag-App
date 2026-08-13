using System;
using System.Collections.Generic;
using System.Management;

namespace SysDiag.Core;

/// <summary>
/// Envoltorio mínimo sobre WMI, compartido por todos los módulos que
/// consultan hardware: evita repetir el mismo try/catch en cada uno.
/// </summary>
public static class Wmi
{
    public static bool LastAccessDenied { get; private set; }

    public static void ResetAccessState() => LastAccessDenied = false;

    public static void MarcarAccesoDenegado() => LastAccessDenied = true;

    public static bool EsAccesoDenegado(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
            return true;

        var texto = ex?.ToString() ?? "";
        return texto.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("denied", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<ManagementObject> Query(string query, string scope = null)
    {
        var results = new List<ManagementObject>();
        try
        {
            using var searcher = scope == null
                ? new ManagementObjectSearcher(query)
                : new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject o in searcher.Get())
                results.Add(o);
        }
        catch (Exception ex)
        {
            if (EsAccesoDenegado(ex)) MarcarAccesoDenegado();
            AppLog.Write($"Consulta WMI fallida ({query}): {ex.Message}", "WARN");
        }
        return results;
    }

    public static ManagementObject First(string className)
    {
        foreach (var o in Query($"SELECT * FROM {className}")) return o;
        return null;
    }

    public static string Str(ManagementObject o, string prop)
    {
        try { return o?[prop]?.ToString() ?? ""; }
        catch { return ""; }
    }

    public static double Num(ManagementObject o, string prop)
    {
        try
        {
            object v = o?[prop];
            return v == null ? 0 : Convert.ToDouble(v);
        }
        catch { return 0; }
    }
}
