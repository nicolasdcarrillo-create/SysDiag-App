using System;
using System.Threading;
using System.Threading.Tasks;
using SysDiag.Models;
using SysDiag.Services;
using SysDiag.Core.Diagnostics;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Headless: iniciando diagnóstico completo...");
        var token = CancellationToken.None;
        var merged = new DiagnosticReport();

        try
        {
            // Red
            var scratch = new DiagnosticReport();
            Console.WriteLine("Ejecutando: Red...");
            await new NetworkService().EjecutarAsync(scratch, token);
            merged.MergeFrom(scratch);
            Console.WriteLine("Red completada.");

            // Rendimiento
            scratch = new DiagnosticReport();
            Console.WriteLine("Ejecutando: Rendimiento...");
            await new PerformanceService().EjecutarAsync(scratch, token);
            merged.MergeFrom(scratch);
            Console.WriteLine("Rendimiento completado.");

            // Térmicas y energía
            scratch = new DiagnosticReport();
            Console.WriteLine("Ejecutando: Térmicas y energía...");
            await new HardwareService().EjecutarAsync(scratch, token);
            merged.MergeFrom(scratch);
            Console.WriteLine("Térmicas completadas.");

            // Almacenamiento
            scratch = new DiagnosticReport();
            Console.WriteLine("Ejecutando: Almacenamiento...");
            await new StorageService().EjecutarAsync(scratch, token);
            merged.MergeFrom(scratch);
            Console.WriteLine("Almacenamiento completado.");

            // Seguridad
            scratch = new DiagnosticReport();
            Console.WriteLine("Ejecutando: Seguridad...");
            await new SecurityService().EjecutarAsync(scratch, token);
            merged.MergeFrom(scratch);
            Console.WriteLine("Seguridad completada.");

            // Estabilidad
            scratch = new DiagnosticReport();
            Console.WriteLine("Ejecutando: Estabilidad...");
            await new StabilityService().EjecutarAsync(scratch, token);
            merged.MergeFrom(scratch);
            Console.WriteLine("Estabilidad completada.");

            // Final: exportar JSON
            string archivo = Exporter.ToJson(merged);
            Console.WriteLine($"Diagnóstico guardado: {archivo}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error durante el diagnóstico: {ex.Message}");
            return 2;
        }
    }
}
