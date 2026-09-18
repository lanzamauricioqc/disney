resource "azurerm_key_vault" "main" {
  name                       = local.key_vault_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true

  purge_protection_enabled        = false
  soft_delete_retention_days      = 7
  public_network_access_enabled   = true
  enabled_for_template_deployment = false

  network_acls {
    bypass         = "AzureServices"
    default_action = "Allow"
  }

  tags = local.tags
}

resource "azurerm_role_assignment" "terraform_key_vault_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "worker_key_vault_secrets_user" {
  scope                            = azurerm_key_vault.main.id
  role_definition_name             = "Key Vault Secrets User"
  principal_id                     = azurerm_user_assigned_identity.worker.principal_id
  skip_service_principal_aad_check = true
}

resource "time_sleep" "key_vault_role_propagation" {
  create_duration = "60s"

  depends_on = [
    azurerm_role_assignment.terraform_key_vault_secrets_officer,
    azurerm_role_assignment.worker_key_vault_secrets_user
  ]
}

resource "azurerm_key_vault_secret" "worker_database_connection" {
  name             = "worker-database-connection"
  value_wo         = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.queue_times.name};Username=${var.postgresql_administrator_login};Pwd=\"${local.postgresql_connection_password}\";SSL Mode=VerifyFull;Trust Server Certificate=false"
  value_wo_version = var.postgresql_password_version
  key_vault_id     = azurerm_key_vault.main.id
  content_type     = "PostgreSQL connection string"
  tags             = local.tags

  depends_on = [time_sleep.key_vault_role_propagation]
}
