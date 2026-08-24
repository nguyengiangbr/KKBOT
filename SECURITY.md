# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in **KKBOT**, please disclose it responsibly by following these steps:

1. **Do not** publicly disclose the vulnerability.
2. Email the details to the security contact: `security@kkbot.dev`.
3. Include a clear description of the issue, steps to reproduce, and any relevant code snippets.
4. We will acknowledge receipt within 48 hours and work with you to resolve the issue.

## Supported versions

All releases that are listed on the [Releases page] are supported for security updates for a minimum of **12 months** after their release date.

| Version | Supported | End of Support |
|---------|-----------|----------------|
| 24.1.x  | Yes       | 2025-12-01     |
| 24.0.x  | Yes       | 2025-06-01     |
| 23.x    | No        | 2024-06-01     |

## Responsible disclosure

We appreciate any disclosures that help us improve the security of the project. Thank you for helping keep KKBOT safe!

## Security Architecture

### Authentication & Authorization

| Layer | Mechanism |
|-------|-----------|
| **Authentication** | JWT (RS256), Refresh Tokens |
| **Authorization** | RBAC + Resource-based (PermissionAuthorizationHandler) |
| **API Security** | Rate Limiting, CORS, Security Headers |
| **Secrets** | AES-256, Key Rotation, Vault-ready |

### Plugin Security

| Layer | Protection |
|-------|------------|
| **Sandbox** | WASM (Wasmtime) with fuel metering |
| **Capabilities** | 20 granular permissions |
| **Signing** | Ed25519 signatures |
| **Isolation** | AssemblyLoadContext / WASM memory isolation |

### Data Protection

| Data | Protection |
|------|------------|
| **Secrets** | AES-256-GCM, Key Rotation |
| **PII** | Encryption at rest, Minimization |
| **Audit** | Immutable, Append-only |
| **Transport** | TLS 1.3, mTLS for gRPC |

## Security Headers

All HTTP responses include:
- `Content-Security-Policy`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`

## Rate Limiting

- API: 100 requests/minute per IP
- Auth endpoints: 10 requests/minute
- Plugin operations: 50 requests/minute

## Audit Logging

All security-relevant events are logged:
- Authentication attempts (success/failure)
- Authorization decisions
- Plugin load/unload
- Configuration changes
- Admin actions

## Dependency Scanning

- Automated via GitHub Dependabot
- Weekly scheduled scans
- Automated PR for vulnerable dependencies

## Penetration Testing

Annual third-party penetration testing conducted. Results available upon request.

## Bug Bounty

We do not currently offer a formal bug bounty program, but we acknowledge and credit security researchers in our [Hall of Fame](https://github.com/your-org/kkbot/security/advisories).

---

*This security policy was created for the KKBOT project. For questions, contact security@kkbot.dev.*