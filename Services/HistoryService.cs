using System;
using System.Collections.Generic;
using SysDiag.Core.Diagnostics;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>Historial de diagnósticos: archivo y tendencia de puntaje.</summary>
public class HistoryService : IHistoryService
{
    public void Archivar(DiagnosticReport reporte) => Exporter.Archivar(reporte);

    public List<(DateTime Fecha, int Puntaje)> Serie(int maximo = 30) => Exporter.Historial(maximo);

    public int PuntajeAnterior(DateTime actual) => Exporter.PuntajeAnterior(actual);
}
