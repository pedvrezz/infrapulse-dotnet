using System.Text.Json;

namespace InfraPulse;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<MonitorConfig> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}", path);
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<MonitorConfig>(stream, Options, cancellationToken)
                     ?? throw new InvalidDataException("Configuration file is empty.");
        Validate(config);
        return config;
    }

    public static void Validate(MonitorConfig config)
    {
        if (config.Services.Count == 0)
        {
            throw new InvalidDataException("At least one service must be configured.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in config.Services)
        {
            if (string.IsNullOrWhiteSpace(service.Name))
            {
                throw new InvalidDataException("Every service requires a non-empty name.");
            }
            if (!names.Add(service.Name))
            {
                throw new InvalidDataException($"Duplicate service name: {service.Name}");
            }
            if (service.TimeoutMs is < 50 or > 120000)
            {
                throw new InvalidDataException($"Service '{service.Name}' timeoutMs must be between 50 and 120000.");
            }

            switch (service.Type.Trim().ToLowerInvariant())
            {
                case "http":
                    if (!Uri.TryCreate(service.Url, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new InvalidDataException($"Service '{service.Name}' requires a valid http/https URL.");
                    }
                    if (service.ExpectedStatus is < 100 or > 599)
                    {
                        throw new InvalidDataException($"Service '{service.Name}' expectedStatus must be between 100 and 599.");
                    }
                    break;
                case "tcp":
                    if (string.IsNullOrWhiteSpace(service.Host))
                    {
                        throw new InvalidDataException($"Service '{service.Name}' requires host.");
                    }
                    if (service.Port is null or < 1 or > 65535)
                    {
                        throw new InvalidDataException($"Service '{service.Name}' requires port between 1 and 65535.");
                    }
                    break;
                default:
                    throw new InvalidDataException($"Service '{service.Name}' has unsupported type '{service.Type}'. Use http or tcp.");
            }
        }
    }
}
