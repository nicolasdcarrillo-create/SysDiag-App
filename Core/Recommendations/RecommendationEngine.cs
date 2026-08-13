using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SysDiag.Models;

namespace SysDiag.Core.Recommendations;

public static class RecommendationEngine
{
    public static List<Recommendation> Generate(DiagnosticReport report)
    {
        var recomendaciones = new List<Recommendation>();
        if (report == null) return recomendaciones;

        if (report.Hallazgos.Any(h => h.Area == "Red" && h.Severity != Severity.Ok))
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Alta",
                Titulo = "Revisar la red",
                Descripcion = "Mide si el problema está en Wi‑Fi, el enrutador o el proveedor: un ping alto al gateway o una señal débil suele ser la causa real de la inestabilidad."
            });
        }

        if (report.Almacenamiento.Any(d => d.Estado == Severity.Bad || d.Estado == Severity.Warn))
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Alta",
                Titulo = "Comprobar almacenamiento",
                Descripcion = "Si el disco muestra desgaste, temperatura alta o errores no corregidos, respalda y revisa la unidad antes de que falle por completo."
            });
        }

        if (report.Arranque.Count >= 20)
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Media",
                Titulo = "Reducir inicio de Windows",
                Descripcion = "Hay varios programas cargándose con el sistema; desactiva los que no necesites para aligerar el arranque y liberar memoria."
            });
        }

        if (report.Programas.Count > 60)
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Media",
                Titulo = "Revisar software instalado",
                Descripcion = "Un entorno con demasiadas aplicaciones instaladas suele generar conflictos, consumo innecesario y más actualizaciones a revisar."
            });
        }

        var ram = report.RendimientoResumen.FirstOrDefault(x => x.Clave.Contains("RAM", StringComparison.OrdinalIgnoreCase));
        if (ram != null && TryParsePercent(ram.Valor, out var usoRam) && usoRam >= 80)
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Alta",
                Titulo = "Bajar presión de RAM",
                Descripcion = "La memoria está muy ocupada. Cierra procesos pesados o revisa si hay software de background que esté consumiendo la RAM en segundo plano."
            });
        }

        var cpu = report.RendimientoResumen.FirstOrDefault(x => x.Clave.Contains("CPU", StringComparison.OrdinalIgnoreCase));
        if (cpu != null && TryParsePercent(cpu.Valor, out var usoCpu) && usoCpu >= 75)
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Media",
                Titulo = "Inspeccionar uso de CPU",
                Descripcion = "Hay carga sostenida del procesador. Revisa procesos de fondo, antivirus o sincronizaciones que estén empeorando el rendimiento."
            });
        }

        if (!recomendaciones.Any())
        {
            recomendaciones.Add(new Recommendation
            {
                Prioridad = "Baja",
                Titulo = "Seguimiento normal",
                Descripcion = "No hubo condiciones anómalas suficientes para recomendar una corrección urgente; conviene mantener la observación y repetir el diagnóstico si cambian los síntomas."
            });
        }

        return recomendaciones.OrderByDescending(r => PrioridadWeight(r.Prioridad)).ThenBy(r => r.Titulo).ToList();
    }

    private static int PrioridadWeight(string prioridad) => prioridad switch
    {
        "Alta" => 3,
        "Media" => 2,
        _ => 1
    };

    private static bool TryParsePercent(string valor, out double percentage)
    {
        percentage = 0d;
        if (string.IsNullOrWhiteSpace(valor)) return false;

        var texto = valor.Trim();
        var idx = texto.IndexOf('%');
        if (idx >= 0) texto = texto.Substring(0, idx).Trim();

        var match = System.Text.RegularExpressions.Regex.Match(texto, @"([0-9]+(?:[.,][0-9]+)?)");
        if (!match.Success) return false;

        return double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out percentage);
    }
}
