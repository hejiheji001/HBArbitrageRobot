# AWS Lambda Deployment Guide

## Overview

This guide explains how to deploy the HBArbitrageServer to AWS Lambda.

## Do I Need Terraform?

**Answer: No, you don't need Terraform** for deploying just the Server to AWS Lambda.

### Why Terraform is Optional

The GitHub Actions workflow `deploy-lambda-server.yml` handles everything automatically:
- Creates ECR repository (if needed)
- Builds and pushes Docker image
- Creates Lambda function (if needed)
- Updates Lambda function code
- Configures all settings

**Terraform adds complexity** and is only beneficial if you need:
- Infrastructure versioning
- Multi-environment management (dev, staging, prod)
- Complex infrastructure with many resources
- Infrastructure drift detection

For a simple Lambda deployment, **the direct GitHub Actions approach is simpler and sufficient**.

## Deployment Method

### Container Image Deployment (Recommended)

The workflow uses Docker container images, which:
- ✅ Uses your existing Dockerfile (no code changes needed)
- ✅ Works with .NET 9 (current version)
- ✅ Automatically handles all dependencies
- ✅ Easier to test locally with Docker

## Setup Instructions

### 1. Configure AWS Credentials

Add these secrets to your GitHub repository (Settings → Secrets and variables → Actions):

- `AWS_ACCESS_KEY_ID` - Your AWS access key
- `AWS_SECRET_ACCESS_KEY` - Your AWS secret key

The workflow automatically uses the Tokyo region (`ap-northeast-1`).

### 2. Deploy

The workflow runs automatically when:
- You push changes to `main` or `master` branch that affect the `Server/` directory
- You manually trigger it via "Actions" tab → "Deploy Server to AWS Lambda" → "Run workflow"

### 3. First Deployment

On the first deployment, the workflow will:
1. Create an ECR repository named `hbarbitrageserver`
2. Create an IAM role with Lambda execution permissions
3. Create the Lambda function `HBArbitrageServer`
4. Create a Function URL for HTTP access (no authentication)

### 4. Subsequent Deployments

On later deployments, the workflow will:
1. Build and push a new Docker image
2. Update the Lambda function with the new image
3. Display the Function URL in the workflow summary

## Lambda Function Configuration

The Lambda function is configured with:

- **Function Name:** `HBArbitrageServer`
- **Region:** `ap-northeast-1` (Tokyo)
- **Runtime:** Container Image (.NET 9)
- **Architecture:** x86_64
- **Memory:** 512 MB
- **Timeout:** 900 seconds (15 minutes)
- **Environment Variables:**
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ASPNETCORE_URLS=http://+:8080`

## Accessing the Server

After deployment, you'll get a Function URL like:
```
https://xxxxxxxxxxxxx.lambda-url.ap-northeast-1.on.aws/
```

Test it with:
```bash
curl https://your-function-url.lambda-url.ap-northeast-1.on.aws/api/data/lof
```

## Monitoring

View logs in AWS CloudWatch:
1. Go to AWS Console → CloudWatch → Log Groups
2. Find `/aws/lambda/HBArbitrageServer`
3. View log streams for function invocations

## Cost Estimate

AWS Lambda pricing (Tokyo region):
- **Requests:** $0.20 per 1M requests
- **Duration:** $0.0000166667 per GB-second
- **Free Tier:** 1M requests and 400,000 GB-seconds per month

For a server with occasional API calls, monthly cost should be **minimal or free**.

## Removing Terraform (Optional)

If you want to remove the Terraform infrastructure:

1. **Keep the new workflow:**
   - `.github/workflows/deploy-lambda-server.yml` (new, simple deployment)

2. **Remove Terraform files:**
   - Delete `terraform/` directory
   - Delete `.github/workflows/terraform-deploy.yml` (old Terraform workflow)

3. **Benefits of removal:**
   - Simpler repository structure
   - No need to maintain Terraform state
   - Faster deployments
   - One less tool to learn

## Troubleshooting

### Function not responding
- Check CloudWatch logs for errors
- Verify Function URL is configured
- Ensure security groups allow outbound traffic (if in VPC)

### Deployment fails
- Verify AWS credentials are correct
- Check IAM permissions for ECR, Lambda, and IAM operations
- Review GitHub Actions workflow logs

### Out of memory
- Increase memory size in the workflow (currently 512 MB)
- Edit the `--memory-size` parameter in `deploy-lambda-server.yml`

## Next Steps

After deployment, you can:
1. Set up CloudWatch alarms for monitoring
2. Configure custom domain with API Gateway (optional)
3. Add authentication to Function URL (if needed)
4. Set up scheduled invocations with EventBridge

## Support

For issues or questions:
- Check CloudWatch logs
- Review GitHub Actions workflow logs
- Consult AWS Lambda documentation
