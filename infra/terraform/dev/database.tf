resource "azurerm_postgresql_flexible_server" "main" {
  name                = local.postgresql_server_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  version             = "17"
  sku_name            = "B_Standard_B1ms"

  delegated_subnet_id           = azurerm_subnet.postgresql.id
  private_dns_zone_id           = azurerm_private_dns_zone.postgresql.id
  public_network_access_enabled = false

  administrator_login               = var.postgresql_administrator_login
  administrator_password_wo         = var.postgresql_administrator_password
  administrator_password_wo_version = var.postgresql_password_version

  storage_mb        = 32768
  storage_tier      = "P4"
  auto_grow_enabled = true

  backup_retention_days        = 7
  geo_redundant_backup_enabled = false

  authentication {
    active_directory_auth_enabled = false
    password_auth_enabled         = true
  }

  maintenance_window {
    day_of_week  = 0
    start_hour   = 6
    start_minute = 0
  }

  tags = local.tags

  depends_on = [
    azurerm_private_dns_zone_virtual_network_link.postgresql
  ]
}

resource "azurerm_postgresql_flexible_server_database" "queue_times" {
  name      = "queue_times"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_management_lock" "postgresql" {
  count = var.enable_database_delete_lock ? 1 : 0

  name       = "prevent-accidental-database-deletion"
  scope      = azurerm_postgresql_flexible_server.main.id
  lock_level = "CanNotDelete"
  notes      = "Remove this lock intentionally before replacing or deleting PostgreSQL."
}
