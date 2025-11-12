#!/bin/bash
# Kubernetes Diagnostic Script for Payment Microservice
# Usage: ./kubernetes-diagnostics.sh [namespace] [deployment-name]

set -e

NAMESPACE="${1:-payment}"
DEPLOYMENT="${2:-payment-api}"

echo "=========================================="
echo "Kubernetes Diagnostic Script"
echo "Namespace: $NAMESPACE"
echo "Deployment: $DEPLOYMENT"
echo "=========================================="
echo ""

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "ERROR: kubectl is not installed or not in PATH"
    exit 1
fi

# Check namespace exists
if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
    echo "ERROR: Namespace '$NAMESPACE' does not exist"
    exit 1
fi

echo "1. Deployment Status"
echo "-------------------"
kubectl get deployment "$DEPLOYMENT" -n "$NAMESPACE" -o wide
echo ""

echo "2. Pod Status"
echo "-------------"
kubectl get pods -n "$NAMESPACE" -l app="$DEPLOYMENT" -o wide
echo ""

echo "3. Pod Resource Usage"
echo "---------------------"
kubectl top pods -n "$NAMESPACE" -l app="$DEPLOYMENT" 2>/dev/null || echo "Metrics server not available"
echo ""

echo "4. Recent Pod Events"
echo "--------------------"
kubectl get events -n "$NAMESPACE" --sort-by='.lastTimestamp' | grep "$DEPLOYMENT" | tail -20
echo ""

echo "5. Service Status"
echo "-----------------"
kubectl get svc -n "$NAMESPACE" -l app="$DEPLOYMENT"
echo ""

echo "6. Ingress Status"
echo "-----------------"
kubectl get ingress -n "$NAMESPACE" 2>/dev/null || echo "No ingress found"
echo ""

echo "7. ConfigMap Status"
echo "-------------------"
kubectl get configmap -n "$NAMESPACE" | grep -E "payment|config"
echo ""

echo "8. Secret Status (names only)"
echo "-----------------------------"
kubectl get secret -n "$NAMESPACE" | grep -E "payment|secret"
echo ""

echo "9. HPA Status"
echo "-------------"
kubectl get hpa -n "$NAMESPACE" 2>/dev/null || echo "No HPA found"
echo ""

echo "10. Pod Logs (last 50 lines from first pod)"
echo "--------------------------------------------"
POD_NAME=$(kubectl get pods -n "$NAMESPACE" -l app="$DEPLOYMENT" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
if [ -n "$POD_NAME" ]; then
    kubectl logs "$POD_NAME" -n "$NAMESPACE" --tail=50
else
    echo "No pods found"
fi
echo ""

echo "11. Health Check Endpoint"
echo "-------------------------"
POD_NAME=$(kubectl get pods -n "$NAMESPACE" -l app="$DEPLOYMENT" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
if [ -n "$POD_NAME" ]; then
    kubectl exec "$POD_NAME" -n "$NAMESPACE" -- curl -s http://localhost:8080/health || echo "Health check failed"
else
    echo "No pods found"
fi
echo ""

echo "12. Circuit Breaker Status"
echo "--------------------------"
POD_NAME=$(kubectl get pods -n "$NAMESPACE" -l app="$DEPLOYMENT" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
if [ -n "$POD_NAME" ]; then
    kubectl exec "$POD_NAME" -n "$NAMESPACE" -- curl -s http://localhost:8080/health/circuit-breaker 2>/dev/null || echo "Circuit breaker endpoint not available"
else
    echo "No pods found"
fi
echo ""

echo "=========================================="
echo "Diagnostic Complete"
echo "=========================================="

