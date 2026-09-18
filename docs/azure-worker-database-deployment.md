# Azure worker and database deployment strategy

## Status

This document records the initial Azure production strategy for the queue collection
worker and PostgreSQL database. The resources have not been deployed yet.

Read and update this document before deploying the API, frontends, or other services.
Those deployments may change the database availability requirements and the most
cost-effective architecture.

## Current workload

- `Worker` is a .NET background service that continuously checks for collection work.
- The worker currently wakes every minute and must run as a long-lived process.
- PostgreSQL stores the append-only queue observation history and application data.
- Database migrations run automatically when the worker starts.
- Both services currently need to operate only during the Quebec City daytime window.

## Operating schedule

Use the IANA time zone `America/Toronto`. Do not use a fixed `EST` UTC offset because
Quebec observes daylight-saving time.

| Time | Operation |
| --- | --- |
| 06:50 | Start PostgreSQL Flexible Server |
| 06:50-07:00 | Wait for PostgreSQL to report `Ready` |
| 07:00 | Scale the worker from zero to one replica |
| 23:59 | Begin gracefully scaling the worker to zero |
| 00:05-00:10 | Stop PostgreSQL after the worker has terminated |

The worker contains bounded exponential startup retries. Its default configuration is:

```json
{
  "DatabaseStartup": {
    "MaxAttempts": 10,
    "InitialDelay": "00:00:02",
    "MaxDelay": "00:00:30"
  }
}
```

The database should still be started before the worker. The retry policy protects
against short readiness delays; it is not a replacement for correct orchestration.

## Recommended Azure resources

The development Terraform configuration defaults to **Canada East**, which is located
in Quebec City. Reassess **Canada Central** for production if availability zones and
broader regional service availability become more important.

| Concern | Initial service | Initial configuration |
| --- | --- | --- |
| Worker runtime | Azure Container Apps Consumption | 0.25 vCPU, 0.5 GiB, no ingress |
| Database | Azure Database for PostgreSQL Flexible Server | Burstable B1ms, 32 GB |
| Container registry | Azure Container Registry Basic | Managed-identity image pull |
| Secrets | Azure Key Vault | Container Apps Key Vault references |
| Logs and metrics | Azure Monitor and Log Analytics | Short retention and cost controls |
| Scheduling | Container Apps KEDA cron and PostgreSQL automation tasks | `America/Toronto` |
| Infrastructure | Terraform | Deployed locally first, then through GitHub Actions |

### Worker

Use a regular Azure Container App rather than a Container Apps Job. The current worker
does not finish after one collection: it remains alive and polls repeatedly. A scheduled
job would require changing the application into a finite execution model.

Configure the Container App with:

- Consumption workload profile.
- `minReplicas` set to `0`.
- `maxReplicas` set to `1`.
- A KEDA cron scaling rule that requests one replica from 07:00 through 23:59 in
  `America/Toronto`.
- Single revision mode.
- External ingress disabled.
- At least 60 seconds for graceful termination.
- `DOTNET_ENVIRONMENT=Production`.
- `QueueCollection__Interval=00:01:00`, unless the collection frequency is intentionally
  changed.

The worker already propagates cancellation to collection and database operations.
Scaling down should therefore stop an active cycle gracefully. PostgreSQL must not be
stopped until the worker replica has terminated.

### PostgreSQL

Start with PostgreSQL Flexible Server using:

- Burstable B1ms compute.
- 32 GB provisioned storage.
- A single availability zone.
- No high availability or read replicas initially.
- Seven-day backup retention with locally redundant backup storage.
- Private network access.
- A delete lock to reduce the risk of accidental removal.

B1ms is appropriate only while load remains small. Monitor CPU credits, CPU usage,
memory, connections, storage growth, and query latency. Move to B2s or a General Purpose
SKU if the burstable tier becomes constrained.

Stopping the server stops compute billing, but storage and backup storage remain billed.
Azure automatically starts a server that remains stopped for seven consecutive days and
may briefly start it for platform maintenance. The daily schedule avoids the seven-day
case.

Use the PostgreSQL resource's Azure automation tasks to start and stop it. Alert on
failed task executions. The stop workflow should confirm that the worker has reached
zero replicas before stopping the database.

## Networking

Create one virtual network with separate delegated subnets:

- A subnet for the Azure Container Apps managed environment.
- A subnet delegated to PostgreSQL Flexible Server.

Configure PostgreSQL with private access and private DNS. The worker should connect
using the database's fully qualified private DNS name. Do not expose PostgreSQL through
a public endpoint.

The worker requires outbound HTTPS access to `https://queue-times.com`. Do not add a NAT
Gateway initially because it adds a meaningful fixed cost. Add one later only if a stable
outbound IP address or stricter egress control becomes necessary.

The production connection string must be provided through a Key Vault reference and
must enable TLS. Prefer full certificate and host verification after validating the
Azure certificate chain; never disable certificate validation.

## Identity and secrets

- Give the Container App a system-assigned managed identity.
- Grant that identity `AcrPull` on the container registry.
- Grant it access only to the required Key Vault secrets.
- Store the PostgreSQL password or complete connection string in Key Vault.
- Do not place production credentials in Terraform variable files, GitHub variables,
  application settings committed to the repository, or container images.
- Use GitHub Actions OpenID Connect federation for Azure deployments instead of a
  long-lived service-principal secret.

PostgreSQL Microsoft Entra authentication can replace password authentication later,
but it is not required for the first deployment.

## Deployment workflow

Create separate development and production resource groups. The initial production
deployment order should be:

1. Deploy the resource group, virtual network, subnets, private DNS, Key Vault, and
   container registry.
2. Deploy PostgreSQL Flexible Server and configure backup retention and the delete lock.
3. Add the production connection secret to Key Vault.
4. Build the worker image and push it to the registry.
5. Deploy the Container Apps environment and worker Container App.
6. Start PostgreSQL and manually run the worker once to verify migrations and collection.
7. Confirm that new observations are stored successfully.
8. Enable and verify the worker and database schedules.
9. Trigger a controlled shutdown and startup cycle before relying on the automation.

Infrastructure definitions should be idempotent Terraform modules. Application deployments
should use immutable image tags based on the Git commit SHA rather than `latest`.

## Migrations and startup

The worker performs migrations before beginning collection. Migrations use a PostgreSQL
advisory lock, and the Container App is limited to one replica, so concurrent migration
execution should not occur during the initial deployment.

If startup database operations fail, the worker retries with bounded exponential delays.
After the maximum attempt count it exits with an error, allowing Container Apps to
restart it and making the failure visible rather than running in a partially initialized
state.

When more services are deployed, move migration execution into a dedicated deployment
step or one-off Container Apps Job. At that point, the API and worker should not each
independently own production migration timing.

## Monitoring and alerts

Do not deploy the repository's Loki, Alloy, and Grafana containers during this initial
phase. Use Azure's managed logging first to avoid additional stateful infrastructure.

Create alerts for:

- PostgreSQL start or stop automation failures.
- Worker restart loops or failure to reach one replica after 07:00.
- Worker remaining above zero replicas after the shutdown window.
- No successful queue observations within an expected interval.
- PostgreSQL CPU credits, CPU, storage, connections, and query latency.
- Unexpected daily or monthly Azure spending.

Keep application logs structured. Configure conservative Log Analytics retention and
avoid verbose per-request or SQL logging in production because log ingestion can exceed
the worker's compute cost.

## Initial cost expectation

The following is an order-of-magnitude estimate in USD for the Canadian regions and must be
rechecked in the Azure Pricing Calculator before deployment:

| Resource | Approximate monthly cost |
| --- | ---: |
| PostgreSQL B1ms compute on the daily schedule | $10 |
| PostgreSQL 32 GB storage | $4 |
| Container Apps worker | $5-10 |
| Registry, secrets, automation, and basic logs | $7-15 |
| **Expected initial total** | **$25-40** |

At B1ms size, stopping PostgreSQL overnight saves only approximately $4 per month.
The schedule is still part of the current requirement, but its operational complexity
should be reconsidered when the API is deployed.

## Decisions deferred until the rest of the solution is deployed

The current resources should be reusable:

- Deploy `Disney.Api` as another Container App in the same managed environment.
- Reuse the VNet, registry, Key Vault, private DNS, PostgreSQL server, and monitoring.
- Evaluate Azure Static Web Apps or Azure Storage with a CDN for the React frontends.
- Reassess whether PostgreSQL can continue to stop overnight. A public API, Stripe
  webhooks, company portal, or customer authentication may require 24-hour database
  availability.
- Consider PostgreSQL high availability only after uptime requirements justify the cost.
- Move migrations out of application startup before allowing multiple independently
  deployed database clients.
- Revisit log retention and managed observability after real traffic volumes are known.

The overnight database shutdown must not be retained automatically once customer-facing
services depend on PostgreSQL outside the current collection window.

## Reference documentation

- [Azure Container Apps scaling](https://learn.microsoft.com/azure/container-apps/scale-app)
- [Azure Container Apps pricing](https://azure.microsoft.com/pricing/details/container-apps/)
- [KEDA cron scaler](https://keda.sh/docs/scalers/cron/)
- [PostgreSQL Flexible Server automation tasks](https://learn.microsoft.com/azure/postgresql/integration/create-automation-tasks)
- [Stop PostgreSQL Flexible Server](https://learn.microsoft.com/azure/postgresql/configure-maintain/how-to-stop-server)
- [PostgreSQL private networking](https://learn.microsoft.com/azure/postgresql/network/concepts-networking-private)
