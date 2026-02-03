output "ecr_server_repository_url" {
  description = "ECR repository URL for Server"
  value       = aws_ecr_repository.server.repository_url
}

output "ecr_client_repository_url" {
  description = "ECR repository URL for Client"
  value       = aws_ecr_repository.client.repository_url
}

output "server_lambda_function_name" {
  description = "Server Lambda function name"
  value       = aws_lambda_function.server.function_name
}

output "client_lambda_function_name" {
  description = "Client Lambda function name"
  value       = aws_lambda_function.client.function_name
}

output "server_api_url" {
  description = "Server API URL (Lambda Function URL)"
  value       = aws_lambda_function_url.server.function_url
}

output "server_schedule" {
  description = "Server schedule (EventBridge rule)"
  value       = aws_cloudwatch_event_rule.server_schedule.schedule_expression
}

output "client_schedule" {
  description = "Client schedule (EventBridge rule)"
  value       = aws_cloudwatch_event_rule.client_schedule.schedule_expression
}

output "server_log_group" {
  description = "CloudWatch Log Group for Server"
  value       = aws_cloudwatch_log_group.server.name
}

output "client_log_group" {
  description = "CloudWatch Log Group for Client"
  value       = aws_cloudwatch_log_group.client.name
}
