# Lambda API Gateway Integration Fix

## Issue
The Lambda deployment workflow was succeeding, but the API Gateway was returning "service unavailable" errors when trying to access the deployed Server API.

## Root Cause
The ASP.NET Core Server application was deployed to AWS Lambda as a container image without the AWS Lambda Web Adapter. When API Gateway HTTP API integrates with Lambda using `AWS_PROXY` integration type and payload format version 2.0, the Lambda function needs to understand the Lambda invocation protocol.

Standard ASP.NET Core applications listen on HTTP and don't natively understand the Lambda protocol. The Lambda service can invoke containers, but the container needs a way to translate between Lambda's invocation format and HTTP requests.

## Solution
Added **AWS Lambda Web Adapter** to the Server Docker container. This is the recommended approach from AWS for running web applications in Lambda containers.

### What is AWS Lambda Web Adapter?
The AWS Lambda Web Adapter is a Lambda extension that allows you to run web applications (like ASP.NET Core APIs) in Lambda without code changes. It acts as a proxy that:
1. Receives Lambda invocations from API Gateway
2. Translates them to HTTP requests
3. Forwards them to your web application (running on localhost:8080)
4. Receives HTTP responses from your application
5. Translates them back to Lambda response format
6. Returns them to API Gateway

### Changes Made

#### 1. Server/Dockerfile
- Added AWS Lambda Web Adapter as a Lambda extension layer
- Added `curl` package (required for downloading adapter)
- Configured readiness check environment variables
- The adapter is copied from the official AWS public ECR image

```dockerfile
# Install AWS Lambda Web Adapter
COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:0.8.4 /lambda-adapter /opt/extensions/lambda-adapter

# AWS Lambda Web Adapter configuration
ENV AWS_LWA_READINESS_CHECK_PATH=/health
ENV AWS_LWA_READINESS_CHECK_PORT=8080
```

#### 2. Server/Controllers/HealthController.cs (New File)
- Added `/health` endpoint for readiness checks
- Added `/` root endpoint for basic API status
- Both endpoints don't require API token authentication

```csharp
[HttpGet("/health")]
public IActionResult GetHealth()
{
    return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
```

#### 3. Server/Program.cs
- Updated authentication middleware to skip health check endpoints
- This ensures the readiness check can succeed without authentication

```csharp
// Skip authentication for health check and root endpoints
if (context.Request.Path.StartsWithSegments("/health") || 
    context.Request.Path == "/")
{
    await next();
    return;
}
```

## How It Works

### Before (Broken)
```
API Gateway → Lambda (payload v2.0) → Container (ASP.NET Core)
                                        ↑ Can't understand Lambda format
                                        ✗ Service Unavailable
```

### After (Fixed)
```
API Gateway → Lambda (payload v2.0) → Web Adapter → HTTP Request → ASP.NET Core
                                                    ← HTTP Response ←
              ← Lambda Response ←
```

## Testing

### Local Testing
```bash
# Build the Docker image
docker build -t test-server -f Server/Dockerfile .

# Run the container
docker run -d -p 8080:8080 --name test-server test-server

# Test the health endpoint
curl http://localhost:8080/health
# Expected: {"status":"healthy","timestamp":"..."}

# Test the root endpoint
curl http://localhost:8080/
# Expected: {"message":"HBArbitrageRobot Server API","status":"running"}

# Test the data endpoint (without token in local mode)
curl http://localhost:8080/api/data
# Expected: {"fetchTime":"...","dataCount":0,"data":[]}
```

### Lambda Testing (After Deployment)
After deploying to AWS Lambda, the API Gateway URL should work:

```bash
# Test via API Gateway (replace with your API Gateway URL)
curl https://ehcn87vhp3.execute-api.ap-northeast-1.amazonaws.com/prod/health

# Test the data endpoint with API token
curl -H "X-Api-Token: YOUR_TOKEN" \
  https://ehcn87vhp3.execute-api.ap-northeast-1.amazonaws.com/prod/api/data
```

## Deployment

The fix will be automatically deployed when these changes are merged to the `main` branch, as the GitHub Actions workflow is triggered by:
- Push to `main` or `master` branch
- Changes to `Server/**` or workflow file

Alternatively, you can manually trigger the deployment using the GitHub Actions "workflow_dispatch" event.

## References

- [AWS Lambda Web Adapter Documentation](https://github.com/awslabs/aws-lambda-web-adapter)
- [Running Web Apps on Lambda](https://aws.amazon.com/blogs/aws/new-aws-lambda-web-adapter-run-web-apps-on-aws-lambda/)
- [AWS Lambda Container Images](https://docs.aws.amazon.com/lambda/latest/dg/images-create.html)
- [API Gateway HTTP API Integration](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html)

## Troubleshooting

### If API Gateway still returns errors:
1. Check Lambda function logs in CloudWatch
2. Verify the Lambda function has enough memory (512MB recommended)
3. Check if cold start timeout is adequate (15 seconds in readiness check)
4. Ensure API Gateway permissions are properly configured

### Common Issues:
- **502 Bad Gateway**: Lambda function crashed or timed out
- **503 Service Unavailable**: Lambda function is not responding or adapter isn't configured
- **504 Gateway Timeout**: Lambda execution exceeded timeout (increase timeout setting)

## Benefits of This Approach

1. **No Code Changes**: Web application code remains unchanged
2. **Standard Development**: Developers can code as if it's a regular web app
3. **Easy Testing**: Can test locally without Lambda
4. **Framework Agnostic**: Works with any web framework (ASP.NET Core, Express, Flask, etc.)
5. **Scalability**: Inherits Lambda's auto-scaling capabilities
6. **Cost Effective**: Pay per request, no idle costs
