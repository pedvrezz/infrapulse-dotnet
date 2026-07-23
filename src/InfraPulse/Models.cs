using System.Text.Json.Serialization;

namespace InfraPulse;

public sealed class MonitorConfig
{
    [JsonPropertyName("services")]
    public List<ServiceDefinition> Services { get; init; } = [];
}

public sealed class ServiceDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("port")]
    public int? Port { get; init; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; init; } = 3000;

    [JsonPropertyName("expectedStatus")]
    public int? ExpectedStatus { get; init; }
}

public sealed record CheckResult(
    string Name,
    string Type,
    string Target,
    bool Up,
    long LatencyMs,
    string Message,
    DateTimeOffset CheckedAt);
