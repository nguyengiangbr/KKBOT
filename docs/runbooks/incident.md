# Runbook: Incident Response

## Overview
Standardized incident response for KKBOT Framework production issues.

## Incident Classification

### SEV-1: Critical (Service Down / Data Breach)
- Response Time: 15 minutes
- Escalation: CISO, CTO, Legal
- Examples: License server down, data breach, all instances failing

### SEV-2: High (Major Feature Down / Data Leak Risk)
- Response Time: 1 hour
- Escalation: Security Lead, Engineering Lead
- Examples: Dashboard unavailable, module failures, license validation issues

### SEV-3: Medium (Minor Feature Degraded)
- Response Time: 4 hours
- Escalation: Team Lead
- Examples: Performance degradation, UI bugs, non-critical module issues

### SEV-4: Low (Minor Issue / Workaround Exists)
- Response Time: 1 business day
- Escalation: Team Member
- Examples: Cosmetic issues, documentation errors, minor UI glitches

## Incident Response Phases

### Phase 1: Detection (T+0)
Trigger: Monitoring alert, user report, automated health check failure

Actions:
1. Acknowledge alert in monitoring system
2. Create incident ticket (Jira/GitHub Issues)
3. Assign incident commander
4. Start incident timeline document

Tools:
- Prometheus AlertManager
- Grafana dashboards
- Custom health checks

### Phase 2: Triage (T+15min)
Actions:
1. Assess severity using classification matrix
2. Assemble response team
3. Communicate status to stakeholders
4. Begin evidence collection

Communication:
```markdown
# Incident #[NUMBER] - [SEVERITY] - [TITLE]
**Status**: Investigating
**Impact**: [User-facing impact description]
**Start Time**: [ISO timestamp]
**Commander**: [Name]
**Team**: [Names]
```

### Phase 3: Containment (T+30min)
Actions:
1. Isolate affected systems
2. Implement mitigations
3. Prevent further damage
4. Preserve evidence

Common Containment Actions:
- Disable affected module: KKBOT.CLI.exe module disable MODULE_NAME
- Rate limit API: Increase rate limit thresholds temporarily
- Failover to backup: Switch to blue environment
- Block malicious IPs: Update firewall rules

### Phase 4: Eradication (T+1hr)
Actions:
1. Identify root cause
2. Apply fix/patch
3. Remove threat artifacts
4. Verify fix in staging

Root Cause Categories:
- Code regression
- Configuration error
- Infrastructure failure
- External dependency
- Security vulnerability

### Phase 5: Recovery (T+2hr)
Actions:
1. Deploy fix to production
2. Verify health checks pass
3. Monitor for regression
4. Gradually restore traffic

Recovery Validation:
- Health checks green
- Performance metrics normal
- Error rates baseline
- User reports resolved

### Phase 6: Post-Incident (T+24hr)
Actions:
1. Write post-mortem document
2. Schedule blameless retrospective
3. Create action items
4. Update runbooks
5. Close incident ticket

Post-Mortem Template:
```markdown
# Post-Mortem: Incident #[NUMBER]

## Summary
- Date: [Date]
- Duration: [Start] to [End]
- Severity: [SEV-1/2/3/4]
- Impact: [Users affected, revenue, etc.]

## Timeline
- [Timestamp] - Detection
- [Timestamp] - Triage
- [Timestamp] - Containment
- [Timestamp] - Eradication
- [Timestamp] - Recovery

## Root Cause
[5 Whys analysis]

## Action Items
- [ ] [Action] - Owner: [Name] - Due: [Date]
- [ ] [Action] - Owner: [Name] - Due: [Date]

## Lessons Learned
[What went well, what didn't, improvements]
```

## Communication Protocols

### Internal (Slack/Teams)
- Dedicated incident channel: #incident-[NUMBER]
- Status updates every 15 min (SEV-1), 30 min (SEV-2), 1 hr (SEV-3)
- @channel for escalation

### External (Customer/Stakeholder)
- Status page updates: status.kkbot.io
- Email notifications for SEV-1/2
- Template messages for each severity

### Status Page Templates
SEV-1: We are experiencing a major service disruption. Our team is investigating. Updates every 15 minutes.
SEV-2: Some users may experience issues with [feature]. We are working on a fix.
SEV-3: Minor performance degradation detected. Monitoring closely.

## Runbook References
- License server down -> License server recovery runbook
- Module failure -> Module recovery runbook
- Database issue -> Database recovery runbook
- Security breach -> Security incident runbook

## Tools & Access
| Tool | Purpose | Access |
|------|---------|--------|
| Grafana | Metrics/Dashboards | All engineers |
| Prometheus | Alerts/Metrics | All engineers |
| Jaeger | Distributed tracing | All engineers |
| ELK/Seq | Log aggregation | All engineers |
| Azure/AWS Console | Infrastructure | DevOps team |
| License Server Admin | License management | Support team |

## Post-Incident Checklist
- [ ] Incident ticket closed
- [ ] Post-mortem written and shared
- [ ] Action items created in Jira
- [ ] Runbooks updated if needed
- [ ] Monitoring rules updated
- [ ] Stakeholder debrief completed
- [ ] Metrics reviewed for improvements

## Contact Matrix
| Role | Primary | Secondary |
|------|---------|-----------|
| Incident Commander | On-call Engineer | Team Lead |
| Communications | Team Lead | Product Manager |
| Technical Lead | Senior Engineer | Architect |
| Database | DBA | DevOps |
| Security | Security Engineer | CISO |
| Legal/Compliance | Legal Counsel | CTO |

## Emergency Contacts
- On-call Engineer: +1-XXX-XXX-XXXX
- Team Lead: +1-XXX-XXX-XXXX
- Engineering Director: +1-XXX-XXX-XXXX
- Security Team: +1-XXX-XXX-XXXX
- Legal: +1-XXX-XXX-XXXX