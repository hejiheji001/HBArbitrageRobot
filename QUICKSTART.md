# Quick Start: Deploy HBArbitrageServer to AWS Lambda

## What You Need to Provide

To complete the deployment, you only need to provide **AWS credentials** to GitHub:

### Required GitHub Secrets

Go to your repository → Settings → Secrets and variables → Actions → New repository secret

Add these two secrets:

1. **AWS_ACCESS_KEY_ID**
   - Your AWS access key ID
   - Example: `AKIAIOSFODNN7EXAMPLE`

2. **AWS_SECRET_ACCESS_KEY**
   - Your AWS secret access key
   - Example: `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`

### How to Get AWS Credentials

If you don't have AWS credentials:

1. Log in to [AWS Console](https://console.aws.amazon.com/)
2. Go to IAM → Users
3. Create a new user or select existing user
4. Go to "Security credentials" tab
5. Click "Create access key"
6. Choose "Application running outside AWS"
7. Copy the Access Key ID and Secret Access Key

### Required IAM Permissions

Your AWS user/role needs these permissions:
- ECR: Create repositories, push images
- Lambda: Create/update functions, create function URLs
- IAM: Create/attach roles (for Lambda execution role)
- CloudWatch Logs: Create log groups

**Easiest approach**: Attach these AWS managed policies:
- `AmazonEC2ContainerRegistryFullAccess`
- `AWSLambda_FullAccess`
- `IAMFullAccess` (or create a custom policy with limited IAM permissions)

## Do You Still Need Terraform?

**No!** Terraform has been removed because:
- ✅ The new workflow handles everything automatically
- ✅ No infrastructure state files to manage
- ✅ Simpler setup - just AWS credentials needed
- ✅ Same result - fully functional Lambda deployment

## Deployment Steps

### 1. Add AWS Credentials (One Time)
Add the two secrets mentioned above to your GitHub repository.

### 2. Deploy
**Option A - Automatic (Recommended):**
- Push your code to `main` or `master` branch
- Workflow runs automatically

**Option B - Manual:**
1. Go to Actions tab in GitHub
2. Select "Deploy Server to AWS Lambda"
3. Click "Run workflow"
4. Click "Run workflow" button

### 3. Access Your Lambda Function
After deployment completes:
1. Go to the workflow run in Actions tab
2. Check the "Deployment Summary" section
3. Copy your Function URL (looks like: `https://xxxxx.lambda-url.ap-northeast-1.on.aws/`)
4. Test it:
   ```bash
   curl https://your-function-url.lambda-url.ap-northeast-1.on.aws/api/data/lof
   ```

## What Gets Created Automatically

The workflow creates everything for you:

1. **ECR Repository** - `hbarbitrageserver` in Tokyo region
2. **Lambda Function** - `HBArbitrageServer` with:
   - .NET 9 container runtime
   - 512 MB memory
   - 15-minute timeout
   - x86_64 architecture
3. **IAM Role** - Execution role with CloudWatch Logs permissions
4. **Function URL** - Public HTTPS endpoint (no authentication)

## Cost Estimate

AWS Lambda pricing (Tokyo region):
- **Free Tier**: 1M requests + 400,000 GB-seconds per month
- After free tier: ~$0.20 per 1M requests
- **Expected cost**: $0-5/month depending on usage

## Monitoring

View Lambda logs:
1. Go to [AWS CloudWatch Console](https://console.aws.amazon.com/cloudwatch/)
2. Select Tokyo (ap-northeast-1) region
3. Go to Logs → Log groups
4. Find `/aws/lambda/HBArbitrageServer`
5. Click to view logs

## Troubleshooting

### Workflow fails with "Access Denied"
- Check AWS credentials are correct
- Verify IAM permissions
- Ensure region is set to ap-northeast-1

### Lambda function not responding
- Check CloudWatch logs for errors
- Verify Function URL is configured
- Test with curl or browser

### Need to update configuration
Edit `.github/workflows/deploy-lambda-server.yml`:
- Change `FUNCTION_NAME` to rename function
- Change `AWS_REGION` to deploy to different region
- Adjust memory/timeout in Lambda configuration section

## Additional Information

- [DEPLOYMENT.md](DEPLOYMENT.md) - Complete deployment guide
- [TERRAFORM_REMOVAL.md](TERRAFORM_REMOVAL.md) - Why Terraform was removed
- [README.md](README.md) - Full project documentation

## Support

If you encounter issues:
1. Check GitHub Actions workflow logs
2. Check AWS CloudWatch logs
3. Review the documentation files listed above
4. Open an issue in the repository
