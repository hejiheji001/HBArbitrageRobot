# GitHub Actions Workflows

This directory contains automated workflows for HBArbitrageRobot.

## Available Workflows

### 1. Deploy Server to AWS Lambda (`deploy-lambda-server.yml`)

**Purpose**: Automates deployment of the Server to AWS Lambda in ap-northeast-1 (Tokyo) region

**Triggers**:
- Push to main/master branch (paths: `Server/**`, workflow file)
- Manual dispatch from Actions UI

**What it does**:
1. Creates ECR repository (if needed)
2. Builds Server Docker image
3. Pushes image to Amazon ECR
4. Creates Lambda function (if needed) with all required configuration
5. Updates Lambda function code (if already exists)
6. Configures Function URL (if not already configured)
7. Displays Function URL for accessing the API

**Required Secrets**:
- `AWS_ACCESS_KEY_ID` - Your AWS access key
- `AWS_SECRET_ACCESS_KEY` - Your AWS secret key

**Lambda Configuration**:
- Function Name: `HBArbitrageServer`
- Region: `ap-northeast-1` (Tokyo)
- Runtime: Container Image (.NET 10)
- Architecture: x86_64
- Memory: 512 MB
- Timeout: 900 seconds (15 minutes)

**Documentation**: See [DEPLOYMENT.md](../../DEPLOYMENT.md)

### 2. Build and Publish Docker Images (`docker-publish.yml`)

**Purpose**: Builds and publishes Docker images to GitHub Container Registry

**Triggers**:
- Push to main/master branch
- Tags (v*)
- Pull requests
- Manual dispatch

**What it does**:
1. Builds Server Docker image
2. Builds Client Docker image
3. Pushes to GitHub Container Registry (ghcr.io)

### 3. Build and Release Multi-Platform Client (`build-release-client.yml`)

**Purpose**: Builds native client applications for multiple platforms

**Triggers**:
- Push to main/master branch
- Tags
- Pull requests
- Manual dispatch

**What it does**:
1. Builds client for Windows, Linux, macOS
2. Creates platform-specific packages
3. Publishes as GitHub releases

## Usage

### Manual Workflow Trigger

1. Go to repository → Actions tab
2. Select desired workflow
3. Click "Run workflow"
4. Configure options if available
5. Click "Run workflow" button

### Automatic Triggers

Workflows run automatically based on their configured triggers (push, PR, tags).

## Monitoring

- Check **Actions** tab for workflow runs
- View logs for debugging
- Check deployment summary in workflow output

## Troubleshooting

### Workflow Not Running

- Check trigger conditions match your changes
- Verify branch name (main vs master)
- Check path filters for changed files

### Workflow Failing

1. Click on failed run in Actions tab
2. Review logs for error messages
3. Check required secrets are configured
4. Verify AWS credentials and permissions

### Secrets Not Working

1. Go to Settings → Secrets → Actions
2. Verify all required secrets exist
3. Check secret names match exactly
4. Secrets are case-sensitive

## Security

- Never commit secrets to repository
- Use GitHub Secrets for all credentials
- Enable branch protection for production
- Review workflow changes in PRs

## More Information

- [DEPLOYMENT.md](../../DEPLOYMENT.md) - AWS Lambda deployment guide
