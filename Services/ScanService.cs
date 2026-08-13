using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysDiag.Diagnostics;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>
/// Corre un conjunto de servicios de diagnóstico, fusiona lo que produce cada
/// uno y evalúa el motor de reglas sobre el resultado combinado. La
/// persistencia entre acciones separadas del usuario (que un escaneo suelto
/// no borre lo medido en uno anterior) es responsabilidad del ViewModel, no
/// de este servicio: aquí solo se combinan los pasos de UNA invocación.
/// </summary>
public class ScanService : IScanService
{
    private readonly DiagnosticEngine _motor = new();

    public async Task<DiagnosticReport> EjecutarAsync(
        DiagnosticReport acumulado, IDiagnosticService[] pasos, CancellationToken token)
    {
        var resultado = acumulado ?? new DiagnosticReport();
        bool huboRendimiento = false;

        foreach (var paso in pasos)
        {
            token.ThrowIfCancellationRequested();

            var scratch = new DiagnosticReport();
            await paso.EjecutarAsync(scratch, token);

            foreach (var f in scratch.Hallazgos) f.Modulo = paso.Clave;

            // El inventario del equipo se refresca con cada paso: se queda
            // con el que corresponde al último que corrió.
            if (scratch.Sistema.Count > 0)
            {
                resultado.Equipo = scratch.Equipo;
                resultado.Sistema = scratch.Sistema;
                resultado.Discos = scratch.Discos;
                resultado.Memoria = scratch.Memoria;
            }
            scratch.Sistema = new();
            scratch.Discos = new();
            scratch.Memoria = new();

            resultado.MergeFrom(scratch);
            if (paso.Clave == "rendimiento") huboRendimiento = true;
        }

        // Las reglas solo se evalúan sobre datos que de verdad se acaban de
        // recolectar en esta pasada: si el módulo de rendimiento no corrió,
        // CpuRules/MemoryRules no tienen nada nuevo que juzgar.
        if (huboRendimiento) _motor.Evaluar(resultado);

        return resultado;
    }
}
