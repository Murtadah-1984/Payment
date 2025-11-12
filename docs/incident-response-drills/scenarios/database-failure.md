# Database Failure Drill Scenario

## Scenario Overview

**Type**: Infrastructure Failure  
**Severity**: Critical  
**Duration**: 2.5 hours  
**Objective**: Test response to database connectivity issues, including failover, data recovery, and service restoration.

## Scenario Setup

### Pre-Drill Setup

1. **Simulate Database Failure**:
   - Configure test database to reject connections
   - Simulate connection pool exhaustion
   - Prepare database failover scenario

2. **Prepare Monitoring**:
   - Set up database health checks
   - Configure database metrics
   - Prepare diagnostic queries

3. **Team Preparation**:
   - Assign roles (DBA, Incident Commander, Technical Lead)
   - Brief team on scenario
   - Ensure database access

## Scenario Timeline

### T+0:00 - Incident Detection

**Simulated Event**: Database connection pool exhausted, connections failing.

**Expected Actions**:
- [ ] Database health check fails
- [ ] Alert triggered
- [ ] Team notified
- [ ] Incident Commander assigned

**Success Criteria**:
- Alert received within 5 minutes
- Team notified within 10 minutes
- Incident channel created

### T+0:15 - Initial Assessment

**Simulated Event**: Database server unresponsive.

**Expected Actions**:
- [ ] Verify database connectivity
- [ ] Check database server status
- [ ] Review connection pool metrics
- [ ] Assess impact on services

**Success Criteria**:
- Database status verified
- Impact assessed
- Severity determined

### T+0:30 - Diagnosis

**Simulated Event**: Primary database server down.

**Expected Actions**:
- [ ] Execute diagnostic queries
- [ ] Review database logs
- [ ] Check replication status
- [ ] Verify backup availability

**Success Criteria**:
- Root cause identified
- Replication status verified
- Backup availability confirmed

### T+1:00 - Failover

**Simulated Event**: Failover to standby database.

**Expected Actions**:
- [ ] Initiate database failover
- [ ] Verify standby database
- [ ] Update connection strings
- [ ] Test application connectivity

**Success Criteria**:
- Failover completed
- Standby database verified
- Application connected

### T+1:30 - Service Restoration

**Simulated Event**: Services restored with standby database.

**Expected Actions**:
- [ ] Verify service health
- [ ] Test payment processing
- [ ] Monitor performance
- [ ] Update incident status

**Success Criteria**:
- Services healthy
- Payment processing functional
- Performance acceptable

### T+2:00 - Recovery

**Simulated Event**: Primary database server recovered.

**Expected Actions**:
- [ ] Verify primary database
- [ ] Plan failback
- [ ] Test primary database
- [ ] Schedule maintenance window

**Success Criteria**:
- Primary database verified
- Failback plan documented
- Maintenance scheduled

### T+2:30 - Post-Incident

**Expected Actions**:
- [ ] Conduct post-incident review
- [ ] Document lessons learned
- [ ] Update runbooks
- [ ] Create action items

**Success Criteria**:
- Post-incident review completed
- Lessons learned documented
- Action items created

## Evaluation Criteria

### Response Time

- **Detection**: < 5 minutes
- **Acknowledgment**: < 10 minutes
- **Diagnosis**: < 30 minutes
- **Failover**: < 1 hour
- **Service Restoration**: < 1.5 hours

### Procedure Adherence

- [ ] Database runbook followed
- [ ] Proper failover procedures
- [ ] Correct diagnostic queries
- [ ] Appropriate access controls

### Communication

- [ ] Clear incident communication
- [ ] Regular status updates
- [ ] Stakeholder notification
- [ ] Documentation completeness

### Tool Utilization

- [ ] Database diagnostic scripts used
- [ ] Monitoring dashboards reviewed
- [ ] Log analysis performed
- [ ] Health checks verified

## Success Metrics

- **MTTD (Mean Time To Detect)**: < 5 minutes
- **MTTR (Mean Time To Resolve)**: < 2.5 hours
- **Failover Time**: < 1 hour
- **Service Restoration**: < 1.5 hours

## Post-Drill Questions

1. Was the database failure detected quickly?
2. Were diagnostic tools adequate?
3. Was failover smooth?
4. Were services restored quickly?
5. What would you do differently next time?

## Lessons Learned Template

- **What Went Well**: 
- **What Could Be Improved**: 
- **Action Items**: 
- **Runbook Updates Needed**: 

