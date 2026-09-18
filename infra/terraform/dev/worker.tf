resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.prefix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  infrastructure_subnet_id   = azurerm_subnet.container_apps.id
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_role_assignment" "worker_acr_pull" {
  scope                            = azurerm_container_registry.main.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_user_assigned_identity.worker.principal_id
  skip_service_principal_aad_check = true
}

resource "azurerm_container_app" "worker" {
  count = var.deploy_worker ? 1 : 0

  name                         = "ca-disney-worker-dev"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  identity {
    type = "UserAssigned"
    identity_ids = [
      azurerm_user_assigned_identity.worker.id
    ]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.worker.id
  }

  secret {
    name                = "database-connection"
    identity            = azurerm_user_assigned_identity.worker.id
    key_vault_secret_id = azurerm_key_vault_secret.worker_database_connection.versionless_id
  }

  template {
    min_replicas                     = 0
    max_replicas                     = 1
    polling_interval_in_seconds      = 30
    cooldown_period_in_seconds       = 30
    termination_grace_period_seconds = 60

    container {
      name   = "worker"
      image  = local.worker_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = "Production"
      }

      env {
        name        = "ConnectionStrings__DefaultConnection"
        secret_name = "database-connection"
      }

      env {
        name  = "QueueCollection__Interval"
        value = "00:01:00"
      }

      env {
        name  = "DatabaseStartup__MaxAttempts"
        value = "10"
      }

      env {
        name  = "DatabaseStartup__InitialDelay"
        value = "00:00:02"
      }

      env {
        name  = "DatabaseStartup__MaxDelay"
        value = "00:00:30"
      }
    }

    custom_scale_rule {
      name             = "quebec-operating-hours"
      custom_rule_type = "cron"

      metadata = {
        timezone        = "America/Toronto"
        start           = "0 7 * * *"
        end             = "59 23 * * *"
        desiredReplicas = "1"
      }
    }
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.worker_acr_pull,
    azurerm_role_assignment.worker_key_vault_secrets_user
  ]
}
