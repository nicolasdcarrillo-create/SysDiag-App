using System;
using System.Windows;
using SysDiag.Core.Windows;

namespace SysDiag.Ui;

public partial class OptimizeWindow : Window
{
    public OptimizeModule.Options Options { get; } = new();

    public OptimizeWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void Aplicar_Click(object sender, RoutedEventArgs e)
    {
        Options.FlushDns = ChkDns.IsChecked == true;
        Options.FlushArp = ChkArp.IsChecked == true;
        Options.WifiMaxPerformance = ChkWifi.IsChecked == true;
        Options.WifiPowerSave = ChkWifiPowerSave.IsChecked == true;
        Options.CpuMaxPercent = ChkCpuMax.IsChecked == true ? 70 : null;
        Options.PublicDns = ChkDnsPublico.IsChecked == true;
        Options.VisualEffects = ChkEfectos.IsChecked == true;
        Options.FixWlanAutoconfig = ChkWlan.IsChecked == true;
        Options.HighPerformancePlan = ChkPlan.IsChecked == true;
        Options.ResetTcpStack = ChkReset.IsChecked == true;

        // El reinicio de la pila es el único destructivo: se confirma aparte.
        if (Options.ResetTcpStack)
        {
            bool ok = Dialog.Confirm("Confirmar reinicio de la pila de red",
                "Se borrará la configuración manual de IP, DNS y VPN. Los cambios no surten " +
                "efecto hasta reiniciar el equipo.",
                "Reiniciar la pila");

            if (!ok) return;
        }

        DialogResult = true;
        Close();
    }
}
