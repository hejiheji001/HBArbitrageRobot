#!/bin/bash
# Quick test script for HBArbitrageRobot Server

set -e

echo "=========================================="
echo "HBArbitrageRobot Server Test Script"
echo "=========================================="
echo ""

# Check if server directory exists
if [ ! -d "Server" ]; then
    echo "Error: Server directory not found. Please run this script from the repository root."
    exit 1
fi

# Build the server
echo "1. Building Server..."
cd Server
dotnet build -c Release > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "   ✓ Server build successful"
else
    echo "   ✗ Server build failed"
    exit 1
fi

# Start the server in background
echo ""
echo "2. Starting Server..."
# Use a random available port to avoid conflicts
TEST_PORT=5123
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://localhost:$TEST_PORT" dotnet run > /tmp/server.log 2>&1 &
SERVER_PID=$!
echo "   Server PID: $SERVER_PID"
echo "   Server URL: http://localhost:$TEST_PORT"

# Wait for server to start
echo "   Waiting for server to start..."
sleep 5

# Check if server is running
if ps -p $SERVER_PID > /dev/null; then
    echo "   ✓ Server is running"
else
    echo "   ✗ Server failed to start"
    cat /tmp/server.log
    exit 1
fi

# Test the API
echo ""
echo "3. Testing API endpoint..."
echo "   GET http://localhost:$TEST_PORT/api/data"
echo ""

# Test with curl
HTTP_CODE=$(curl -s -o /tmp/response.json -w "%{http_code}" http://localhost:$TEST_PORT/api/data)

if [ "$HTTP_CODE" == "200" ] || [ "$HTTP_CODE" == "404" ]; then
    echo "   ✓ API is responding (HTTP $HTTP_CODE)"
    echo ""
    echo "   Response:"
    cat /tmp/response.json | jq . || cat /tmp/response.json
    echo ""
else
    echo "   ✗ API returned unexpected status code: $HTTP_CODE"
fi

# Show server logs
echo ""
echo "4. Recent server logs:"
echo "   ----------------------------------------"
tail -10 /tmp/server.log | sed 's/^/   /'
echo "   ----------------------------------------"

# Cleanup
echo ""
echo "5. Cleaning up..."
kill $SERVER_PID 2>/dev/null || true
wait $SERVER_PID 2>/dev/null || true
echo "   ✓ Server stopped"

echo ""
echo "=========================================="
echo "Test completed successfully!"
echo "=========================================="
echo ""
echo "For more testing options, see TESTING.md"
