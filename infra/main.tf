locals {
  prefixo       = "${var.project}-${var.environment}"
  function_name = "${local.prefixo}-auth-cpf"
  cria_role     = var.lambda_role_arn == ""
  role_arn      = local.cria_role ? aws_iam_role.lambda[0].arn : var.lambda_role_arn
}

# ---------------------------------------------------------------------------
# Recursos compartilhados, criados pelos outros repositorios (contrato por nome/tag):
#   - VPC "<projeto>-<ambiente>-vpc" com subnets privadas (tag Tier=private)  -> infra Kubernetes
#   - Security group "<projeto>-<ambiente>-db-clients", liberado no RDS     -> infra banco
#   - Segredo "<projeto>/<ambiente>/db" (JSON host/port/dbname/username/password) -> infra banco
#   - Segredo "<projeto>/<ambiente>/jwt" (chave HS256 compartilhada com a API) -> infra Kubernetes
#   - API Gateway HTTP "<projeto>-<ambiente>-api"                             -> infra Kubernetes
# ---------------------------------------------------------------------------

data "aws_vpc" "this" {
  tags = { Name = "${local.prefixo}-vpc" }
}

data "aws_subnets" "privadas" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.this.id]
  }
  tags = { Tier = "private" }
}

data "aws_security_group" "db_clients" {
  vpc_id = data.aws_vpc.this.id
  name   = "${local.prefixo}-db-clients"
}

data "aws_secretsmanager_secret" "db" {
  name = "${var.project}/${var.environment}/db"
}

data "aws_secretsmanager_secret" "jwt" {
  name = "${var.project}/${var.environment}/jwt"
}

# --- IAM (somente quando nao for informada uma role existente) ---------------

resource "aws_iam_role" "lambda" {
  count = local.cria_role ? 1 : 0
  name  = "${local.function_name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

# Logs no CloudWatch + interfaces de rede para rodar dentro da VPC
resource "aws_iam_role_policy_attachment" "vpc_access" {
  count      = local.cria_role ? 1 : 0
  role       = aws_iam_role.lambda[0].name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# Leitura apenas dos dois segredos que a funcao usa
resource "aws_iam_role_policy" "segredos" {
  count = local.cria_role ? 1 : 0
  name  = "ler-segredos"
  role  = aws_iam_role.lambda[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["secretsmanager:GetSecretValue"]
      Resource = [data.aws_secretsmanager_secret.db.arn, data.aws_secretsmanager_secret.jwt.arn]
    }]
  })
}

# --- Lambda ------------------------------------------------------------------

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${local.function_name}"
  retention_in_days = var.log_retention_days
}

resource "aws_lambda_function" "auth_cpf" {
  function_name    = local.function_name
  description      = "Valida CPF, consulta o cliente e emite JWT para as APIs protegidas"
  role             = local.role_arn
  runtime          = "dotnet8"
  handler          = "Oficina.Auth.Lambda::Oficina.Auth.Lambda.Function::FunctionHandler"
  filename         = var.package_path
  source_code_hash = filebase64sha256(var.package_path)
  memory_size      = var.memory_size
  timeout          = var.timeout

  vpc_config {
    subnet_ids         = data.aws_subnets.privadas.ids
    security_group_ids = [data.aws_security_group.db_clients.id]
  }

  environment {
    variables = {
      DB_SECRET_ARN          = data.aws_secretsmanager_secret.db.arn
      JWT_SECRET_ARN         = data.aws_secretsmanager_secret.jwt.arn
      JWT_ISSUER             = var.jwt_issuer
      JWT_AUDIENCE           = var.jwt_audience
      JWT_EXPIRATION_MINUTES = tostring(var.jwt_expiration_minutes)
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda,
    aws_iam_role_policy_attachment.vpc_access,
    aws_iam_role_policy.segredos,
  ]
}

# --- Rota no API Gateway: POST /auth/cpf -> Lambda ---------------------------

data "aws_apigatewayv2_apis" "this" {
  count         = var.attach_to_api_gateway ? 1 : 0
  name          = "${local.prefixo}-api"
  protocol_type = "HTTP"
}

locals {
  api_id = var.attach_to_api_gateway ? one(data.aws_apigatewayv2_apis.this[0].ids) : null
}

data "aws_apigatewayv2_api" "this" {
  count  = var.attach_to_api_gateway ? 1 : 0
  api_id = local.api_id
}

resource "aws_apigatewayv2_integration" "auth_cpf" {
  count                  = var.attach_to_api_gateway ? 1 : 0
  api_id                 = local.api_id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.auth_cpf.invoke_arn
  payload_format_version = "2.0"
}

# Rotas publicas de autenticacao: cliente (CPF) e funcionario (usuario/senha), mesma funcao
resource "aws_apigatewayv2_route" "auth" {
  for_each  = var.attach_to_api_gateway ? toset(["POST /auth/cpf", "POST /auth/login"]) : toset([])
  api_id    = local.api_id
  route_key = each.value
  target    = "integrations/${aws_apigatewayv2_integration.auth_cpf[0].id}"
}

resource "aws_lambda_permission" "api_gateway" {
  count         = var.attach_to_api_gateway ? 1 : 0
  statement_id  = "AllowApiGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.auth_cpf.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${data.aws_apigatewayv2_api.this[0].execution_arn}/*/*/auth/*"
}

# Rate limit mais baixo nas rotas de autenticacao (protege contra forca bruta de CPF/senha).
# O stage pertence ao repositorio oficina-infra-k8s (que ignora route_settings); como a AWS so
# aceita limite em rota existente, ele e aplicado aqui, logo depois de criar as rotas.
locals {
  # {"POST /auth/cpf": {...}, "POST /auth/login": {...}} no formato do --route-settings da AWS CLI
  auth_route_settings = jsonencode({
    for rota in keys(aws_apigatewayv2_route.auth) : rota => {
      ThrottlingRateLimit  = var.auth_rate_limit
      ThrottlingBurstLimit = var.auth_rate_limit * 2
    }
  })
}

resource "terraform_data" "auth_throttling" {
  count            = var.attach_to_api_gateway ? 1 : 0
  triggers_replace = [values(aws_apigatewayv2_route.auth)[*].id, var.auth_rate_limit]

  provisioner "local-exec" {
    interpreter = ["bash", "-c"]
    command     = "aws apigatewayv2 update-stage --region ${var.aws_region} --api-id ${local.api_id} --stage-name '$default' --route-settings '${local.auth_route_settings}'"
  }
}

# --- Job de inativacao de clientes (EventBridge, diario) ---------------------

resource "aws_cloudwatch_log_group" "inativacao" {
  name              = "/aws/lambda/${local.prefixo}-inativacao-clientes"
  retention_in_days = var.log_retention_days
}

resource "aws_lambda_function" "inativacao_clientes" {
  function_name    = "${local.prefixo}-inativacao-clientes"
  description      = "Inativa clientes sem OS ha ${var.meses_inatividade} meses (politica de inatividade)"
  role             = local.role_arn
  runtime          = "dotnet8"
  handler          = "Oficina.Auth.Lambda::Oficina.Auth.Lambda.InativacaoClientesFunction::FunctionHandler"
  filename         = var.package_path
  source_code_hash = filebase64sha256(var.package_path)
  memory_size      = 256
  timeout          = 60

  vpc_config {
    subnet_ids         = data.aws_subnets.privadas.ids
    security_group_ids = [data.aws_security_group.db_clients.id]
  }

  environment {
    variables = {
      DB_SECRET_ARN     = data.aws_secretsmanager_secret.db.arn
      MESES_INATIVIDADE = tostring(var.meses_inatividade)
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.inativacao,
    aws_iam_role_policy_attachment.vpc_access,
    aws_iam_role_policy.segredos,
  ]
}

resource "aws_cloudwatch_event_rule" "inativacao_diaria" {
  name                = "${local.prefixo}-inativacao-clientes-diaria"
  description         = "Dispara a politica de inativacao de clientes"
  schedule_expression = var.inativacao_schedule
}

resource "aws_cloudwatch_event_target" "inativacao" {
  rule = aws_cloudwatch_event_rule.inativacao_diaria.name
  arn  = aws_lambda_function.inativacao_clientes.arn
}

resource "aws_lambda_permission" "eventbridge" {
  statement_id  = "AllowEventBridgeInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.inativacao_clientes.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.inativacao_diaria.arn
}
