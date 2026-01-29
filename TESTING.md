# Testing Guide for HBArbitrageRobot

This guide explains how to test both the Server and Client components of the HBArbitrageRobot.

## Testing the Server

### Method 1: Local Testing with dotnet run

1. **Navigate to the Server directory:**
   ```bash
   cd Server
   ```

2. **Run the server:**
   ```bash
   dotnet run
   ```
   
   The server will start and display output like:
   ```
   info: Server.Services.DataFetchService[0]
         Data Fetch Service started
   info: Server.Services.DataFetchService[0]
         Next fetch scheduled at: 01/29/2026 14:00:00 (in XX:XX:XX)
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://localhost:5290
   ```

3. **Test the API endpoint:**
   
   In a new terminal, test the `/api/data` endpoint:
   
   ```bash
   # Test with curl
   curl http://localhost:5290/api/data
   
   # Manually trigger a data fetch for testing
   curl -X POST http://localhost:5290/api/data/fetch
   
   # Check the fetched data
   curl http://localhost:5290/api/data
   
   # Or with wget
   wget -qO- http://localhost:5290/api/data
   
   # Or with httpie (if installed)
   http http://localhost:5290/api/data
   ```
   
   **Expected Response (before data is fetched):**
   ```json
   {
     "message": "No data available yet. Data is fetched daily at 14:00 Shanghai time."
   }
   ```
   
   **Expected Response (after manual fetch trigger):**
   ```json
   {
     "message": "Data fetch completed successfully"
   }
   ```
   
   **Expected Response (after data is available):**
   ```json
   [
     {
       "lofFund": "160416",
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
   
   Note: The actual data will vary based on the current LOF funds from jisilu.cn that meet the filtering criteria (discount_rt > 10% and redeem_status = "开放赎回").

### Method 2: Testing with Docker

1. **Build the Server Docker image:**
   ```bash
   docker build -t hbarbitrage-server -f Server/Dockerfile .
   ```

2. **Run the container:**
   ```bash
   docker run -p 8080:8080 hbarbitrage-server
   ```

3. **Test the API:**
   ```bash
   curl http://localhost:8080/api/data
   ```

### Method 3: Testing with Docker Compose

1. **Start both Server and Client:**
   ```bash
   docker-compose up server
   ```
   
   Or to run in detached mode:
   ```bash
   docker-compose up -d server
   ```

2. **Test the API:**
   ```bash
   curl http://localhost:8080/api/data
   ```

3. **View logs:**
   ```bash
   docker-compose logs -f server
   ```

4. **Stop the service:**
   ```bash
   docker-compose down
   ```

## Testing the Client

### Prerequisites

The Client requires:
- A running Server instance
- Valid touker.com credentials
- Transaction password

### Method 1: Local Testing

1. **Ensure Server is running** (see Server testing above)

2. **Navigate to Client directory:**
   ```bash
   cd Client
   ```

3. **Run with command-line arguments:**
   ```bash
   dotnet run <username> <password> http://localhost:5290 <transaction_pwd> 14:15
   ```

4. **Or run with environment variables:**
   ```bash
   export USERNAME="your_username"
   export PASSWORD="your_password"
   export PWD="your_transaction_password"
   export PURCHASE_TIME="14:15"
   export API_URL="http://localhost:5290"
   dotnet run
   ```

### Method 2: Testing with Docker

1. **Create a .env file:**
   ```bash
   cat > .env << EOF
   USERNAME=your_username
   PASSWORD=your_password
   PWD=your_transaction_password
   PURCHASE_TIME=14:15
   EOF
   ```

2. **Run with Docker Compose:**
   ```bash
   docker-compose up
   ```

## Manual API Testing

### Using curl

```bash
# Get LOF arbitrage data
curl -X GET http://localhost:5290/api/data \
  -H "Content-Type: application/json" | jq .

# Manually trigger a data fetch
curl -X POST http://localhost:5290/api/data/fetch \
  -H "Content-Type: application/json" | jq .

# Pretty print with jq
curl -s http://localhost:5290/api/data | jq .
```

### Using Postman or Similar Tools

1. **Import the following request:**
   - Method: `GET`
   - URL: `http://localhost:5290/api/data`
   - Headers: `Content-Type: application/json`

2. **Expected responses:** See Method 1 above

## Testing Workflow

### Complete Integration Test

1. **Start the Server:**
   ```bash
   cd Server
   dotnet run &
   SERVER_PID=$!
   ```

2. **Wait for server to start:**
   ```bash
   sleep 5
   ```

3. **Test the API:**
   ```bash
   curl http://localhost:5290/api/data
   ```

4. **Run the Client (with test credentials):**
   ```bash
   cd ../Client
   dotnet run testuser testpass http://localhost:5290 testpwd 14:15
   ```

5. **Clean up:**
   ```bash
   kill $SERVER_PID
   ```

## Troubleshooting

### Server Issues

**Port already in use:**
```bash
# Find process using port 5290
lsof -i :5290
# or
netstat -tulpn | grep 5290

# Kill the process
kill -9 <PID>
```

**Server won't start:**
- Check .NET 10 is installed: `dotnet --version`
- Verify all dependencies: `dotnet restore`
- Check for compilation errors: `dotnet build`

### Client Issues

**Playwright browsers not found:**
```bash
cd Client
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

**API connection refused:**
- Verify Server is running
- Check the API_URL parameter matches the Server's listening port
- Test with: `curl http://localhost:5290/api/data`

### Docker Issues

**Build failures:**
```bash
# Clean Docker build cache
docker builder prune

# Rebuild without cache
docker build --no-cache -t hbarbitrage-server -f Server/Dockerfile .
```

**Container won't start:**
```bash
# Check logs
docker logs <container_id>

# Check if ports are available
docker ps -a
```

## Performance Testing

### Load Testing the Server

Using Apache Bench (ab):
```bash
ab -n 1000 -c 10 http://localhost:5290/api/data
```

Using wrk:
```bash
wrk -t4 -c100 -d30s http://localhost:5290/api/data
```

## Monitoring

### Server Health Check

```bash
# Check if server is responsive
curl -f http://localhost:5290/api/data || echo "Server is down"
```

### Continuous Monitoring

```bash
# Monitor server logs
tail -f /path/to/logs

# Or with Docker
docker-compose logs -f server
```

## Automated Testing

Currently, the project doesn't have automated unit tests. To add testing:

1. **Create test projects:**
   ```bash
   dotnet new xunit -n Server.Tests
   dotnet new xunit -n Client.Tests
   ```

2. **Add test references:**
   ```bash
   cd Server.Tests
   dotnet add reference ../Server/Server.csproj
   ```

3. **Run tests:**
   ```bash
   dotnet test
   ```
