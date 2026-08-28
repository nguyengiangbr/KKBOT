# Deployment Guide

## Overview

This guide covers deploying KKBOT in various environments: local development, Docker, and Kubernetes.

## Prerequisites

- .NET 10 SDK
- Docker 24+
- Kubernetes 1.28+ (for production)
- PostgreSQL 16 / SQLite (development)
- Redis 7+
- Ollama (for AI features)

## Local Development

### 1. Clone and Build

```bash
git clone https://github.com/your-org/kkbot.git
cd kkbot
dotnet restore
dotnet build
```

### 2. Run Database Migrations

```bash
dotnet ef database update --project src/KKBOT.Persistence.EfCore
```

### 3. Start Services

```bash
# Terminal 1: Start API
dotnet run --project src/KKBOT.API

# Terminal 2: Start Dashboard
dotnet run --project src/KKBOT.UI.Dashboard
```

### 4. Access Dashboard

Open http://localhost:5000 in browser.

## Docker Deployment

### Development with Docker Compose

```bash
docker-compose up -d
```

Services started:
- kkbot-api (port 8080)
- kkbot-dashboard (port 8081)
- postgres (port 5432)
- redis (port 6379)
- ollama (port 11434)
- jaeger (port 16686)
- prometheus (port 9090)
- grafana (port 3000)

### Production with Docker Compose

```bash
docker-compose --profile postgres --profile redis up -d
```

## Kubernetes Deployment

### Prerequisites

- Kubernetes cluster 1.28+
- Helm 3.12+
- cert-manager (for TLS)
- ingress-nginx

### Install with Helm

```bash
# Add Helm repo
helm repo add kkbot https://charts.kkbot.example.com
helm repo update

# Install
helm install kkbot ./helm/kkbot \
  --namespace kkbot \
  --create-namespace \
  -f values-prod.yaml
```

### values-prod.yaml Example

```yaml
image:
  repository: ghcr.io/your-org/kkbot
  tag: latest
  pullPolicy: IfNotPresent

replicaCount: 3

resources:
  limits:
    cpu: 2000m
    memory: 2Gi
  requests:
    cpu: 500m
    memory: 512Mi

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 20
  targetCPUUtilizationPercentage: 70

postgresql:
  enabled: true
  auth:
    username: kkbot
    password: POSTGRES_PASSWORD_PLACEHOLDER
    database: kkbot

redis:
  enabled: true
  auth:
    password: REDIS_PASSWORD_PLACEHOLDER

ollama:
  enabled: true
  gpu:
    enabled: true

ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
  hosts:
    - host: kkbot.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: kkbot-tls
      hosts:
        - kkbot.example.com

monitoring:
  prometheus:
    enabled: true
  grafana:
    enabled: true
  jaeger:
    enabled: true
```

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| CONNECTIONSTRINGS__KKBOTDB | Database connection string | Yes |
| SECURITY__ENCRYPTIONPASSPHRASE | Encryption passphrase (32 chars) | Yes |
| SECURITY__ENCRYPTIONSALT | Encryption salt (16 chars) | Yes |
| JWT__KEY | JWT signing key (32+ chars) | Yes |
| JWT__ISSUER | JWT issuer | Yes |
| JWT__AUDIENCE | JWT audience | Yes |
| AI__OLLAMA__BASEURL | Ollama API URL | No |
| AI__OLLAMA__MODEL | Default LLM model | No |
| REDIS__CONNECTIONSTRING | Redis connection string | Yes |
| TELEGRAM__BOTTOKEN | Telegram bot token | No |
| TELEGRAM__CHATIDS | Allowed chat IDs (comma-separated) | No |

### Secrets Management

Store secrets in:
- Kubernetes Secrets
- HashiCorp Vault
- Azure Key Vault
- AWS Secrets Manager

Example Kubernetes Secret:
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: kkbot-secrets
  namespace: kkbot
type: Opaque
stringData:
  ConnectionStrings__KkbotDb: "Host=postgres;Database=kkbot;Username=kkbot;Password=xxx"
  Security__EncryptionPassphrase: "your-32-char-passphrase-here"
  Security__EncryptionSalt: "your-16-char-salt"
  Jwt__Key: "your-super-secret-jwt-key-min-32-chars"
  Jwt__Issuer: "KKBOT"
  Jwt__Audience: "KKBOT"
  Redis__ConnectionString: "redis://:password@redis:6379"
```

## Plugin Deployment

### Building Plugins

```bash
dotnet build src/KKBOT.Plugin.AccountLogin -c Release
dotnet build src/KKBOT.Plugin.AntiDetection -c Release
dotnet build src/KKBOT.Plugin.ResourceOptimizer -c Release
```

### Installing Plugins

Copy plugin DLLs to plugins directory:

```bash
mkdir -p plugins
cp src/KKBOT.Plugin.AccountLogin/bin/Release/net10.0/KKBOT.Plugin.AccountLogin.dll plugins/
cp src/KKBOT.Plugin.AntiDetection/bin/Release/net10.0/KKBOT.Plugin.AntiDetection.dll plugins/
cp src/KKBOT.Plugin.ResourceOptimizer/bin/Release/net10.0/KKBOT.Plugin.ResourceOptimizer.dll plugins/
```

### WASM Plugins

For Rust-based WASM plugins:

```bash
cd samples/wasm-plugin-sample
cargo build --release --target wasm32-wasi
cp target/wasm32-wasi/release/kkbot_plugin_sample.wasm plugins/
cp samples/wasm-plugin-sample/manifest.json plugins/
```

## Monitoring & Observability

### Health Checks

- API: `GET /health`
- Dashboard: Built-in health checks

### Metrics

- Prometheus: `GET /metrics`
- Grafana dashboards: Import from `monitoring/grafana/dashboards/`

### Logs

- Loki: Configured via Docker Compose
- Structured JSON logs in `logs/`

### Traces

- Jaeger: http://localhost:16686
- OTLP: http://localhost:4317

## Scaling

### Horizontal Pod Autoscaler

Configured via Helm values:
- CPU-based scaling (70% target)
- Custom metrics scaling (queue depth, active instances)

### Database

- Read replicas for read-heavy workloads
- Connection pooling (PgBouncer for PostgreSQL)

### Redis

- Cluster mode for high availability
- Sentinel for failover

## Backup & Recovery

### Database Backup

```bash
# PostgreSQL
pg_dump -h postgres -U kkbot kkbot > backup_$(date +%Y%m%d).sql

# Restore
psql -h postgres -U kkbot kkbot < backup_20240115.sql
```

### Plugin Backup

```bash
tar -czf plugins_backup_$(date +%Y%m%d).tar.gz plugins/
```

## Security

### Network Policies

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: kkbot-network-policy
  namespace: kkbot
spec:
  podSelector:
    matchLabels:
      app: kkbot
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx
      ports:
        - protocol: TCP
          port: 8080
  egress:
    - to:
        - namespaceSelector:
            matchLabels:
              name: kube-system
      ports:
        - protocol: TCP
          port: 53
        - protocol: UDP
          port: 53
```

### Pod Security

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: kkbot-api
spec:
  securityContext:
    runAsNonRoot: true
    runAsUser: 1000
    fsGroup: 1000
  containers:
    - name: kkbot-api
      securityContext:
        allowPrivilegeEscalation: false
        readOnlyRootFilesystem: true
        capabilities:
          drop:
            - ALL
```

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Database connection failed | Check connection string, network policies, PostgreSQL status |
| Redis connection failed | Verify Redis password, network connectivity |
| Ollama not responding | Check GPU availability, Ollama service status |
| Plugin not loading | Check DLL dependencies, plugin manifest validity |
| High memory usage | Enable RAM cleaner, reduce FPS limits |

### Logs

```bash
# View API logs
kubectl logs -n kkbot deployment/kkbot-api -f

# View Dashboard logs
kubectl logs -n kkbot deployment/kkbot-dashboard -f
```

### Debugging

Enable debug logging in appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "KKBOT": "Debug"
    }
  }
}
```

## Rollback Procedure

```bash
# Helm rollback
helm rollback kkbot <revision> -n kkbot

# Database rollback (if migration applied)
dotnet ef database update <previous_migration> --project src/KKBOT.Persistence.EfCore
```

## Support

- Documentation: https://docs.kkbot.example.com
- Issues: https://github.com/your-org/kkbot/issues
- Discussions: https://github.com/your-org/kkbot/discussions