#!/bin/bash

# OWASP ZAP Full Security Scan (including authenticated endpoints)
# This script runs a comprehensive security scan including authenticated endpoints

set -e

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
JWT_TOKEN="${JWT_TOKEN:-}"
ZAP_HOST="${ZAP_HOST:-localhost}"
ZAP_PORT="${ZAP_PORT:-8080}"
ZAP_API_KEY="${ZAP_API_KEY:-}"
REPORT_DIR="${REPORT_DIR:-./testing/security/reports}"
TIMEOUT="${TIMEOUT:-600}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}Starting OWASP ZAP Full Security Scan${NC}"
echo "API URL: ${API_URL}"

if [ -z "${JWT_TOKEN}" ]; then
    echo -e "${YELLOW}Warning: JWT_TOKEN not provided. Authenticated endpoints will not be scanned.${NC}"
fi

# Create reports directory
mkdir -p "${REPORT_DIR}"

# Check if ZAP is running
ZAP_API_URL="http://${ZAP_HOST}:${ZAP_PORT}"
if [ -n "${ZAP_API_KEY}" ]; then
    ZAP_API_URL="${ZAP_API_URL}?apikey=${ZAP_API_KEY}"
fi

if ! curl -s "${ZAP_API_URL}/JSON/core/view/version/" > /dev/null 2>&1; then
    echo -e "${RED}ZAP is not running. Please start ZAP first.${NC}"
    exit 1
fi

# Start new session
echo -e "${YELLOW}Starting new ZAP session...${NC}"
curl -s "${ZAP_API_URL}/JSON/core/action/newSession/" > /dev/null

# Configure authentication context if JWT token provided
if [ -n "${JWT_TOKEN}" ]; then
    echo -e "${YELLOW}Configuring authentication context...${NC}"
    
    # Create authentication context
    CONTEXT_NAME="PaymentAPI"
    curl -s "${ZAP_API_URL}/JSON/context/action/newContext/?contextName=${CONTEXT_NAME}" > /dev/null
    
    # Add JWT authentication
    AUTH_CONFIG='{
        "type": "httpHeaderAuthentication",
        "parameters": {
            "headerName": "Authorization",
            "headerValue": "Bearer '${JWT_TOKEN}'"
        }
    }'
    
    curl -s -X POST \
        -H "Content-Type: application/json" \
        -d "${AUTH_CONFIG}" \
        "${ZAP_API_URL}/JSON/authentication/action/setAuthenticationMethod/?contextId=0" > /dev/null
    
    # Include all URLs in context
    curl -s "${ZAP_API_URL}/JSON/context/action/includeInContext/?contextName=${CONTEXT_NAME}&regex=${API_URL}.*" > /dev/null
fi

# Spider the application
echo -e "${YELLOW}Spidering application...${NC}"
SPIDER_ID=$(curl -s "${ZAP_API_URL}/JSON/spider/action/scan/?url=${API_URL}" | jq -r '.scan')

# Wait for spider
while true; do
    STATUS=$(curl -s "${ZAP_API_URL}/JSON/spider/view/status/?scanId=${SPIDER_ID}" | jq -r '.status')
    if [ "${STATUS}" = "100" ]; then
        break
    fi
    echo "Spider progress: ${STATUS}%"
    sleep 2
done

# AJAX Spider (for modern SPAs)
echo -e "${YELLOW}Running AJAX spider...${NC}"
AJAX_SPIDER_ID=$(curl -s "${ZAP_API_URL}/JSON/ajaxSpider/action/scan/?url=${API_URL}" | jq -r '.scan')

while true; do
    STATUS=$(curl -s "${ZAP_API_URL}/JSON/ajaxSpider/view/status/" | jq -r '.status')
    if [ "${STATUS}" = "stopped" ]; then
        break
    fi
    sleep 2
done

# Active scan with all policies
echo -e "${YELLOW}Starting comprehensive active scan...${NC}"
ACTIVE_SCAN_ID=$(curl -s "${ZAP_API_URL}/JSON/ascan/action/scan/?url=${API_URL}&recurse=true&inScopeOnly=false" | jq -r '.scan')

# Wait for active scan
START_TIME=$(date +%s)
while true; do
    CURRENT_TIME=$(date +%s)
    ELAPSED=$((CURRENT_TIME - START_TIME))
    
    if [ ${ELAPSED} -gt ${TIMEOUT} ]; then
        echo -e "${YELLOW}Scan timeout reached. Generating report...${NC}"
        break
    fi
    
    STATUS=$(curl -s "${ZAP_API_URL}/JSON/ascan/view/status/?scanId=${ACTIVE_SCAN_ID}" | jq -r '.status')
    if [ "${STATUS}" = "100" ]; then
        break
    fi
    echo "Active scan progress: ${STATUS}%"
    sleep 5
done

# Generate reports
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
curl -s "${ZAP_API_URL}/OTHER/core/other/htmlreport/" > "${REPORT_DIR}/zap-full-report-${TIMESTAMP}.html"
curl -s "${ZAP_API_URL}/JSON/core/view/alerts/?baseurl=${API_URL}" > "${REPORT_DIR}/zap-full-alerts-${TIMESTAMP}.json"
curl -s "${ZAP_API_URL}/OTHER/core/other/xmlreport/" > "${REPORT_DIR}/zap-full-report-${TIMESTAMP}.xml"

echo -e "${GREEN}Full scan completed!${NC}"
echo "Reports: ${REPORT_DIR}/zap-full-*-${TIMESTAMP}.*"

# Summary
if command -v jq &> /dev/null; then
    echo -e "\n${YELLOW}Alert Summary:${NC}"
    HIGH=$(jq '[.[] | select(.risk == "High")] | length' "${REPORT_DIR}/zap-full-alerts-${TIMESTAMP}.json")
    MEDIUM=$(jq '[.[] | select(.risk == "Medium")] | length' "${REPORT_DIR}/zap-full-alerts-${TIMESTAMP}.json")
    LOW=$(jq '[.[] | select(.risk == "Low")] | length' "${REPORT_DIR}/zap-full-alerts-${TIMESTAMP}.json")
    
    echo "  High: ${HIGH}"
    echo "  Medium: ${MEDIUM}"
    echo "  Low: ${LOW}"
    
    if [ "${HIGH}" -gt 0 ]; then
        echo -e "\n${RED}⚠️  High-risk vulnerabilities found!${NC}"
        exit 1
    fi
fi

