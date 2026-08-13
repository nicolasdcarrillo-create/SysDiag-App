using System;
using System.Windows;
using SysDiag.Core;
using SysDiag.Core.Windows;
using SysDiag.Models;

namespace SysDiag.Ui;

public partial class ProfilesWindow : Window
{
    public ProfilesWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

    private void Universidad_Click(object sender, RoutedEventArgs e) => Aplicar("Universidad — Silencioso",
        new OptimizeModule.Options
        {
            FlushDns = true,
            FlushArp = true,
            FixWlanAutoconfig = true,
            WifiPowerSave = true,
            WifiMaxPerformance = false,
            HighPerformancePlan = false,
            VisualEffects = true,
            GameMode = false,
            CpuMaxPercent = 60
        });

    private void Trabajo_Click(object sender, RoutedEventArgs e) => Aplicar("Trabajo — Equilibrado",
        new OptimizeModule.Options
        {
            FlushDns = true,
            FlushArp = true,
            FixWlanAutoconfig = true,
            WifiPowerSave = false,
            WifiMaxPerformance = false,
            HighPerformancePlan = false,
            VisualEffects = false,
            PublicDns = true,
            GameMode = false,
            CpuMaxPercent = 85
        });

    private void Juego_Click(object sender, RoutedEventArgs e) => Aplicar("Juego — Rendimiento",
        new OptimizeModule.Options
        {
            FlushDns = true,
            FlushArp = true,
            FixWlanAutoconfig = true,
            WifiMaxPerformance = true,
            WifiPowerSave = false,
            HighPerformancePlan = true,
            VisualEffects = false,
            GameMode = true,
            CpuMaxPercent = 100
        });

    private void Aplicar(string nombre, OptimizeModule.Options opciones)
    {
        if (!AppEnv.IsAdmin)
        {
            bool elevar = Dialog.Confirm("Se necesitan permisos de administrador",
                $"Aplicar el perfil «{nombre}» cambia configuración del sistema.",
                "Reiniciar como administrador");

            if (elevar && AppEnv.RelaunchElevated()) Application.Current.Shutdown();
            return;
        }

        bool ok = Dialog.Confirm($"Aplicar perfil «{nombre}»",
            "Se guarda el estado actual antes de aplicar. Se revierte desde «Restaurar estado» en cualquier momento.",
            "Aplicar");
        if (!ok) return;

        try
        {
            OptimizeModule.Run(new DiagnosticReport(), opciones);
            Dialog.Info("Perfil aplicado", $"«{nombre}» está activo.");
            Close();
        }
        catch (Exception ex)
        {
            Dialog.Error("No se pudo aplicar el perfil", ex.Message);
        }
    }
}
