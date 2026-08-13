using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SysDiag.Models;

namespace SysDiag.Core.Windows;

public class SavedState
{
    public string Fecha { get; set; } = "";
    public string PlanEnergia { get; set; } = "";
    public string WlanAutoconfig { get; set; } = "";
    public string DnsPrevio { get; set; } = "";
    public string InterfazDns { get; set; } = "";
    public int EfectosVisuales { get; set; } = -1;
    public string WifiPowerIndex { get; set; } = "";
    public string CpuMaxPrevio { get; set; } = "";
    public int GameModePrevio { get; set; } = -1;
}

public static class OptimizeModule
{
    /// <summary>Acciones que el usuario marcó en la interfaz antes de aplicar.</summary>
    public class Options
    {
        public bool FlushDns = true;
        public bool FlushArp = true;
        public bool FixWlanAutoconfig = true;
        public bool WifiMaxPerformance = false;
        public bool WifiPowerSave = false;
        public bool PublicDns = false;
        public bool VisualEffects = false;
        public bool HighPerformancePlan = false;
        public bool ResetTcpStack = false;
        /// <summary>0-100, o null para no tocarlo. Topar el máximo de CPU es la
        /// palanca más directa contra el ruido del ventilador: menos frecuencia
        /// permitida, menos calor generado, menos necesidad de refrigerar.</summary>
        public int? CpuMaxPercent = null;
        public bool GameMode = false;
    }

    // Subgrupo y ajustes de energía del procesador.
    private const string SubProcesador = "54533251-82be-4824-96c1-47b60b740d00";
    private const string SettingCpuMax = "bc5038f7-23e0-4960-96da-33abaf5935ec";

    // Subgrupo y ajuste de energía del adaptador inalámbrico, en GUID: son los
    // mismos en todo Windows y no dependen del idioma del sistema.
    private const string SubWireless = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1";
    private const string SettingPowerSave = "12bbebe6-58d6-4636-95bb-3217ef867c1a";

    public static string ReadWlanAutoconfig()
    {
        string raw = AppEnv.RunConsole("netsh", "wlan show settings");
        if (string.IsNullOrWhiteSpace(raw)) return "desconocido";

        // "deshabilitado" contiene "habilitad", así que se comprueba primero.
        if (Regex.IsMatch(raw, "deshabilitad|disabled", RegexOptions.IgnoreCase)) return "deshabilitado";
        if (Regex.IsMatch(raw, "habilitad|enabled", RegexOptions.IgnoreCase)) return "habilitado";
        return "desconocido";
    }

    public static void SaveState()
    {
        string iface = InterfazPrincipal();

        var estado = new SavedState
        {
            Fecha = DateTime.Now.ToString("s"),
            PlanEnergia = AppEnv.RunConsole("powercfg", "/getactivescheme").Trim(),
            WlanAutoconfig = ReadWlanAutoconfig(),
            InterfazDns = iface,
            DnsPrevio = LeerDns(iface),
            EfectosVisuales = LeerEfectosVisuales(),
            WifiPowerIndex = LeerWifiPowerIndex(),
            CpuMaxPrevio = LeerCpuMaxPercent(),
            GameModePrevio = LeerGameMode()
        };

        try
        {
            File.WriteAllText(AppEnv.BackupFile,
                JsonSerializer.Serialize(estado, new JsonSerializerOptions { WriteIndented = true }));
            AppLog.Write($"Estado previo respaldado en {AppEnv.BackupFile}", "OK");
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo escribir el respaldo: {ex.Message}", "ERROR");
        }
    }

    public static void Run(DiagnosticReport r, Options opt)
    {
        AppLog.Write("Optimización", "STEP");

        if (!AppEnv.IsAdmin)
        {
            AppLog.Write("Se requieren privilegios de administrador para este módulo.", "ERROR");
            return;
        }

        SaveState();

        if (opt.FlushDns)
        {
            AppEnv.RunConsole("ipconfig", "/flushdns");
            AppLog.Write("Caché DNS vaciada.", "OK");
        }

        if (opt.FixWlanAutoconfig)
        {
            string estado = ReadWlanAutoconfig();
            if (estado == "deshabilitado")
            {
                r.Add(Severity.Bad, "Wi-Fi", "La configuración automática de WLAN estaba deshabilitada.",
                    "Con eso apagado el equipo no se reconecta solo a las redes guardadas. Se vuelve a habilitar.",
                    "wlan-autoconfig");

                foreach (string iface in WirelessInterfaces())
                {
                    AppEnv.RunConsole("netsh", $"wlan set autoconfig enabled=yes interface=\"{iface}\"");
                    AppLog.Write($"Configuración automática rehabilitada en «{iface}».", "OK");
                }
            }
            else
            {
                AppLog.Write($"Configuración automática de WLAN: {estado}.", "OK");
            }
        }

        if (opt.HighPerformancePlan)
        {
            string lista = AppEnv.RunConsole("powercfg", "/list");
            var linea = lista.Split('\n')
                .FirstOrDefault(l => Regex.IsMatch(l, "alto rendimiento|high performance", RegexOptions.IgnoreCase));

            if (linea != null)
            {
                var m = Regex.Match(linea, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");
                if (m.Success)
                {
                    AppEnv.RunConsole("powercfg", $"/setactive {m.Value}");
                    AppLog.Write("Plan de energía cambiado a alto rendimiento.", "OK");
                }
            }
            else
            {
                AppLog.Write("El plan de alto rendimiento no está disponible en este equipo.", "WARN");
            }
        }

        if (opt.FlushArp)
        {
            AppEnv.RunConsole("netsh", "interface ip delete arpcache");
            AppLog.Write("Caché ARP vaciada.", "OK");
        }

        if (opt.WifiMaxPerformance)
        {
            // 0 = máximo rendimiento. El ahorro de energía del adaptador es una
            // causa habitual de picos de ping en portátiles.
            AppEnv.RunConsole("powercfg", $"/setacvalueindex SCHEME_CURRENT {SubWireless} {SettingPowerSave} 0");
            AppEnv.RunConsole("powercfg", "/setactive SCHEME_CURRENT");
            AppLog.Write("Adaptador inalámbrico en máximo rendimiento (con corriente).", "OK");
        }

        if (opt.WifiPowerSave)
        {
            // 3 = máximo ahorro. Reduce consumo y calor de la radio a cambio de
            // algo de latencia; sentido en el perfil silencioso/batería.
            AppEnv.RunConsole("powercfg", $"/setacvalueindex SCHEME_CURRENT {SubWireless} {SettingPowerSave} 3");
            AppEnv.RunConsole("powercfg", "/setactive SCHEME_CURRENT");
            AppLog.Write("Adaptador inalámbrico en ahorro de energía.", "OK");
        }

        if (opt.CpuMaxPercent is int max)
        {
            AppEnv.RunConsole("powercfg", $"/setacvalueindex SCHEME_CURRENT {SubProcesador} {SettingCpuMax} {max}");
            AppEnv.RunConsole("powercfg", "/setactive SCHEME_CURRENT");
            AppLog.Write($"CPU topada a un máximo de {max}% de su frecuencia. Menos calor, menos ventilador.", "OK");
        }

        if (opt.GameMode)
        {
            EscribirGameMode(1);
            AppLog.Write("Modo de juego de Windows activado.", "OK");
        }

        if (opt.PublicDns)
        {
            string iface = InterfazPrincipal();
            if (string.IsNullOrEmpty(iface))
            {
                AppLog.Write("No se identificó la interfaz principal; se omite el cambio de DNS.", "WARN");
            }
            else
            {
                AppEnv.RunConsole("netsh", $"interface ip set dnsservers name=\"{iface}\" source=static addr=1.1.1.1 register=primary validate=no");
                AppEnv.RunConsole("netsh", $"interface ip add dnsservers name=\"{iface}\" addr=1.0.0.1 index=2 validate=no");
                AppEnv.RunConsole("ipconfig", "/flushdns");
                AppLog.Write($"DNS de «{iface}» apuntando a 1.1.1.1 / 1.0.0.1.", "OK");
                r.Add(Severity.Ok, "Red", "DNS cambiado a un resolutor público.",
                    "Suele resolver nombres más rápido que el del proveedor. Se revierte a DHCP desde «Restaurar estado».");
            }
        }

        if (opt.VisualEffects)
        {
            EscribirEfectosVisuales(2);   // 2 = ajustar para obtener el mejor rendimiento
            AppLog.Write("Efectos visuales ajustados a rendimiento. Cierra sesión para verlo aplicado.", "OK");
        }

        if (opt.ResetTcpStack)
        {
            AppEnv.RunConsole("netsh", "winsock reset");
            AppEnv.RunConsole("netsh", "int ip reset");
            AppLog.Write("Winsock y pila TCP/IP reiniciados. Hay que REINICIAR el equipo.", "WARN");
            r.Add(Severity.Warn, "Red", "Se reinició la pila de red.",
                "Los cambios no surten efecto hasta que reinicies el equipo. Si tenías IP fija, DNS personalizados o VPN, hay que reconfigurarlos.");
        }
    }

    public static string Restore()
    {
        if (!File.Exists(AppEnv.BackupFile))
            return "No hay ningún respaldo guardado todavía.";

        try
        {
            var estado = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(AppEnv.BackupFile));
            if (estado == null) return "El respaldo está vacío o dañado.";

            AppLog.Write($"Restaurando el estado del {estado.Fecha}", "STEP");

            var m = Regex.Match(estado.PlanEnergia,
                @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");
            if (m.Success)
            {
                AppEnv.RunConsole("powercfg", $"/setactive {m.Value}");
                AppLog.Write("Plan de energía restaurado.", "OK");
            }

            if (!string.IsNullOrEmpty(estado.InterfazDns))
            {
                if (string.IsNullOrWhiteSpace(estado.DnsPrevio) || estado.DnsPrevio == "dhcp")
                {
                    AppEnv.RunConsole("netsh", $"interface ip set dnsservers name=\"{estado.InterfazDns}\" source=dhcp");
                    AppLog.Write("DNS devuelto a automático (DHCP).", "OK");
                }
                else
                {
                    var servidores = estado.DnsPrevio.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    AppEnv.RunConsole("netsh", $"interface ip set dnsservers name=\"{estado.InterfazDns}\" source=static addr={servidores[0]} register=primary validate=no");
                    for (int i = 1; i < servidores.Length; i++)
                        AppEnv.RunConsole("netsh", $"interface ip add dnsservers name=\"{estado.InterfazDns}\" addr={servidores[i]} index={i + 1} validate=no");
                    AppLog.Write("DNS previo restaurado.", "OK");
                }
                AppEnv.RunConsole("ipconfig", "/flushdns");
            }

            if (estado.EfectosVisuales >= 0)
            {
                EscribirEfectosVisuales(estado.EfectosVisuales);
                AppLog.Write("Efectos visuales restaurados.", "OK");
            }

            if (!string.IsNullOrEmpty(estado.WifiPowerIndex))
            {
                AppEnv.RunConsole("powercfg", $"/setacvalueindex SCHEME_CURRENT {SubWireless} {SettingPowerSave} {estado.WifiPowerIndex}");
                AppEnv.RunConsole("powercfg", "/setactive SCHEME_CURRENT");
                AppLog.Write("Energía del adaptador inalámbrico restaurada.", "OK");
            }

            if (!string.IsNullOrEmpty(estado.CpuMaxPrevio))
            {
                AppEnv.RunConsole("powercfg", $"/setacvalueindex SCHEME_CURRENT {SubProcesador} {SettingCpuMax} {estado.CpuMaxPrevio}");
                AppEnv.RunConsole("powercfg", "/setactive SCHEME_CURRENT");
                AppLog.Write($"Tope de CPU restaurado a {estado.CpuMaxPrevio}%.", "OK");
            }

            if (estado.GameModePrevio >= 0)
            {
                EscribirGameMode(estado.GameModePrevio);
                AppLog.Write("Modo de juego restaurado.", "OK");
            }

            if (estado.WlanAutoconfig == "deshabilitado")
            {
                AppLog.Write("El respaldo indica que la autoconfiguración de WLAN estaba deshabilitada.", "WARN");
                AppLog.Write("No se revierte a propósito: dejarla apagada rompe la reconexión automática.", "INFO");
            }

            return $"Estado del {estado.Fecha} restaurado.";
        }
        catch (Exception ex)
        {
            AppLog.Write($"Fallo al restaurar: {ex.Message}", "ERROR");
            return "No se pudo leer el respaldo.";
        }
    }

    /// <summary>Nombre de la interfaz activa con puerta de enlace: la que usa el equipo para salir.</summary>
    private static string InterfazPrincipal()
    {
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                foreach (var g in ni.GetIPProperties().GatewayAddresses)
                {
                    if (g?.Address == null) continue;
                    if (g.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    if (g.Address.ToString() == "0.0.0.0") continue;
                    return ni.Name;
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>DNS actuales de la interfaz, o «dhcp» si los recibe automáticamente.</summary>
    private static string LeerDns(string iface)
    {
        if (string.IsNullOrEmpty(iface)) return "";

        string raw = AppEnv.RunConsole("netsh", $"interface ip show dnsservers name=\"{iface}\"");
        if (string.IsNullOrWhiteSpace(raw)) return "";

        if (Regex.IsMatch(raw, "(?i)dhcp|autom")) return "dhcp";

        var ips = Regex.Matches(raw, @"\b\d{1,3}(?:\.\d{1,3}){3}\b")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        return string.Join(",", ips);
    }

    private static int LeerEfectosVisuales()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            return k?.GetValue("VisualFXSetting") is int v ? v : 0;
        }
        catch { return -1; }
    }

    private static void EscribirEfectosVisuales(int valor)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            k?.SetValue("VisualFXSetting", valor, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo ajustar los efectos visuales: {ex.Message}", "WARN");
        }
    }

    private static string LeerWifiPowerIndex()
    {
        string raw = AppEnv.RunConsole("powercfg", $"/query SCHEME_CURRENT {SubWireless} {SettingPowerSave}");
        var m = Regex.Match(raw, @"(?i)AC Power Setting Index:\s*0x([0-9a-f]+)");
        return m.Success ? Convert.ToInt32(m.Groups[1].Value, 16).ToString() : "";
    }

    private static string LeerCpuMaxPercent()
    {
        string raw = AppEnv.RunConsole("powercfg", $"/query SCHEME_CURRENT {SubProcesador} {SettingCpuMax}");
        var m = Regex.Match(raw, @"(?i)AC Power Setting Index:\s*0x([0-9a-f]+)");
        return m.Success ? Convert.ToInt32(m.Groups[1].Value, 16).ToString() : "";
    }

    /// <summary>1 activo, 0 desactivado, -1 si la clave no existe (Windows lo trata como activo por defecto).</summary>
    private static int LeerGameMode()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            return k?.GetValue("AutoGameModeEnabled") is int v ? v : -1;
        }
        catch { return -1; }
    }

    private static void EscribirGameMode(int valor)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
            k?.SetValue("AutoGameModeEnabled", valor, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudo ajustar el Modo de juego: {ex.Message}", "WARN");
        }
    }

    private static IEnumerable<string> WirelessInterfaces()
    {
        var nombres = new List<string>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                    nombres.Add(ni.Name);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"No se pudieron listar las interfaces: {ex.Message}", "WARN");
        }
        return nombres;
    }
}
