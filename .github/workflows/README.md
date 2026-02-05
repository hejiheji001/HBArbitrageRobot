# GitHub Actions Workflows

This directory contains automated workflows for HBArbitrageRobot.

## Available Workflows

### 1. Deploy to AWS with Terraform (`terraform-deploy.yml`)

**Purpose**: Automates deployment to AWS Lambda in ap-northeast-1 region

**Triggers**:
- Push to main/master branch (paths: `terraform/**`, `Server/**`, workflow file)
- Manual dispatch from Actions UI

**What it does**:
1. Runs Terraform to create/update AWS infrastructure
2. Builds Docker images for Server and Client
3. Pushes images to Amazon ECR
4. Updates Lambda functions
5. Tests deployment

**Required Secrets**:
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `TOUKER_USERNAME`
- `TOUKER_PASSWORD`
- `TOUKER_TRANSACTION_PASSWORD`

**Documentation**: See [GITHUB_ACTIONS_WORKFLOW.md](../../GITHUB_ACTIONS_WORKFLOW.md)

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

- [GITHUB_ACTIONS_WORKFLOW.md](../../GITHUB_ACTIONS_WORKFLOW.md) - Detailed workflow guide
- [DEPLOYMENT_AWS.md](../../DEPLOYMENT_AWS.md) - AWS deployment guide
- [AWS_DEPLOYMENT_SUMMARY.md](../../AWS_DEPLOYMENT_SUMMARY.md) - Deployment overview
