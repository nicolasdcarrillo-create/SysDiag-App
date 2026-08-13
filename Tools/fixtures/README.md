diagnostico_fixture.json

Uso:
- Contiene un diagnóstico real generado en runtime y sirve como fixture para pruebas de integración y para desarrollar la UI sin ejecutar WMI/COM.
- Ruta en el repo: Tools\fixtures\diagnostico_fixture.json

Ejemplo rápido (C#):
var reporte = Exporter.Cargar("Tools\\fixtures\\diagnostico_fixture.json");
// Usar "reporte" en pruebas o para alimentar vistas.

Uso desde consola (PowerShell):
PS> .\Tools\validate_fixture.ps1

Notas:
- El fixture incluye hallazgos críticos y avisos (Red, Estabilidad, Termicas).
- Actualizar el fixture cuando se capture un diagnóstico más representativo.