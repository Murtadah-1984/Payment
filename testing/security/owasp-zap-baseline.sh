#!/bin/bash

# OWASP ZAP Baseline Security Scan
# This script runs a baseline security scan against the Payment API

set -e

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
ZAP_HOST="${ZAP_HOST:-localhost}"
ZAP_PORT="${ZAP_PORT:-8080}"
ZAP_API_KEY="${ZAP_API_KEY:-}"
REPORT_DIR="${REPORT_DIR:-./testing/security/reports}"
TIMEOUT="${TIMEOUT:-300}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Starting OWASP ZAP Baseline Security Scan${NC}"
echo "API URL: ${API_URL}"
echo "ZAP Host: ${ZAP_HOST}:${ZAP_PORT}"

# Create reports directory
mkdir -p "${REPORT_DIR}"

# Check if ZAP is running
echo -e "${YELLOW}Checking if ZAP is running...${NC}"
if ! curl -s "http://${ZAP_HOST}:${ZAP_PORT}/JSON/core/view/version/" > /dev/null 2>&1; then
    echo -e "${RED}ZAP is not running. Please start ZAP first:${NC}"
    echo "  docker run -d -p ${ZAP_PORT}:8080 owasp/zap2docker-stable zap.sh -daemon -host 0.0.0.0 -port 8080"
    exit 1
fi

# Build ZAP API URL
ZAP_API_URL="http://${ZAP_HOST}:${ZAP_PORT}"
if [ -n "${ZAP_API_KEY}" ]; then
    ZAP_API_URL="${ZAP_API_URL}?apikey=${ZAP_API_KEY}"
fi

# Start new session
echo -e "${YELLOW}Starting new ZAP session...${NC}"
SESSION_NAME="payment-api-scan-$(date +%Y%m%d-%H%M%S)"
curl -s "${ZAP_API_URL}/JSON/core/action/newSession/" > /dev/null

# Spider the application
echo -e "${YELLOW}Spidering application...${NC}"
SPIDER_ID=$(curl -s "${ZAP_API_URL}/JSON/spider/action/scan/?url=${API_URL}" | jq -r '.scan')
echo "Spider scan ID: ${SPIDER_ID}"

# Wait for spider to complete
echo -e "${YELLOW}Waiting for spider to complete...${NC}"
while true; do
    STATUS=$(curl -s "${ZAP_API_URL}/JSON/spider/view/status/?scanId=${SPIDER_ID}" | jq -r '.status')
    if [ "${STATUS}" = "100" ]; then
        break
    fi
    echo "Spider progress: ${STATUS}%"
    sleep 2
done
echo -e "${GREEN}Spider completed${NC}"

# Active scan
echo -e "${YELLOW}Starting active scan...${NC}"
ACTIVE_SCAN_ID=$(curl -s "${ZAP_API_URL}/JSON/ascan/action/scan/?url=${API_URL}&recurse=true" | jq -r '.scan')
echo "Active scan ID: ${ACTIVE_SCAN_ID}"

# Wait for active scan to complete
echo -e "${YELLOW}Waiting for active scan to complete (timeout: ${TIMEOUT}s)...${NC}"
START_TIME=$(date +%s)
while true; do
    CURRENT_TIME=$(date +%s)
    ELAPSED=$((CURRENT_TIME - START_TIME))
    
    if [ ${ELAPSED} -gt ${TIMEOUT} ]; then
        echo -e "${YELLOW}Scan timeout reached. Generating report with current results...${NC}"
        break
    fi
    
    STATUS=$(curl -s "${ZAP_API_URL}/JSON/ascan/view/status/?scanId=${ACTIVE_SCAN_ID}" | jq -r '.status')
    if [ "${STATUS}" = "100" ]; then
        break
    fi
    echo "Active scan progress: ${STATUS}%"
    sleep 5
done
echo -e "${GREEN}Active scan completed${NC}"

# Generate reports
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# HTML Report
echo -e "${YELLOW}Generating HTML report...${NC}"
curl -s "${ZAP_API_URL}/OTHER/core/other/htmlreport/" > "${REPORT_DIR}/zap-report-${TIMESTAMP}.html"

# JSON Report
echo -e "${YELLOW}Generating JSON report...${NC}"
curl -s "${ZAP_API_URL}/JSON/core/view/alerts/?baseurl=${API_URL}" > "${REPORT_DIR}/zap-alerts-${TIMESTAMP}.json"

# XML Report (for CI/CD integration)
echo -e "${YELLOW}Generating XML report...${NC}"
curl -s "${ZAP_API_URL}/OTHER/core/other/xmlreport/" > "${REPORT_DIR}/zap-report-${TIMESTAMP}.xml"

# Summary
echo -e "${GREEN}Scan completed!${NC}"
echo "Reports saved to: ${REPORT_DIR}/"
echo "  - HTML: zap-report-${TIMESTAMP}.html"
echo "  - JSON: zap-alerts-${TIMESTAMP}.json"
echo "  - XML: zap-report-${TIMESTAMP}.xml"

# Count alerts by risk level
if command -v jq &> /dev/null; then
    echo -e "\n${YELLOW}Alert Summary:${NC}"
    HIGH=$(jq '[.[] | select(.risk == "High")] | length' "${REPORT_DIR}/zap-alerts-${TIMESTAMP}.json")
    MEDIUM=$(jq '[.[] | select(.risk == "Medium")] | length' "${REPORT_DIR}/zap-alerts-${TIMESTAMP}.json")
    LOW=$(jq '[.[] | select(.risk == "Low")] | length' "${REPORT_DIR}/zap-alerts-${TIMESTAMP}.json")
    INFO=$(jq '[.[] | select(.risk == "Informational")] | length' "${REPORT_DIR}/zap-alerts-${TIMESTAMP}.json")
    
    echo "  High: ${HIGH}"
    echo "  Medium: ${MEDIUM}"
    echo "  Low: ${LOW}"
    echo "  Informational: ${INFO}"
    
    if [ "${HIGH}" -gt 0 ]; then
        echo -e "\n${RED}⚠️  High-risk vulnerabilities found!${NC}"
        exit 1
    fi
fi

echo -e "\n${GREEN}✅ Security scan completed successfully${NC}"

