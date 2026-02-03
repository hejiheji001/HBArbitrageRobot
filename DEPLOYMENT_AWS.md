# AWS Serverless Deployment Guide

This guide explains how to deploy HBArbitrageRobot to AWS Lambda for serverless execution during trading hours (14:00-15:00 Shanghai time, Mon-Fri).

## Table of Contents
- [Overview](#overview)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Deployment Steps](#deployment-steps)
- [Configuration](#configuration)
- [Monitoring](#monitoring)
- [Cost Analysis](#cost-analysis)
- [Troubleshooting](#troubleshooting)

## Overview

The AWS serverless deployment uses:
- **AWS Lambda**: Serverless compute for Server and Client components
- **Amazon ECR**: Container registry for Docker images
- **EventBridge**: Scheduled execution during trading hours
- **Lambda Function URLs**: HTTP endpoint for Server API
- **CloudWatch Logs**: Logging and monitoring

### Why Serverless?

✅ **Cost Effective**: Only pay for execution time (~$0.80/month)
✅ **Auto Scaling**: Handles load automatically
✅ **Scheduled**: Runs only during trading hours (Mon-Fri 14:00-15:00 Shanghai time)
✅ **Managed**: No server maintenance required
✅ **Reliable**: AWS-managed infrastructure

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     AWS Cloud                                │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  EventBridge (Scheduler)                              │  │
│  │  - Server: cron(0 6 ? * MON-FRI *)  → 14:00 Shanghai │  │
│  │  - Client: cron(20 6 ? * MON-FRI *) → 14:20 Shanghai │  │
│  └────────┬──────────────────────────┬──────────────────┘  │
│           │                          │                      │
│           ▼                          ▼                      │
│  ┌─────────────────┐       ┌─────────────────┐            │
│  │ Server Lambda   │◄──────│ Client Lambda   │            │
│  │ (512 MB)        │  API  │ (1024 MB)       │            │
│  │                 │       │                 │            │
│  │ - Fetch LOF     │       │ - Login touker  │            │
│  │ - Filter funds  │       │ - Create orders │            │
│  │ - Cache data    │       │ - Automation    │            │
│  └────────┬────────┘       └─────────────────┘            │
│           │                                                 │
│           │ Lambda Function URL                             │
│           │ (https://xxx.lambda-url.us-east-1.on.aws/)    │
│           │                                                 │
│  ┌────────▼────────┐       ┌─────────────────┐            │
│  │ CloudWatch Logs │       │ ECR Repositories │            │
│  │ - Server logs   │       │ - Server image   │            │
│  │ - Client logs   │       │ - Client image   │            │
│  └─────────────────┘       └─────────────────┘            │
│                                                              │
└─────────────────────────────────────────────────────────────┘
       │                                    │
       ▼ (14:00)                            ▼ (14:20)
  jisilu.cn API                        touker.com API
```

## Prerequisites

### 1. AWS Account
- Create an AWS account at https://aws.amazon.com
- Note your Account ID (12-digit number)

### 2. Install Required Tools

#### Terraform
```bash
# macOS
brew install terraform

# Linux
wget https://releases.hashicorp.com/terraform/1.6.0/terraform_1.6.0_linux_amd64.zip
unzip terraform_1.6.0_linux_amd64.zip
sudo mv terraform /usr/local/bin/

# Windows (using Chocolatey)
choco install terraform

# Verify installation
terraform version
```

#### AWS CLI
```bash
# macOS
brew install awscli

# Linux
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# Windows (using MSI installer)
# Download from: https://aws.amazon.com/cli/

# Verify installation
aws --version
```

#### Docker
```bash
# Install from: https://docs.docker.com/get-docker/
docker --version
```

### 3. Configure AWS Credentials

Option A: Interactive configuration
```bash
aws configure
```
Enter:
- AWS Access Key ID
- AWS Secret Access Key
- Default region (e.g., us-east-1)
- Default output format: json

Option B: Environment variables
```bash
export AWS_ACCESS_KEY_ID="your_access_key"
export AWS_SECRET_ACCESS_KEY="your_secret_key"
export AWS_DEFAULT_REGION="us-east-1"
```

### 4. Create IAM User (if needed)

1. Go to AWS Console → IAM → Users
2. Create new user with programmatic access
3. Attach policies:
   - `AWSLambda_FullAccess`
   - `AmazonEC2ContainerRegistryFullAccess`
   - `CloudWatchLogsFullAccess`
   - `IAMFullAccess` (for creating roles)
   - `AmazonEventBridgeFullAccess`
4. Save Access Key ID and Secret Access Key

## Deployment Steps

### Step 1: Clone Repository
```bash
git clone https://github.com/hejiheji001/HBArbitrageRobot.git
cd HBArbitrageRobot
```

### Step 2: Configure Terraform Variables
```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars`:
```hcl
# AWS Configuration
aws_region   = "us-east-1"  # Choose your region
project_name = "hbarbitrage"

# Docker Image Tag
image_tag = "latest"

# Touker.com Credentials (REQUIRED)
touker_username             = "your_touker_username"
touker_password             = "your_touker_password"
touker_transaction_password = "your_transaction_password"

# Trading Configuration
purchase_time  = "14:15"  # HH:MM format (Shanghai time)
max_investment = "1000"   # Yuan per stock holder per fund

# API Security (optional)
enable_api_auth = false  # Set true for IAM authentication

# Logging
log_retention_days = 7  # Days to keep CloudWatch logs

# Tags
tags = {
  Project     = "HBArbitrageRobot"
  Environment = "Production"
  ManagedBy   = "Terraform"
}
```

**⚠️ IMPORTANT**: Never commit `terraform.tfvars` to version control!

### Step 3: Deploy with Automated Script

Use the provided deployment script for easy deployment:
```bash
./deploy.sh
```

The script will:
1. ✓ Check prerequisites (Terraform, AWS CLI, Docker)
2. ✓ Initialize Terraform
3. ✓ Show infrastructure changes
4. ✓ Create AWS resources (after confirmation)
5. ✓ Build Docker images
6. ✓ Push images to ECR
7. ✓ Update Lambda functions
8. ✓ Test deployment

### Step 4: Manual Deployment (Alternative)

If you prefer manual control:

#### 4.1. Initialize Terraform
```bash
terraform init
```

#### 4.2. Review Changes
```bash
terraform plan
```

#### 4.3. Apply Configuration
```bash
terraform apply
```
Type `yes` to confirm.

#### 4.4. Get ECR URLs
```bash
SERVER_ECR=$(terraform output -raw ecr_server_repository_url)
CLIENT_ECR=$(terraform output -raw ecr_client_repository_url)
echo "Server ECR: $SERVER_ECR"
echo "Client ECR: $CLIENT_ECR"
```

#### 4.5. Login to ECR
```bash
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin $(echo $SERVER_ECR | cut -d'/' -f1)
```

#### 4.6. Build and Push Images
```bash
cd ..  # Back to project root

# Build Server
docker build -t hbarbitrage-server:latest -f Server/Dockerfile .
docker tag hbarbitrage-server:latest $SERVER_ECR:latest
docker push $SERVER_ECR:latest

# Build Client
docker build -t hbarbitrage-client:latest -f Client/Dockerfile .
docker tag hbarbitrage-client:latest $CLIENT_ECR:latest
docker push $CLIENT_ECR:latest
```

#### 4.7. Update Lambda Functions
```bash
aws lambda update-function-code \
  --function-name hbarbitrage-server \
  --image-uri $SERVER_ECR:latest

aws lambda update-function-code \
  --function-name hbarbitrage-client \
  --image-uri $CLIENT_ECR:latest
```

### Step 5: Verify Deployment

#### Test Server API
```bash
SERVER_URL=$(cd terraform && terraform output -raw server_api_url)
curl ${SERVER_URL}api/data
```

Expected response:
- `200 OK`: Data available (array of LOF funds)
- `404 Not Found`: Data fetch in progress
- `[]`: No qualifying funds today

#### Test Manual Invocation
```bash
# Invoke Server Lambda
aws lambda invoke --function-name hbarbitrage-server output.json
cat output.json

# Invoke Client Lambda (will execute arbitrage workflow)
aws lambda invoke --function-name hbarbitrage-client output.json
cat output.json
```

#### View Logs
```bash
# Server logs
aws logs tail /aws/lambda/hbarbitrage-server --follow

# Client logs
aws logs tail /aws/lambda/hbarbitrage-client --follow
```

## Configuration

### Modifying Schedule

To change execution times, edit `terraform/main.tf`:

```hcl
# Server schedule (14:00 Shanghai time = 06:00 UTC)
resource "aws_cloudwatch_event_rule" "server_schedule" {
  schedule_expression = "cron(0 6 ? * MON-FRI *)"
}

# Client schedule (14:20 Shanghai time = 06:20 UTC)
resource "aws_cloudwatch_event_rule" "client_schedule" {
  schedule_expression = "cron(20 6 ? * MON-FRI *)"
}
```

**Cron Format**: `cron(minute hour day month day-of-week year)`
- Times in UTC
- Shanghai time = UTC + 8 hours
- Example: 15:30 Shanghai = 07:30 UTC → `cron(30 7 ? * MON-FRI *)`

After changes:
```bash
cd terraform
terraform apply
```

### Updating Credentials

1. Edit `terraform/terraform.tfvars`
2. Apply changes: `terraform apply`

Credentials are encrypted at rest in Lambda environment variables.

### Adjusting Resources

Edit `terraform/main.tf`:

```hcl
# Server Lambda memory (MB)
resource "aws_lambda_function" "server" {
  memory_size = 512   # Increase if needed
  timeout     = 900   # Max 15 minutes
}

# Client Lambda memory (MB)
resource "aws_lambda_function" "client" {
  memory_size = 1024  # Increase if Playwright needs more
  timeout     = 900
}
```

## Monitoring

### CloudWatch Logs

View logs in AWS Console:
1. Go to CloudWatch → Logs → Log groups
2. Select:
   - `/aws/lambda/hbarbitrage-server`
   - `/aws/lambda/hbarbitrage-client`

Or via CLI:
```bash
# Real-time logs
aws logs tail /aws/lambda/hbarbitrage-server --follow

# Recent logs
aws logs tail /aws/lambda/hbarbitrage-server --since 1h

# Filter logs
aws logs filter-log-events \
  --log-group-name /aws/lambda/hbarbitrage-server \
  --filter-pattern "ERROR"
```

### Lambda Metrics

View metrics in AWS Console:
1. Go to Lambda → Functions → [function-name]
2. Select "Monitor" tab
3. View:
   - Invocations
   - Duration
   - Errors
   - Throttles
   - Memory usage

### EventBridge Monitoring

Check schedule execution:
1. Go to EventBridge → Rules
2. Select rule
3. View "Invocations" and "Failed invocations"

### Setting Up Alarms

Create CloudWatch alarm for errors:
```bash
aws cloudwatch put-metric-alarm \
  --alarm-name hbarbitrage-client-errors \
  --alarm-description "Alert on Client Lambda errors" \
  --metric-name Errors \
  --namespace AWS/Lambda \
  --statistic Sum \
  --period 300 \
  --evaluation-periods 1 \
  --threshold 1 \
  --comparison-operator GreaterThanThreshold \
  --dimensions Name=FunctionName,Value=hbarbitrage-client \
  --alarm-actions arn:aws:sns:us-east-1:123456789012:my-topic
```

## Cost Analysis

### Pricing (us-east-1 region)

**Lambda**:
- Requests: $0.20 per 1M requests
- Duration: $0.0000166667 per GB-second
- Free tier: 1M requests + 400,000 GB-seconds/month

**ECR**:
- Storage: $0.10 per GB/month
- First 50 GB free for 12 months

**CloudWatch Logs**:
- Ingestion: $0.50 per GB
- Storage: $0.03 per GB/month
- First 5 GB free

### Monthly Cost Estimate

**Usage** (Mon-Fri, ~22 trading days/month):
- Server: 22 invocations × 30 seconds × 512 MB
- Client: 22 invocations × 60 seconds × 1024 MB

**Calculation**:
```
Server Lambda:
  22 requests × $0.0000002 = $0.0000044
  22 × 30s × 0.5 GB × $0.0000166667 = $0.0055
  Subtotal: ~$0.01

Client Lambda:
  22 requests × $0.0000002 = $0.0000044
  22 × 60s × 1 GB × $0.0000166667 = $0.022
  Subtotal: ~$0.02

ECR Storage (2 images × 500 MB): ~$0.10
CloudWatch Logs (~100 MB/month): ~$0.50

Total: ~$0.63/month
```

**Note**: Actual costs may vary based on:
- Execution time (Client depends on arbitrage operations)
- Log volume
- AWS region
- Free tier eligibility

### Cost Optimization Tips

1. **Reduce Log Volume**: Decrease log retention period
   ```hcl
   log_retention_days = 3  # In terraform/variables.tf
   ```

2. **Right-size Memory**: Monitor CloudWatch metrics and adjust
3. **Use Free Tier**: New accounts get 12 months free tier
4. **Regional Pricing**: Some regions are cheaper (check AWS pricing)

## Troubleshooting

### Issue: Lambda Timeout (15 min limit)

**Symptoms**: Function times out
**Solution**:
1. Check CloudWatch Logs for bottlenecks
2. Optimize slow operations
3. Increase timeout (if under 15 min):
   ```hcl
   timeout = 900  # seconds
   ```

### Issue: Out of Memory

**Symptoms**: "Runtime exited with error: signal: killed"
**Solution**:
1. Check memory usage in CloudWatch metrics
2. Increase memory allocation:
   ```hcl
   memory_size = 2048  # MB
   ```

### Issue: Image Pull Error

**Symptoms**: "RepositoryNotFoundException" or pull errors
**Solution**:
1. Verify image exists in ECR:
   ```bash
   aws ecr describe-images --repository-name hbarbitrage-server
   ```
2. Check image tag:
   ```bash
   terraform output ecr_server_repository_url
   ```
3. Re-push image if missing

### Issue: Schedule Not Triggering

**Symptoms**: Lambda not invoked at scheduled time
**Solution**:
1. Check EventBridge rule is enabled:
   ```bash
   aws events describe-rule --name hbarbitrage-server-schedule
   ```
2. Verify cron expression (UTC, not Shanghai time!)
3. Check Lambda permissions:
   ```bash
   aws lambda get-policy --function-name hbarbitrage-server
   ```

### Issue: API Returns 502/504

**Symptoms**: Lambda Function URL returns gateway errors
**Solution**:
1. Cold start: First request after idle takes longer
2. Check Lambda logs for errors
3. Increase timeout if needed
4. Warm up with periodic health checks

### Issue: Credentials Not Working

**Symptoms**: Authentication errors in Client logs
**Solution**:
1. Verify credentials in `terraform.tfvars`
2. Apply changes: `terraform apply`
3. Check environment variables:
   ```bash
   aws lambda get-function-configuration --function-name hbarbitrage-client
   ```

### Issue: High Costs

**Symptoms**: Unexpected AWS bill
**Solution**:
1. Check CloudWatch Logs cost:
   ```bash
   aws cloudwatch get-metric-statistics \
     --namespace AWS/Logs \
     --metric-name IncomingBytes \
     --dimensions Name=LogGroupName,Value=/aws/lambda/hbarbitrage-client \
     --start-time 2024-01-01T00:00:00Z \
     --end-time 2024-01-31T23:59:59Z \
     --period 86400 \
     --statistics Sum
   ```
2. Reduce log retention: `log_retention_days = 3`
3. Review Lambda execution times
4. Check for unexpected invocations

### Getting Help

1. **CloudWatch Logs**: First place to check for errors
2. **AWS Support**: Free basic support plan
3. **GitHub Issues**: Report bugs or ask questions
4. **AWS Forums**: Community help

## Cleanup

To remove all AWS resources:

```bash
cd terraform
terraform destroy
```

Type `yes` to confirm.

**⚠️ Warning**: This will delete:
- Lambda functions
- ECR repositories (and all images)
- CloudWatch Log groups (and all logs)
- EventBridge rules
- IAM roles

**Before destroying**:
- Export important logs
- Backup any data
- Verify no active trading positions

## Next Steps

- ✅ Deploy Server and Client to AWS Lambda
- ✅ Set up monitoring and alarms
- ✅ Configure mobile client (see [DEPLOYMENT_ANDROID.md](DEPLOYMENT_ANDROID.md))
- ✅ Test end-to-end workflow
- ✅ Monitor execution logs
- ✅ Optimize costs

## Additional Resources

- [Terraform AWS Provider Documentation](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [AWS Lambda Documentation](https://docs.aws.amazon.com/lambda/)
- [EventBridge Scheduling](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-create-rule-schedule.html)
- [Lambda Function URLs](https://docs.aws.amazon.com/lambda/latest/dg/lambda-urls.html)
- [ECR User Guide](https://docs.aws.amazon.com/ecr/)
- [CloudWatch Logs](https://docs.aws.amazon.com/cloudwatch/latest/logs/)
