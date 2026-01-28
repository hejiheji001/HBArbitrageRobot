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
RUN dotnet publish Client.csproj -c Release -o /app/publish

# Runtime stage - use SDK image for Playwright support
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install necessary dependencies for Playwright
RUN apt-get update && apt-get install -y \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libdbus-1-3 \
    libxkbcommon0 \
    libatspi2.0-0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    libxshmfence1 \
    libxshmfence-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Install Playwright browsers
RUN apt-get update && apt-get install -y wget unzip && \
    apt-get install -y pwsh && \
    pwsh -c "dotnet /app/playwright.ps1 install chromium" && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Set environment variables
ENV HEADLESS=true
ENV API_URL=http://server:8080

ENTRYPOINT ["dotnet", "Client.dll"]
