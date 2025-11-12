#!/bin/bash
# Health Check Script for Payment Microservice
# Usage: ./health-check.sh [namespace] [deployment-name] [endpoint]

set -e

NAMESPACE="${1:-payment}"
DEPLOYMENT="${2:-payment-api}"
ENDPOINT="${3:-http://localhost:8080}"

echo "=========================================="
echo "Health Check Script"
echo "Namespace: $NAMESPACE"
echo "Deployment: $DEPLOYMENT"
echo "Endpoint: $ENDPOINT"
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

# Health check function
check_health() {
    local POD=$1
    local ENDPOINT=$2
    
    echo "Checking pod: $POD"
    
    # Basic health check
    echo -n "  Basic health: "
    HEALTH=$(kubectl exec "$POD" -n "$NAMESPACE" -- curl -s -o /dev/null -w "%{http_code}" "$ENDPOINT/health" 2>/dev/null || echo "000")
    if [ "$HEALTH" = "200" ]; then
        echo "✓ OK"
    else
        echo "✗ FAILED (HTTP $HEALTH)"
    fi
    
    # Readiness check
    echo -n "  Readiness: "
    READY=$(kubectl exec "$POD" -n "$NAMESPACE" -- curl -s -o /dev/null -w "%{http_code}" "$ENDPOINT/health/ready" 2>/dev/null || echo "000")
    if [ "$READY" = "200" ]; then
        echo "✓ OK"
    else
        echo "✗ FAILED (HTTP $READY)"
    fi
    
    # Liveness check
    echo -n "  Liveness: "
    LIVE=$(kubectl exec "$POD" -n "$NAMESPACE" -- curl -s -o /dev/null -w "%{http_code}" "$ENDPOINT/health/live" 2>/dev/null || echo "000")
    if [ "$LIVE" = "200" ]; then
        echo "✓ OK"
    else
        echo "✗ FAILED (HTTP $LIVE)"
    fi
    
    # Circuit breaker status
    echo -n "  Circuit breaker: "
    CB=$(kubectl exec "$POD" -n "$NAMESPACE" -- curl -s "$ENDPOINT/health/circuit-breaker" 2>/dev/null || echo "unavailable")
    if [ "$CB" != "unavailable" ]; then
        echo "$CB"
    else
        echo "✗ UNAVAILABLE"
    fi
    
    # Database connectivity
    echo -n "  Database: "
    DB=$(kubectl exec "$POD" -n "$NAMESPACE" -- curl -s "$ENDPOINT/health/db" 2>/dev/null || echo "unavailable")
    if [ "$DB" != "unavailable" ]; then
        if echo "$DB" | grep -q "healthy"; then
            echo "✓ OK"
        else
            echo "✗ FAILED"
        fi
    else
        echo "✗ UNAVAILABLE"
    fi
    
    # Payment provider health
    echo -n "  Payment providers: "
    PROVIDERS=$(kubectl exec "$POD" -n "$NAMESPACE" -- curl -s "$ENDPOINT/health/providers" 2>/dev/null || echo "unavailable")
    if [ "$PROVIDERS" != "unavailable" ]; then
        echo "$PROVIDERS"
    else
        echo "✗ UNAVAILABLE"
    fi
    
    echo ""
}

# Check each pod
for POD in $PODS; do
    check_health "$POD" "$ENDPOINT"
done

# Overall deployment status
echo "Deployment Status"
echo "-----------------"
kubectl get deployment "$DEPLOYMENT" -n "$NAMESPACE" -o jsonpath='{.status}' | jq '.' 2>/dev/null || kubectl get deployment "$DEPLOYMENT" -n "$NAMESPACE"

echo ""
echo "Pod Status Summary"
echo "------------------"
kubectl get pods -n "$NAMESPACE" -l app="$DEPLOYMENT" -o custom-columns=NAME:.metadata.name,STATUS:.status.phase,READY:.status.containerStatuses[0].ready,RESTARTS:.status.containerStatuses[0].restartCount

echo ""
echo "=========================================="
echo "Health Check Complete"
echo "=========================================="

