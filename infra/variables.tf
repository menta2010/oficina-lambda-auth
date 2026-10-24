variable "aws_region" {
  description = "Regiao AWS"
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Prefixo comum a todos os recursos do projeto"
  type        = string
  default     = "oficina"
}

variable "environment" {
  description = "Ambiente: homolog ou prod"
  type        = string

  validation {
    condition     = contains(["homolog", "prod"], var.environment)
    error_message = "environment deve ser homolog ou prod."
  }
}

variable "package_path" {
  description = "Caminho do zip gerado por `dotnet lambda package`"
  type        = string
  default     = "../artifact/function.zip"
}

variable "lambda_role_arn" {
  description = "Role existente para a Lambda (ex.: LabRole no AWS Academy). Vazio = o Terraform cria a role."
  type        = string
  default     = ""
}

variable "attach_to_api_gateway" {
  description = "Cria a rota POST /auth/cpf no API Gateway do ambiente (criado pelo repositorio de infra Kubernetes)"
  type        = bool
  default     = true
}

variable "auth_rate_limit" {
  description = "Requisicoes por segundo nas rotas /auth/* (rajada = 2x)"
  type        = number
  default     = 5
}

variable "jwt_issuer" {
  type    = string
  default = "oficina-api"
}

variable "jwt_audience" {
  type    = string
  default = "oficina-clients"
}

variable "jwt_expiration_minutes" {
  type    = number
  default = 60
}

variable "memory_size" {
  type    = number
  default = 512
}

variable "timeout" {
  type    = number
  default = 15
}

variable "meses_inatividade" {
  description = "Meses sem abrir OS para o cliente ser inativado"
  type        = number
  default     = 24
}

variable "inativacao_schedule" {
  description = "Agendamento do job de inativacao (EventBridge). Padrao: todo dia as 06:00 UTC"
  type        = string
  default     = "cron(0 6 * * ? *)"
}

variable "log_retention_days" {
  type    = number
  default = 7
}
