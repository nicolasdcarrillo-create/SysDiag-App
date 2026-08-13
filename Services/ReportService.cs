using SysDiag.Core.Diagnostics;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>Genera el informe HTML del diagnóstico actual.</summary>
public class ReportService : IReportService
{
    public string GenerarHtml(DiagnosticReport reporte) => ReportBuilder.Build(reporte);
}
