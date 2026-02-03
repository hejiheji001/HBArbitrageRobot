# HBArbitrageRobot Terraform Infrastructure

This directory contains Terraform configuration for deploying HBArbitrageRobot to AWS Lambda with scheduled execution.

## Architecture

The Terraform configuration creates:

1. **ECR Repositories**: For storing Docker images
   - Server repository
   - Client repository

2. **Lambda Functions**: Serverless execution
   - Server Lambda (512 MB memory, 15 min timeout)
   - Client Lambda (1024 MB memory, 15 min timeout)

3. **Lambda Function URL**: HTTP endpoint for Server API
   - Accessible via public URL (optional IAM auth)
   - CORS enabled

4. **EventBridge Rules**: Scheduled execution
   - Server: Runs at 14:00 Shanghai time (Mon-Fri)
   - Client: Runs at 14:20 Shanghai time (Mon-Fri)

5. **CloudWatch Logs**: Logging and monitoring
   - Configurable retention period
   - Separate log groups for Server and Client

6. **IAM Roles**: Security and permissions
   - Lambda execution roles
   - Minimal required permissions

## Prerequisites

1. **Terraform**: Install Terraform >= 1.0
   ```bash
   # macOS
   brew install terraform
   
   # Linux
   wget https://releases.hashicorp.com/terraform/1.6.0/terraform_1.6.0_linux_amd64.zip
   unzip terraform_1.6.0_linux_amd64.zip
   sudo mv terraform /usr/local/bin/
   ```

2. **AWS CLI**: Configure AWS credentials
   ```bash
   aws configure
   ```
   
   Or set environment variables:
   ```bash
   export AWS_ACCESS_KEY_ID="your_access_key"
   export AWS_SECRET_ACCESS_KEY="your_secret_key"
   export AWS_DEFAULT_REGION="us-east-1"
   ```

3. **Docker**: For building and pushing images
   ```bash
   # Install Docker from https://docs.docker.com/get-docker/
   ```

## Quick Start

### 1. Configure Variables

Copy the example variables file:
```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` with your settings:
```hcl
aws_region   = "us-east-1"
project_name = "hbarbitrage"

touker_username             = "your_username"
touker_password             = "your_password"
touker_transaction_password = "your_transaction_password"

purchase_time  = "14:15"
max_investment = "1000"
```

**IMPORTANT**: Never commit `terraform.tfvars` to version control!

### 2. Initialize Terraform

```bash
cd terraform
terraform init
```

### 3. Review Infrastructure Plan

```bash
terraform plan
```

This shows what resources will be created.

### 4. Create Infrastructure

```bash
terraform apply
```

Type `yes` to confirm.

After completion, note the outputs:
```
ecr_server_repository_url = "123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-server"
ecr_client_repository_url = "123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-client"
server_api_url           = "https://abc123.lambda-url.us-east-1.on.aws/"
```

### 5. Build and Push Docker Images

```bash
# Navigate to project root
cd ..

# Get ECR login token
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789012.dkr.ecr.us-east-1.amazonaws.com

# Build and push Server image
docker build -t hbarbitrage-server:latest -f Server/Dockerfile .
docker tag hbarbitrage-server:latest 123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-server:latest
docker push 123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-server:latest

# Build and push Client image
docker build -t hbarbitrage-client:latest -f Client/Dockerfile .
docker tag hbarbitrage-client:latest 123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-client:latest
docker push 123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-client:latest
```

### 6. Update Lambda Functions

After pushing images, update Lambda functions to use the new images:

```bash
cd terraform

# Update Lambda functions to use latest images
aws lambda update-function-code \
  --function-name hbarbitrage-server \
  --image-uri 123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-server:latest

aws lambda update-function-code \
  --function-name hbarbitrage-client \
  --image-uri 123456789012.dkr.ecr.us-east-1.amazonaws.com/hbarbitrage-client:latest
```

### 7. Test Deployment

Test the Server API:
```bash
curl https://abc123.lambda-url.us-east-1.on.aws/api/data
```

Test manual invocation:
```bash
# Test Server Lambda
aws lambda invoke --function-name hbarbitrage-server output.json
cat output.json

# Test Client Lambda (will run arbitrage workflow)
aws lambda invoke --function-name hbarbitrage-client output.json
cat output.json
```

### 8. Monitor Logs

View CloudWatch logs:
```bash
# Server logs
aws logs tail /aws/lambda/hbarbitrage-server --follow

# Client logs
aws logs tail /aws/lambda/hbarbitrage-client --follow
```

## Deployment Script

For convenience, use the provided deployment script:

```bash
# From project root
./terraform/deploy.sh
```

This script:
1. Applies Terraform configuration
2. Builds Docker images
3. Pushes images to ECR
4. Updates Lambda functions
5. Shows deployment status

## Schedule Configuration

The Lambda functions run on the following schedule (Shanghai time, UTC+8):

- **Server**: 14:00 Mon-Fri (fetches LOF data)
- **Client**: 14:20 Mon-Fri (executes arbitrage)

To modify the schedule, edit the cron expressions in `main.tf`:

```hcl
# Server schedule (14:00 Shanghai = 06:00 UTC)
schedule_expression = "cron(0 6 ? * MON-FRI *)"

# Client schedule (14:20 Shanghai = 06:20 UTC)
schedule_expression = "cron(20 6 ? * MON-FRI *)"
```

**Cron format**: `cron(minute hour day month day-of-week year)`
- Times are in UTC
- Shanghai time = UTC + 8 hours

## Cost Estimation

AWS Lambda costs are based on:
- **Invocations**: $0.20 per 1M requests
- **Duration**: $0.0000166667 per GB-second

Estimated monthly cost (Mon-Fri execution):
- Server: ~22 invocations/month × 512 MB × 30s = ~$0.06/month
- Client: ~22 invocations/month × 1024 MB × 60s = ~$0.24/month
- CloudWatch Logs: ~$0.50/month
- **Total**: ~$0.80/month

**Note**: Client execution time may vary based on arbitrage operations.

## Security Considerations

1. **Credentials**: Stored in Lambda environment variables (encrypted at rest)
2. **API Access**: Lambda Function URL (optional IAM authentication)
3. **Network**: Lambda runs in AWS-managed VPC (no VPC required)
4. **Logs**: CloudWatch Logs (contains execution details)

### Enable API Authentication

To enable IAM authentication for Server API:

```hcl
# In terraform.tfvars
enable_api_auth = true
```

Then access the API with AWS Signature V4:
```bash
aws lambda invoke-url \
  --url https://abc123.lambda-url.us-east-1.on.aws/api/data \
  --output response.json
```

## Updating Configuration

To update Lambda environment variables:

1. Edit `terraform.tfvars`
2. Run `terraform apply`

To update Lambda code:

1. Build and push new Docker images
2. Run Lambda update commands (or deployment script)

## Cleanup

To destroy all resources:

```bash
cd terraform
terraform destroy
```

Type `yes` to confirm.

**Warning**: This will delete all resources including logs and ECR images.

## Troubleshooting

### Lambda Timeout

If Lambda times out (15 minutes max):
- Check CloudWatch Logs for errors
- Increase timeout in `main.tf`
- Optimize application code

### Out of Memory

If Lambda runs out of memory:
- Increase memory in `main.tf`
- Monitor memory usage in CloudWatch metrics

### Schedule Not Triggering

If EventBridge schedule not triggering:
- Verify schedule expression is correct
- Check Lambda permissions
- View EventBridge rule metrics

### Image Pull Errors

If Lambda cannot pull ECR image:
- Verify image exists in ECR
- Check IAM permissions
- Ensure Lambda is in same region as ECR

## Additional Resources

- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [AWS Lambda Documentation](https://docs.aws.amazon.com/lambda/)
- [EventBridge Cron Expressions](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-create-rule-schedule.html)
- [AWS Lambda Function URLs](https://docs.aws.amazon.com/lambda/latest/dg/lambda-urls.html)
