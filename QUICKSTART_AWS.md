# Quick Start Guide for AWS Serverless Deployment

This is a quick reference for deploying HBArbitrageRobot to AWS Lambda in **ap-northeast-1** (Tokyo, Asia Pacific). For detailed instructions, see [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md).

## Prerequisites Checklist

- [ ] AWS Account created
- [ ] AWS CLI installed and configured (region: ap-northeast-1)
- [ ] Terraform installed (v1.0+) - OR use GitHub Actions
- [ ] Docker installed - OR use GitHub Actions
- [ ] Touker.com credentials ready

## Deployment Options

### Option 1: GitHub Actions (Recommended - Easiest)

**Zero setup required** - Deploy directly from GitHub!

1. **Configure GitHub Secrets** (Settings → Secrets → Actions):
   - `AWS_ACCESS_KEY_ID`
   - `AWS_SECRET_ACCESS_KEY`
   - `TOUKER_USERNAME`
   - `TOUKER_PASSWORD`
   - `TOUKER_TRANSACTION_PASSWORD`

2. **Trigger Deployment**:
   - Push to main branch, OR
   - Go to Actions → "Deploy to AWS with Terraform" → "Run workflow"

3. **Done!** Check Actions tab for deployment status

📖 **Full Guide**: [GITHUB_ACTIONS_WORKFLOW.md](GITHUB_ACTIONS_WORKFLOW.md)

### Option 2: Local Deployment (5 Minutes)

1. **Configure**:
```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your credentials
```

2. **Deploy**:
```bash
./deploy.sh
```

That's it! The script will:
- ✓ Create AWS infrastructure
- ✓ Build Docker images
- ✓ Deploy to Lambda
- ✓ Set up scheduled execution

### 3. Verify
```bash
# Test Server API
curl https://your-lambda-url.lambda-url.ap-northeast-1.on.aws/api/data

# View logs
aws logs tail /aws/lambda/hbarbitrage-server --follow --region ap-northeast-1
```

## What Gets Created

- **Region**: ap-northeast-1 (Tokyo, Asia Pacific)
- **2 Lambda Functions**: Server (512MB) + Client (1024MB)
- **2 ECR Repositories**: For Docker images
- **2 EventBridge Rules**: Scheduled execution (Mon-Fri 14:00 & 14:20 Shanghai time)
- **1 Lambda Function URL**: Public API endpoint for Server
- **2 CloudWatch Log Groups**: For monitoring
- **2 IAM Roles**: Lambda execution permissions

## Cost

Approximately **$0.80/month** for:
- Lambda executions (22 days/month)
- ECR storage (2 images)
- CloudWatch Logs (7 days retention)

## Schedule

- **Server**: Runs at 14:00 Shanghai time (Mon-Fri) - fetches LOF data
- **Client**: Runs at 14:20 Shanghai time (Mon-Fri) - executes arbitrage

## Monitoring

```bash
# View Server logs
aws logs tail /aws/lambda/hbarbitrage-server --follow --region ap-northeast-1

# View Client logs
aws logs tail /aws/lambda/hbarbitrage-client --follow --region ap-northeast-1

# Manual invocation (for testing)
aws lambda invoke --function-name hbarbitrage-server --region ap-northeast-1 output.json
aws lambda invoke --function-name hbarbitrage-client --region ap-northeast-1 output.json
```

## Updating

To update the application:

```bash
# Update code and rebuild images
cd terraform
./deploy.sh
```

## Cleanup

To remove all AWS resources:

```bash
cd terraform
terraform destroy
```

## Need Help?

- **GitHub Actions Workflow**: [GITHUB_ACTIONS_WORKFLOW.md](GITHUB_ACTIONS_WORKFLOW.md)
- **Detailed Guide**: [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md)
- **Android Setup**: [DEPLOYMENT_ANDROID.md](DEPLOYMENT_ANDROID.md)
- **Terraform Docs**: [terraform/README.md](terraform/README.md)
- **Main README**: [README.md](README.md)

## Common Issues

### Issue: Terraform not found
```bash
# Install Terraform
brew install terraform  # macOS
# or download from https://www.terraform.io/downloads
```

### Issue: AWS CLI not configured
```bash
aws configure
# Enter your AWS credentials
```

### Issue: Docker not running
```bash
# Start Docker Desktop
# Or: sudo systemctl start docker (Linux)
```

### Issue: Permission denied on deploy.sh
```bash
chmod +x terraform/deploy.sh
```

## Architecture Diagram

```
EventBridge (Scheduler)
    │
    ├─→ Server Lambda (14:00 Shanghai)
    │       │
    │       ├─→ Fetch LOF data from jisilu.cn
    │       ├─→ Filter funds (discount > 10%)
    │       └─→ Expose via Lambda Function URL
    │
    └─→ Client Lambda (14:20 Shanghai)
            │
            ├─→ Fetch LOF data from Server
            ├─→ Login to touker.com
            └─→ Create arbitrage orders
```

## Next Steps

1. ✅ Deploy to AWS (follow this guide)
2. ✅ Set up Android monitoring (see [DEPLOYMENT_ANDROID.md](DEPLOYMENT_ANDROID.md))
3. ✅ Configure CloudWatch alarms
4. ✅ Test end-to-end workflow
5. ✅ Monitor first trading day

---

**For detailed documentation, troubleshooting, and advanced configuration, see [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md)**
