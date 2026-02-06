# Lambda Function URL 配置管理解决方案

## 问题描述

目前 Lambda 的 function URL 已经设置为 IAM 授权模式。那么：
1. Client 部分需要实现这个认证
2. 由于 function URL 可能每次配置后都不同，如何固定下来？

## 解决方案

### 1. IAM 认证实现

**状态**: ✅ 已实现

IAM 认证已经在 `Client/Services/ApiClientService.cs` 中实现，使用 AWS Sigv4 签名：
- 自动检测是否为 Lambda URL（非 localhost）
- 使用 AWS 凭证对请求进行签名
- 支持从多种来源获取凭证（环境变量、~/.aws/credentials、IAM 角色）

### 2. Function URL 固定化

**状态**: ✅ 已实现

使用 AWS Systems Manager (SSM) Parameter Store 来集中存储和管理 Lambda function URL。

#### 工作流程

1. **部署时（自动）**：
   - GitHub Actions workflow 部署 Lambda 函数
   - 自动获取 Lambda function URL
   - 将 URL 存储到 SSM Parameter Store (`/hbarbitrage/lambda/server-url`)

2. **客户端运行时（自动）**：
   - 检查 `API_URL` 环境变量
   - 如果未设置，自动从 SSM 获取 URL
   - 使用获取的 URL 连接到服务器
   - 自动应用 IAM 认证

#### 优势

✅ **无需手动更新** - URL 自动同步  
✅ **集中管理** - 单一配置源  
✅ **自动容错** - URL 变化自动传播  
✅ **安全** - IAM 控制访问权限  
✅ **跨区域支持** - 支持多个 AWS 区域

## 使用方法

### 方式一：自动配置（推荐）

无需任何配置！客户端会自动：
1. 从 SSM 获取 Lambda function URL
2. 使用环境中的 AWS 凭证
3. 应用 IAM 认证

**要求**：
- AWS 凭证已配置（环境变量、~/.aws/credentials 或 IAM 角色）
- 可选：设置 `AWS_REGION` 环境变量（默认：ap-northeast-1）

**所需 IAM 权限**：
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

### 方式二：手动指定 URL

如果需要手动指定 API URL：

```bash
export API_URL="https://your-lambda-url.lambda-url.ap-northeast-1.on.aws/"
```

这将覆盖 SSM 自动获取。

### 方式三：本地开发

对于本地开发（localhost Server）：

```bash
export API_URL="http://localhost:5000"
```

客户端会自动检测 localhost URL 并跳过 IAM 认证。

## 获取 Lambda Function URL

### 通过 AWS CLI

```bash
# 从 SSM Parameter Store 获取
aws ssm get-parameter \
  --name "/hbarbitrage/lambda/server-url" \
  --query 'Parameter.Value' \
  --output text \
  --region ap-northeast-1
```

### 通过 AWS 控制台

1. 进入 AWS 控制台 → Systems Manager → Parameter Store
2. 搜索 `/hbarbitrage/lambda/server-url`
3. 点击参数查看值

### 通过 Lambda 控制台

1. 进入 AWS 控制台 → Lambda → Functions
2. 选择 `HBArbitrageServer`
3. 进入 Configuration → Function URL
4. 复制 Function URL

## 故障排除

### 问题：无法从 SSM 获取 URL

**症状**：
```
⚠️  Failed to retrieve Lambda function URL from SSM: Access Denied
Using default URL: http://localhost:5000
```

**解决方法**：
1. 验证 AWS 凭证已配置：
   ```bash
   aws sts get-caller-identity
   ```
2. 检查 IAM 权限包含 `ssm:GetParameter`
3. 验证参数存在：
   ```bash
   aws ssm get-parameter --name "/hbarbitrage/lambda/server-url" --region ap-northeast-1
   ```

### 问题：区域错误

**症状**：
```
⚠️  Failed to retrieve Lambda function URL from SSM: ParameterNotFound
```

**解决方法**：
设置正确的 AWS 区域：
```bash
export AWS_REGION="ap-northeast-1"
```

### 问题：参数不存在

**症状**：
```
⚠️  Lambda function URL not found in SSM Parameter Store.
```

**解决方法**：
1. 使用 GitHub Actions 部署 Server - 这将创建参数
2. 手动创建参数：
   ```bash
   aws ssm put-parameter \
     --name "/hbarbitrage/lambda/server-url" \
     --type "String" \
     --value "https://your-url.lambda-url.ap-northeast-1.on.aws/" \
     --region ap-northeast-1
   ```

### 问题：IAM 认证失败

**症状**：
```
Failed to fetch data from API: Forbidden (403)
```

**解决方法**：
1. 验证 AWS 凭证有效：
   ```bash
   aws sts get-caller-identity
   ```
2. 检查 IAM 用户/角色有权限调用 Lambda function URL
3. 验证 Lambda function URL 已启用 IAM 认证：
   ```bash
   aws lambda get-function-url-config --function-name HBArbitrageServer --region ap-northeast-1
   ```

## 技术实现细节

### 1. GitHub Actions Workflow

文件：`.github/workflows/deploy-lambda-server.yml`

新增步骤：
```yaml
- name: Store Function URL in SSM Parameter Store
  if: steps.function-url.outputs.function_url != ''
  run: |
    aws ssm put-parameter \
      --name "/hbarbitrage/lambda/server-url" \
      --type "String" \
      --value "${{ steps.function-url.outputs.function_url }}" \
      --overwrite \
      --region ${{ env.AWS_REGION }}
```

### 2. Client 配置服务

文件：`Client/Services/ConfigurationService.cs`

关键功能：
- `GetApiUrlAsync()`: 异步获取 API URL
- 使用 `Lazy<T>` 模式实现线程安全的 SSM 客户端单例
- 具体的异常处理，提供清晰的错误消息

### 3. IAM 认证

文件：`Client/Services/ApiClientService.cs`

关键功能：
- `SignRequestWithIAM()`: 使用 AWS Sigv4 签名请求
- 自动检测 Lambda URL
- 支持临时凭证（session token）

## 相关文档

- [LAMBDA_URL_CONFIG.md](LAMBDA_URL_CONFIG.md) - Lambda function URL 配置完整指南（英文）
- [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) - AWS Lambda 部署指南（英文）
- [QUICKSTART_AWS.md](QUICKSTART_AWS.md) - 快速开始指南（英文）
- [README.md](README.md) - 项目主文档（英文）

## 总结

通过使用 AWS Systems Manager Parameter Store，我们解决了 Lambda function URL 可能变化的问题：

1. ✅ **IAM 认证已实现** - 自动使用 AWS Sigv4 签名
2. ✅ **URL 自动管理** - 通过 SSM Parameter Store 集中存储
3. ✅ **无需手动配置** - 客户端自动获取最新 URL
4. ✅ **安全可靠** - IAM 控制访问权限
5. ✅ **易于维护** - 单一配置源，自动同步

现在您可以部署和更新 Lambda 函数，而无需担心 URL 变化导致客户端配置失效！
