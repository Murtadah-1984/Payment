#!/bin/bash
# Database Diagnostic Queries for Payment Microservice
# Usage: ./database-queries.sh [query-type] [connection-string]
# Query types: health, payments, failures, audit, performance

set -e

QUERY_TYPE="${1:-health}"
CONNECTION_STRING="${2:-}"

if [ -z "$CONNECTION_STRING" ]; then
    echo "ERROR: Connection string is required"
    echo "Usage: ./database-queries.sh [query-type] [connection-string]"
    echo "Query types: health, payments, failures, audit, performance"
    exit 1
fi

# Check if psql is available
if ! command -v psql &> /dev/null; then
    echo "ERROR: psql is not installed or not in PATH"
    exit 1
fi

echo "=========================================="
echo "Database Diagnostic Queries"
echo "Query Type: $QUERY_TYPE"
echo "=========================================="
echo ""

case "$QUERY_TYPE" in
    health)
        echo "1. Database Health Check"
        echo "-----------------------"
        psql "$CONNECTION_STRING" -c "SELECT version();"
        psql "$CONNECTION_STRING" -c "SELECT pg_database_size(current_database()) as database_size;"
        psql "$CONNECTION_STRING" -c "SELECT count(*) as active_connections FROM pg_stat_activity WHERE state = 'active';"
        psql "$CONNECTION_STRING" -c "SELECT count(*) as idle_connections FROM pg_stat_activity WHERE state = 'idle';"
        ;;
    
    payments)
        echo "2. Payment Statistics (Last 24 Hours)"
        echo "--------------------------------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                status,
                COUNT(*) as count,
                SUM(amount) as total_amount
            FROM \"Payments\"
            WHERE \"CreatedAt\" >= NOW() - INTERVAL '24 hours'
            GROUP BY status
            ORDER BY count DESC;
        "
        
        echo ""
        echo "3. Recent Failed Payments"
        echo "-------------------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                \"Id\",
                \"OrderId\",
                \"Status\",
                \"Amount\",
                \"Provider\",
                \"CreatedAt\",
                \"FailureReason\"
            FROM \"Payments\"
            WHERE \"Status\" IN ('Failed', 'Cancelled')
            AND \"CreatedAt\" >= NOW() - INTERVAL '1 hour'
            ORDER BY \"CreatedAt\" DESC
            LIMIT 20;
        "
        ;;
    
    failures)
        echo "4. Payment Failure Analysis"
        echo "--------------------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                \"Provider\",
                \"Status\",
                COUNT(*) as failure_count,
                AVG(EXTRACT(EPOCH FROM (\"UpdatedAt\" - \"CreatedAt\"))) as avg_processing_time_seconds
            FROM \"Payments\"
            WHERE \"Status\" IN ('Failed', 'Cancelled')
            AND \"CreatedAt\" >= NOW() - INTERVAL '24 hours'
            GROUP BY \"Provider\", \"Status\"
            ORDER BY failure_count DESC;
        "
        
        echo ""
        echo "5. Failure Reasons"
        echo "-----------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                \"FailureReason\",
                COUNT(*) as count
            FROM \"Payments\"
            WHERE \"FailureReason\" IS NOT NULL
            AND \"CreatedAt\" >= NOW() - INTERVAL '24 hours'
            GROUP BY \"FailureReason\"
            ORDER BY count DESC
            LIMIT 10;
        "
        ;;
    
    audit)
        echo "6. Recent Audit Log Entries"
        echo "--------------------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                \"Id\",
                \"UserId\",
                \"EventType\",
                \"Resource\",
                \"IpAddress\",
                \"Timestamp\"
            FROM \"AuditLogs\"
            WHERE \"Timestamp\" >= NOW() - INTERVAL '1 hour'
            ORDER BY \"Timestamp\" DESC
            LIMIT 50;
        "
        
        echo ""
        echo "7. Security Events"
        echo "-----------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                \"EventType\",
                COUNT(*) as count,
                COUNT(DISTINCT \"UserId\") as unique_users,
                COUNT(DISTINCT \"IpAddress\") as unique_ips
            FROM \"AuditLogs\"
            WHERE \"EventType\" LIKE '%Security%'
            AND \"Timestamp\" >= NOW() - INTERVAL '24 hours'
            GROUP BY \"EventType\"
            ORDER BY count DESC;
        "
        ;;
    
    performance)
        echo "8. Slow Queries (if pg_stat_statements is enabled)"
        echo "------------------------------------------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                query,
                calls,
                total_exec_time,
                mean_exec_time,
                max_exec_time
            FROM pg_stat_statements
            WHERE query NOT LIKE '%pg_stat_statements%'
            ORDER BY mean_exec_time DESC
            LIMIT 10;
        " 2>/dev/null || echo "pg_stat_statements extension not available"
        
        echo ""
        echo "9. Table Sizes"
        echo "-------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                schemaname,
                tablename,
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
            FROM pg_tables
            WHERE schemaname = 'public'
            ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
        "
        
        echo ""
        echo "10. Index Usage"
        echo "---------------"
        psql "$CONNECTION_STRING" -c "
            SELECT 
                schemaname,
                tablename,
                indexname,
                idx_scan as index_scans,
                idx_tup_read as tuples_read,
                idx_tup_fetch as tuples_fetched
            FROM pg_stat_user_indexes
            WHERE schemaname = 'public'
            ORDER BY idx_scan DESC
            LIMIT 20;
        "
        ;;
    
    *)
        echo "ERROR: Unknown query type: $QUERY_TYPE"
        echo "Available types: health, payments, failures, audit, performance"
        exit 1
        ;;
esac

echo ""
echo "=========================================="
echo "Query Complete"
echo "=========================================="

