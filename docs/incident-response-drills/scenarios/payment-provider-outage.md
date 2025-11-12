# Payment Provider Outage Drill Scenario

## Scenario Overview

**Type**: Payment Failure  
**Severity**: High  
**Duration**: 2 hours  
**Objective**: Test response to payment provider outage, including circuit breaker activation, failover, and stakeholder communication.

## Scenario Setup

### Pre-Drill Setup

1. **Simulate Provider Outage**:
   - Configure test environment to simulate Stripe API failures
   - Set circuit breaker threshold to trigger quickly
   - Ensure backup provider (PayPal) is available

2. **Prepare Monitoring**:
   - Set up test alerts
   - Prepare Grafana dashboards
   - Configure log aggregation

3. **Team Preparation**:
   - Assign roles (Incident Commander, Technical Lead, Communications Lead)
   - Brief team on scenario
   - Ensure access to all tools

## Scenario Timeline

### T+0:00 - Incident Detection

**Simulated Event**: Stripe API returns 500 errors for all payment requests.

**Expected Actions**:
- [ ] Alert triggered in monitoring system
- [ ] Team notified via Slack/PagerDuty
- [ ] Incident Commander assigned
- [ ] Incident channel created

**Success Criteria**:
- Alert received within 5 minutes
- Team notified within 10 minutes
- Incident channel created

### T+0:15 - Initial Assessment

**Simulated Event**: Circuit breaker opens after threshold reached.

**Expected Actions**:
- [ ] Verify circuit breaker status
- [ ] Check provider status page
- [ ] Assess impact (number of affected payments)
- [ ] Review recent payment failures

**Success Criteria**:
- Circuit breaker status verified
- Impact assessment completed
- Initial severity determined

### T+0:30 - Response Execution

**Simulated Event**: Automatic failover to backup provider.

**Expected Actions**:
- [ ] Verify failover mechanism activated
- [ ] Test backup provider connectivity
- [ ] Monitor payment success rate
- [ ] Update incident status

**Success Criteria**:
- Failover completed within 15 minutes
- Backup provider processing payments
- Success rate > 95%

### T+1:00 - Stakeholder Communication

**Simulated Event**: Business stakeholders request status update.

**Expected Actions**:
- [ ] Prepare status update
- [ ] Notify stakeholders
- [ ] Update incident dashboard
- [ ] Document actions taken

**Success Criteria**:
- Status update sent within 1 hour
- Stakeholders informed
- Documentation complete

### T+1:30 - Resolution

**Simulated Event**: Primary provider recovers.

**Expected Actions**:
- [ ] Verify provider recovery
- [ ] Test primary provider
- [ ] Plan failback (if needed)
- [ ] Update incident status

**Success Criteria**:
- Provider recovery verified
- Primary provider tested
- Failback plan documented

### T+2:00 - Post-Incident

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
- **Initial Assessment**: < 30 minutes
- **Failover**: < 15 minutes
- **Stakeholder Notification**: < 1 hour

### Procedure Adherence

- [ ] Runbook followed correctly
- [ ] Proper escalation procedures
- [ ] Correct use of diagnostic tools
- [ ] Appropriate access controls

### Communication

- [ ] Clear incident communication
- [ ] Regular status updates
- [ ] Stakeholder notification
- [ ] Documentation completeness

### Tool Utilization

- [ ] Monitoring dashboards used effectively
- [ ] Diagnostic scripts executed correctly
- [ ] Log analysis performed
- [ ] Health checks verified

## Success Metrics

- **MTTD (Mean Time To Detect)**: < 5 minutes
- **MTTR (Mean Time To Resolve)**: < 2 hours
- **Failover Time**: < 15 minutes
- **Communication Timeliness**: 100% within SLA

## Post-Drill Questions

1. Was the incident detected quickly enough?
2. Were the runbooks clear and easy to follow?
3. Was failover automatic or manual?
4. Were stakeholders notified in a timely manner?
5. What tools were most helpful?
6. What tools were missing or inadequate?
7. What would you do differently next time?

## Lessons Learned Template

- **What Went Well**: 
- **What Could Be Improved**: 
- **Action Items**: 
- **Runbook Updates Needed**: 

