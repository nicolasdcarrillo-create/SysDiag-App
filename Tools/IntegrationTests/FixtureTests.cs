using System;
using System.Text.Json;
using System.IO;
using Xunit;

namespace IntegrationTests;

public class FixtureTests
{
    [Fact]
    public void FixtureContainsCriticalFindings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "diagnostico_fixture.json");
        Assert.True(File.Exists(path), $"Fixture not found: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (!root.TryGetProperty("Hallazgos", out var hallazgos)) Assert.True(false, "No hallazgos array");
        bool anyBad = false;
        foreach (var h in hallazgos.EnumerateArray())
        {
            if (h.TryGetProperty("Severity", out var s) && s.GetString() == "Bad") { anyBad = true; break; }
        }
        Assert.True(anyBad, "Fixture must contain at least one finding with Severity == Bad");
    }
}
