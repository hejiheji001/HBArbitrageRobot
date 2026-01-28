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
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Server.dll"]
