# HBArbitrageRobot

A .NET 10 application with Server and Client components for automated arbitrage operations.

## Features

- **Server Component**: 
  - RESTful API to expose arbitrage data
  - Scheduled data fetching from example.com every day at 14:00 Shanghai time
  - Built with ASP.NET Core Web API

- **Client Component**:
  - Playwright-based browser automation
  - Reads data from Server API
  - Automated login to touker.com
  - Navigates to trading interface after successful authentication

- **Docker Support**: Fully containerized with Docker and Docker Compose
- **CI/CD**: Automated Docker image building and publishing via GitHub Actions

## Requirements

- .NET 10 SDK
- Docker and Docker Compose (for containerized deployment)
- PowerShell (for Playwright browser installation)

## Project Structure

```
HBArbitrageRobot/
├── Server/                 # Web API Server
│   ├── Controllers/        # API Controllers
│   ├── Models/            # Data Models
│   ├── Services/          # Background Services
│   └── Dockerfile         # Server Docker image
├── Client/                 # Playwright Client
│   ├── Models/            # Data Models
│   └── Dockerfile         # Client Docker image
├── docker-compose.yml      # Docker Compose configuration
└── README.md
```

## Development Setup

### 1. Clone the Repository

```bash
git clone https://github.com/hejiheji001/HBArbitrageRobot.git
cd HBArbitrageRobot
```

### 2. Build the Solution

```bash
dotnet build
```

### 3. Install Playwright Browsers (for Client)

```bash
cd Client
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
cd ..
```

### 4. Run the Server

```bash
cd Server
dotnet run
```

The server will start at `http://localhost:5000` (or `https://localhost:5001`).

### 5. Run the Client (Testing Mode)

```bash
cd Client
dotnet run <username> <password> http://localhost:5000
```

Or use environment variables:

```bash
export USERNAME="your_username"
export PASSWORD="your_password"
export API_URL="http://localhost:5000"
dotnet run
```

## Docker Deployment

### 1. Create Environment File

Create a `.env` file in the project root:

```env
USERNAME=your_username
PASSWORD=your_password
```

### 2. Build and Run with Docker Compose

```bash
docker-compose up --build
```

This will:
- Build both Server and Client Docker images
- Start the Server on port 8080
- Start the Client with automatic connection to the Server

### 3. Run in Detached Mode

```bash
docker-compose up -d
```

### 4. View Logs

```bash
# View all logs
docker-compose logs -f

# View Server logs only
docker-compose logs -f server

# View Client logs only
docker-compose logs -f client
```

### 5. Stop the Services

```bash
docker-compose down
```

## API Endpoints

### GET /api/data

Returns the latest fetched arbitrage data.

**Response (Success - 200 OK):**
```json
{
  "content": "...",
  "fetchedAt": "2026-01-28T14:00:00+08:00",
  "source": "example.com"
}
```

**Response (No Data - 404 Not Found):**
```json
{
  "message": "No data available yet. Data is fetched daily at 14:00 Shanghai time."
}
```

## Configuration

### Server Configuration

The server automatically fetches data from `example.com` at 14:00 Shanghai time daily.

### Client Configuration

The client supports configuration via:

1. **Command Line Arguments** (Testing):
   ```bash
   dotnet run <username> <password> [api_url]
   ```

2. **Environment Variables** (Production):
   - `USERNAME`: Login username
   - `PASSWORD`: Login password
   - `API_URL`: Server API URL (default: http://localhost:5000)
   - `HEADLESS`: Run browser in headless mode (default: true)

## Publishing Docker Images

### Automated Publishing (GitHub Actions)

Docker images are automatically built and published to GitHub Container Registry (ghcr.io) when:
- Code is pushed to `main` or `master` branch
- A new version tag (e.g., `v1.0.0`) is created
- Manually triggered via GitHub Actions workflow dispatch

Published images:
- Server: `ghcr.io/hejiheji001/hbarbitragerobot-server:latest`
- Client: `ghcr.io/hejiheji001/hbarbitragerobot-client:latest`

To use the published images:

```bash
# Pull images
docker pull ghcr.io/hejiheji001/hbarbitragerobot-server:latest
docker pull ghcr.io/hejiheji001/hbarbitragerobot-client:latest

# Or use with Docker Compose (update docker-compose.yml)
services:
  server:
    image: ghcr.io/hejiheji001/hbarbitragerobot-server:latest
  client:
    image: ghcr.io/hejiheji001/hbarbitragerobot-client:latest
```

### Manual Publishing

If you need to publish images manually to a different registry:

```bash
# Build Server image
docker build -t hbarbitragerobot-server:latest -f Server/Dockerfile .

# Build Client image
docker build -t hbarbitragerobot-client:latest -f Client/Dockerfile .

# Tag for registry
docker tag hbarbitragerobot-server:latest your-registry/hbarbitragerobot-server:latest
docker tag hbarbitragerobot-client:latest your-registry/hbarbitragerobot-client:latest

# Push to registry
docker push your-registry/hbarbitragerobot-server:latest
docker push your-registry/hbarbitragerobot-client:latest
```

## Notes

- The Server runs continuously and fetches data at 14:00 Shanghai time daily
- The Client runs once and exits after completing its task
- Login selectors in the Client may need adjustment based on the actual touker.com page structure
- For production use, ensure proper security measures for credential management

## License

This project is licensed under the MIT License.
