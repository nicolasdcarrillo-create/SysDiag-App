using System.ComponentModel;

namespace SysDiag.Models;

public class SecurityCheckRow
{
    [DisplayName("Componente")] public string Componente { get; set; } = "";
    [DisplayName("Estado")] public string Estado { get; set; } = "";
    [DisplayName("Detalle")] public string Detalle { get; set; } = "";
    [Browsable(false)] public Severity Nivel { get; set; }
}
