# InfraPulse

InfraPulse is a dependency-free .NET 8 service monitor for HTTP endpoints and TCP ports. It can run once in scripts or continuously as a small agent, producing human-readable, JSON or Prometheus textfile output.

## Practical use cases

- Check websites, APIs, SSH, RDP, SMB, databases and controller ports
- Feed metrics into Prometheus through Node Exporter's textfile collector
- Run health checks from Task Scheduler, cron, Docker or systemd
- Return a non-zero exit code when any service is unavailable

## Requirements

- .NET 8 SDK to build
- .NET 8 runtime to execute a framework-dependent publish

## Run

```bash
cp examples/monitor.json monitor.json
dotnet run --project src/InfraPulse/InfraPulse.csproj -- --config monitor.json
```

Continuous Prometheus output:

```bash
dotnet run --project src/InfraPulse/InfraPulse.csproj -- \
  --config monitor.json \
  --format prometheus \
  --output infrapulse.prom \
  --interval 30
```

Run the built-in functional test:

```bash
dotnet run --project src/InfraPulse/InfraPulse.csproj -- --self-test
```

## Configuration

```json
{
  "services": [
    {
      "name": "intranet",
      "type": "http",
      "url": "https://intranet.example.local/health",
      "timeoutMs": 3000,
      "expectedStatus": 200
    },
    {
      "name": "domain-controller-ldap",
      "type": "tcp",
      "host": "10.10.0.10",
      "port": 389,
      "timeoutMs": 1000
    }
  ]
}
```

## Exit codes

- `0`: all services are up, clean shutdown, or self-test passed
- `1`: at least one service is down in one-shot mode
- `2`: invalid arguments, configuration or runtime error

## Deployment

A hardened example unit is available in `deploy/systemd/infrapulse.service`. A multi-stage `Dockerfile` is also included.

## Design choices

The project intentionally uses only the .NET standard library. This keeps deployment simple, reduces supply-chain risk and demonstrates async networking, configuration validation, cancellation, atomic file updates and metrics formatting.

## License

MIT
