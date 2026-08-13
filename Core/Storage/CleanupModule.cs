using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SysDiag.Models;

namespace SysDiag.Core.Storage;

public static class CleanupModule
{
    /// <summary>
    /// Qué se limpia. Los destinos con riesgo o con costo de recuperación van
    /// desmarcados por defecto: borrar la caché de Windows Update obliga a
    /// volver a descargar, y Prefetch hace más lentos los primeros arranques
    /// hasta que Windows lo reconstruye.
    /// </summary>
    public class Options
    {
        public bool TempUsuario = true;
        public bool TempWindows = true;
        public bool CacheInternet = true;
        public bool VolcadosApp = true;
        public bool LogsJuegos = true;
        public bool Miniaturas = true;
        public bool ShaderCache = true;
        public bool ErroresWindows = true;
        public bool CacheWindowsUpdate = false;
        public bool DeliveryOptimization = false;
        public bool Prefetch = false;
        public bool Papelera = false;
    }

    public static Options Opts = new();
    private static readonly EnumerationOptions Opciones = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    private static List<(string Nombre, string Ruta)> Objetivos()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var lista = new List<(string, string)>();
        void Add(bool activo, string nombre, string ruta)
        {
            if (activo) lista.Add((nombre, ruta));
        }

        Add(Opts.TempUsuario, "Temporales del usuario", Path.GetTempPath());
        Add(Opts.TempWindows, "Temporales de Windows", Path.Combine(windows, "Temp"));
        Add(Opts.CacheInternet, "Caché de Internet", Path.Combine(local, "Microsoft", "Windows", "INetCache"));
        Add(Opts.VolcadosApp, "Volcados de aplicaciones", Path.Combine(local, "CrashDumps"));
        Add(Opts.LogsJuegos, "Registros de League of Legends", @"C:\Riot Games\League of Legends\Logs");
        Add(Opts.Miniaturas, "Caché de miniaturas", Path.Combine(local, "Microsoft", "Windows", "Explorer"));
        Add(Opts.ShaderCache, "Caché de sombreadores DirectX", Path.Combine(local, "D3DSCache"));
        Add(Opts.ErroresWindows, "Informes de errores de Windows", Path.Combine(local, "Microsoft", "Windows", "WER"));
        Add(Opts.CacheWindowsUpdate, "Caché de Windows Update", Path.Combine(windows, "SoftwareDistribution", "Download"));
        Add(Opts.DeliveryOptimization, "Archivos de Delivery Optimization", Path.Combine(windows, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"));
        Add(Opts.Prefetch, "Prefetch", Path.Combine(windows, "Prefetch"));

        return lista;
    }

    /// <summary>
    /// Recorre los objetivos una sola vez y se queda con la lista de archivos.
    /// Enumerar con DirectoryInfo trae el tamaño ya poblado: pedirlo con un
    /// FileInfo nuevo por archivo era una consulta extra al sistema de archivos
    /// por cada uno, y en %TEMP% eso son decenas de miles.
    /// </summary>
    public static void Analyze(DiagnosticReport r, CancellationToken token)
    {
        AppLog.Write("Análisis de temporales", "STEP");

        var filas = new List<CleanupRow>();
        long total = 0;

        foreach (var o in Objetivos())
        {
            token.ThrowIfCancellationRequested();
            if (!Directory.Exists(o.Ruta)) continue;

            var archivos = Enumerar(o.Ruta);
            long bytes = 0;
            foreach (var f in archivos)
            {
                try { bytes += f.Length; } catch { /* desapareció entremedio */ }
            }

            total += bytes;
            filas.Add(new CleanupRow
            {
                Ubicacion = o.Nombre,
                Ruta = o.Ruta,
                Archivos = archivos.Count,
                Ocupa = AppEnv.FormatBytes(bytes),
                Bytes = bytes,
                Items = archivos
            });

            AppLog.Write($"{o.Nombre,-34} {archivos.Count,7} archivos   {AppEnv.FormatBytes(bytes)}");
        }

        r.Limpieza = filas;
        r.EspacioLiberado = "";
        AppLog.Write($"Total recuperable: {AppEnv.FormatBytes(total)}", "OK");

        if (total > 2L * 1024 * 1024 * 1024)
            r.Add(Severity.Warn, "Limpieza",
                $"Hay {AppEnv.FormatBytes(total)} en archivos temporales.",
                "Se pueden borrar sin riesgo: Windows y las aplicaciones los regeneran cuando los necesitan.",
                "limpiar-temp");
    }

    /// <summary>
    /// Borra usando la lista que dejó <see cref="Analyze"/>. Antes se volvía a
    /// recorrer el disco entero dos veces más para hacer exactamente lo mismo.
    /// </summary>
    public static void Clean(DiagnosticReport r, List<CleanupRow> filas, CancellationToken token)
    {
        if (filas == null || filas.Count == 0)
        {
            Analyze(r, token);
            filas = r.Limpieza;
        }
        else
        {
            r.Limpieza = filas;
        }

        AppLog.Write("Limpieza", "STEP");

        long liberado = 0;
        long previsto = filas.Sum(x => x.Bytes);

        foreach (var fila in filas)
        {
            token.ThrowIfCancellationRequested();

            foreach (var f in fila.Items)
            {
                try
                {
                    long size = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    liberado += size;
                }
                catch { /* en uso por un proceso activo */ }
            }

            // Carpetas vacías, de la más profunda a la más superficial.
            // La raíz del objetivo nunca se elimina.
            try
            {
                foreach (var d in Directory.GetDirectories(fila.Ruta, "*", SearchOption.AllDirectories)
                             .OrderByDescending(x => x.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(d).Any())
                            Directory.Delete(d);
                    }
                    catch { }
                }
            }
            catch { }

            AppLog.Write($"{fila.Ubicacion,-34} limpiado", "OK");
        }

        if (Opts.Papelera) liberado += VaciarPapelera();

        r.EspacioLiberado = AppEnv.FormatBytes(liberado);
        AppLog.Write($"Espacio liberado: {r.EspacioLiberado}", "OK");

        long bloqueado = previsto - liberado;
        if (bloqueado > 1024 * 1024)
            AppLog.Write($"{AppEnv.FormatBytes(bloqueado)} en uso por procesos activos, no se pudo borrar.");
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

    /// <summary>
    /// Vaciar la papelera es irreversible, por eso llega aquí solo si el
    /// usuario la marcó explícitamente en el diálogo y confirmó después.
    /// </summary>
    private static long VaciarPapelera()
    {
        try
        {
            // 0x1 sin confirmación del shell (ya se confirmó en la app),
            // 0x2 sin barra de progreso, 0x4 sin sonido al terminar.
            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
            AppLog.Write(hr == 0 ? "Papelera de reciclaje vaciada." : "La papelera ya estaba vacía.", "OK");
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo vaciar la papelera: {ex.Message}", "WARN");
        }
        return 0;
    }

    private static List<FileInfo> Enumerar(string ruta)
    {
        try
        {
            return new DirectoryInfo(ruta).EnumerateFiles("*", Opciones).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo recorrer {ruta}: {ex.Message}", "WARN");
            return new List<FileInfo>();
        }
    }
}
