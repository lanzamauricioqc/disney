variable "subscription_id" {
  type        = string
  description = "Azure subscription ID in which the development resources are created."
}

variable "location" {
  type        = string
  description = "Azure region for the development resources."
  default     = "canadaeast"
}

variable "resource_group_name" {
  type        = string
  description = "Development resource group name."
  default     = "disney-dev"
}

variable "name_suffix" {
  type        = string
  description = "Lowercase alphanumeric suffix used to make globally scoped resource names unique."

  validation {
    condition     = can(regex("^[a-z0-9]{3,8}$", var.name_suffix))
    error_message = "name_suffix must contain between 3 and 8 lowercase letters or digits."
  }
}

variable "postgresql_administrator_login" {
  type        = string
  description = "PostgreSQL administrator login."
  default     = "disneyadmin"
}

variable "postgresql_administrator_password" {
  type        = string
  description = "PostgreSQL administrator password. Supply it through TF_VAR_postgresql_administrator_password."
  sensitive   = true
  ephemeral   = true

  validation {
    condition     = length(var.postgresql_administrator_password) >= 12
    error_message = "postgresql_administrator_password must contain at least 12 characters."
  }
}

variable "postgresql_password_version" {
  type        = number
  description = "Increment this value when rotating the PostgreSQL administrator password."
  default     = 1
}

variable "worker_image_repository" {
  type        = string
  description = "Repository name inside Azure Container Registry."
  default     = "worker"
}

variable "worker_image_tag" {
  type        = string
  description = "Immutable worker image tag, normally a Git commit SHA."
  default     = null
  nullable    = true
}

variable "deploy_worker" {
  type        = bool
  description = "Deploy the worker after its image has been pushed to the registry."
  default     = false
}

variable "enable_database_schedule" {
  type        = bool
  description = "Enable the daily PostgreSQL start and stop schedules after manual lifecycle verification."
  default     = false
}

variable "database_start_schedule_start_time" {
  type        = string
  description = "First 06:50 Eastern execution in RFC3339 format and at least five minutes in the future."
  default     = null
  nullable    = true
}

variable "database_stop_schedule_start_time" {
  type        = string
  description = "First 00:10 Eastern execution in RFC3339 format and at least five minutes in the future."
  default     = null
  nullable    = true
}

variable "enable_database_delete_lock" {
  type        = bool
  description = "Protect the PostgreSQL server from accidental deletion."
  default     = false
}

check "worker_image_tag" {
  assert {
    condition     = !var.deploy_worker || var.worker_image_tag != null
    error_message = "worker_image_tag must be set when deploy_worker is true."
  }
}

check "database_schedule_start_times" {
  assert {
    condition = (
      !var.enable_database_schedule ||
      (
        var.database_start_schedule_start_time != null &&
        var.database_stop_schedule_start_time != null
      )
    )
    error_message = "Both database schedule start times must be set when enable_database_schedule is true."
  }
}
