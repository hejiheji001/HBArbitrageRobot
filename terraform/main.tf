# HBArbitrageRobot AWS Serverless Deployment
# Terraform configuration for deploying Server and Client to AWS Lambda

terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

# ECR Repository for Server
resource "aws_ecr_repository" "server" {
  name                 = "${var.project_name}-server"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = var.tags
}

# ECR Repository for Client
resource "aws_ecr_repository" "client" {
  name                 = "${var.project_name}-client"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = var.tags
}

# ECR Lifecycle Policy for Server
resource "aws_ecr_lifecycle_policy" "server" {
  repository = aws_ecr_repository.server.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep last 10 images"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = {
        type = "expire"
      }
    }]
  })
}

# ECR Lifecycle Policy for Client
resource "aws_ecr_lifecycle_policy" "client" {
  repository = aws_ecr_repository.client.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep last 10 images"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = {
        type = "expire"
      }
    }]
  })
}

# IAM Role for Server Lambda
resource "aws_iam_role" "server_lambda" {
  name = "${var.project_name}-server-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "lambda.amazonaws.com"
      }
    }]
  })

  tags = var.tags
}

# IAM Role Policy for Server Lambda
resource "aws_iam_role_policy_attachment" "server_lambda_basic" {
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
  role       = aws_iam_role.server_lambda.name
}

# IAM Role for Client Lambda
resource "aws_iam_role" "client_lambda" {
  name = "${var.project_name}-client-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "lambda.amazonaws.com"
      }
    }]
  })

  tags = var.tags
}

# IAM Role Policy for Client Lambda
resource "aws_iam_role_policy_attachment" "client_lambda_basic" {
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
  role       = aws_iam_role.client_lambda.name
}

# Lambda Function for Server
resource "aws_lambda_function" "server" {
  function_name = "${var.project_name}-server"
  role          = aws_iam_role.server_lambda.arn
  package_type  = "Image"
  image_uri     = "${aws_ecr_repository.server.repository_url}:${var.image_tag}"

  timeout     = 900 # 15 minutes max
  memory_size = 512 # MB

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT = "Production"
      ASPNETCORE_URLS        = "http://+:8080"
    }
  }

  tags = var.tags

  # Prevent destroy if Lambda has active triggers
  lifecycle {
    ignore_changes = [image_uri]
  }
}

# Lambda Function URL for Server (serverless HTTP endpoint)
resource "aws_lambda_function_url" "server" {
  function_name      = aws_lambda_function.server.function_name
  authorization_type = var.enable_api_auth ? "AWS_IAM" : "NONE"

  cors {
    allow_origins  = ["*"]
    allow_methods  = ["GET", "POST"]
    allow_headers  = ["*"]
    expose_headers = ["*"]
    max_age        = 3600
  }
}

# Lambda Function for Client
resource "aws_lambda_function" "client" {
  function_name = "${var.project_name}-client"
  role          = aws_iam_role.client_lambda.arn
  package_type  = "Image"
  image_uri     = "${aws_ecr_repository.client.repository_url}:${var.image_tag}"

  timeout     = 900  # 15 minutes max
  memory_size = 1024 # MB (Playwright needs more memory)

  environment {
    variables = {
      USERNAME            = var.touker_username
      PASSWORD            = var.touker_password
      PWD                 = var.touker_transaction_password
      PURCHASE_TIME       = var.purchase_time
      API_URL             = aws_lambda_function_url.server.function_url
      MAX_INVESTMENT      = var.max_investment
      HEADLESS            = "true"
      RETRY_DELAY_SECONDS = "60"
    }
  }

  tags = var.tags

  lifecycle {
    ignore_changes = [image_uri]
  }
}

# EventBridge Rule for Server (runs daily at 14:00 Shanghai time)
# Cron expression: cron(0 6 ? * MON-FRI *) - 6 AM UTC = 14:00 Shanghai (UTC+8)
resource "aws_cloudwatch_event_rule" "server_schedule" {
  name                = "${var.project_name}-server-schedule"
  description         = "Trigger server Lambda at 14:00 Shanghai time (Mon-Fri)"
  schedule_expression = "cron(0 6 ? * MON-FRI *)"

  tags = var.tags
}

# EventBridge Target for Server
resource "aws_cloudwatch_event_target" "server_schedule" {
  rule      = aws_cloudwatch_event_rule.server_schedule.name
  target_id = "ServerLambda"
  arn       = aws_lambda_function.server.arn
}

# Permission for EventBridge to invoke Server Lambda
resource "aws_lambda_permission" "server_schedule" {
  statement_id  = "AllowExecutionFromEventBridge"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.server.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.server_schedule.arn
}

# EventBridge Rule for Client (runs daily at 14:20 Shanghai time)
# Cron expression: cron(20 6 ? * MON-FRI *) - 6:20 AM UTC = 14:20 Shanghai (UTC+8)
resource "aws_cloudwatch_event_rule" "client_schedule" {
  name                = "${var.project_name}-client-schedule"
  description         = "Trigger client Lambda at 14:20 Shanghai time (Mon-Fri)"
  schedule_expression = "cron(20 6 ? * MON-FRI *)"

  tags = var.tags
}

# EventBridge Target for Client
resource "aws_cloudwatch_event_target" "client_schedule" {
  rule      = aws_cloudwatch_event_rule.client_schedule.name
  target_id = "ClientLambda"
  arn       = aws_lambda_function.client.arn
}

# Permission for EventBridge to invoke Client Lambda
resource "aws_lambda_permission" "client_schedule" {
  statement_id  = "AllowExecutionFromEventBridge"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.client.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.client_schedule.arn
}

# CloudWatch Log Group for Server
resource "aws_cloudwatch_log_group" "server" {
  name              = "/aws/lambda/${aws_lambda_function.server.function_name}"
  retention_in_days = var.log_retention_days

  tags = var.tags
}

# CloudWatch Log Group for Client
resource "aws_cloudwatch_log_group" "client" {
  name              = "/aws/lambda/${aws_lambda_function.client.function_name}"
  retention_in_days = var.log_retention_days

  tags = var.tags
}
