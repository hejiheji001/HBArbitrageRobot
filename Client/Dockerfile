# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY Client/Client.csproj Client/
RUN dotnet restore Client/Client.csproj

# Copy everything else and build
COPY Client/ Client/
WORKDIR /src/Client
RUN dotnet build Client.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish Client.csproj -c Release -f net10.0 -o /app/publish

# Runtime stage - use SDK image for Playwright support
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

# Set Playwright to install browsers in the running directory instead of AppData
# Setting PLAYWRIGHT_BROWSERS_PATH=0 uses a path relative to the executable
# This must be set BEFORE installing browsers to ensure consistency
ENV PLAYWRIGHT_BROWSERS_PATH=0

# Install Playwright dependencies using official Playwright installation
# First install PowerShell and required tools
RUN apt-get update && \
    apt-get install -y wget ca-certificates && \
    update-ca-certificates && \
    wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y powershell && \
    rm -rf /var/lib/apt/lists/*

# Install Playwright browsers and dependencies
# The playwright.ps1 script is included with Microsoft.Playwright package in the publish output
# First install system dependencies, then install the browser
RUN pwsh /app/playwright.ps1 install-deps chromium \
    && pwsh /app/playwright.ps1 install chromium \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set environment variables
ENV HEADLESS=true
ENV API_URL=http://server:8080

ENTRYPOINT ["dotnet", "Client.dll"]
