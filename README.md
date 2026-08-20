# Disney queue history

The solution collects attraction queue times and stores an append-only history in PostgreSQL.

## Architecture

- `Disney.Domain`: park, land, attraction, and observation entities.
- `Disney.Application`: collection use cases, persistence/provider abstractions, and historical query contracts.
- `Disney.Infrastructure`: PostgreSQL/Dapper persistence, Queue-Times HTTP integration, migrations, and historical queries.
- `Disney.Api`: versioned dashboard endpoints, OpenAPI, health checks, caching, and rate limiting.
- `frontend`: React, TypeScript, Vite, TanStack Query, and ECharts dashboard served by Nginx.
- `Worker`: scheduled process and dependency-injection composition.
- `Disney.Tests`: unit and migration-shape tests.

The API depends on `Disney.Application` contracts rather than querying PostgreSQL directly.

## API

Dashboard aggregation endpoints use the trailing three months:

```text
GET /api/v1/parks/{parkId}/wait-times/current
GET /api/v1/parks
GET /api/v1/parks/{parkId}/analytics/wait-times/history?attractionId={attractionId}&from={timestamp}&to={timestamp}
GET /api/v1/parks/{parkId}/analytics/wait-times/daily?attractionId={attractionId}
GET /api/v1/parks/{parkId}/analytics/wait-times/weekday-quarter-hourly
GET /api/v1/parks/{parkId}/analytics/closures/weekday-quarter-hourly
```

Historical observation queries use an inclusive `from`, exclusive `to`, and a maximum
31-day range. Timestamps must include an offset.

The analytics endpoints group local park time into 15-minute buckets and return
`localHour` and `localMinute`. They accept an optional `attractionId` query parameter.
OpenAPI is available at `/openapi/v1.json`, with liveness and readiness at
`/health/live` and `/health/ready`. Swagger UI is available at `/swagger`.

## Database migrations

SQL migrations are embedded from `Disney.Infrastructure\Migrations` and run automatically
before the worker starts. Applied versions are recorded in `public.schema_migrations`.
The initial migration targets a new, empty database; it does not import or upgrade the
discarded prototype schema.

Create new migrations with the next numeric prefix:

```text
Disney.Infrastructure\Migrations\003_description.sql
```

Migrations must be forward-only and safe to execute inside a transaction.

## Local development

```powershell
docker compose up -d --build
dotnet test Disney.slnx
```

The dashboard is available at `http://localhost:8081`. For frontend-only development,
run `npm install` and `npm run dev` from `frontend`; Vite proxies API requests to
`http://localhost:8080`.
