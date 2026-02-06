# Lambda Function URL Configuration Guide

## Problem Statement

When deploying the Server to AWS Lambda, the function URL may change after each deployment or configuration change. This creates a challenge for the Client to always know the correct URL to connect to.

## Solution: AWS Systems Manager Parameter Store

We use AWS Systems Manager (SSM) Parameter Store to centrally store and retrieve the Lambda function URL. This provides a stable reference point that both deployment workflows and clients can use.

## How It Works

### 1. Deployment (Automatic)

When you deploy the Server using GitHub Actions (`.github/workflows/deploy-lambda-server.yml`):

1. The workflow creates/updates the Lambda function
2. The workflow retrieves the Lambda function URL
3. **The workflow automatically stores the URL in SSM Parameter Store** at `/hbarbitrage/lambda/server-url`

This happens automatically - no manual intervention required!

### 2. Client Configuration (Automatic)

When the Client starts:

1. It checks for the `API_URL` environment variable
2. If `API_URL` is not set, **it automatically retrieves the URL from SSM Parameter Store**
3. The retrieved URL is used to connect to the Server
4. IAM authentication is automatically applied for non-localhost URLs

This means the Client will always use the latest Lambda function URL without manual configuration!

## Benefits

✅ **No manual URL updates needed** - The URL is automatically synchronized
✅ **Centralized configuration** - Single source of truth for the Lambda URL
✅ **Resilient to changes** - URL changes are automatically propagated
✅ **Secure** - SSM is encrypted at rest and uses IAM for access control
✅ **Region-aware** - Works across different AWS regions

## Configuration Options

### Option 1: Automatic (Recommended)

No configuration needed! The Client will automatically:
1. Retrieve the Lambda function URL from SSM
2. Use AWS credentials from the environment
3. Apply IAM authentication

**Requirements:**
- AWS credentials configured (via environment variables, `~/.aws/credentials`, or IAM role)
- Optional: Set `AWS_REGION` environment variable (defaults to `ap-northeast-1`)

**Required IAM Permissions:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["ssm:GetParameter"],
      "Resource": "arn:aws:ssm:*:*:parameter/hbarbitrage/lambda/server-url"
    },
    {
      "Effect": "Allow",
      "Action": ["lambda:InvokeFunctionUrl"],
      "Resource": "*"
    }
  ]
}
```

### Option 2: Manual Override

If you need to manually specify the API URL:

```bash
export API_URL="https://your-lambda-url.lambda-url.ap-northeast-1.on.aws/"
```

This will override the automatic SSM retrieval.

### Option 3: Local Development

For local development with localhost Server:

```bash
export API_URL="http://localhost:5000"
```

The Client will automatically detect localhost URLs and skip IAM authentication.

## Retrieving the Lambda Function URL

### Via AWS CLI

```bash
# Get the URL from SSM Parameter Store
aws ssm get-parameter \
  --name "/hbarbitrage/lambda/server-url" \
  --query 'Parameter.Value' \
  --output text \
  --region ap-northeast-1
```

### Via AWS Console

1. Go to AWS Console → Systems Manager → Parameter Store
2. Search for `/hbarbitrage/lambda/server-url`
3. Click on the parameter to view the value

### Via Lambda Console

1. Go to AWS Console → Lambda → Functions
2. Select `HBArbitrageServer`
3. Go to Configuration → Function URL
4. Copy the Function URL

## Updating the Lambda Function URL

The URL is automatically updated by the GitHub Actions workflow whenever you deploy. However, if you need to manually update it:

```bash
# Get the current Lambda function URL
FUNCTION_URL=$(aws lambda get-function-url-config \
  --function-name HBArbitrageServer \
  --region ap-northeast-1 \
  --query 'FunctionUrl' \
  --output text)

# Store it in SSM Parameter Store
aws ssm put-parameter \
  --name "/hbarbitrage/lambda/server-url" \
  --type "String" \
  --value "$FUNCTION_URL" \
  --overwrite \
  --region ap-northeast-1
```

## Troubleshooting

### Issue: Client can't retrieve URL from SSM

**Symptoms:**
```
⚠️  Failed to retrieve Lambda function URL from SSM: Access Denied
Using default URL: http://localhost:5000
```

**Solutions:**
1. Verify AWS credentials are configured:
   ```bash
   aws sts get-caller-identity
   ```
2. Check IAM permissions include `ssm:GetParameter`
3. Verify the parameter exists:
   ```bash
   aws ssm get-parameter --name "/hbarbitrage/lambda/server-url" --region ap-northeast-1
   ```

### Issue: Wrong region

**Symptoms:**
```
⚠️  Failed to retrieve Lambda function URL from SSM: ParameterNotFound
```

**Solution:**
Set the correct AWS region:
```bash
export AWS_REGION="ap-northeast-1"
```

### Issue: Parameter doesn't exist

**Symptoms:**
```
⚠️  Failed to retrieve Lambda function URL from SSM: ParameterNotFound
```

**Solutions:**
1. Deploy the Server using GitHub Actions - this will create the parameter
2. Manually create the parameter:
   ```bash
   aws ssm put-parameter \
     --name "/hbarbitrage/lambda/server-url" \
     --type "String" \
     --value "https://your-url.lambda-url.ap-northeast-1.on.aws/" \
     --region ap-northeast-1
   ```

### Issue: IAM authentication fails

**Symptoms:**
```
Failed to fetch data from API: Forbidden (403)
```

**Solutions:**
1. Verify AWS credentials are valid:
   ```bash
   aws sts get-caller-identity
   ```
2. Check the IAM user/role has permission to invoke the Lambda function URL
3. Verify the Lambda function URL has IAM authentication enabled:
   ```bash
   aws lambda get-function-url-config --function-name HBArbitrageServer --region ap-northeast-1
   ```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     Deployment Flow                          │
│                                                              │
│  GitHub Actions Workflow                                     │
│         │                                                    │
│         ▼                                                    │
│  ┌──────────────┐       ┌────────────────┐                 │
│  │ Create/Update│──────▶│ Get Function   │                 │
│  │ Lambda       │       │ URL            │                 │
│  └──────────────┘       └────────┬───────┘                 │
│                                  │                          │
│                                  ▼                          │
│                         ┌─────────────────┐                │
│                         │ Store in SSM    │                │
│                         │ Parameter Store │                │
│                         └─────────────────┘                │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                     Client Flow                              │
│                                                              │
│  Client Application                                          │
│         │                                                    │
│         ▼                                                    │
│  ┌──────────────┐       ┌────────────────┐                 │
│  │ Check API_URL│  No   │ Retrieve from  │                 │
│  │ env var?     │──────▶│ SSM Parameter  │                 │
│  └──────┬───────┘       │ Store          │                 │
│         │ Yes           └────────┬───────┘                 │
│         │                        │                          │
│         ▼                        ▼                          │
│  ┌──────────────────────────────────┐                      │
│  │ Connect to Server API            │                      │
│  │ (with IAM authentication)        │                      │
│  └──────────────────────────────────┘                      │
└─────────────────────────────────────────────────────────────┘
```

## Security Considerations

### SSM Parameter Encryption

- SSM parameters are encrypted at rest using AWS KMS
- Access is controlled via IAM policies
- Parameter history is tracked (previous values retained)

### IAM Authentication

- Lambda function URLs use AWS Sigv4 signing
- Requires valid AWS credentials with appropriate permissions
- Requests are authenticated and authorized by AWS IAM

### Best Practices

1. **Limit SSM access**: Grant `ssm:GetParameter` only to the specific parameter
2. **Use IAM roles**: When running on AWS (EC2, Lambda, ECS), use IAM roles instead of credentials
3. **Rotate credentials**: Regularly rotate AWS access keys
4. **Enable CloudTrail**: Monitor SSM parameter access
5. **Use least privilege**: Grant only the minimum required permissions

## Related Documentation

- [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) - AWS Lambda deployment guide
- [QUICKSTART_AWS.md](QUICKSTART_AWS.md) - Quick start guide
- [README.md](README.md) - Main project documentation

## Support

If you encounter issues:
1. Check CloudWatch Logs for the Client
2. Verify AWS credentials and permissions
3. Test SSM parameter retrieval manually
4. Review GitHub Actions workflow logs

For more help, see [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) troubleshooting section.
