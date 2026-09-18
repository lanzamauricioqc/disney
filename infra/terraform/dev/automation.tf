resource "azurerm_automation_account" "main" {
  name                          = "aa-${local.prefix}"
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  sku_name                      = "Basic"
  local_authentication_enabled  = false
  public_network_access_enabled = true
  tags                          = local.tags

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_role_assignment" "automation_postgresql_contributor" {
  scope                            = azurerm_postgresql_flexible_server.main.id
  role_definition_name             = "Contributor"
  principal_id                     = azurerm_automation_account.main.identity[0].principal_id
  skip_service_principal_aad_check = true
}

resource "azurerm_automation_runbook" "manage_postgresql" {
  name                    = "Manage-DisneyPostgreSQLServer"
  location                = azurerm_resource_group.main.location
  resource_group_name     = azurerm_resource_group.main.name
  automation_account_name = azurerm_automation_account.main.name
  runbook_type            = "PowerShell72"
  log_verbose             = false
  log_progress            = false
  description             = "Starts or stops the Disney PostgreSQL Flexible Server using managed identity."
  content                 = file("${path.module}/runbooks/Manage-PostgreSQLFlexibleServer.ps1")
  tags                    = local.tags
}

resource "azurerm_automation_schedule" "start_postgresql" {
  count = var.enable_database_schedule ? 1 : 0

  name                    = "start-postgresql-0650-eastern"
  resource_group_name     = azurerm_resource_group.main.name
  automation_account_name = azurerm_automation_account.main.name
  frequency               = "Day"
  interval                = 1
  timezone                = "America/Toronto"
  start_time              = var.database_start_schedule_start_time
  description             = "Starts PostgreSQL daily at 06:50 Quebec City time."

  lifecycle {
    ignore_changes = [start_time]
  }
}

resource "azurerm_automation_schedule" "stop_postgresql" {
  count = var.enable_database_schedule ? 1 : 0

  name                    = "stop-postgresql-0010-eastern"
  resource_group_name     = azurerm_resource_group.main.name
  automation_account_name = azurerm_automation_account.main.name
  frequency               = "Day"
  interval                = 1
  timezone                = "America/Toronto"
  start_time              = var.database_stop_schedule_start_time
  description             = "Stops PostgreSQL daily at 00:10 Quebec City time."

  lifecycle {
    ignore_changes = [start_time]
  }
}

resource "azurerm_automation_job_schedule" "start_postgresql" {
  count = var.enable_database_schedule ? 1 : 0

  resource_group_name     = azurerm_resource_group.main.name
  automation_account_name = azurerm_automation_account.main.name
  schedule_name           = azurerm_automation_schedule.start_postgresql[0].name
  runbook_name            = azurerm_automation_runbook.manage_postgresql.name

  parameters = {
    operation  = "start"
    resourceid = azurerm_postgresql_flexible_server.main.id
  }
}

resource "azurerm_automation_job_schedule" "stop_postgresql" {
  count = var.enable_database_schedule ? 1 : 0

  resource_group_name     = azurerm_resource_group.main.name
  automation_account_name = azurerm_automation_account.main.name
  schedule_name           = azurerm_automation_schedule.stop_postgresql[0].name
  runbook_name            = azurerm_automation_runbook.manage_postgresql.name

  parameters = {
    operation  = "stop"
    resourceid = azurerm_postgresql_flexible_server.main.id
  }
}
