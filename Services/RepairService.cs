using SysDiag.Core.Diagnostics;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>
/// Reparaciones de un clic. Envuelve el catálogo existente en
/// Core.Diagnostics.Remediation, que ya aplica la regla de fondo: solo entran
/// acciones seguras, reversibles, y con confirmación explícita en la interfaz
/// antes de llegar aquí.
/// </summary>
public class RepairService : IRepairService
{
    public string Ejecutar(string accionId, DiagnosticReport contexto) =>
        Remediation.Ejecutar(accionId, contexto);
}
