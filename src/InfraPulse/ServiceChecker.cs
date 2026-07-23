using System.Diagnostics;
using System.Net.Sockets;

namespace InfraPulse;

public sealed class ServiceChecker : IDisposable
{
    private readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10)
    })
    {
        DefaultRequestVersion = new Version(2, 0),
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
    };

    public Task<CheckResult> CheckAsync(ServiceDefinition service, CancellationToken cancellationToken) =>
        service.Type.Trim().ToLowerInvariant() switch
        {
            "http" => CheckHttpAsync(service, cancellationToken),
            "tcp" => CheckTcpAsync(service, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported service type: {service.Type}")
        };

    private async Task<CheckResult> CheckHttpAsync(ServiceDefinition service, CancellationToken outerToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        timeout.CancelAfter(service.TimeoutMs);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, service.Url);
            request.Headers.UserAgent.ParseAdd("InfraPulse/1.0");
            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            stopwatch.Stop();

            var status = (int)response.StatusCode;
            var up = service.ExpectedStatus.HasValue
                ? status == service.ExpectedStatus.Value
                : status is >= 200 and < 400;
            var expectation = service.ExpectedStatus.HasValue
                ? $"expected {service.ExpectedStatus.Value}"
                : "expected 2xx or 3xx";
            return new CheckResult(
                service.Name,
                "http",
                service.Url!,
                up,
                stopwatch.ElapsedMilliseconds,
                $"HTTP {status}; {expectation}",
                checkedAt);
        }
        catch (OperationCanceledException) when (!outerToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new CheckResult(service.Name, "http", service.Url!, false,
                stopwatch.ElapsedMilliseconds, $"Timeout after {service.TimeoutMs} ms", checkedAt);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            stopwatch.Stop();
            return new CheckResult(service.Name, "http", service.Url!, false,
                stopwatch.ElapsedMilliseconds, exception.Message, checkedAt);
        }
    }

    private static async Task<CheckResult> CheckTcpAsync(ServiceDefinition service, CancellationToken outerToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        timeout.CancelAfter(service.TimeoutMs);
        var target = $"{service.Host}:{service.Port}";

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(service.Host!, service.Port!.Value, timeout.Token);
            stopwatch.Stop();
            return new CheckResult(service.Name, "tcp", target, true,
                stopwatch.ElapsedMilliseconds, "TCP connection established", checkedAt);
        }
        catch (OperationCanceledException) when (!outerToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new CheckResult(service.Name, "tcp", target, false,
                stopwatch.ElapsedMilliseconds, $"Timeout after {service.TimeoutMs} ms", checkedAt);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            stopwatch.Stop();
            return new CheckResult(service.Name, "tcp", target, false,
                stopwatch.ElapsedMilliseconds, exception.Message, checkedAt);
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
