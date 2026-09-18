output "resource_group_name" {
  value       = azurerm_resource_group.main.name
  description = "Development resource group."
}

output "container_registry_name" {
  value       = azurerm_container_registry.main.name
  description = "Container registry name used by az acr login."
}

output "container_registry_login_server" {
  value       = azurerm_container_registry.main.login_server
  description = "Container registry login server."
}

output "worker_image_repository" {
  value       = "${azurerm_container_registry.main.login_server}/${var.worker_image_repository}"
  description = "Repository to which the worker image must be pushed."
}

output "postgresql_server_name" {
  value       = azurerm_postgresql_flexible_server.main.name
  description = "PostgreSQL Flexible Server name."
}

output "postgresql_fqdn" {
  value       = azurerm_postgresql_flexible_server.main.fqdn
  description = "Private PostgreSQL server FQDN."
}

output "key_vault_name" {
  value       = azurerm_key_vault.main.name
  description = "Key Vault containing the worker database connection."
}

output "worker_container_app_name" {
  value       = var.deploy_worker ? azurerm_container_app.worker[0].name : null
  description = "Worker Container App name when deployment is enabled."
}
