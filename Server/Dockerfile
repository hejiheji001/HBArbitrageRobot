# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY Server/Server.csproj Server/
RUN dotnet restore Server/Server.csproj

# Copy everything else and build
COPY Server/ Server/
WORKDIR /src/Server
RUN dotnet build Server.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish Server.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install timezone data for Asia/Shanghai timezone support and curl
RUN apt-get update && apt-get install -y --no-install-recommends \
    tzdata \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install AWS Lambda Web Adapter
# This allows standard web applications to run in Lambda and handle API Gateway requests
COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:0.8.4 /lambda-adapter /opt/extensions/lambda-adapter

COPY --from=publish /app/publish .

# Expose port (Lambda uses 8080 by default)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

# AWS Lambda Web Adapter configuration
# Readiness check is used to determine when the app is ready
ENV AWS_LWA_READINESS_CHECK_PATH=/health
ENV AWS_LWA_READINESS_CHECK_PORT=8080

# Lambda will invoke through the Web Adapter which proxies to our ASP.NET Core app
ENTRYPOINT ["dotnet", "Server.dll"]
