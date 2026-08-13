using System.Threading;
using System.Threading.Tasks;
using SysDiag.Core.Windows;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>Programas de inicio, servicios automáticos y software instalado.</summary>
public class StartupService : IDiagnosticService
{
    public string Clave => "arranque";

    public Task EjecutarAsync(DiagnosticReport reporte, CancellationToken token) => Task.Run(() =>
    {
        StartupModule.Run(reporte);
    }, token);
}
