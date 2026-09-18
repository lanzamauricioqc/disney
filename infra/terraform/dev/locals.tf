locals {
  environment = "dev"
  prefix      = "disney-${local.environment}"

  container_registry_name = "disneydev${var.name_suffix}"
  key_vault_name          = "kv-disney-dev-${var.name_suffix}"
  postgresql_server_name  = "psql-disney-dev-${var.name_suffix}"

  postgresql_connection_password = replace(
    var.postgresql_administrator_password,
    "\"",
    "\"\""
  )

  worker_image = var.worker_image_tag == null ? null : join(
    ":",
    [
      "${azurerm_container_registry.main.login_server}/${var.worker_image_repository}",
      var.worker_image_tag
    ]
  )

  tags = {
    environment = local.environment
    application = "disney"
    managed-by  = "terraform"
  }
}
