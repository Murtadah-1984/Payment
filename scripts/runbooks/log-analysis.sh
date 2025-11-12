#!/bin/bash
# Log Analysis Script for Payment Microservice
# Usage: ./log-analysis.sh [namespace] [deployment-name] [time-range]
# Time range: 1h, 6h, 24h (default: 1h)

set -e

NAMESPACE="${1:-payment}"
DEPLOYMENT="${2:-payment-api}"
TIME_RANGE="${3:-1h}"

echo "=========================================="
echo "Log Analysis Script"
echo "Namespace: $NAMESPACE"
echo "Deployment: $DEPLOYMENT"
echo "Time Range: $TIME_RANGE"
echo "=========================================="
echo ""

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "ERROR: kubectl is not installed or not in PATH"
    exit 1
fi

# Get all pods
PODS=$(kubectl get pods -n "$NAMESPACE" -l app="$DEPLOYMENT" -o jsonpath='{.items[*].metadata.name}')

if [ -z "$PODS" ]; then
    echo "ERROR: No pods found for deployment '$DEPLOYMENT' in namespace '$NAMESPACE'"
    exit 1
fi

echo "1. Error Count by Type"
echo "----------------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "error\|exception\|failure" | \
        sed 's/.*\(ERROR\|Exception\|Failure\).*/\1/' | \
        sort | uniq -c | sort -rn | head -10
    echo ""
done

echo "2. Payment Processing Errors"
echo "---------------------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "payment.*error\|payment.*failure\|payment.*exception" | \
        tail -20
    echo ""
done

echo "3. Circuit Breaker Events"
echo "------------------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "circuit.*breaker\|circuit.*open\|circuit.*closed" | \
        tail -20
    echo ""
done

echo "4. Provider Errors"
echo "-----------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "provider.*error\|stripe\|paypal\|square" | \
        tail -20
    echo ""
done

echo "5. Security Events"
echo "-----------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "security\|unauthorized\|authentication.*failed\|authorization.*denied" | \
        tail -20
    echo ""
done

echo "6. Webhook Events"
echo "---------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "webhook\|callback" | \
        tail -20
    echo ""
done

echo "7. High Frequency Errors (Top 10)"
echo "--------------------------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "error\|exception" | \
        awk '{print $NF}' | \
        sort | uniq -c | sort -rn | head -10
    echo ""
done

echo "8. Request Latency Warnings"
echo "-------------------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -i "slow\|timeout\|latency.*high" | \
        tail -20
    echo ""
done

echo "9. Recent Critical Errors (Last 50)"
echo "-----------------------------------"
for POD in $PODS; do
    echo "Pod: $POD"
    kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | \
        grep -iE "critical|fatal|panic" | \
        tail -50
    echo ""
done

echo "10. Log Summary Statistics"
echo "------------------------"
for POD in $PODS; do
    TOTAL_LINES=$(kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | wc -l)
    ERROR_LINES=$(kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | grep -ic "error\|exception" || echo "0")
    WARN_LINES=$(kubectl logs "$POD" -n "$NAMESPACE" --since="$TIME_RANGE" 2>/dev/null | grep -ic "warn" || echo "0")
    
    echo "Pod: $POD"
    echo "  Total log lines: $TOTAL_LINES"
    echo "  Error lines: $ERROR_LINES"
    echo "  Warning lines: $WARN_LINES"
    if [ "$TOTAL_LINES" -gt 0 ]; then
        ERROR_PERCENT=$((ERROR_LINES * 100 / TOTAL_LINES))
        echo "  Error percentage: ${ERROR_PERCENT}%"
    fi
    echo ""
done

echo "=========================================="
echo "Log Analysis Complete"
echo "=========================================="

