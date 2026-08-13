using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using SysDiag.Models;

namespace SysDiag.Core.Windows;

/// <summary>
/// Actualizaciones de programas mediante winget, el gestor de paquetes oficial
/// de Microsoft. Se usa winget y no descargas directas a propósito: los
/// paquetes vienen de repositorios validados por Microsoft, con el instalador
/// del propio fabricante y su hash verificado. Es la diferencia entre
/// actualizar por un canal auditado y bajar binarios de un espejo cualquiera.
/// </summary>
public static class UpdateModule
{
    public static bool Disponible()
    {
        try
        {
            var psi = new ProcessStartInfo("winget", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(6000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Run(DiagnosticReport r)
    {
        AppLog.Write("Actualizaciones de programas (winget)", "STEP");

        if (!Disponible())
        {
            AppLog.Write("winget no está disponible en este equipo.", "WARN");
            r.Add(Severity.Warn, "Actualizaciones", "winget no está instalado.",
                "Se instala desde la Microsoft Store como «Instalador de aplicaciones». Sin él no se puede auditar qué programas tienen versión nueva.");
            return;
        }

        string salida = AppEnv.RunConsole("winget",
            "upgrade --include-unknown --accept-source-agreements", 90000);

        var filas = Parse(salida);
        r.Actualizaciones = filas;

        AppLog.Write($"Programas con actualización disponible: {filas.Count}");
        foreach (var f in filas.Take(20))
            AppLog.Write($"  {f.Nombre,-42} {f.Actual,-16} -> {f.Disponible}");

        if (filas.Count >= 10)
            r.Add(Severity.Warn, "Actualizaciones", $"{filas.Count} programas tienen versión más reciente.",
                "Actualizar cierra fallos conocidos y agujeros de seguridad. Puedes hacerlo desde el botón «Actualizar con winget» en la vista Datos.");
        else if (filas.Count > 0)
            r.Add(Severity.Ok, "Actualizaciones", $"{filas.Count} programa(s) con versión más reciente disponible.");
        else
            r.Add(Severity.Ok, "Actualizaciones", "Todos los programas gestionables están al día.");
    }

    /// <summary>
    /// winget imprime una tabla de ancho fijo cuyos encabezados cambian con el
    /// idioma, así que las columnas se ubican por la posición de la línea de
    /// guiones en vez de por el nombre del encabezado.
    /// </summary>
    private static List<UpdateRow> Parse(string salida)
    {
        var filas = new List<UpdateRow>();
        if (string.IsNullOrWhiteSpace(salida)) return filas;

        var lineas = salida.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        int sep = lineas.FindIndex(l => l.StartsWith("---") || Regex.IsMatch(l, @"^-{5,}"));
        if (sep <= 0) return filas;

        string encabezado = lineas[sep - 1];

        // Cada columna empieza donde empieza su palabra en el encabezado.
        var inicios = Regex.Matches(encabezado, @"\S+")
            .Select(m => m.Index)
            .ToList();

        if (inicios.Count < 4) return filas;

        foreach (string linea in lineas.Skip(sep + 1))
        {
            if (string.IsNullOrWhiteSpace(linea)) continue;
            if (linea.StartsWith(" ")) continue;
            if (Regex.IsMatch(linea, @"^\d+\s")) continue;   // línea de resumen final

            string Campo(int i)
            {
                int ini = inicios[i];
                if (ini >= linea.Length) return "";
                int fin = i + 1 < inicios.Count ? Math.Min(inicios[i + 1], linea.Length) : linea.Length;
                return linea.Substring(ini, fin - ini).Trim();
            }

            string nombre = Campo(0);
            string id = Campo(1);
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(id)) continue;
            if (nombre.Contains("upgrade") || nombre.Contains("actualiz")) continue;

            filas.Add(new UpdateRow
            {
                Nombre = nombre,
                Id = id,
                Actual = Campo(2),
                Disponible = Campo(3)
            });
        }

        return filas;
    }

    /// <summary>
    /// Lanza winget en una consola visible. A propósito NO se ejecuta oculto:
    /// el usuario ve qué se está instalando y puede cortarlo. La app no
    /// descarga ni ejecuta nada por su cuenta.
    /// </summary>
    public static void LanzarActualizacion(string id = null)
    {
        string args = string.IsNullOrEmpty(id)
            ? "/k winget upgrade --all --include-unknown"
            : $"/k winget upgrade --id \"{id}\"";

        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", args) { UseShellExecute = true });
            AppLog.Write($"winget lanzado en consola: {args}", "OK");
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo lanzar winget: {ex.Message}", "ERROR");
        }
    }
}
