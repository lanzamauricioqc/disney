# Disney queue history

The solution collects attraction queue times and stores an append-only history in PostgreSQL.

## Architecture

- `Disney.Domain`: park, land, attraction, and observation entities.
- `Disney.Application`: collection use cases, persistence/provider abstractions, and historical query contracts.
- `Disney.Infrastructure`: PostgreSQL/Dapper persistence, Queue-Times HTTP integration, migrations, and historical queries.
- `Worker`: scheduled process and dependency-injection composition.
- `Disney.Tests`: unit and migration-shape tests.

The API is intentionally not part of the solution yet. A future API should depend on
`Disney.Application` and use `IQueueHistoryReader` rather than querying PostgreSQL directly.

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
