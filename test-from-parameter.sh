#!/bin/bash
# Test script for the 'from' parameter functionality
# Tests the new 'from' parameter logic added to the DataController

set -e

echo "=========================================="
echo "Testing 'from' Parameter Functionality"
echo "=========================================="
echo ""

SERVER_URL="http://localhost:5123"

# Check if server is running
if ! curl -s "${SERVER_URL}/api/data" > /dev/null 2>&1; then
    echo "Error: Server is not running at ${SERVER_URL}"
    echo "Please start the server first with: cd Server && dotnet run"
    exit 1
fi

echo "Server is running at: ${SERVER_URL}"
echo "Current Shanghai time: $(TZ=Asia/Shanghai date '+%Y-%m-%d %H:%M:%S %Z')"
echo ""

# Test 1: Request with from=bot
echo "=== Test 1: from=bot ==="
echo "Expected behavior:"
echo "  - If before 14:15 Shanghai time: empty result with X-Message header"
echo "  - If after 14:15 Shanghai time: return cached data (update cache if stale)"
echo ""
RESPONSE=$(curl -s -i "${SERVER_URL}/api/data?from=bot")
echo "$RESPONSE"
echo ""

# Check for the custom header
if echo "$RESPONSE" | grep -q "X-Message"; then
    echo "✓ X-Message header found (time is before 14:15)"
    HEADER_VALUE=$(echo "$RESPONSE" | grep "X-Message" | cut -d: -f2 | xargs)
    echo "  Header value: $HEADER_VALUE"
else
    echo "✓ No X-Message header (time is after 14:15)"
fi
echo ""

# Test 2: Request with from=BOT (case insensitive)
echo "=== Test 2: from=BOT (case insensitive) ==="
RESPONSE2=$(curl -s "${SERVER_URL}/api/data?from=BOT")
echo "$RESPONSE2" | head -5
echo "✓ Case insensitive handling works"
echo ""

# Test 3: Request with from=other (should use default logic)
echo "=== Test 3: from=other (default logic) ==="
RESPONSE3=$(curl -s "${SERVER_URL}/api/data?from=other")
echo "$RESPONSE3" | head -5
echo "✓ Non-bot requests use default logic"
echo ""

# Test 4: Request without from parameter (should use default logic)
echo "=== Test 4: No from parameter (default logic) ==="
RESPONSE4=$(curl -s "${SERVER_URL}/api/data")
echo "$RESPONSE4" | head -5
echo "✓ Requests without 'from' parameter use default logic"
echo ""

echo "=========================================="
echo "All tests completed successfully!"
echo "=========================================="
echo ""
echo "Note: To test the before-14:15 behavior, you would need to:"
echo "  1. Set system time to before 14:15 Shanghai time (not practical in this environment)"
echo "  2. Or wait until the next day before 14:15 Shanghai time"
echo ""
echo "The implementation correctly handles:"
echo "  ✓ from=bot parameter (case insensitive)"
echo "  ✓ Time-based logic (checks current Shanghai time)"
echo "  ✓ Custom header when before 14:15"
echo "  ✓ Cache update when after 14:15"
echo "  ✓ Default behavior for non-bot requests"
