#!/bin/bash

# Penetration Testing Script
# Tests common security vulnerabilities manually

set -e

API_URL="${API_URL:-http://localhost:5000}"
API_VERSION="${API_VERSION:-v1}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}Starting Penetration Tests${NC}"
echo "API URL: ${API_URL}"

# Test results
PASSED=0
FAILED=0

# Test function
test_case() {
    local name="$1"
    local command="$2"
    local expected="$3"
    
    echo -e "\n${YELLOW}Testing: ${name}${NC}"
    
    if eval "${command}" | grep -q "${expected}"; then
        echo -e "${GREEN}✓ PASSED${NC}"
        ((PASSED++))
        return 0
    else
        echo -e "${RED}✗ FAILED${NC}"
        ((FAILED++))
        return 1
    fi
}

# 1. SQL Injection Test
echo -e "\n${YELLOW}=== SQL Injection Tests ===${NC}"
test_case "SQL Injection in Payment ID" \
    "curl -s '${API_URL}/api/${API_VERSION}/payments/1%27%20OR%20%271%27%3D%271' -H 'Accept: application/json'" \
    "400\|404\|500"

# 2. XSS Test
echo -e "\n${YELLOW}=== XSS Tests ===${NC}"
test_case "XSS in Order ID" \
    "curl -s -X POST '${API_URL}/api/${API_VERSION}/payments' \
        -H 'Content-Type: application/json' \
        -d '{\"orderId\":\"<script>alert(1)</script>\",\"amount\":100,\"currency\":\"USD\"}'" \
    "400\|validation"

# 3. Authentication Bypass
echo -e "\n${YELLOW}=== Authentication Tests ===${NC}"
test_case "Unauthenticated access to protected endpoint" \
    "curl -s -X POST '${API_URL}/api/${API_VERSION}/payments' \
        -H 'Content-Type: application/json' \
        -d '{\"amount\":100,\"currency\":\"USD\"}'" \
    "401\|Unauthorized"

# 4. CSRF Test
echo -e "\n${YELLOW}=== CSRF Tests ===${NC}"
test_case "CSRF token validation" \
    "curl -s -X POST '${API_URL}/api/${API_VERSION}/payments' \
        -H 'Content-Type: application/json' \
        -H 'X-CSRF-Token: invalid'" \
    "400\|403\|CSRF"

# 5. Rate Limiting Test
echo -e "\n${YELLOW}=== Rate Limiting Tests ===${NC}"
echo "Sending 20 rapid requests..."
RATE_LIMIT_HIT=0
for i in {1..20}; do
    RESPONSE=$(curl -s -w "%{http_code}" -o /dev/null \
        -X POST "${API_URL}/api/${API_VERSION}/payments" \
        -H 'Content-Type: application/json' \
        -d '{"amount":100,"currency":"USD"}')
    
    if [ "${RESPONSE}" = "429" ]; then
        RATE_LIMIT_HIT=1
        break
    fi
    sleep 0.1
done

if [ "${RATE_LIMIT_HIT}" = "1" ]; then
    echo -e "${GREEN}✓ Rate limiting is working${NC}"
    ((PASSED++))
else
    echo -e "${RED}✗ Rate limiting may not be working${NC}"
    ((FAILED++))
fi

# 6. Input Validation Test
echo -e "\n${YELLOW}=== Input Validation Tests ===${NC}"
test_case "Negative amount validation" \
    "curl -s -X POST '${API_URL}/api/${API_VERSION}/payments' \
        -H 'Content-Type: application/json' \
        -d '{\"amount\":-100,\"currency\":\"USD\"}'" \
    "400\|validation"

test_case "Invalid currency validation" \
    "curl -s -X POST '${API_URL}/api/${API_VERSION}/payments' \
        -H 'Content-Type: application/json' \
        -d '{\"amount\":100,\"currency\":\"INVALID\"}'" \
    "400\|validation"

# 7. Path Traversal Test
echo -e "\n${YELLOW}=== Path Traversal Tests ===${NC}"
test_case "Path traversal in payment ID" \
    "curl -s '${API_URL}/api/${API_VERSION}/payments/../../../etc/passwd'" \
    "400\|404"

# 8. HTTP Method Override Test
echo -e "\n${YELLOW}=== HTTP Method Override Tests ===${NC}"
test_case "HTTP method override protection" \
    "curl -s -X POST '${API_URL}/api/${API_VERSION}/payments' \
        -H 'X-HTTP-Method-Override: DELETE'" \
    "405\|Method Not Allowed"

# 9. Security Headers Test
echo -e "\n${YELLOW}=== Security Headers Tests ===${NC}"
HEADERS=$(curl -s -I "${API_URL}/health")
test_case "X-Content-Type-Options header" \
    "echo '${HEADERS}'" \
    "X-Content-Type-Options: nosniff"

test_case "X-Frame-Options header" \
    "echo '${HEADERS}'" \
    "X-Frame-Options"

test_case "X-XSS-Protection header" \
    "echo '${HEADERS}'" \
    "X-XSS-Protection"

# Summary
echo -e "\n${GREEN}=== Test Summary ===${NC}"
echo "Passed: ${PASSED}"
echo "Failed: ${FAILED}"

if [ "${FAILED}" -gt 0 ]; then
    echo -e "\n${RED}⚠️  Some tests failed. Review the results above.${NC}"
    exit 1
else
    echo -e "\n${GREEN}✅ All penetration tests passed!${NC}"
    exit 0
fi

