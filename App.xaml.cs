using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using SysDiag.Core;

namespace SysDiag;

public partial class App : Application
{
    // Dos diagnósticos simultáneos se pisarían al escribir el registro y el
    // historial, así que solo se permite una instancia.
    private static Mutex _instancia;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instancia = new Mutex(true, @"Local\SysDiag.SingleInstance", out bool esNueva);
        if (!esNueva)
        {
            MessageBox.Show("SysDiag ya está abierto.", "SysDiag",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Los números del informe se escriben siempre igual, sin importar la
        // configuración regional del equipo donde se ejecute.
        var cultura = new CultureInfo("es-CL");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write($"Excepción no controlada: {args.Exception}", "ERROR");
            Ui.Dialog.Error("Error inesperado",
                args.Exception.Message + "\n\nEl detalle quedó guardado en el registro.");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Write($"Error fatal: {args.ExceptionObject}", "ERROR");

        // Antes de esto, los parámetros de cada módulo (segundos de muestreo,
        // días de eventos, etc.) quedaban en el valor por defecto siempre.
        Core.Windows.SettingsService.Aplicar(Core.Windows.SettingsService.Cargar());

        base.OnStartup(e);
    }
}
