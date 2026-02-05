variable "aws_region" {
  description = "AWS region for deployment"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "hbarbitrage"
}

variable "image_tag" {
  description = "Docker image tag to deploy"
  type        = string
  default     = "latest"
}

variable "touker_username" {
  description = "Touker.com login username"
  type        = string
  sensitive   = true
}

variable "touker_password" {
  description = "Touker.com login password"
  type        = string
  sensitive   = true
}

variable "touker_transaction_password" {
  description = "Touker.com transaction password"
  type        = string
  sensitive   = true
}

variable "purchase_time" {
  description = "Purchase time in HH:MM format (Shanghai time)"
  type        = string
  default     = "14:15"
}

variable "max_investment" {
  description = "Maximum investment per stock holder per fund (in CNY)"
  type        = string
  default     = "1000"
}

variable "enable_api_auth" {
  description = "Enable IAM authentication for Server API"
  type        = bool
  default     = false
}

variable "log_retention_days" {
  description = "CloudWatch Logs retention period in days"
  type        = number
  default     = 7
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default = {
    Project     = "HBArbitrageRobot"
    Environment = "Production"
    ManagedBy   = "Terraform"
  }
}
