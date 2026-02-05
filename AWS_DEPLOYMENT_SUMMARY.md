# AWS Serverless Deployment Summary

This document provides a summary of the AWS serverless deployment setup for HBArbitrageRobot.

## Overview

HBArbitrageRobot can now be deployed to AWS Lambda serverless infrastructure in the **ap-northeast-1** (Tokyo, Asia Pacific) region using:

1. **Terraform** - Infrastructure as Code for AWS resources
2. **GitHub Actions** - Automated CI/CD workflow
3. **Manual Scripts** - Alternative deployment method

## What Was Created

### 1. Terraform Configuration (`terraform/` directory)

**Main Infrastructure** (`main.tf`):
- ECR repositories for Docker images (Server and Client)
- Lambda functions (Server: 512MB, Client: 1024MB)
- Lambda Function URL for Server API
- EventBridge scheduled triggers (14:00 and 14:20 Shanghai time)
- CloudWatch Log Groups with configurable retention
- IAM roles with minimal required permissions
- ECR lifecycle policies to manage image retention

**Configuration** (`variables.tf`):
- Default region: `ap-northeast-1` (Tokyo, Asia)
- Configurable parameters for credentials, schedules, and resources
- Sensitive variable handling for credentials

**Example Config** (`terraform.tfvars.example`):
- Template with ap-northeast-1 as default region
- All required variables documented
- Security best practices included

### 2. GitHub Actions Workflow (`.github/workflows/terraform-deploy.yml`)

**Features**:
- ✅ Automated deployment on push to main/master branch
- ✅ Manual deployment via workflow_dispatch
- ✅ Terraform plan/apply/destroy operations
- ✅ Docker image build and push to ECR
- ✅ Lambda function updates
- ✅ Deployment testing and validation
- ✅ Deployment summary in GitHub UI

**Triggers**:
- Push to main/master when `terraform/`, `Server/`, or workflow file changes
- Manual trigger from GitHub Actions UI with action choice (plan/apply/destroy)

**Required GitHub Secrets**:
- `AWS_ACCESS_KEY_ID` - AWS access key
- `AWS_SECRET_ACCESS_KEY` - AWS secret key
- `TOUKER_USERNAME` - touker.com username
- `TOUKER_PASSWORD` - touker.com password
- `TOUKER_TRANSACTION_PASSWORD` - Transaction password

**Workflow Steps**:
1. Terraform job: Initialize, validate, plan, and apply infrastructure
2. Build job: Build Docker images and push to ECR
3. Deploy job: Update Lambda functions with new images
4. Test job: Validate deployment with API tests
5. Summary job: Display deployment information

### 3. Documentation

**New Files**:
- `GITHUB_ACTIONS_WORKFLOW.md` - Complete guide for GitHub Actions workflow
  - Setup instructions
  - Usage examples
  - Troubleshooting guide
  - Security best practices
  - Cost management tips

**Updated Files**:
- `README.md` - Added GitHub Actions workflow reference and ap-northeast-1 region
- `QUICKSTART_AWS.md` - Added GitHub Actions deployment option as recommended method
- `DEPLOYMENT_AWS.md` - Updated all region references and added workflow section
- `terraform/README.md` - Added GitHub Actions deployment method as recommended

## Deployment Methods

### Method 1: GitHub Actions (Recommended)

**Advantages**:
- ✅ No local tools required (Terraform, Docker)
- ✅ Fully automated from GitHub
- ✅ Consistent environment
- ✅ Built-in CI/CD
- ✅ Easy rollback
- ✅ Deployment history

**Setup**:
1. Configure GitHub Secrets
2. Push to main or trigger manually
3. Monitor in Actions tab

**Use Cases**:
- Production deployments
- Team collaboration
- Automated updates
- No local development environment

### Method 2: Local Script (deploy.sh)

**Advantages**:
- ✅ Full control over process
- ✅ Interactive prompts
- ✅ Step-by-step execution
- ✅ Local testing

**Setup**:
1. Install Terraform, AWS CLI, Docker
2. Configure terraform.tfvars
3. Run ./deploy.sh

**Use Cases**:
- Development and testing
- One-time deployments
- Debugging issues
- Learning Terraform

### Method 3: Manual Terraform

**Advantages**:
- ✅ Maximum control
- ✅ Custom workflows
- ✅ Integration with other tools

**Setup**:
1. Install Terraform, AWS CLI, Docker
2. Run terraform init/plan/apply manually
3. Build and push images manually
4. Update Lambda functions manually

**Use Cases**:
- Custom CI/CD pipelines
- Advanced Terraform usage
- Specific deployment requirements

## AWS Resources Created

| Resource | Type | Purpose | Cost |
|----------|------|---------|------|
| Server Lambda | AWS Lambda | API endpoint and data fetching | ~$0.01/month |
| Client Lambda | AWS Lambda | Arbitrage execution | ~$0.02/month |
| Server ECR | Amazon ECR | Docker image storage | ~$0.05/month |
| Client ECR | Amazon ECR | Docker image storage | ~$0.05/month |
| Server Logs | CloudWatch Logs | Monitoring | ~$0.25/month |
| Client Logs | CloudWatch Logs | Monitoring | ~$0.25/month |
| Server Schedule | EventBridge | Trigger at 14:00 | Free |
| Client Schedule | EventBridge | Trigger at 14:20 | Free |
| Lambda URL | Lambda Function URL | Public API endpoint | Free |
| IAM Roles | AWS IAM | Permissions | Free |

**Total Monthly Cost**: ~$0.63/month

## Region Selection: ap-northeast-1

**Why Tokyo (ap-northeast-1)?**

1. **Geographic Proximity**: Closer to China (Shanghai stock market)
2. **Low Latency**: Faster API calls to jisilu.cn and touker.com
3. **Stable Infrastructure**: Mature AWS region with good service availability
4. **Similar Timezone**: UTC+9 (close to Shanghai UTC+8)
5. **Cost Effective**: Competitive pricing similar to us-east-1

**Alternative Regions**:
- `ap-southeast-1` (Singapore) - Also in Asia, slightly farther
- `ap-northeast-3` (Osaka) - Japan backup region
- `ap-east-1` (Hong Kong) - Closest geographically, but higher costs

## Schedule Configuration

The system runs on Shanghai trading days (Monday-Friday):

| Component | Schedule | UTC Time | Shanghai Time | Purpose |
|-----------|----------|----------|---------------|---------|
| Server | `cron(0 6 ? * MON-FRI *)` | 06:00 | 14:00 | Fetch LOF data |
| Client | `cron(20 6 ? * MON-FRI *)` | 06:20 | 14:20 | Execute arbitrage |

**Schedule Format**: AWS EventBridge uses cron expressions in UTC timezone.

**Customization**: Edit `terraform/main.tf` to change schedules.

## Security Considerations

### Credentials Management

1. **GitHub Secrets**: Encrypted at rest, only accessible to workflow
2. **AWS Secrets**: Never committed to repository
3. **Terraform Variables**: Marked as sensitive where applicable
4. **Lambda Environment**: Variables encrypted at rest

### IAM Permissions

Minimal required permissions:
- Lambda execution role: CloudWatch Logs only
- Deployment user: Lambda, ECR, CloudWatch, EventBridge, IAM

### Network Security

- Lambda Function URL: Public (required for API access)
- ECR: Private (requires AWS authentication)
- Lambda: Runs in AWS managed VPC

### Best Practices

1. ✅ Use GitHub Secrets for all credentials
2. ✅ Enable MFA on AWS account
3. ✅ Rotate AWS access keys regularly
4. ✅ Review CloudWatch logs for suspicious activity
5. ✅ Use environment protection rules for production
6. ✅ Enable AWS CloudTrail for audit logs

## Monitoring and Maintenance

### CloudWatch Logs

**Server Logs**: `/aws/lambda/hbarbitrage-server`
- Data fetch operations
- API requests
- Errors and warnings

**Client Logs**: `/aws/lambda/hbarbitrage-client`
- Login attempts
- Order creation
- Automation workflow

**Retention**: 7 days (configurable in terraform/variables.tf)

### Metrics

**Lambda Metrics**:
- Invocations
- Duration
- Errors
- Throttles
- Memory usage

**EventBridge Metrics**:
- Successful invocations
- Failed invocations
- Throttled events

### Alerts

Recommended CloudWatch alarms:
1. Lambda errors > 0
2. Lambda duration > threshold
3. Lambda throttles > 0
4. EventBridge failed invocations > 0

### Updates

**Code Updates**:
1. Push changes to main branch (automatic)
2. Or manually trigger workflow

**Infrastructure Updates**:
1. Edit terraform/ files
2. Push changes to main branch
3. Terraform will apply changes

**Dependency Updates**:
1. Update Dockerfile
2. Push changes to main branch
3. New images built and deployed

## Testing

### Local Testing

Before deploying:
1. Test Server locally: `dotnet run --project Server`
2. Test Client locally: `dotnet run --project Client`
3. Build Docker images: `docker build -f Server/Dockerfile .`

### Deployment Testing

After deploying:
1. Test Server API: `curl $SERVER_API_URL/api/data`
2. Manual invoke: `aws lambda invoke --function-name hbarbitrage-server`
3. Check logs: `aws logs tail /aws/lambda/hbarbitrage-server`

### Integration Testing

Full workflow test:
1. Wait for Server schedule trigger (14:00 Shanghai time)
2. Check CloudWatch logs for data fetch
3. Wait for Client schedule trigger (14:20 Shanghai time)
4. Check CloudWatch logs for order creation

## Troubleshooting

### Common Issues

1. **Workflow fails with AWS auth error**
   - Check GitHub Secrets are set correctly
   - Verify AWS IAM user has required permissions

2. **Terraform apply fails**
   - Check terraform state is not corrupted
   - Verify AWS region is valid
   - Check resource limits (Lambda, ECR)

3. **Docker build fails**
   - Check Dockerfile syntax
   - Verify base images are accessible
   - Check for syntax errors in code

4. **Lambda update fails**
   - Verify image was pushed to ECR
   - Check Lambda function exists
   - Verify image size < 10 GB

5. **API returns 5xx errors**
   - Check Lambda execution logs
   - Verify environment variables
   - Check Lambda timeout setting

### Getting Help

- Review workflow logs in GitHub Actions
- Check CloudWatch logs in AWS console
- See [GITHUB_ACTIONS_WORKFLOW.md](GITHUB_ACTIONS_WORKFLOW.md) for detailed troubleshooting
- See [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) for infrastructure issues

## Cost Optimization

### Tips to Reduce Costs

1. **Reduce log retention**: Set to 3 days in `terraform/variables.tf`
2. **Right-size memory**: Monitor usage and reduce if possible
3. **Clean up old images**: ECR lifecycle policy handles this
4. **Use AWS Free Tier**: Available for 12 months for new accounts

### Cost Monitoring

Set up billing alerts:
1. Go to AWS Console → Billing → Budgets
2. Create budget for $5/month
3. Set alerts at 80% and 100%

## Rollback Procedure

If deployment has issues:

1. **Rollback code**:
   ```bash
   git revert HEAD
   git push origin main
   ```

2. **Rollback infrastructure**:
   ```bash
   cd terraform
   terraform plan -destroy
   terraform destroy  # Remove everything
   # Or restore previous state
   ```

3. **Rollback Lambda images**:
   ```bash
   aws lambda update-function-code \
     --function-name hbarbitrage-server \
     --image-uri <previous-image>
   ```

## Next Steps

1. ✅ Review and test the GitHub Actions workflow
2. ✅ Configure GitHub Secrets
3. ✅ Trigger a test deployment
4. ✅ Monitor the first execution
5. ✅ Set up CloudWatch alarms
6. ✅ Configure Android client for monitoring
7. ✅ Document any custom configurations

## Related Documentation

- [GITHUB_ACTIONS_WORKFLOW.md](GITHUB_ACTIONS_WORKFLOW.md) - Workflow guide
- [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) - Detailed AWS deployment
- [QUICKSTART_AWS.md](QUICKSTART_AWS.md) - Quick start guide
- [terraform/README.md](terraform/README.md) - Terraform documentation
- [README.md](README.md) - Main project documentation

---

**Last Updated**: 2026-02-05
**Infrastructure Version**: 1.0
**Terraform Version**: 1.6.0
**AWS Region**: ap-northeast-1 (Tokyo, Asia Pacific)
