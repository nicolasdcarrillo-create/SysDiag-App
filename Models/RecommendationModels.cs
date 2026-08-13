using System.ComponentModel;

namespace SysDiag.Models;

public class Recommendation
{
    [DisplayName("Prioridad")] public string Prioridad { get; set; } = "Media";
    [DisplayName("Título")] public string Titulo { get; set; } = "";
    [DisplayName("Acción")] public string Descripcion { get; set; } = "";
}
