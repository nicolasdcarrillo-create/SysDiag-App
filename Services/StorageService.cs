using System.Threading;
using System.Threading.Tasks;
using SysDiag.Core.Hardware;
using SysDiag.Core.Storage;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>Salud SMART del disco: desgaste, temperatura y errores no corregidos.</summary>
public class StorageService : IStorageService
{
    public string Clave => "almacenamiento";

    public Task EjecutarAsync(DiagnosticReport reporte, CancellationToken token) => Task.Run(() =>
    {
        SystemModule.Run(reporte);
        StorageModule.Run(reporte);
    }, token);
}
