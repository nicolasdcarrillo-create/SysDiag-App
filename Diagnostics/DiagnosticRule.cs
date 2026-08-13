using SysDiag.Models;

namespace SysDiag.Diagnostics;

/// <summary>
/// Una regla de diagnóstico independiente: evalúa el reporte ya recolectado
/// y agrega hallazgos si corresponde. Es la pieza que separa "recolectar
/// datos" (los servicios de Core/) de "decidir si algo está mal" — hoy esa
/// decisión vive mezclada dentro de cada módulo de Core/ mediante llamadas a
/// r.Add(...) inline, que siguen siendo válidas y probadas.
///
/// Esta interfaz es la puerta de entrada para reglas NUEVAS que se agreguen
/// de aquí en más, o para extraer una regla existente cuando de verdad
/// convenga aislarla (por ejemplo, para poder testearla con datos de
/// ejemplo sin correr WMI). Portar automáticamente cada r.Add(...) actual a
/// una clase de regla aparte sería un cambio grande sin beneficio funcional:
/// el motor y las reglas existentes conviven a propósito.
/// </summary>
public interface IDiagnosticRule
{
    string Id { get; }
    string Categoria { get; }
    void Evaluar(DiagnosticReport reporte);
}
