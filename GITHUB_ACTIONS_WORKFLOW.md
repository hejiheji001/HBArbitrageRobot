# GitHub Actions Terraform Deployment Workflow

This document describes the GitHub Actions workflow for automating AWS serverless deployments with Terraform.

## Overview

The `terraform-deploy.yml` workflow automates the complete deployment process:
1. Creates AWS infrastructure with Terraform
2. Builds Docker images for Server and Client
3. Pushes images to Amazon ECR
4. Updates Lambda functions with new images
5. Tests the deployment

## Workflow Features

- ✅ **Automated Deployments**: Triggers on push to main/master branch
- ✅ **Manual Deployments**: Trigger via GitHub Actions UI
- ✅ **Secure**: Uses GitHub Secrets for credentials
- ✅ **Regional**: Deploys to ap-northeast-1 (Tokyo, Asia Pacific)
- ✅ **Testing**: Validates deployment with API tests
- ✅ **Summary**: Provides deployment summary in GitHub UI

## Prerequisites

Before using the workflow, you need to:

### 1. Set up AWS Credentials

Create an IAM user with appropriate permissions:

Required policies:
- `AWSLambda_FullAccess`
- `AmazonEC2ContainerRegistryFullAccess`
- `CloudWatchLogsFullAccess`
- `IAMFullAccess` (for creating roles)
- `AmazonEventBridgeFullAccess`

### 2. Configure GitHub Secrets

Go to your repository → **Settings** → **Secrets and variables** → **Actions**

Add the following secrets:

| Secret Name | Description | Example |
|------------|-------------|---------|
| `AWS_ACCESS_KEY_ID` | AWS access key ID | `AKIAIOSFODNN7EXAMPLE` |
| `AWS_SECRET_ACCESS_KEY` | AWS secret access key | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` |
| `TOUKER_USERNAME` | touker.com username | `your_username` |
| `TOUKER_PASSWORD` | touker.com password | `your_password` |
| `TOUKER_TRANSACTION_PASSWORD` | Transaction password | `your_txn_password` |

**Important**: Never commit these values to your repository!

### 3. Optional: Configure Environment Protection

For production deployments, you can set up environment protection:

1. Go to **Settings** → **Environments**
2. Create a `production` environment
3. Add protection rules:
   - Required reviewers
   - Wait timer
   - Deployment branches (limit to main/master)

## Usage

### Automatic Deployment

The workflow runs automatically when you push changes to the main branch that affect:
- `terraform/**` - Terraform configuration
- `Server/**` - Server code
- `.github/workflows/terraform-deploy.yml` - The workflow itself

**Example**:
```bash
# Make changes to your code
git add .
git commit -m "Update server configuration"
git push origin main
```

The workflow will:
1. Detect changes
2. Run Terraform to update infrastructure
3. Build and deploy updated Docker images
4. Update Lambda functions

### Manual Deployment

You can manually trigger the workflow from GitHub:

1. Go to your repository on GitHub
2. Click the **Actions** tab
3. Select **"Deploy to AWS with Terraform"** workflow
4. Click **"Run workflow"** button
5. Select action:
   - **plan**: Show changes without applying
   - **apply**: Deploy infrastructure
   - **destroy**: Remove all AWS resources
6. Click **"Run workflow"**

### Initial Deployment

For the first deployment:

1. **Set up GitHub Secrets** (see above)
2. **Create terraform.tfvars** locally (for testing):
   ```bash
   cd terraform
   cp terraform.tfvars.example terraform.tfvars
   # Edit with your values
   ```
3. **Trigger workflow manually** with action `apply`
4. **Wait for completion** (typically 5-10 minutes)
5. **Check deployment summary** in the workflow output

### Updating Configuration

To update Lambda environment variables or other settings:

1. Edit Terraform configuration files in `terraform/`
2. Commit and push changes:
   ```bash
   git add terraform/
   git commit -m "Update Lambda configuration"
   git push origin main
   ```
3. Workflow will automatically apply changes

### Updating Code

To deploy code changes:

1. Edit Server or Client code
2. Commit and push:
   ```bash
   git add Server/ Client/
   git commit -m "Update application code"
   git push origin main
   ```
3. Workflow will rebuild and redeploy Docker images

## Workflow Jobs

### Job 1: Terraform

This job manages AWS infrastructure:

**Steps**:
1. **Checkout**: Clone repository
2. **Configure AWS**: Set up credentials
3. **Setup Terraform**: Install Terraform CLI
4. **Format Check**: Verify Terraform formatting
5. **Init**: Initialize Terraform backend
6. **Validate**: Validate configuration syntax
7. **Plan**: Show infrastructure changes
8. **Apply/Destroy**: Create or destroy resources (based on trigger)

**Runs**: On all workflow triggers

### Job 2: Build and Deploy

This job builds and deploys Docker images:

**Steps**:
1. **Checkout**: Clone repository
2. **Configure AWS**: Set up credentials
3. **Login to ECR**: Authenticate with Amazon ECR
4. **Get ECR URLs**: Retrieve repository URLs from Terraform
5. **Setup Docker**: Configure Docker Buildx
6. **Build Server**: Build Server Docker image
7. **Push Server**: Push image to ECR
8. **Build Client**: Build Client Docker image
9. **Push Client**: Push image to ECR
10. **Update Lambdas**: Update Lambda functions with new images
11. **Test**: Test Server API endpoint
12. **Summary**: Display deployment summary

**Runs**: Only after successful Terraform apply

## Workflow Outputs

After successful deployment, the workflow provides:

### GitHub Actions Summary

A summary is displayed in the workflow run, showing:
- ✅ Deployment status
- **Resources created**:
  - Server Lambda function name
  - Client Lambda function name
  - Server API URL
  - AWS region
- **Schedules**:
  - Server: Daily at 14:00 Shanghai time (Mon-Fri)
  - Client: Daily at 14:20 Shanghai time (Mon-Fri)

### Terraform Outputs

Access outputs using Terraform CLI:
```bash
cd terraform
terraform output server_api_url
terraform output ecr_server_repository_url
terraform output ecr_client_repository_url
```

Or view in workflow logs under "Get Terraform Outputs" step.

## Monitoring

### View Workflow Status

1. Go to **Actions** tab in GitHub
2. See list of workflow runs with status:
   - 🟢 Success
   - 🔴 Failure
   - 🟡 In Progress
3. Click a run to view detailed logs

### Check Lambda Logs

View logs in AWS CloudWatch:
```bash
# Server logs
aws logs tail /aws/lambda/hbarbitrage-server --follow --region ap-northeast-1

# Client logs
aws logs tail /aws/lambda/hbarbitrage-client --follow --region ap-northeast-1
```

### Test Deployment

Test the Server API:
```bash
# Get API URL from Terraform outputs
cd terraform
SERVER_URL=$(terraform output -raw server_api_url)

# Test endpoint
curl ${SERVER_URL}api/data
```

Expected responses:
- `200 OK`: Data available
- `404 Not Found`: Data fetch in progress
- `[]`: No qualifying funds today

## Troubleshooting

### Workflow Fails: "Terraform Init Failed"

**Cause**: Backend configuration issue

**Solution**:
1. Check AWS credentials in GitHub Secrets
2. Verify IAM permissions
3. Run locally: `cd terraform && terraform init`

### Workflow Fails: "AWS Authentication Failed"

**Cause**: Invalid or expired credentials

**Solution**:
1. Verify `AWS_ACCESS_KEY_ID` in GitHub Secrets
2. Verify `AWS_SECRET_ACCESS_KEY` in GitHub Secrets
3. Check IAM user has required policies
4. Generate new access keys if needed

### Workflow Fails: "Docker Build Failed"

**Cause**: Build errors in Server or Client code

**Solution**:
1. Check workflow logs for error details
2. Test build locally:
   ```bash
   docker build -f Server/Dockerfile .
   docker build -f Client/Dockerfile .
   ```
3. Fix code issues and push again

### Workflow Fails: "Lambda Update Failed"

**Cause**: Lambda function doesn't exist or image too large

**Solution**:
1. Ensure Terraform apply completed successfully
2. Check Lambda function exists:
   ```bash
   aws lambda get-function --function-name hbarbitrage-server --region ap-northeast-1
   ```
3. Check image size (Lambda limit: 10 GB)

### API Returns 502/504

**Cause**: Lambda cold start or timeout

**Solution**:
1. Wait a few seconds and retry (cold start)
2. Check Lambda timeout in `terraform/main.tf` (default: 900s)
3. View CloudWatch logs for errors

### Changes Not Deploying

**Cause**: Workflow not triggered or failed

**Solution**:
1. Check **Actions** tab for workflow status
2. Verify changes were pushed to main/master branch
3. Check workflow path filters match changed files
4. Manually trigger workflow if needed

## Advanced Configuration

### Customize Region

To deploy to a different AWS region:

1. Edit `terraform/variables.tf`:
   ```hcl
   variable "aws_region" {
     default = "us-west-2"  # or your preferred region
   }
   ```

2. Update workflow `.github/workflows/terraform-deploy.yml`:
   ```yaml
   env:
     AWS_REGION: us-west-2
   ```

3. Commit and push changes

### Customize Schedule

To change Lambda execution times, edit `terraform/main.tf`:

```hcl
# Server schedule (14:00 Shanghai = 06:00 UTC)
resource "aws_cloudwatch_event_rule" "server_schedule" {
  schedule_expression = "cron(0 6 ? * MON-FRI *)"
}

# Client schedule (14:20 Shanghai = 06:20 UTC)
resource "aws_cloudwatch_event_rule" "client_schedule" {
  schedule_expression = "cron(20 6 ? * MON-FRI *)"
}
```

**Note**: Times are in UTC. Shanghai = UTC + 8 hours.

### Add Environment-Specific Configurations

To support multiple environments (dev/staging/prod):

1. Create separate `.tfvars` files:
   - `terraform.dev.tfvars`
   - `terraform.staging.tfvars`
   - `terraform.prod.tfvars`

2. Update workflow to use appropriate file:
   ```yaml
   - name: Terraform Plan
     run: |
       terraform plan -var-file="terraform.prod.tfvars"
   ```

3. Add environment-specific secrets in GitHub

## Security Best Practices

1. **Never commit secrets**: Always use GitHub Secrets
2. **Use IAM roles**: Consider using OIDC for GitHub Actions
3. **Limit permissions**: Use minimum required IAM permissions
4. **Enable MFA**: Require MFA for production deployments
5. **Review changes**: Use environment protection rules
6. **Rotate credentials**: Regularly rotate AWS access keys
7. **Monitor access**: Enable AWS CloudTrail logging

## Cost Management

### Estimated Costs

**Monthly** (based on 22 trading days):
- Lambda execution: ~$0.03
- ECR storage: ~$0.10
- CloudWatch Logs: ~$0.50
- **Total: ~$0.63/month**

### Cost Optimization

1. **Reduce log retention**: 
   ```hcl
   log_retention_days = 3
   ```

2. **Right-size memory**:
   Monitor CloudWatch metrics and adjust:
   ```hcl
   memory_size = 512  # Reduce if possible
   ```

3. **Delete unused images**:
   ECR lifecycle policy automatically keeps last 10 images

4. **Use AWS Free Tier**:
   New accounts get 12 months of free tier

## Support

### Getting Help

1. **Workflow logs**: Check GitHub Actions logs for errors
2. **CloudWatch logs**: View Lambda execution logs
3. **Terraform docs**: https://registry.terraform.io/providers/hashicorp/aws/latest/docs
4. **GitHub Issues**: Report bugs or ask questions

### Useful Commands

```bash
# View workflow runs
gh run list --workflow=terraform-deploy.yml

# View specific run logs
gh run view <run-id>

# Rerun failed workflow
gh run rerun <run-id>

# List Lambda functions
aws lambda list-functions --region ap-northeast-1

# Get Lambda configuration
aws lambda get-function-configuration \
  --function-name hbarbitrage-server \
  --region ap-northeast-1

# View CloudWatch logs
aws logs tail /aws/lambda/hbarbitrage-server \
  --follow \
  --region ap-northeast-1
```

## Related Documentation

- [DEPLOYMENT_AWS.md](../DEPLOYMENT_AWS.md) - Comprehensive AWS deployment guide
- [terraform/README.md](../terraform/README.md) - Terraform configuration guide
- [QUICKSTART_AWS.md](../QUICKSTART_AWS.md) - Quick start guide
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Terraform Documentation](https://www.terraform.io/docs)
