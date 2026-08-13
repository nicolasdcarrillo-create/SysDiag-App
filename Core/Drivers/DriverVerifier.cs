using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SysDiag.Models;

namespace SysDiag.Core.Drivers;

/// <summary>
/// Verificación de un archivo de driver bajado de cualquier origen.
///
/// La idea es invertir el problema: en vez de confiar en el sitio, se comprueba
/// el archivo. Un driver legítimo de Intel, NVIDIA, Realtek o ASUS viene
/// firmado por su fabricante con un certificado válido; uno alterado o
/// empaquetado con basura falla la verificación de firma, porque el hash del
/// contenido deja de coincidir con el que el certificado ampara.
///
/// Lo que esto NO puede detectar: que el driver sea auténtico pero no
/// corresponda al hardware de este equipo. Esa es la razón de fondo por la que
/// la app no descarga desde agregadores automáticos — la firma sería válida y
/// el driver, igualmente equivocado.
/// </summary>
public static class DriverVerifier
{
    public class Resultado
    {
        public string Archivo = "";
        public string Tamano = "";
        public string Sha256 = "";
        public bool Firmado;
        public string Editor = "";
        public string Emisor = "";
        public string ValidoHasta = "";
        public bool CadenaValida;
        public string EstadoFirma = "";
        public string Antivirus = "";
        public Severity Nivel = Severity.Warn;
        public List<string> Notas = new();
        public bool AptoParaInstalar;
    }

    private static readonly string[] ExtensionesValidas =
        { ".inf", ".cab", ".exe", ".msi", ".sys", ".zip" };

    public static Resultado Verificar(string ruta, Action<string> progreso = null)
    {
        var r = new Resultado { Archivo = Path.GetFileName(ruta) };
        AppLog.Write($"Verificando {r.Archivo}", "STEP");

        if (!File.Exists(ruta))
        {
            r.Notas.Add("El archivo no existe.");
            r.Nivel = Severity.Bad;
            return r;
        }

        var info = new FileInfo(ruta);
        r.Tamano = AppEnv.FormatBytes(info.Length);

        string ext = Path.GetExtension(ruta).ToLowerInvariant();
        if (!ExtensionesValidas.Contains(ext))
            r.Notas.Add($"Extensión «{ext}» poco habitual para un driver.");

        // ---- Hash: identifica el archivo de forma única -------------------
        progreso?.Invoke("Calculando hash...");
        try
        {
            using var stream = File.OpenRead(ruta);
            using var sha = SHA256.Create();
            r.Sha256 = Convert.ToHexString(sha.ComputeHash(stream));
            AppLog.Write($"SHA-256: {r.Sha256}");
        }
        catch (Exception ex)
        {
            r.Notas.Add($"No se pudo calcular el hash: {ex.Message}");
        }

        // ---- Firma digital ------------------------------------------------
        progreso?.Invoke("Comprobando la firma digital...");
        VerificarFirma(ruta, r);

        // ---- Antivirus ----------------------------------------------------
        progreso?.Invoke("Analizando con Microsoft Defender...");
        r.Antivirus = EscanearConDefender(ruta);

        // ---- Veredicto ----------------------------------------------------
        Concluir(r);

        AppLog.Write($"Veredicto: {r.EstadoFirma} · {r.Antivirus}",
            r.Nivel == Severity.Ok ? "OK" : r.Nivel == Severity.Warn ? "WARN" : "ERROR");

        return r;
    }

    private static void VerificarFirma(string ruta, Resultado r)
    {
        try
        {
            // Extrae el certificado con el que se firmó el archivo. Si el
            // contenido fue alterado después de firmarlo, esto ya falla.
            var cert = X509Certificate.CreateFromSignedFile(ruta);
            var cert2 = new X509Certificate2(cert);

            r.Firmado = true;
            r.Editor = NombreComun(cert2.Subject);
            r.Emisor = NombreComun(cert2.Issuer);
            r.ValidoHasta = cert2.NotAfter.ToString("yyyy-MM-dd");

            // La cadena confirma que el certificado lo emitió una autoridad en
            // la que Windows confía, y que no está revocado.
            using var cadena = new X509Chain();
            cadena.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            cadena.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            cadena.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(15);
            // Un driver viejo puede estar firmado con un certificado ya
            // caducado y aun así ser legítimo: eso se evalúa aparte.
            cadena.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

            r.CadenaValida = cadena.Build(cert2);

            if (!r.CadenaValida)
            {
                var motivos = cadena.ChainStatus
                    .Where(s => s.Status != X509ChainStatusFlags.NoError)
                    .Select(s => s.StatusInformation.Trim())
                    .Distinct();
                foreach (var m in motivos) r.Notas.Add($"Cadena de certificados: {m}");
            }

            if (cert2.NotAfter < DateTime.Now)
                r.Notas.Add($"El certificado caducó el {r.ValidoHasta}. En drivers antiguos es normal si la firma llevaba marca de tiempo.");

            r.EstadoFirma = r.CadenaValida
                ? $"Firmado por {r.Editor}"
                : $"Firmado por {r.Editor}, pero la cadena no valida";

            AppLog.Write($"Firma: {r.EstadoFirma}");
            AppLog.Write($"Emisor: {r.Emisor}");
        }
        catch (Exception ex)
        {
            r.Firmado = false;
            r.EstadoFirma = "Sin firma digital válida";
            r.Notas.Add("El archivo no tiene firma digital, o fue modificado después de firmarse. " +
                        "Un driver de un fabricante conocido siempre viene firmado: si este no lo está, no lo instales.");
            AppLog.Write($"Sin firma verificable: {ex.Message}", "WARN");
        }
    }

    /// <summary>
    /// Análisis bajo demanda con el antivirus que ya trae Windows. No sustituye
    /// a la firma: detecta código malicioso conocido, no drivers equivocados.
    /// </summary>
    private static string EscanearConDefender(string ruta)
    {
        string mpcmd = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Windows Defender", "MpCmdRun.exe");

        if (!File.Exists(mpcmd))
        {
            AppLog.Write("Microsoft Defender no está disponible para el análisis.", "WARN");
            return "no analizado (Defender no disponible)";
        }

        try
        {
            var psi = new ProcessStartInfo(mpcmd, $"-Scan -ScanType 3 -File \"{ruta}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return "no analizado";

            string salida = p.StandardOutput.ReadToEnd();
            p.WaitForExit(180000);

            // 0 = sin amenazas; 2 = encontró y trató amenazas.
            return p.ExitCode switch
            {
                0 => "sin amenazas",
                2 => "AMENAZA DETECTADA",
                _ => $"resultado no concluyente (código {p.ExitCode})"
            };
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo analizar con Defender: {ex.Message}", "WARN");
            return "no analizado";
        }
    }

    private static void Concluir(Resultado r)
    {
        if (r.Antivirus.Contains("AMENAZA"))
        {
            r.Nivel = Severity.Bad;
            r.AptoParaInstalar = false;
            r.Notas.Insert(0, "Defender detectó una amenaza. No lo instales y borra el archivo.");
            return;
        }

        if (!r.Firmado)
        {
            r.Nivel = Severity.Bad;
            r.AptoParaInstalar = false;
            return;
        }

        if (!r.CadenaValida)
        {
            r.Nivel = Severity.Warn;
            r.AptoParaInstalar = false;
            r.Notas.Insert(0, "La firma existe pero Windows no puede validar quién la emitió. " +
                              "Descárgalo de nuevo desde el sitio oficial del fabricante antes de instalarlo.");
            return;
        }

        r.Nivel = Severity.Ok;
        r.AptoParaInstalar = true;
        r.Notas.Insert(0, "Firma válida y sin amenazas. Comprueba además que el driver corresponda " +
                          "a tu modelo exacto de hardware: la firma garantiza el origen, no la compatibilidad.");
    }

    /// <summary>Instala un .inf con la herramienta que trae Windows.</summary>
    public static string Instalar(string ruta)
    {
        if (!AppEnv.IsAdmin) return "Instalar un driver requiere ejecutar SysDiag como administrador.";

        string ext = Path.GetExtension(ruta).ToLowerInvariant();

        if (ext != ".inf")
        {
            // Los .exe y .msi traen su propio instalador: se abre y el usuario
            // decide, en vez de ejecutarlo en silencio a sus espaldas.
            try
            {
                Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
                return "Se abrió el instalador del fabricante. Sigue sus pasos.";
            }
            catch (Exception ex)
            {
                return $"No se pudo abrir el instalador: {ex.Message}";
            }
        }

        string salida = AppEnv.RunConsole("pnputil", $"/add-driver \"{ruta}\" /install", 120000);
        AppLog.Write(salida);

        return salida.Contains("correctamente", StringComparison.OrdinalIgnoreCase)
            || salida.Contains("successfully", StringComparison.OrdinalIgnoreCase)
            ? "Driver instalado. Puede hacer falta reiniciar."
            : "pnputil terminó sin confirmar la instalación. Revisa el registro para el detalle.";
    }

    private static string NombreComun(string dn)
    {
        foreach (var parte in dn.Split(','))
        {
            var t = parte.Trim();
            if (t.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return t.Substring(3).Trim('"');
        }
        return dn;
    }

    /// <summary>Página oficial de descarga según quién publica el driver.</summary>
    /// <summary>
    /// Página de soporte según quién publica el driver, o según el fabricante
    /// del equipo cuando se le pasa el texto completo de "Equipo" (que WMI
    /// entrega como "FABRICANTE MODELO", p. ej. "ASUSTeK COMPUTER INC. ASUS
    /// TUF Gaming F15..."). Son enlaces genéricos al portal de soporte, no a
    /// un modelo específico: no hay forma confiable de adivinar la URL exacta
    /// de cada modelo para cada fabricante sin arriesgarse a mandar a alguien
    /// a la página de un equipo que no es el suyo.
    /// </summary>
    public static string SitioOficial(string proveedor)
    {
        string p = (proveedor ?? "").ToLowerInvariant();

        if (p.Contains("intel")) return "https://www.intel.com/content/www/us/en/download-center/home.html";
        if (p.Contains("nvidia")) return "https://www.nvidia.com/Download/index.aspx";
        if (p.Contains("advanced micro") || p.Contains("amd")) return "https://www.amd.com/en/support";
        if (p.Contains("realtek")) return "https://www.realtek.com/downloads";
        if (p.Contains("mediatek")) return "https://www.mediatek.com/";
        if (p.Contains("microsoft") || p.Contains("surface")) return "https://support.microsoft.com/surface";

        // Fabricantes de equipo completo. "asustek" antes que "asus" a secas
        // porque WMI suele devolver la razón social completa.
        if (p.Contains("asustek") || p.Contains("asus")) return "https://www.asus.com/support/";
        if (p.Contains("dell")) return "https://www.dell.com/support/home/";
        if (p.Contains("hp") || p.Contains("hewlett")) return "https://support.hp.com/";
        if (p.Contains("lenovo")) return "https://pcsupport.lenovo.com/";
        if (p.Contains("acer")) return "https://www.acer.com/support";
        if (p.Contains("msi") || p.Contains("micro-star")) return "https://www.msi.com/support";
        if (p.Contains("samsung")) return "https://www.samsung.com/support/";
        if (p.Contains("gigabyte")) return "https://www.gigabyte.com/Support";
        if (p.Contains("toshiba") || p.Contains("dynabook")) return "https://dynabook.com/support/";
        if (p.Contains("system76")) return "https://support.system76.com/";
        if (p.Contains("lg electronics") || p.Contains("lg ")) return "https://www.lg.com/support";
        if (p.Contains("huawei")) return "https://consumer.huawei.com/en/support/";
        if (p.Contains("razer")) return "https://mysupport.razer.com/";
        if (p.Contains("framework")) return "https://frame.work/support";

        // Catálogo oficial de Microsoft: se puede buscar por ID de hardware y
        // todo lo publicado ahí está firmado y validado. Es el respaldo
        // razonable cuando no se reconoce el fabricante.
        return "https://catalog.update.microsoft.com/";
    }
}
