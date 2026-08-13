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
