using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SysDiag.Ui;

/// <summary>
/// Una barra del gráfico comparativo. El ancho llega ya resuelto en píxeles
/// porque una barra tiene que poder compararse con las demás de un vistazo, y
/// eso exige una escala común calculada sobre el máximo del conjunto.
/// </summary>
public class BarItem
{
    public string Etiqueta { get; init; } = "";
    public string ValorTexto { get; init; } = "";
    public double Ancho { get; init; }
    public Brush Color { get; init; } = Brushes.Gray;
    public string Detalle { get; init; } = "";
}

/// <summary>Gráfico de barras horizontales con escala compartida.</summary>
public class BarChart
{
    public string Titulo { get; init; } = "";
    public string Nota { get; init; } = "";
    public List<BarItem> Barras { get; init; } = new();
    public bool Visible => Barras.Count > 0;

    private const double AnchoMaximo = 420;

    public static BarChart Crear(string titulo, string nota,
        IEnumerable<(string Etiqueta, double Valor, string Texto, Brush Color, string Detalle)> datos)
    {
        var lista = datos.ToList();
        if (lista.Count == 0) return new BarChart { Titulo = titulo };

        // La escala se toma del mayor del conjunto: si todo se dibujara sobre
        // un máximo fijo, un grupo de valores pequeños quedaría plano y sin
        // diferencias visibles.
        double max = lista.Max(x => x.Valor);
        if (max <= 0) max = 1;

        return new BarChart
        {
            Titulo = titulo,
            Nota = nota,
            Barras = lista.Select(x => new BarItem
            {
                Etiqueta = x.Etiqueta,
                ValorTexto = x.Texto,
                // Mínimo visible: una barra de cero píxeles se lee como dato
                // ausente y no como valor bajo.
                Ancho = Math.Max(3, x.Valor / max * AnchoMaximo),
                Color = x.Color,
                Detalle = x.Detalle
            }).ToList()
        };
    }
}

/// <summary>
/// Línea del historial de puntaje. Se dibuja como polilínea sobre un lienzo de
/// tamaño fijo; los puntos vienen ya proyectados.
/// </summary>
public class HistoryChart
{
    public PointCollection Puntos { get; init; } = new();
    public PointCollection Area { get; init; } = new();
    public List<HistoryDot> Marcas { get; init; } = new();
    public string Rango { get; init; } = "";
    public bool Visible => Puntos.Count >= 2;

    public const double Ancho = 620;
    public const double Alto = 120;

    public static HistoryChart Crear(List<(DateTime Fecha, int Puntaje)> serie)
    {
        if (serie == null || serie.Count < 2) return new HistoryChart();

        var datos = serie.OrderBy(x => x.Fecha).TakeLast(30).ToList();

        var puntos = new PointCollection();
        var marcas = new List<HistoryDot>();

        for (int i = 0; i < datos.Count; i++)
        {
            double x = datos.Count == 1 ? 0 : i / (double)(datos.Count - 1) * Ancho;
            // El eje va de 0 a 100 y se invierte, porque en pantalla el origen
            // está arriba y un puntaje alto tiene que dibujarse arriba.
            double y = Alto - datos[i].Puntaje / 100.0 * Alto;

            puntos.Add(new Point(x, y));
            marcas.Add(new HistoryDot
            {
                X = x - 3,
                Y = y - 3,
                Tooltip = $"{datos[i].Fecha:yyyy-MM-dd HH:mm} · {datos[i].Puntaje}/100"
            });
        }

        // El área bajo la línea se cierra por la base para poder rellenarla.
        var area = new PointCollection(puntos) { new Point(Ancho, Alto), new Point(0, Alto) };

        return new HistoryChart
        {
            Puntos = puntos,
            Area = area,
            Marcas = marcas,
            Rango = $"{datos.First().Fecha:dd MMM} — {datos.Last().Fecha:dd MMM}  ·  {datos.Count} diagnósticos"
        };
    }
}

public class HistoryDot
{
    public double X { get; init; }
    public double Y { get; init; }
    public string Tooltip { get; init; } = "";
}
