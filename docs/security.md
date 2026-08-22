# Security Guide

Comprehensive security documentation for KKBOT.

## Table of Contents

1. [Threat Model](#threat-model)
2. [Secret Management](#secret-management)
3. [Encryption](#encryption)
4. [Authentication & Authorization](#authentication--authorization)
5. [Audit Logging](#audit-logging)
6. [Network Security](#network-security)
7. [Container Security](#container-security)
8. [Kubernetes Security](#kubernetes-security)
9. [Compliance](#compliance)
10. [Incident Response](#incident-response)

---

## Threat Model

### Assets to Protect

| Asset | Classification | Impact if Compromised |
|-------|---------------|----------------------|
| Game account credentials | **Critical** | Account takeover, financial loss |
| API keys (Ollama, OpenAI, etc.) | **High** | Unauthorized usage, cost, data leak |
| Encryption keys | **Critical** | All encrypted data exposed |
| Database (instances, jobs, configs) | **High** | Operational disruption, data leak |
| Plugin code | **Medium** | IP theft, supply chain attack |
| Audit logs | **Medium** | Forensic capability loss |

### Trust Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│                      Internet                               │
└────────────────────────────┬────────────────────────────────┘
                             │ HTTPS/TLS 1.3
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                   Ingress Controller                        │
│              (WAF, Rate Limit, TLS Termination)             │
└────────────────────────────┬────────────────────────────────┘
                             │ mTLS (optional)
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    Kubernetes Cluster                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   KKBOT     │  │  PostgreSQL │  │      Redis          │  │
│  │   Pods      │  │   (DB)      │  │   (Cache/Sessions)  │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                      │            │
│         └────────────────┴──────────────────────┘            │
│                          │                                     │
│                          ▼                                     │
│  ┌─────────────────────────────────────────────────────┐     │
│  │              HashiCorp Vault / Azure Key Vault       │     │
│  │         (Encryption keys, API keys, DB passwords)    │     │
│  └─────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

### Attack Vectors

1. **Credential Theft** - Phishing, keyloggers, memory scraping
2. **Injection Attacks** - SQL injection, command injection via plugin configs
3. **Plugin Supply Chain** - Malicious plugin code
4. **Container Escape** - Kernel exploits, misconfigured privileges
5. **Network Lateral Movement** - Compromised pod accessing DB
6. **Data Exfiltration** - Unauthorized access to logs, configs

---

## Secret Management

### Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Application   │────▶│  ISecretManager  │────▶│  Secret Store   │
│   (Consumers)   │     │   (Abstraction)  │     │  (Implementations)│
└─────────────────┘     └──────────────────┘     └────────┬────────┘
                                                          │
                        ┌─────────────────────────────────┼────────────────┐
                        ▼                                 ▼                ▼
               ┌─────────────────┐            ┌─────────────────┐ ┌─────────────┐
               │  FileSecretMgr  │            │ VaultSecretMgr  │ │  AzureKV    │
               │  (Dev/Local)    │            │  (Production)   │ │  SecretMgr  │
               └─────────────────┘            └─────────────────┘ └─────────────┘
```

### Interface

```csharp
public interface ISecretManager
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
    Task SetSecretAsync(string key, string value, CancellationToken ct = default);
    Task DeleteSecretAsync(string key, CancellationToken ct = default);
}
```

### Development: FileSecretManager

```json
// secrets.json (auto-created)
{
  "Database__Password": "dev-password",
  "Ollama__ApiKey": "dev-key",
  "Encryption__Passphrase": "dev-passphrase",
  "Encryption__Salt": "dev-salt"
}
```

```csharp
// Registration (automatic in dev)
services.AddSingleton<ISecretManager>(sp => 
    new FileSecretManager("secrets.json", sp.GetRequiredService<ILogger<FileSecretManager>>()));
```

### Production: HashiCorp Vault

```csharp
public sealed class VaultSecretManager : ISecretManager
{
    private readonly VaultClient _client;
    private readonly string _mountPoint;
    private readonly string _path;
    private readonly ILogger<VaultSecretManager> _logger;

    public VaultSecretManager(string vaultUrl, string token, string mountPoint, string path, ILogger<VaultSecretManager> logger)
    {
        _client = new VaultClient(new VaultClientSettings { Address = vaultUrl, Token = token });
        _mountPoint = mountPoint;
        _path = path;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        try
        {
            var secret = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                path: _path, 
                mountPoint: _mountPoint, 
                cancellationToken: ct);
            return secret?.Data?.Data?.GetValueOrDefault(key)?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get secret {Key} from Vault", key);
            return null;
        }
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken ct)
    {
        var data = new Dictionary<string, object> { [key] = value };
        await _client.V1.Secrets.KeyValue.V2.WriteSecretAsync(_path, data, _mountPoint, cancellationToken: ct);
    }

    public async Task DeleteSecretAsync(string key, CancellationToken ct)
    {
        // Vault KV v2 doesn't support single key delete easily
        // Read all, remove key, write back
        var secret = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(_path, _mountPoint, cancellationToken: ct);
        if (secret?.Data?.Data != null)
        {
            var data = secret.Data.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            data.Remove(key);
            await _client.V1.Secrets.KeyValue.V2.WriteSecretAsync(_path, data, _mountPoint, cancellationToken: ct);
        }
    }
}
```

### Registration

```csharp
// Production
services.AddSingleton<ISecretManager>(sp => 
    new VaultSecretManager(
        vaultUrl: "https://vault.example.com",
        token: Environment.GetEnvironmentVariable("VAULT_TOKEN")!,
        mountPoint: "secret",
        path: "kkbot/production",
        sp.GetRequiredService<ILogger<VaultSecretManager>>()));

// Azure Key Vault alternative
services.AddSingleton<ISecretManager>(sp => 
    new AzureKeyVaultSecretManager(
        vaultUrl: "https://myvault.vault.azure.net",
        credential: new DefaultAzureCredential(),
        sp.GetRequiredService<ILogger<AzureKeyVaultSecretManager>>()));
```

### Best Practices

| Practice | Description |
|----------|-------------|
| **Never hardcode secrets** | Use `ISecretManager` everywhere |
| **Rotate regularly** | Automate rotation every 90 days |
| **Least privilege** | Separate Vault policies per service |
| **Audit access** | Enable Vault audit logging |
| **Version secrets** | Use KV v2 for versioning |
| **Backup** | Regular Vault snapshots |

---

## Encryption

### AES-256-GCM (Recommended)

```csharp
public interface IEncryptionService
{
    string Encrypt(string plainText, string? associatedData = null);
    string Decrypt(string cipherText, string? associatedData = null);
}
```

### Implementation (AES-256-GCM with PBKDF2)

```csharp
public sealed class AesGcmEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private const int KeySize = 32;      // 256 bits
    private const int NonceSize = 12;    // 96 bits (recommended for GCM)
    private const int TagSize = 16;      // 128 bits
    private const int Iterations = 100_000;

    public AesGcmEncryptionService(string passphrase, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            passphrase, 
            Encoding.UTF8.GetBytes(salt), 
            Iterations, 
            HashAlgorithmName.SHA256);
        _key = pbkdf2.GetBytes(KeySize);
    }

    public string Encrypt(string plainText, string? associatedData = null)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var associatedBytes = associatedData != null ? Encoding.UTF8.GetBytes(associatedData) : null;
        var cipherBytes = new byte[plainBytes.Length + TagSize];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag, associatedBytes);

        // Combine: nonce + ciphertext + tag
        var result = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + cipherBytes.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText, string? associatedData = null)
    {
        var data = Convert.FromBase64String(cipherText);
        
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext");

        var nonce = data[..NonceSize];
        var cipherBytes = data[NonceSize..^TagSize];
        var tag = data[^TagSize..];
        var associatedBytes = associatedData != null ? Encoding.UTF8.GetBytes(associatedData) : null;
        var plainBytes = new byte[cipherBytes.Length - TagSize];

        using var aes = new AesGcm(_key);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes, associatedBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

### Key Derivation

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Algorithm | PBKDF2-SHA256 | NIST approved |
| Iterations | 100,000+ | OWASP recommendation (2023: 600k+) |
| Salt | 16+ bytes random | Unique per deployment |
| Key Size | 32 bytes (256 bits) | AES-256 |

### Key Rotation

```csharp
public sealed class KeyRotationService
{
    private readonly ISecretManager _secrets;
    private readonly IEncryptionService _current;
    private readonly ILogger<KeyRotationService> _logger;

    public async Task RotateAsync(CancellationToken ct)
    {
        // 1. Generate new passphrase/salt
        var newPassphrase = GenerateSecureString(32);
        var newSalt = GenerateSecureString(16);
        
        // 2. Store new keys in secret manager
        await _secrets.SetSecretAsync("encryption:current:passphrase", newPassphrase, ct);
        await _secrets.SetSecretAsync("encryption:current:salt", newSalt, ct);
        
        // 3. Archive old keys for decryption of existing data
        var oldPassphrase = await _secrets.GetSecretAsync("encryption:current:passphrase", ct);
        var oldSalt = await _secrets.GetSecretAsync("encryption:current:salt", ct);
        await _secrets.SetSecretAsync($"encryption:archive:{DateTime.UtcNow:yyyyMMdd}:passphrase", oldPassphrase!, ct);
        await _secrets.SetSecretAsync($"encryption:archive:{DateTime.UtcNow:yyyyMMdd}:salt", oldSalt!, ct);
        
        // 4. Re-encrypt sensitive data (background job)
        _logger.LogInformation("Key rotation initiated. Background re-encryption started.");
    }
}
```

---

## Authentication & Authorization

### Current State (v1.0)

- **No built-in auth** - Designed for trusted network deployment
- **API Keys** - Optional, configurable
- **Future**: OAuth2/OIDC, JWT, RBAC

### Planned Implementation

```csharp
// Future: JWT Authentication
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.example.com";
        options.Audience = "kkbot-api";
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

// Future: Role-based Authorization
services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    options.AddPolicy("Operator", policy => policy.RequireRole("operator", "admin"));
    options.AddPolicy("Viewer", policy => policy.RequireRole("viewer", "operator", "admin"));
});
```

### API Key Authentication (Optional)

```json
{
  "Security": {
    "ApiKeys": {
      "Enabled": true,
      "HeaderName": "X-API-Key",
      "ValidKeys": {
        "admin-key-123": ["admin"],
        "operator-key-456": ["operator"],
        "viewer-key-789": ["viewer"]
      }
    }
  }
}
```

---

## Audit Logging

### Interface

```csharp
public interface IAuditLogger
{
    Task LogAsync(string action, string userId, string details, CancellationToken ct = default);
}
```

### Implementation

```csharp
public sealed class AuditLogger : IAuditLogger
{
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ILogger<AuditLogger> logger) => _logger = logger;

    public Task LogAsync(string action, string userId, string details, CancellationToken ct)
    {
        var auditEntry = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            UserId = userId,
            Details = details,
            CorrelationId = Activity.Current?.Id,
            Source = "KKBOT"
        };

        _logger.LogInformation("AUDIT: {@AuditEntry}", auditEntry);
        return Task.CompletedTask;
    }
}
```

### Audit Events

| Event | Action | Details |
|-------|--------|---------|
| Instance Created | `INSTANCE_CREATE` | Instance ID, name, module, user |
| Instance Started | `INSTANCE_START` | Instance ID, user |
| Instance Stopped | `INSTANCE_STOP` | Instance ID, user |
| Job Created | `JOB_CREATE` | Job ID, type, instance, payload |
| Job Completed | `JOB_COMPLETE` | Job ID, status, duration |
| Plugin Loaded | `PLUGIN_LOAD` | Plugin ID, version, path |
| Config Changed | `CONFIG_CHANGE` | Section, old/new values (masked) |
| Secret Accessed | `SECRET_ACCESS` | Key, operation (get/set/delete) |
| Encryption Used | `ENCRYPT/DECRYPT` | Operation, key version |
| Login/Access | `ACCESS` | IP, user agent, success/failure |

### Structured Logging Format

```json
{
  "Timestamp": "2024-01-15T10:30:00.123Z",
  "Level": "Information",
  "MessageTemplate": "AUDIT: {AuditEntry}",
  "AuditEntry": {
    "Timestamp": "2024-01-15T10:30:00.123Z",
    "Action": "INSTANCE_START",
    "UserId": "admin",
    "Details": "Instance 'main-bot' started",
    "CorrelationId": "00-1234567890abcdef-1234567890abcdef-00",
    "Source": "KKBOT"
  }
}
```

### Log Retention

| Environment | Retention | Storage |
|-------------|-----------|---------|
| Development | 7 days | Local file |
| Staging | 30 days | Centralized (Seq/Elastic) |
| Production | 1 year | Immutable storage (S3/GCS + Glacier) |

---

## Network Security

### TLS Configuration

```yaml
# Ingress TLS
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
    nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
spec:
  tls:
  - hosts:
    - kkbot.example.com
    secretName: kkbot-tls
```

### mTLS (Service Mesh)

```yaml
# Istio PeerAuthentication
apiVersion: security.istio.io/v1beta1
kind: PeerAuthentication
metadata:
  name: default
  namespace: kkbot
spec:
  mtls:
    mode: STRICT
```

### Network Policies

```yaml
# Deny all by default
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: deny-all
  namespace: kkbot
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
---
# Allow KKBOT ingress
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-kkbot-ingress
  namespace: kkbot
spec:
  podSelector:
    matchLabels:
      app.kubernetes.io/name: kkbot
  policyTypes:
  - Ingress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
---
# Allow KKBOT to DB
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-kkbot-db
  namespace: kkbot
spec:
  podSelector:
    matchLabels:
      app.kubernetes.io/name: kkbot
  policyTypes:
  - Egress
  egress:
  - to:
    - podSelector:
        matchLabels:
          app.kubernetes.io/name: postgresql
    ports:
    - protocol: TCP
      port: 5432
```

---

## Container Security

### Dockerfile Hardening

```dockerfile
# Use distroless or minimal base
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Non-root user
RUN addgroup -g 1000 -S kkbot && adduser -u 1000 -S kkbot -G kkbot
USER kkbot

# Read-only root filesystem
# (Set in Kubernetes: readOnlyRootFilesystem: true)

# Drop all capabilities
# (Set in Kubernetes: capabilities: { drop: ["ALL"] })

# No shell
# ENTRYPOINT ["dotnet", "KKBOT.UI.Dashboard.dll"]
```

### Image Scanning

```yaml
# .github/workflows/ci.yml - Trivy scan
- name: Run Trivy vulnerability scanner
  uses: aquasecurity/trivy-action@master
  with:
    scan-type: 'fs'
    scan-ref: '.'
    format: 'sarif'
    output: 'trivy-results.sarif'
    severity: 'CRITICAL,HIGH'
    ignore-unfixed: true
```

### Base Image Updates

```yaml
# dependabot.yml
- package-ecosystem: "docker"
  directory: "/"
  schedule:
    interval: "weekly"
```

---

## Kubernetes Security

### Pod Security Standards

```yaml
# Restricted PodSecurityPolicy (or Pod Security Admission)
apiVersion: v1
kind: Namespace
metadata:
  name: kkbot
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
```

### Security Context

```yaml
# In deployment.yaml
securityContext:
  runAsNonRoot: true
  runAsUser: 1000
  runAsGroup: 1000
  fsGroup: 1000
  seccompProfile:
    type: RuntimeDefault
containers:
- name: kkbot
  securityContext:
    allowPrivilegeEscalation: false
    readOnlyRootFilesystem: true
    capabilities:
      drop:
      - ALL
    runAsNonRoot: true
    runAsUser: 1000
```

### RBAC

```yaml
# ServiceAccount with minimal permissions
apiVersion: v1
kind: ServiceAccount
metadata:
  name: kkbot
  namespace: kkbot
---
# Role for reading own resources
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: kkbot-self
  namespace: kkbot
rules:
- apiGroups: [""]
  resources: ["pods", "configmaps", "secrets"]
  verbs: ["get", "list", "watch"]
  resourceNames: ["kkbot-*"]  # Only own resources
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: kkbot-self
  namespace: kkbot
subjects:
- kind: ServiceAccount
  name: kkbot
  namespace: kkbot
roleRef:
  kind: Role
  name: kkbot-self
  apiGroup: rbac.authorization.k8s.io
```

### Secrets Management

```yaml
# External Secrets Operator (recommended)
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: kkbot-secrets
  namespace: kkbot
spec:
  refreshInterval: 1h
  secretStoreRef:
    kind: ClusterSecretStore
    name: vault-backend
  target:
    name: kkbot-secrets
    creationPolicy: Owner
  data:
  - secretKey: encryption-passphrase
    remoteRef:
      key: kkbot/production
      property: encryptionPassphrase
  - secretKey: encryption-salt
    remoteRef:
      key: kkbot/production
      property: encryptionSalt
  - secretKey: postgres-password
    remoteRef:
      key: kkbot/production
      property: postgresPassword
```

---

## Compliance

### GDPR

| Requirement | Implementation |
|-------------|----------------|
| Data minimization | Only store necessary instance/job data |
| Right to access | API endpoints to retrieve user data |
| Right to erasure | `DELETE /api/v1/instances/{id}` cascades |
| Data portability | Export instances/jobs as JSON |
| Privacy by design | Encryption at rest, pseudonymization |
| Breach notification | Audit logs + monitoring alerts |

### SOC 2 Type II

| Control | Implementation |
|---------|----------------|
| CC6.1 - Logical access | RBAC, mTLS, API keys |
| CC6.7 - Data encryption | AES-256-GCM, TLS 1.3 |
| CC7.2 - System monitoring | Health checks, Prometheus, audit logs |
| CC8.1 - Change management | GitOps, CI/CD, signed commits |

### Data Classification

```csharp
public enum DataClassification
{
    Public,        // Dashboard UI, docs
    Internal,      // Instance configs, job payloads
    Confidential,  // API keys, encryption keys
    Restricted     // Game credentials, PII
}
```

---

## Incident Response

### Runbook: Credential Compromise

1. **Detect** - Alert from audit log / Vault audit
2. **Contain** - Revoke compromised credentials immediately
   ```bash
   # Vault: revoke lease
   vault lease revoke -prefix auth/token/
   
   # Rotate encryption keys
   kubectl exec -n kkbot deploy/kkbot -- dotnet kkbot rotate-keys
   ```
3. **Assess** - Check audit logs for unauthorized access
4. **Recover** - Deploy new credentials, verify service health
5. **Post-mortem** - Document timeline, root cause, improvements

### Runbook: Container Compromise

1. **Detect** - Falco/Trivy alert, unusual process/network activity
2. **Contain** - Quarantine pod
   ```bash
   kubectl cordon <node>
   kubectl delete pod -n kkbot <compromised-pod> --force --grace-period=0
   ```
3. **Assess** - Forensic analysis of container image/logs
3. **Recover** - Deploy clean image, verify integrity
4. **Post-mortem** - Update base images, patch vulnerabilities

### Contacts

| Role | Contact | Escalation |
|------|---------|------------|
| Security Team | security@kkbot.example.com | Immediate |
| On-Call Engineer | +1-xxx-xxx-xxxx | 15 min |
| Management | management@kkbot.example.com | 1 hour |

---

## Security Checklist (Pre-Deployment)

- [ ] All secrets in Vault/Key Vault (no plaintext in config)
- [ ] Encryption keys rotated within 90 days
- [ ] TLS 1.3 enforced everywhere
- [ ] mTLS between services
- [ ] Network policies applied (default deny)
- [ ] Pod Security Standards: Restricted
- [ ] Non-root containers, read-only rootfs
- [ ] All capabilities dropped
- [ ] Image scanned (Trivy, 0 CRITICAL/HIGH)
- [ ] SBOM generated and reviewed
- [ ] Dependabot enabled for all ecosystems
- [ ] CodeQL analysis passing
- [ ] Audit logging to immutable storage
- [ ] Alert rules for security events
- [ ] Incident response plan tested
- [ ] Penetration test completed (annual)
- [ ] Security training for team (annual)

---

## Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)
- [Kubernetes Security Best Practices](https://kubernetes.io/docs/concepts/security/)
- [HashiCorp Vault Best Practices](https://www.vaultproject.io/docs/concepts/best-practices)
- [.NET Security Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/)