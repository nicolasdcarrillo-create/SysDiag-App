using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SysDiag.Core.Diagnostics;
using SysDiag.Models;

namespace SysDiag.Ui;

/// <summary>Una fila de la lista de historial, con el color de puntaje ya resuelto para el binding.</summary>
public class HistorialItemVm
{
    public DateTime Fecha { get; init; }
    public int Puntaje { get; init; }
    public string Archivo { get; init; } = "";
    public Brush Color { get; init; } = Brushes.Gray;
}

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        Cargar();
    }

    private void Cargar()
    {
        var entradas = Exporter.Listar(200);
        var items = new List<HistorialItemVm>();

        foreach (var e in entradas)
        {
            var nivel = HealthScore.Nivel(e.Puntaje);
            string clave = nivel switch { Severity.Bad => "BBad", Severity.Warn => "BWarn", _ => "BOk" };
            var color = Application.Current.Resources[clave] as Brush ?? Brushes.Gray;

            items.Add(new HistorialItemVm { Fecha = e.Fecha, Puntaje = e.Puntaje, Archivo = e.Archivo, Color = color });
        }

        Lista.ItemsSource = items;

        if (items.Count == 0)
        {
            PanelVacio.Visibility = Visibility.Visible;
            var txt = (System.Windows.Controls.TextBlock)((System.Windows.Controls.StackPanel)PanelVacio).Children[0];
            txt.Text = "Todavía no hay diagnósticos guardados. Corré «Diagnóstico completo» al menos una vez.";
        }
    }

    private void Lista_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Lista.SelectedItem is not HistorialItemVm item) return;

        DiagnosticReport reporte;
        try
        {
            reporte = Exporter.Cargar(item.Archivo);
        }
        catch (Exception ex)
        {
            Dialog.Error("No se pudo abrir ese diagnóstico", ex.Message);
            return;
        }

        if (reporte == null) return;

        PanelVacio.Visibility = Visibility.Collapsed;
        PanelDetalle.Visibility = Visibility.Visible;

        TxtEquipo.Text = string.IsNullOrWhiteSpace(reporte.Equipo) ? "Equipo" : reporte.Equipo;

        int criticos = reporte.Hallazgos.Count(h => h.Severity == Severity.Bad);
        int avisos = reporte.Hallazgos.Count(h => h.Severity == Severity.Warn);
        TxtResumen.Text = $"{item.Fecha:dd MMM yyyy, HH:mm} · Puntaje {item.Puntaje}/100 · " +
                          $"{criticos} crítico(s) · {avisos} aviso(s)";

        ListaHallazgos.ItemsSource = reporte.Hallazgos
            .OrderBy(h => h.Severity == Severity.Bad ? 0 : h.Severity == Severity.Warn ? 1 : 2)
            .ToList();
    }
}
