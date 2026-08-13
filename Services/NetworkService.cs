using System.Threading;
using System.Threading.Tasks;
using SysDiag.Core.Hardware;
using SysDiag.Core.Network;
using SysDiag.Models;

namespace SysDiag.Services;

/// <summary>
/// Latencia, Wi-Fi, canales cercanos y traceroute. Ya es asíncrono de
/// extremo a extremo (ICMP sin bloquear), así que no necesita Task.Run.
/// </summary>
public class NetworkService : INetworkService
{
    public string Clave => "red";

    public async Task EjecutarAsync(DiagnosticReport reporte, CancellationToken token)
    {
        await Task.Run(() => SystemModule.Run(reporte), token);
        await NetworkModule.RunAsync(reporte, token);
    }
}
