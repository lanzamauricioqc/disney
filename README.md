# Disney queue history

The solution collects attraction queue times and stores an append-only history in PostgreSQL.

## Architecture

- `Disney.Domain`: park, land, attraction, and observation entities.
- `Disney.Application`: collection use cases, persistence/provider abstractions, and historical query contracts.
- `Disney.Infrastructure`: PostgreSQL/Dapper persistence, Queue-Times HTTP integration, migrations, and historical queries.
- `Disney.Api`: versioned dashboard endpoints, OpenAPI, health checks, caching, and rate limiting.
- `frontend`: React, TypeScript, Vite, TanStack Query, and ECharts dashboard served by Nginx.
- `marketing-frontend`: independent React/Vite public site for product, pricing,
  plan comparison, waitlist registration, and Stripe Checkout.
- `company-frontend`: independent React/Vite operations portal for travel agencies
  and tour operators.
- `Worker`: scheduled process and dependency-injection composition.
- `Disney.Tests`: unit and migration-shape tests.

The API depends on `Disney.Application` contracts rather than querying PostgreSQL directly.

## Deployment

Read [the Azure worker and database deployment strategy](docs/azure-worker-database-deployment.md)
before provisioning Azure resources or deploying additional parts of the solution.

## API

Dashboard aggregation endpoints use the trailing three months:

```text
GET /api/v1/parks/{parkId}/wait-times/current
GET /api/v1/parks
GET /api/v1/parks/{parkId}/analytics/wait-times/history?attractionId={attractionId}&from={timestamp}&to={timestamp}
GET /api/v1/parks/{parkId}/analytics/wait-times/daily?attractionId={attractionId}
GET /api/v1/parks/{parkId}/analytics/wait-times/weekday-quarter-hourly
GET /api/v1/parks/{parkId}/analytics/closures/weekday-quarter-hourly
GET /api/v1/parks/{parkId}/attractions/{attractionId}/wait-time-prediction?at={timestamp}
GET /api/v1/parks/{parkId}/walking-time?fromAttractionId={id}&toAttractionId={id}
POST /api/v1/parks/{parkId}/itineraries/optimize
POST /api/v1/waitlist
GET /api/v1/billing/products
POST /api/v1/billing/checkout-sessions
POST /api/v1/billing/webhooks/stripe
POST /api/v1/company/auth/bootstrap
POST /api/v1/company/auth/login
GET /api/v1/company/dashboard
GET|POST /api/v1/company/customers
GET|POST /api/v1/company/visits
POST /api/v1/company/visits/{visitId}/entitlement
POST /api/v1/company/visits/{visitId}/access-link
GET /api/v1/company/credits
POST /api/v1/company/billing/checkout-sessions
POST /api/v1/company/integrations/reservations
```

Historical observation queries use an inclusive `from`, exclusive `to`, and a maximum
31-day range. Timestamps must include an offset.

Wait-time predictions accept a future offset-bearing timestamp up to 24 hours ahead.
They use the median of valid, open observations from the matching park-local weekday
and 15-minute slot during the trailing three months. Available predictions include a
confidence score from 0 to 1 based on historical sample volume and consistency around
the median. Results explicitly identify when there is insufficient historical data.

Walking-time estimates prefer the shortest route through the persisted park graph.
When graph data has not yet been loaded, they fall back to attraction coordinates,
Haversine distance, and a 1.25 route-distance multiplier. Both modes use a 1.4 m/s
walking speed and identify the route source and algorithm version in the response.

The initial itinerary optimizer accepts a visit window and attraction preferences.
It returns deterministic scheduled stops with walking, queue, and attraction timing,
plus explicit reasons for attractions that could not be scheduled.

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

The dashboard is available at `http://localhost:8081`, the marketing site at
`http://localhost:8082`, and the company portal at `http://localhost:8083`.
For frontend-only development, run `npm install` and `npm run dev` from the
relevant frontend directory. The applications proxy API requests to
`http://localhost:8080`.

Stripe Checkout remains disabled until these environment variables are set:

```text
STRIPE_SECRET_KEY
STRIPE_WEBHOOK_SECRET
STRIPE_VISIT_PASS_PRICE_ID
STRIPE_TRIP_PASS_PRICE_ID
STRIPE_COMPANY_STARTER_PRICE_ID
STRIPE_COMPANY_GROWTH_PRICE_ID
```

Use `STRIPE_SUCCESS_URL` and `STRIPE_CANCEL_URL` to override the default local
marketing-site return URLs. Use `STRIPE_COMPANY_SUCCESS_URL` and
`STRIPE_COMPANY_CANCEL_URL` for company credit checkout.

Company authentication requires JWT issuer, audience, signing key, and token
lifetime configuration under `CompanyAuthentication`. Development defaults are
provided locally; production deployments must supply their own signing key.
Invitations and visitor-access secrets are returned once and stored only as
hashes. Notification outbox entries currently retain delivery metadata only;
an external secure dispatcher is still required to deliver those one-time
secrets by email or SMS.
