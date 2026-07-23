using System.Text;
using System.Text.Json;

namespace InfraPulse;

public static class OutputWriter
{
    public static string AsTable(IEnumerable<CheckResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{\"SERVICE\",-22} {\"TYPE\",-6} {\"STATUS\",-7} {\"LATENCY\",-10} TARGET");
        builder.AppendLine(new string('-', 86));
        foreach (var result in results)
        {
            builder.AppendLine($"{result.Name,-22} {result.Type,-6} {(result.Up ? "UP" : "DOWN"),-7} {result.LatencyMs + " ms",-10} {result.Target}");
            if (!result.Up)
            {
                builder.AppendLine($"  reason: {result.Message}");
            }
        }
        return builder.ToString();
    }

    public static string AsJson(IEnumerable<CheckResult> results) =>
        JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });

    public static string AsPrometheus(IEnumerable<CheckResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# HELP infrapulse_service_up Whether the configured service is reachable (1 = up, 0 = down).");
        builder.AppendLine("# TYPE infrapulse_service_up gauge");
        foreach (var result in results)
        {
            var labels = $"name=\"{EscapeLabel(result.Name)}\",type=\"{EscapeLabel(result.Type)}\",target=\"{EscapeLabel(result.Target)}\"";
            builder.AppendLine($"infrapulse_service_up{{{labels}}} {(result.Up ? 1 : 0)}");
        }
        builder.AppendLine("# HELP infrapulse_service_latency_milliseconds Last check latency in milliseconds.");
        builder.AppendLine("# TYPE infrapulse_service_latency_milliseconds gauge");
        foreach (var result in results)
        {
            var labels = $"name=\"{EscapeLabel(result.Name)}\",type=\"{EscapeLabel(result.Type)}\",target=\"{EscapeLabel(result.Target)}\"";
            builder.AppendLine($"infrapulse_service_latency_milliseconds{{{labels}}} {result.LatencyMs}");
        }
        return builder.ToString();
    }

    private static string EscapeLabel(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}
