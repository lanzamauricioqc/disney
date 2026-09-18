# Disney development infrastructure

This Terraform root module deploys only the infrastructure currently required by the
queue collection worker:

- The `disney-dev` resource group.
- Private PostgreSQL Flexible Server and the `queue_times` database.
- Azure Container Apps environment and scheduled worker.
- Azure Container Registry.
- Key Vault and managed identities.
- Virtual network, delegated subnets, and private DNS.
- Log Analytics.
- Azure Automation start and stop schedules for PostgreSQL.

Terraform defaults to **Canada East**, the Quebec City Azure region. Set `location` to
`canadacentral` before the first apply if availability-zone support and broader regional
service availability are more important.

## Prerequisites

- An Azure subscription.
- Azure CLI.
- Terraform 1.11 or later.
- Docker with BuildKit.
- Permission to create resources and role assignments in the subscription.

Register the resource providers before the first deployment:

```powershell
$providers = @(
    "Microsoft.App",
    "Microsoft.Automation",
    "Microsoft.ContainerRegistry",
    "Microsoft.DBforPostgreSQL",
    "Microsoft.KeyVault",
    "Microsoft.ManagedIdentity",
    "Microsoft.Network",
    "Microsoft.OperationalInsights"
)

foreach ($provider in $providers) {
    az provider register --namespace $provider
}
```

## Remote state bootstrap

The Azure Storage backend must exist before Terraform can create `disney-dev`. Create a
small, separate state resource group and storage account once:

```powershell
az group create `
    --name disney-terraform-state `
    --location canadaeast

az storage account create `
    --name <globally-unique-storage-name> `
    --resource-group disney-terraform-state `
    --location canadaeast `
    --sku Standard_LRS `
    --allow-blob-public-access false

az storage container create `
    --name tfstate `
    --account-name <globally-unique-storage-name> `
    --auth-mode login
```

Grant the deploying identity `Storage Blob Data Contributor` on that storage account.
Copy `backend.hcl.example` to the ignored `backend.hcl`, replace its values, and run:

```powershell
terraform init -backend-config=backend.hcl
```

## Configure deployment values

Copy `terraform.tfvars.example` to the ignored `terraform.tfvars` and replace the
subscription ID and globally unique `name_suffix`.

Do not put the PostgreSQL password in a tfvars file. Supply it only through an ephemeral
environment variable:

```powershell
$password = Read-Host "PostgreSQL administrator password" -AsSecureString
$env:TF_VAR_postgresql_administrator_password =
    [Net.NetworkCredential]::new("", $password).Password
```

Terraform uses write-only AzureRM attributes for the PostgreSQL password and Key Vault
secret, so the password is not retained in Terraform state.

## Phase 1: deploy infrastructure

Keep `deploy_worker = false`, then run:

```powershell
terraform fmt -check
terraform validate
terraform plan -out disney-dev.tfplan
terraform apply disney-dev.tfplan
```

This creates PostgreSQL, Key Vault, ACR, networking, automation, and the Container Apps
environment without creating the worker app before its image exists.

## Build and push the first worker image

Read the registry values:

```powershell
$registryName = terraform output -raw container_registry_name
$repository = terraform output -raw worker_image_repository
$imageTag = git rev-parse HEAD

az acr login --name $registryName
```

The worker Docker build requires the private NuGet package source credential when the
package is not already cached:

```powershell
$env:DOCKER_BUILDKIT = "1"

docker build `
    --secret id=nuget_credentials,env=NUGET_NORDICTECH_CREDENTIALS `
    --file Worker\Dockerfile `
    --tag "${repository}:${imageTag}" `
    .

docker push "${repository}:${imageTag}"
```

Never pass the NuGet credential as a Docker build argument.

## Phase 2: deploy the worker

Set the following in the ignored `terraform.tfvars`:

```hcl
deploy_worker    = true
worker_image_tag = "<Git commit SHA pushed above>"
```

Apply again. The worker has no ingress and scales to one replica daily between 07:00
and 23:59 in `America/Toronto`.

```powershell
terraform plan -out disney-dev.tfplan
terraform apply disney-dev.tfplan
```

The worker runs database migrations before beginning collection.

## Enable the PostgreSQL schedule

First verify manually that:

1. PostgreSQL is ready before the worker starts.
2. The worker performs migrations and stores queue observations.
3. The worker reaches zero replicas after 23:59.
4. PostgreSQL can be stopped and started without intervention.

Then set:

```hcl
enable_database_schedule = true

# These must be the next future executions when the schedules are first created.
database_start_schedule_start_time = "<RFC3339 timestamp for the next 06:50 Eastern>"
database_stop_schedule_start_time  = "<RFC3339 timestamp for the next 00:10 Eastern>"
```

Examples during daylight-saving time use `-04:00`; standard time uses `-05:00`.
Azure Automation stores the schedule in `America/Toronto` and adjusts future
executions for daylight-saving changes.

After creating the schedules, Terraform ignores subsequent changes to their original
start timestamps. Replace the timestamps if a schedule must be recreated.

## Password rotation

Set a new `TF_VAR_postgresql_administrator_password`, increment
`postgresql_password_version`, and apply. This updates both PostgreSQL and the Key Vault
secret without placing the password in state. Restart the worker revision after rotation
so it refreshes the Key Vault secret reference.

## Cleanup

Disable `enable_database_delete_lock` before destroying the database. Then run:

```powershell
terraform destroy
```

Remove the password from the current shell after Terraform finishes:

```powershell
Remove-Item Env:\TF_VAR_postgresql_administrator_password
```
