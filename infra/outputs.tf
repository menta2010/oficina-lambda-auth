output "function_name" {
  value = aws_lambda_function.auth_cpf.function_name
}

output "function_arn" {
  value = aws_lambda_function.auth_cpf.arn
}

output "auth_cpf_url" {
  description = "Autenticacao do cliente por CPF (quando ligado ao API Gateway)"
  value       = var.attach_to_api_gateway ? "${data.aws_apigatewayv2_api.this[0].api_endpoint}/auth/cpf" : null
}

output "auth_login_url" {
  description = "Login de funcionario (quando ligado ao API Gateway)"
  value       = var.attach_to_api_gateway ? "${data.aws_apigatewayv2_api.this[0].api_endpoint}/auth/login" : null
}

output "inativacao_function_name" {
  value = aws_lambda_function.inativacao_clientes.function_name
}
