using System.Net;
using System.Net.Sockets;
using InfraPulse;

internal static class Program
{
    private sealed record Options(
        string ConfigPath,
        string Format,
        string? OutputPath,
        int? IntervalSeconds,
        bool SelfTest);

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
            {
                PrintUsage(Console.Out);
                return 0;
            }

            var options = ParseArgs(args);
            if (options.SelfTest)
            {
                return await RunSelfTestAsync();
            }

            using var shutdown = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };

            var config = await ConfigLoader.LoadAsync(options.ConfigPath, shutdown.Token);
            using var checker = new ServiceChecker();

            do
            {
                var results = await Task.WhenAll(config.Services.Select(service => checker.CheckAsync(service, shutdown.Token)));
                var output = options.Format switch
                {
                    "table" => OutputWriter.AsTable(results),
                    "json" => OutputWriter.AsJson(results),
                    "prometheus" => OutputWriter.AsPrometheus(results),
                    _ => throw new InvalidOperationException("Invalid output format")
                };

                if (options.OutputPath is not null)
                {
                    var temporary = options.OutputPath + ".tmp";
                    await File.WriteAllTextAsync(temporary, output, shutdown.Token);
                    File.Move(temporary, options.OutputPath, true);
                }
                else
                {
                    Console.WriteLine(output);
                }

                if (!options.IntervalSeconds.HasValue)
                {
                    return results.All(result => result.Up) ? 0 : 1;
                }

                await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds.Value), shutdown.Token);
            } while (!shutdown.IsCancellationRequested);

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            PrintUsage(Console.Error);
            return 2;
        }
    }

    private static Options ParseArgs(string[] args)
    {
        var config = "monitor.json";
        var format = "table";
        string? output = null;
        int? interval = null;
        var selfTest = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            string Next()
            {
                index++;
                if (index >= args.Length) throw new ArgumentException($"Missing value after {argument}");
                return args[index];
            }

            switch (argument)
            {
                case "--config": config = Next(); break;
                case "--format": format = Next().ToLowerInvariant(); break;
                case "--output": output = Next(); break;
                case "--interval": interval = int.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--self-test": selfTest = true; break;
                default: throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        if (format is not ("table" or "json" or "prometheus"))
            throw new ArgumentException("--format must be table, json, or prometheus");
        if (interval is <= 0 or > 86400)
            throw new ArgumentException("--interval must be between 1 and 86400 seconds");
        return new Options(config, format, output, interval, selfTest);
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("InfraPulse - dependency-free service monitor for .NET 8");
        writer.WriteLine("Usage: infrapulse [--config monitor.json] [--format table|json|prometheus]");
        writer.WriteLine("                  [--output FILE] [--interval SECONDS] [--self-test]");
    }

    private static async Task<int> RunSelfTestAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        var config = new MonitorConfig
        {
            Services =
            [
                new ServiceDefinition
                {
                    Name = "local-test",
                    Type = "tcp",
                    Host = "127.0.0.1",
                    Port = port,
                    TimeoutMs = 1000
                }
            ]
        };
        ConfigLoader.Validate(config);
        using var checker = new ServiceChecker();
        var result = await checker.CheckAsync(config.Services[0], CancellationToken.None);
        using var accepted = await acceptTask;
        listener.Stop();

        if (!result.Up) throw new InvalidOperationException("TCP self-test failed");
        if (!OutputWriter.AsPrometheus([result]).Contains("infrapulse_service_up", StringComparison.Ordinal))
            throw new InvalidOperationException("Prometheus output self-test failed");

        Console.WriteLine("All InfraPulse self-tests passed.");
        return 0;
    }
}
