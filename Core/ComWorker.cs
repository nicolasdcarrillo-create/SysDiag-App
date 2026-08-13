using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SysDiag.Core;

/// <summary>
/// Hilo único y persistente donde viven todos los objetos COM del Agente de
/// Windows Update.
///
/// Es necesario porque los resultados de una búsqueda son punteros COM que solo
/// valen dentro del apartamento donde se crearon: si la búsqueda corre en un
/// hilo del grupo y la instalación en otro, el acceso cruza apartamentos y
/// falla. Manteniendo un solo hilo STA propietario, buscar e instalar comparten
/// contexto y los punteros siguen siendo válidos entre una llamada y otra.
/// </summary>
public sealed class ComWorker : IDisposable
{
    private readonly BlockingCollection<Action> _cola = new();
    private readonly Thread _hilo;

    public ComWorker(string nombre = "SysDiag.COM")
    {
        _hilo = new Thread(Bucle)
        {
            IsBackground = true,
            Name = nombre
        };
        _hilo.SetApartmentState(ApartmentState.STA);
        _hilo.Start();
    }

    private void Bucle()
    {
        foreach (var trabajo in _cola.GetConsumingEnumerable())
        {
            try { trabajo(); }
            catch (Exception ex) { AppLog.Write($"Fallo en el hilo COM: {ex.Message}", "ERROR"); }
        }
    }

    /// <summary>Ejecuta el trabajo en el hilo COM y espera el resultado.</summary>
    public T Run<T>(Func<T> trabajo)
    {
        T resultado = default;
        Exception error = null;

        using var listo = new ManualResetEventSlim(false);

        _cola.Add(() =>
        {
            try { resultado = trabajo(); }
            catch (Exception ex) { error = ex; }
            finally { listo.Set(); }
        });

        listo.Wait();
        if (error != null) throw error;
        return resultado;
    }

    public void Dispose()
    {
        _cola.CompleteAdding();
        _cola.Dispose();
    }
}
