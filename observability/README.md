# Observability

The application writes provider-neutral structured JSON logs to standard output
through `Microsoft.Extensions.Logging`. Grafana Alloy discovers containers labeled
`logs.collect=true`, enriches their logs with Docker metadata, and sends them to
Loki. Grafana is provisioned with Loki as its default data source.

Start the complete stack:

```powershell
docker compose up --build -d
```

Open Grafana at <http://localhost:3000> and sign in with `admin` / `admin`.
Set `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` before starting the stack
to override those local defaults.

Useful Explore queries:

```logql
{service="worker"}
{service="worker", log_level="Error"}
{service="worker"} | json | EventId=3004
{service="worker"} |= "\"CollectionRunId\":1"
```

Loki retains logs for 30 days. Application code has no dependency on Loki,
Alloy, or Grafana; another collector or logging backend can consume the same
JSON output without changing business logic.
