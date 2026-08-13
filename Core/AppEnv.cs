using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace SysDiag.Core;

public static class AppEnv
{
    public static readonly string Version = "5.7.0";

    /// <summary>Carpeta de salida: Documentos\SysDiag.</summary>
    public static string OutputPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SysDiag");

    public static string LogPath { get; } = Path.Combine(OutputPath, "logs");

    public static string BackupFile { get; } = Path.Combine(OutputPath, "estado-previo.json");

    static AppEnv()
    {
        Directory.CreateDirectory(OutputPath);
        Directory.CreateDirectory(LogPath);
        RotarLogs();
    }

    /// <summary>
    /// Cada ejecución deja un archivo nuevo; sin esto la carpeta crece sin
    /// límite. Se conservan los más recientes, que son los únicos que sirven
    /// para reconstruir qué pasó.
    /// </summary>
    /// <summary>
    /// Cuántos registros conservar. Es la única configuración que no aplica
    /// en caliente: la rotación ya corrió para cuando la configuración
    /// termina de cargarse (pasa en el constructor estático, que se dispara
    /// con el primer acceso a esta clase). Un cambio se refleja en la
    /// próxima vez que se abra la app, no en la sesión actual.
    /// </summary>
    public static int LogsMaximo = 30;

    private static void RotarLogs()
    {
        try
        {
            var viejos = new DirectoryInfo(LogPath)
                .GetFiles("sysdiag_*.log")
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(LogsMaximo);

            foreach (var f in viejos)
            {
                try { f.Delete(); } catch { }
            }
        }
        catch
        {
            // La rotación es mantenimiento, nunca motivo para no arrancar.
        }
    }

    public static bool IsAdmin
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Relanza la propia aplicación pidiendo elevación al usuario.</summary>
    public static bool RelaunchElevated()
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(info);
            return true;
        }
        catch
        {
            // El usuario canceló el diálogo de UAC.
            return false;
        }
    }

    public static string FormatBytes(double bytes)
    {
        if (bytes >= 1024d * 1024 * 1024) return $"{bytes / (1024d * 1024 * 1024):N2} GB";
        if (bytes >= 1024d * 1024) return $"{bytes / (1024d * 1024):N1} MB";
        if (bytes >= 1024d) return $"{bytes / 1024d:N0} KB";
        return $"{(long)bytes} B";
    }

    /// <summary>Ejecuta una utilidad de consola y devuelve su salida completa.</summary>
    public static string RunConsole(string file, string args, int timeoutMs = 15000)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeoutMs);
            return output;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Fallo al ejecutar {file}: {ex.Message}", "ERROR");
            return "";
        }
    }
}

public static class AppLog
{
    private static readonly object Gate = new();
    private static string _file = Path.Combine(
        AppEnv.LogPath, $"sysdiag_{DateTime.Now:yyyyMMdd_HHmmss}.log");

    public static string File => _file;

    /// <summary>Se dispara con cada línea para que la interfaz la muestre en vivo.</summary>
    public static event Action<string, string> Line;

    public static void Write(string message, string level = "INFO")
    {
        string stamp = DateTime.Now.ToString("HH:mm:ss");
        lock (Gate)
        {
            try
            {
                System.IO.File.AppendAllText(_file,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // Un fallo de escritura en el log nunca debe tumbar un diagnóstico.
            }
        }
        Line?.Invoke($"{stamp}  {message}", level);
    }
}
