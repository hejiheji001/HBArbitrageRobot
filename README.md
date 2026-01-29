# HBArbitrageRobot

A .NET 10 application with Server and Client components for automated arbitrage operations.

## Features

- **Server Component**: 
  - RESTful API to expose LOF arbitrage data
  - Scheduled data fetching from jisilu.cn every day at 14:00 Shanghai time
  - Fetches data from 4 LOF endpoints (index LOF, stock LOF, and QDII)
  - Filters funds with discount_rt > 10% and apply_status != "暂停申购"
  - Automatically sets purchase date to current date
  - Returns array of LOF fund arbitrage configurations
  - Built with ASP.NET Core Web API

- **Client Component**:
  - Playwright-based browser automation with mobile device emulation (iPhone 13 Pro)
  - Automated login to touker.com mobile site
  - Fetches stock holder list from touker.com API
  - Reads LOF arbitrage data from Server API
  - Creates arbitrage orders via touker.com API
  - Supports configurable purchase time and transaction password

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
dotnet run <username> <password> [api_url] [pwd] [purchase_time] [max_investment]
# Example:
# dotnet run myuser mypass http://localhost:5000 my_txn_pwd 14:15 1000
```

Or use environment variables:

```bash
export USERNAME="your_username"
export PASSWORD="your_password"
export PWD="your_transaction_password"
export PURCHASE_TIME="14:15"
export API_URL="http://localhost:5000"
export MAX_INVESTMENT="1000"
dotnet run
```

## Docker Deployment

### 1. Create Environment File

Create a `.env` file in the project root:

```env
USERNAME=your_username
PASSWORD=your_password
PWD=your_transaction_password
PURCHASE_TIME=14:15
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

## API Documentation

The Server provides an interactive API documentation interface powered by **Scalar**.

### Accessing API Documentation

**Development Environment:**
API documentation is automatically enabled when running in development mode:

```bash
cd Server
dotnet run
```

The server will display the listening URL in the startup logs, typically:
```
Now listening on: http://localhost:5290
```

Then access:
- **Scalar API Reference UI**: `http://localhost:5290/scalar/v1` (replace port if different)
  - Beautiful, modern interactive API documentation
  - Test API endpoints directly from the browser
  - View request/response schemas
  - Example requests and responses

- **OpenAPI Specification**: `http://localhost:5290/openapi/v1.json`
  - Raw OpenAPI 3.1 specification in JSON format
  - Can be imported into tools like Postman, Insomnia, etc.

**Note:** The actual port number may vary. Always check the server startup logs for the exact URL.

**Production Environment:**
For security reasons, API documentation is disabled by default in production. To enable it, set the `EnableApiDocs` environment variable:

```bash
# Docker
docker run -p 8080:8080 -e EnableApiDocs=true hbarbitrage-server

# Docker Compose (add to docker-compose.yml)
services:
  server:
    environment:
      - EnableApiDocs=true

# Or in .env file
EnableApiDocs=true
```

Then access at `http://localhost:8080/scalar/v1` (Docker) or the configured port.

## API Endpoints

### GET /api/data

Returns LOF arbitrage data from jisilu.cn with intelligent caching:
- **Cache Hit (Same Day)**: Returns cached data immediately if already fetched today (Shanghai time)
- **Cache Miss (Different Day)**: Automatically fetches fresh data, updates cache, and returns new data
- **First Request**: Fetches data and caches it for the day

**Behavior:**
- Uses Shanghai timezone to determine "same day"
- Cache is validated by comparing year, month, and day
- Automatically refreshes data when date changes
- Falls back to stale cache if fetch fails

**Response (Success - 200 OK):**
```json
[
  {
    "lofFund": "160416",
    "applyStatus": "限10",
    "lofArbitrage": {
      "purchaseQuantity": "1",
      "purchaseDate": "2026-01-29"
    },
    "lofArbitrageStrategy": {
      "policyType": 4,
      "sellTime": "",
      "sellStockPrice": "",
      "turnBack": ""
    }
  }
]
```

**Response (Empty - 200 OK):**
```json
[]
```

**Response (Fetch in Progress - 404 Not Found):**
```json
{
  "message": "Data fetch in progress. Please try again in a moment."
}
```

### POST /api/data/fetch

Manually triggers a data fetch from jisilu.cn and returns the latest fetched data (for testing purposes).
Forces a fresh fetch regardless of cache status.

**Response (Success - 200 OK):**
```json
{
  "message": "Data fetch completed successfully",
  "fetchTime": "2026-01-29T14:00:00",
  "dataCount": 2,
  "data": [
    {
      "lofFund": "160416",
      "applyStatus": "限10",
      "lofArbitrage": {
        "purchaseQuantity": "1",
        "purchaseDate": "2026-01-29"
      },
      "lofArbitrageStrategy": {
        "policyType": 4,
        "sellTime": "",
        "sellStockPrice": "",
        "turnBack": ""
      }
    }
  ]
}
```

**Response (Concurrent Fetch - 409 Conflict):**
```json
{
  "message": "A data fetch operation is already in progress"
}
```

**Response (Error - 500 Internal Server Error):**
```json
{
  "message": "Data fetch failed. Please try again later."
}
```

## Client Workflow

The Client performs the following automated workflow:

1. **Login**: Authenticates to touker.com using provided credentials
2. **Fetch Stock Holders**: Queries `queryStockHolderNoList.do` to get all stock holder accounts
3. **Fetch LOF Data**: Retrieves LOF arbitrage configuration from the Server API
4. **Calculate Investment**: For each LOF fund, calculates total investment amount:
   - Parses `applyStatus` to extract limit value:
     - "限10" → 10 yuan
     - "限1万" → 10,000 yuan
     - "限1千" → 1,000 yuan
     - "开放申购" → unlimited (uses max limit)
   - Applies max investment limit per holder (default: 1000 yuan, configurable via MAX_INVESTMENT)
   - Calculates effective investment: `min(apply_status_limit, max_per_holder)`
   - Calculates total per fund: `number_of_stockholders × effective_limit`
   - Displays total investment amount across all funds
5. **Create Arbitrage**: Sends POST request to `createLofArbitrage.do` with:
   - Stock holder list
   - Purchase time (default: 14:15)
   - LOF fund configurations
   - Transaction password

### Investment Calculation Example

**Configuration:**
- Max investment per holder: 1000 yuan
- Stock holders: 3

**LOF Funds:**
- Fund A: apply_status = "限10" → effective: min(10, 1000) = 10 → investment: 3 × 10 = 30 yuan
- Fund B: apply_status = "限100" → effective: min(100, 1000) = 100 → investment: 3 × 100 = 300 yuan
- Fund C: apply_status = "限1万" → effective: min(10000, 1000) = 1000 → investment: 3 × 1000 = 3000 yuan
- Fund D: apply_status = "开放申购" → effective: 1000 → investment: 3 × 1000 = 3000 yuan
- **Total Investment**: 6330 yuan

## Configuration

### Server Configuration

The server automatically fetches data from jisilu.cn at 14:00 Shanghai time daily. It queries 4 different LOF endpoints:
- Index LOF list
- Stock LOF list  
- QDII list (E type, LOF only)
- QDII list (C type)

**Path Base Support (.NET 10 Compatibility):**
- The server supports path base configuration for hosting at custom paths (e.g., `/myapi`)
- Path bases can be specified in `ASPNETCORE_URLS` environment variable (e.g., `http://+:8080/myapi`)
- The path base is automatically extracted and configured using `UsePathBase()` middleware
- This ensures .NET 10 compatibility which requires path bases to be configured via middleware
- Example: `ASPNETCORE_URLS="http://localhost:8080/myapi"` will host the API at `http://localhost:8080/myapi/api/data`
  - Note: The application's API routes already use `/api` prefix, so with path base `/myapi`, endpoints become `/myapi/api/...`
- If multiple URLs with different path bases are provided, the first path base is used for all URLs

**Data Caching:**
- Fetched data is cached with a timestamp (Shanghai time)
- Cache is validated by date (year, month, day)
- GET /api/data returns cached data if from the same day
- GET /api/data automatically fetches new data if date has changed
- POST /api/data/fetch forces a fresh fetch and returns the data immediately

The server filters funds that meet both criteria:
- discount_rt > 10%
- apply_status != "暂停申购" (not suspended for purchase)

Each filtered fund is added to the arbitrage configuration with:
- Purchase quantity: 1
- Purchase date: Current date in Shanghai timezone
- Policy type: 4 (default strategy)

### Client Configuration

The client supports configuration via:

1. **Command Line Arguments** (Testing):
   ```bash
   dotnet run <username> <password> [api_url] [pwd] [purchase_time] [max_investment]
   ```

2. **Environment Variables** (Production):
   - `USERNAME`: Login username (required)
   - `PASSWORD`: Login password (required)
   - `PWD`: Transaction password for creating arbitrage (required)
   - `PURCHASE_TIME`: Time for arbitrage orders (default: 14:15)
   - `API_URL`: Server API URL (default: http://localhost:5000)
   - `MAX_INVESTMENT`: Max investment per stock holder per fund in yuan (default: 1000)
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

## Testing

For detailed testing instructions, see [TESTING.md](TESTING.md).

Quick test commands:

**Test Server API:**
```bash
# Start server
cd Server && dotnet run

# In another terminal, test the API
curl http://localhost:5290/api/data

# Manually trigger a data fetch (for testing)
curl -X POST http://localhost:5290/api/data/fetch
```

**Test Client:**
```bash
cd Client
dotnet run <username> <password> http://localhost:5290 <pwd> 14:15
```

## Notes

- The Server runs continuously and fetches data at 14:00 Shanghai time daily
- The Client runs once and exits after completing its task (not configured to restart automatically in Docker)
- The Client uses mobile device emulation (iPhone 13 Pro) to properly render and interact with touker.com mobile pages
- Login selectors in the Client may need adjustment based on the actual touker.com page structure
- For production use, ensure proper security measures for credential management:
  - Never commit the `.env` file to version control
  - Use Docker secrets, environment-specific config, or secret management services for production credentials
  - Avoid passing credentials via command-line arguments in production (they appear in process lists)
- The Server includes timezone fallback (UTC+8) if timezone data is not available in the container

## License

This project is licensed under the MIT License.
