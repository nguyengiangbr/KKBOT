# KKBOT Deployment Guide

## Overview

This guide covers deploying KKBOT using Docker, Docker Compose, and Kubernetes (Helm).

## Prerequisites

- Docker 24+ and Docker Compose 2+
- Kubernetes 1.28+ (for Helm deployment)
- Helm 3.12+
- kubectl configured for your cluster

---

## Docker Deployment

### Build the Image

```bash
# Build locally
docker build -t kkbot:latest .

# Or use docker-compose
docker-compose build
```

### Run with Docker Compose

#### Development
```bash
# Uses docker-compose.override.yml for hot reload
docker-compose up -d
```

#### Production
```bash
# Set required environment variables
export ENCRYPTION_PASSPHRASE="your-secure-passphrase"
export ENCRYPTION_SALT="your-secure-salt"
export POSTGRES_PASSWORD="secure-db-password"
export GRAFANA_PASSWORD="secure-grafana-password"

# Start with production profile
docker-compose --profile postgres --profile redis up -d
```

#### With Monitoring
```bash
docker-compose --profile monitoring up -d
# Access Prometheus at http://localhost:9090
# Access Grafana at http://localhost:3000 (admin/admin)
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ENCRYPTION_PASSPHRASE` | Passphrase for AES encryption | Required in production |
| `ENCRYPTION_SALT` | Salt for key derivation | Required in production |
| `POSTGRES_PASSWORD` | PostgreSQL password | Required with postgres profile |
| `GRAFANA_PASSWORD` | Grafana admin password | Required with monitoring profile |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | Production |
| `ConnectionStrings__KkbotDb` | Database connection string | `DataSource=/app/data/kkbot.db` |

### Volumes

- `kkbot_data`: SQLite database, secrets, persistent data
- `kkbot_logs`: Application logs
- `postgres_data`: PostgreSQL data (with postgres profile)
- `redis_data`: Redis data (with redis profile)
- `prometheus_data`: Prometheus metrics (with monitoring profile)
- `grafana_data`: Grafana dashboards (with monitoring profile)

---

## Kubernetes Deployment (Helm)

### Install Helm Chart

```bash
# Add repository (if published)
helm repo add kkbot https://your-org.github.io/kkbot-helm
helm repo update

# Or install from local chart
helm install kkbot ./helm/kkbot \
  --namespace kkbot \
  --create-namespace \
  --set image.repository=ghcr.io/your-org/kkbot \
  --set image.tag=v1.0.0
```

### Configuration

Create a `values-prod.yaml` for production:

```yaml
replicaCount: 2

image:
  repository: ghcr.io/your-org/kkbot
  tag: v1.0.0

ingress:
  enabled: true
  hosts:
    - host: kkbot.yourdomain.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: kkbot-tls
      hosts:
        - kkbot.yourdomain.com

persistence:
  data:
    enabled: true
    storageClass: fast-ssd
    size: 10Gi
  logs:
    enabled: true
    storageClass: standard
    size: 5Gi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10

secrets:
  create: true
  encryptionPassphrase: "your-production-passphrase"
  encryptionSalt: "your-production-salt"

postgresql:
  enabled: true
  auth:
    password: "secure-postgres-password"

redis:
  enabled: true

monitoring:
  prometheus:
    enabled: true
    serviceMonitor:
      enabled: true
```

Then install:
```bash
helm install kkbot ./helm/kkbot -f values-prod.yaml -n kkbot --create-namespace
```

### Upgrade

```bash
helm upgrade kkbot ./helm/kkbot -f values-prod.yaml -n kkbot
```

### Uninstall

```bash
helm uninstall kkbot -n kkbot
```

---

## GitHub Actions CI/CD

The repository includes a comprehensive CI/CD pipeline (`.github/workflows/ci.yml`):

### Pipeline Stages

1. **Build & Test**
   - Restore dependencies
   - Build solution
   - Run unit tests with coverage
   - Upload test results and coverage reports

2. **Docker Build**
   - Multi-arch build (amd64, arm64)
   - Push to GHCR with semantic tags
   - Generate SBOM

3. **Security Scan**
   - Trivy filesystem scan
   - CodeQL analysis

4. **Deploy Staging** (on `develop` branch)
   - Automatic deployment to staging environment

5. **Deploy Production** (on release)
   - Manual approval required
   - Deployment to production environment

### Required Secrets

Configure these in GitHub repository settings:

| Secret | Description |
|--------|-------------|
| `GITHUB_TOKEN` | Auto-provided for GHCR push |
| `KUBECONFIG_STAGING` | kubeconfig for staging cluster |
| `KUBECONFIG_PRODUCTION` | kubeconfig for production cluster |
| `ENCRYPTION_PASSPHRASE` | Production encryption passphrase |
| `ENCRYPTION_SALT` | Production encryption salt |
| `POSTGRES_PASSWORD` | Production PostgreSQL password |
| `GRAFANA_PASSWORD` | Production Grafana password |

---

## Monitoring & Observability

### Health Checks

- **Liveness**: `GET /health` - Kubernetes liveness probe
- **Readiness**: `GET /health` - Kubernetes readiness probe
- **Docker**: `curl -f http://localhost:8080/health`

### Metrics (Prometheus)

The application exposes metrics at `/metrics` (when configured):

- `kkbot_instances_total` - Total instances
- `kkbot_instances_status` - Instances by status
- `kkbot_jobs_total` - Total jobs
- `kkbot_jobs_status` - Jobs by status
- `kkbot_job_duration_seconds` - Job execution duration
- `kkbot_plugin_count` - Loaded plugins count

### Grafana Dashboards

Import the provided dashboards from `monitoring/grafana/dashboards/` or create custom ones using the metrics above.

### Logging

Logs are written to:
- Console (stdout) - captured by Docker/Kubernetes
- File: `/app/logs/kkbot-YYYYMMDD.log` - rolling daily, 30 days retention

Configure log aggregation with:
- **Loki** + **Promtail** (recommended for Kubernetes)
- **ELK Stack** (Elasticsearch, Logstash, Kibana)
- **Datadog** / **New Relic** / **Azure Monitor**

---

## Backup & Disaster Recovery

### Database Backup

```bash
# SQLite (development)
kubectl exec -n kkbot deployment/kkbot -- cp /app/data/kkbot.db /app/data/kkbot.db.backup.$(date +%Y%m%d)

# PostgreSQL (production)
kubectl exec -n kkbot postgresql-0 -- pg_dump -U kkbot kkbot > kkbot-backup-$(date +%Y%m%d).sql
```

### Restore

```bash
# SQLite
kubectl cp kkbot-backup.db kkbot/kkbot-pod:/app/data/kkbot.db

# PostgreSQL
kubectl exec -i -n kkbot postgresql-0 -- psql -U kkbot kkbot < kkbot-backup.sql
```

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| ImagePullBackOff | Check GHCR credentials / image name |
| CrashLoopBackOff | Check logs: `kubectl logs -n kkbot deployment/kkbot` |
| Health check failing | Verify `/health` endpoint responds |
| PVC pending | Check storage class availability |
| Secrets not loaded | Verify secret exists and keys match |

### Debug Commands

```bash
# View logs
kubectl logs -n kkbot deployment/kkbot -f

# Exec into pod
kubectl exec -n kkbot -it deployment/kkbot -- /bin/bash

# Check events
kubectl get events -n kkbot --sort-by='.lastTimestamp'

# Describe resources
kubectl describe pod -n kkbot -l app.kubernetes.io/name=kkbot
```

---

## Security Checklist

- [ ] Change default encryption passphrase and salt
- [ ] Use strong PostgreSQL password
- [ ] Enable TLS with valid certificates
- [ ] Restrict network policies
- [ ] Enable Pod Security Standards
- [ ] Scan images for vulnerabilities regularly
- [ ] Rotate secrets periodically
- [ ] Audit RBAC permissions
- [ ] Enable audit logging

---

## Scaling Considerations

### Horizontal Scaling
- Increase `replicaCount` or enable HPA
- Use Redis for session/cache sharing
- Consider PostgreSQL read replicas

### Vertical Scaling
- Adjust `resources.limits` and `requests`
- Monitor CPU/memory usage

### Plugin Scaling
- Mount plugins via ReadOnlyMany PVC
- Consider separate plugin loader sidecar for large plugin sets

---

## Support

For deployment issues:
1. Check logs and events
2. Review this guide
3. Open GitHub issue with relevant logs
4. Consult Kubernetes/Docker documentation