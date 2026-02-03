#!/bin/bash
# Deployment script for HBArbitrageRobot to AWS Lambda
# This script automates the entire deployment process

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo -e "${GREEN}=== HBArbitrageRobot AWS Deployment Script ===${NC}\n"

# Check prerequisites
echo "Checking prerequisites..."

if ! command -v terraform &> /dev/null; then
    echo -e "${RED}Error: Terraform is not installed${NC}"
    exit 1
fi

if ! command -v aws &> /dev/null; then
    echo -e "${RED}Error: AWS CLI is not installed${NC}"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Prerequisites check passed${NC}\n"

# Navigate to Terraform directory
cd "$SCRIPT_DIR"

# Check if terraform.tfvars exists
if [ ! -f "terraform.tfvars" ]; then
    echo -e "${YELLOW}Warning: terraform.tfvars not found${NC}"
    echo "Please create terraform.tfvars from terraform.tfvars.example"
    echo -e "\n  cp terraform.tfvars.example terraform.tfvars"
    echo "  # Edit terraform.tfvars with your settings\n"
    exit 1
fi

# Initialize Terraform
echo "Initializing Terraform..."
terraform init

# Plan and apply infrastructure
echo -e "\n${YELLOW}Reviewing infrastructure changes...${NC}"
terraform plan

echo -e "\n${YELLOW}Apply these changes? (yes/no)${NC}"
read -r APPLY_CONFIRM

if [ "$APPLY_CONFIRM" != "yes" ]; then
    echo "Deployment cancelled"
    exit 0
fi

echo -e "\n${GREEN}Applying Terraform configuration...${NC}"
terraform apply -auto-approve

# Get outputs
echo -e "\n${GREEN}Getting deployment outputs...${NC}"
SERVER_ECR=$(terraform output -raw ecr_server_repository_url)
CLIENT_ECR=$(terraform output -raw ecr_client_repository_url)
SERVER_API_URL=$(terraform output -raw server_api_url)
SERVER_LAMBDA=$(terraform output -raw server_lambda_function_name)
CLIENT_LAMBDA=$(terraform output -raw client_lambda_function_name)
AWS_REGION=$(terraform output -json | jq -r '.aws_region.value // "us-east-1"')

echo -e "  Server ECR: ${GREEN}$SERVER_ECR${NC}"
echo -e "  Client ECR: ${GREEN}$CLIENT_ECR${NC}"
echo -e "  Server API: ${GREEN}$SERVER_API_URL${NC}"

# Extract AWS account ID and region from ECR URL
AWS_ACCOUNT_ID=$(echo "$SERVER_ECR" | cut -d'.' -f1)
ECR_REGION=$(echo "$SERVER_ECR" | cut -d'.' -f4)

# Navigate to project root
cd "$PROJECT_ROOT"

# ECR Login
echo -e "\n${GREEN}Logging in to ECR...${NC}"
aws ecr get-login-password --region "$ECR_REGION" | docker login --username AWS --password-stdin "${AWS_ACCOUNT_ID}.dkr.ecr.${ECR_REGION}.amazonaws.com"

# Build and push Server image
echo -e "\n${GREEN}Building Server Docker image...${NC}"
docker build -t hbarbitrage-server:latest -f Server/Dockerfile .

echo -e "\n${GREEN}Tagging and pushing Server image to ECR...${NC}"
docker tag hbarbitrage-server:latest "$SERVER_ECR:latest"
docker push "$SERVER_ECR:latest"

# Build and push Client image
echo -e "\n${GREEN}Building Client Docker image...${NC}"
docker build -t hbarbitrage-client:latest -f Client/Dockerfile .

echo -e "\n${GREEN}Tagging and pushing Client image to ECR...${NC}"
docker tag hbarbitrage-client:latest "$CLIENT_ECR:latest"
docker push "$CLIENT_ECR:latest"

# Update Lambda functions
echo -e "\n${GREEN}Updating Lambda functions...${NC}"

echo "Updating Server Lambda..."
aws lambda update-function-code \
  --function-name "$SERVER_LAMBDA" \
  --image-uri "$SERVER_ECR:latest" \
  --region "$ECR_REGION" \
  > /dev/null

echo "Updating Client Lambda..."
aws lambda update-function-code \
  --function-name "$CLIENT_LAMBDA" \
  --image-uri "$CLIENT_ECR:latest" \
  --region "$ECR_REGION" \
  > /dev/null

echo -e "${GREEN}✓ Lambda functions updated${NC}"

# Wait for Lambda functions to be active
echo -e "\n${GREEN}Waiting for Lambda functions to be ready...${NC}"
aws lambda wait function-updated --function-name "$SERVER_LAMBDA" --region "$ECR_REGION" || true
aws lambda wait function-updated --function-name "$CLIENT_LAMBDA" --region "$ECR_REGION" || true

# Test deployment
echo -e "\n${GREEN}Testing Server API...${NC}"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${SERVER_API_URL}api/data" || echo "000")

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "404" ]; then
    echo -e "${GREEN}✓ Server API is accessible (HTTP $HTTP_CODE)${NC}"
else
    echo -e "${YELLOW}⚠ Server API returned HTTP $HTTP_CODE (may need warm-up)${NC}"
fi

# Display summary
echo -e "\n${GREEN}=== Deployment Complete ===${NC}\n"
echo "Server API URL: $SERVER_API_URL"
echo ""
echo "Test the deployment:"
echo "  curl ${SERVER_API_URL}api/data"
echo ""
echo "View logs:"
echo "  aws logs tail /aws/lambda/$SERVER_LAMBDA --follow --region $ECR_REGION"
echo "  aws logs tail /aws/lambda/$CLIENT_LAMBDA --follow --region $ECR_REGION"
echo ""
echo "Manual invocation:"
echo "  aws lambda invoke --function-name $SERVER_LAMBDA output.json --region $ECR_REGION"
echo "  aws lambda invoke --function-name $CLIENT_LAMBDA output.json --region $ECR_REGION"
echo ""
echo -e "${GREEN}Deployment successful!${NC}"
