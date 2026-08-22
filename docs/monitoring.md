# Monitoring & Observability Guide

Complete monitoring setup for KKBOT.

## Table of Contents

1. [Overview](#overview)
2. [Metrics](#metrics)
3. [Logging](#logging)
4. [Tracing](#tracing)
5. [Health Checks](#health-checks)
6. [Alerting](#alerting)
7. [Grafana Dashboards](#grafana-dashboards)
8. [Log Aggregation](#log-aggregation)
9. [Performance Profiling](#performance-profiling)
10. [Runbooks](#runbooks)

---

## Overview

### Observability Stack

```
┌─────────────────────────────────────────────────────────────────┐
│                      KKBOT Application                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│  │  Metrics    │  │   Logs      │  │   Traces    │             │
│  │ (/metrics)  │  │ (Serilog)   │  │ (OpenTelemetry)           │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘             │
└─────────┼────────────────┼────────────────┼────────────────────┘
          │                │                │
          ▼                ▼                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Collection Layer                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ Prometheus  │  │   Loki      │  │     Jaeger / Tempo      │  │
│  │  (scrape)   │  │  (promtail) │  │    (OTLP collector)     │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Visualization & Alerting                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  Grafana    │  │ Alertmanager│  │   Custom Dashboards     │  │
│  │  (dashboards)   │  (alerts)   │  │   (business metrics)    │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Key Pillars

| Pillar | Tool | Endpoint | Retention |
|--------|------|----------|-----------|
| **Metrics** | Prometheus | `/metrics` | 30d (configurable) |
| **Logs** | Loki + Promtail | `/api/logs` | 30d |
| **Traces** | Jaeger/Tempo | OTLP 4317 | 7d |
| **Health** | Built-in | `/health` | Real-time |
| **Alerts** | Alertmanager | Webhook/Email | N/A |

---

## Metrics

### Application Metrics

#### Instance Metrics

```prometheus
# Total instances by status
kkbot_instances_total{status="Running"} 3
kkbot_instances_total{status="Stopped"} 1
kkbot_instances_total{status="Error"} 0

# Instance info
kkbot_instance_info{instance_id="...", name="main", module="sample-rpg", version="1.0.0"} 1
```

#### Job Metrics

```prometheus
# Jobs by type and status
kkbot_jobs_total{type="OneShot",status="Completed"} 150
kkbot_jobs_total{type="Repeating",status="Running"} 2
kkbot_jobs_total{type="Manual",status="Failed"} 1

# Job duration histogram
kkbot_job_duration_seconds_bucket{type="OneShot",le="1"} 120
kkbot_job_duration_seconds_bucket{type="OneShot",le="5"} 145
kkbot_job_duration_seconds_bucket{type="OneShot",le="+Inf"} 150
kkbot_job_duration_seconds_sum{type="OneShot"} 450.5
kkbot_job_duration_seconds_count{type="OneShot"} 150

# Job queue depth
kkbot_jobs_pending 5
kkbot_jobs_running 2
```

#### Plugin Metrics

```prometheus
# Loaded plugins
kkbot_plugin_count 3

# Plugin info
kkbot_plugin_info{plugin_id="sample-rpg", name="Sample RPG", version="1.0.0"} 1

# Detector performance
kkbot_detector_duration_seconds{detector="hp_bar",plugin="sample-rpg"} 0.012
kkbot_detector_confidence{detector="hp_bar",plugin="sample-rpg"} 0.92
```

#### AI Metrics

```prometheus
# AI requests
kkbot_ai_requests_total{model="llama3.1:8b",status="success"} 1250
kkbot_ai_requests_total{model="llava:7b",status="error"} 3

# AI latency
kkbot_ai_duration_seconds_bucket{model="llama3.1:8b",le="1"} 800
kkbot_ai_duration_seconds_bucket{model="llama3.1:8b",le="5"} 1200
kkbot_ai_duration_seconds_bucket{model="llama3.1:8b",le="+Inf"} 1250

# Token usage
kkbot_ai_tokens_total{model="llama3.1:8b",type="prompt"} 45000
kkbot_ai_tokens_total{model="llama3.1:8b",type="completion"} 32000
```

#### Device Metrics

```prometheus
# Connected devices
kkbot_devices_connected{type="ADB"} 2
kkbot_devices_connected{type="Simulator"} 1

# ADB command latency
kkbot_adb_command_duration_seconds_bucket{command="shell",le="0.1"} 45
kkbot_adb_command_duration_seconds_bucket{command="shell",le="1"} 98
```

#### System Metrics (via Prometheus Node Exporter)

```prometheus
# CPU
process_cpu_seconds_total 1234.5
process_resident_memory_bytes 536870912
process_virtual_memory_bytes 2147483648

# GC
dotnet_gc_heap_size_bytes 268435456
dotnet_gc_gen0_collections_total 150
dotnet_gc_gen1_collections_total 25
dotnet_gc_gen2_collections_total 5

# HTTP
http_requests_total{method="GET",path="/api/v1/instances",status="200"} 500
http_request_duration_seconds_bucket{path="/api/v1/instances",le="0.1"} 450
```

### Custom Metrics Implementation

```csharp
// In your services, use IMetrics (Prometheus.AspNetCore)
public class InstanceService
{
    private readonly Counter _instancesCreated;
    private readonly Gauge _instancesRunning;
    private readonly Histogram _jobDuration;

    public InstanceService(IMetrics metrics)
    {
        _instancesCreated = metrics.CreateCounter("kkbot_instances_created_total", "Total instances created");
        _instancesRunning = metrics.CreateGauge("kkbot_instances_running", "Currently running instances");
        _jobDuration = metrics.CreateHistogram("kkbot_job_duration_seconds", "Job execution duration");
    }

    public async Task<Instance> CreateAsync(CreateInstanceRequest request)
    {
        var instance = new Instance(...);
        _instancesCreated.Inc();
        return instance;
    }

    public async Task<JobResult> ExecuteJobAsync(Job job)
    {
        using var timer = _jobDuration.NewTimer();
        // ... execute job
        return result;
    }
}
```

### Prometheus Configuration

```yaml
# monitoring/prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    cluster: 'production'
    environment: 'prod'

scrape_configs:
- job_name: 'kubernetes-apiservers'
  kubernetes_sd_configs:
  - role: endpoints
  scheme: https
  tls_config:
    ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
  bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
  relabel_configs:
  - source_labels: [__meta_kubernetes_namespace, __meta_kubernetes_service_name, __meta_kubernetes_endpoint_port_name]
    action: keep
    regex: default;kubernetes;https

- job_name: 'kubernetes-nodes'
  kubernetes_sd_configs:
  - role: node
  scheme: https
  tls_config:
    ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
  bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
  relabel_configs:
  - action: labelmap
    regex: __meta_kubernetes_node_label_(.+)

- job_name: 'kubernetes-pods'
  kubernetes_sd_configs:
  - role: pod
  relabel_configs:
  - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
    action: keep
    regex: true
  - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
    action: replace
    target_label: __metrics_path__
    regex: (.+)
  - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
    action: replace
    regex: ([^:]+)(?::\d+)?;(\d+)
    replacement: $1:$2
    target_label: __address__
  - action: labelmap
    regex: __meta_kubernetes_pod_label_(.+)
  - source_labels: [__meta_kubernetes_namespace]
    action: replace
    target_label: kubernetes_namespace
  - source_labels: [__meta_kubernetes_pod_name]
    action: replace
    target_label: kubernetes_pod_name

- job_name: 'kkbot'
  kubernetes_sd_configs:
  - role: pod
    namespaces:
      names:
      - kkbot
  relabel_configs:
  - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
    action: keep
    regex: true
```

---

## Logging

### Serilog Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Information",
        "KKBOT": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/kkbot-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600,
          "rollOnFileSizeLimit": true,
          "shared": true,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq:5341",
          "apiKey": "${SEQ_API_KEY}",
          "controlLevelSwitch": "Serilog.Core.LoggingLevelSwitch"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithProcessId",
      "WithThreadId",
      "WithEnvironmentName",
      "WithAssemblyName",
      "WithAssemblyVersion",
      "WithProperty",
      "WithCorrelationId"
    ],
    "Properties": {
      "Application": "KKBOT",
      "Environment": "Production",
      "Version": "1.0.0"
    }
  }
}
```

### Structured Logging Best Practices

```csharp
// ✅ Good: Structured logging with properties
_logger.LogInformation("Instance {InstanceId} started with module {ModuleId} by user {UserId}", 
    instance.Id, instance.ModuleId, userId);

// ✅ Good: Log exceptions with context
try 
{
    await _device.ConnectAsync(ct);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to connect to device {DeviceId} for instance {InstanceId}", 
        deviceId, instanceId);
    throw;
}

// ❌ Bad: String interpolation (loses structure)
_logger.LogInformation($"Instance {instance.Id} started"); // Don't do this

// ✅ Good: High cardinality as properties, not message template
_logger.LogInformation("Job {JobId} completed in {DurationMs}ms with status {Status}", 
    job.Id, durationMs, job.Status);

// ✅ Good: Debug level for verbose diagnostics
_logger.LogDebug("Detector {DetectorId} processing frame {FrameNumber}", 
    detector.Id, frame.Number);
```

### Log Levels

| Level | When to Use |
|-------|-------------|
| **Trace** | Extremely detailed (per-frame, per-pixel) |
| **Debug** | Diagnostic info (detector scores, state transitions) |
| **Information** | Business events (instance start, job complete, plugin load) |
| **Warning** | Recoverable issues (retry, fallback, deprecated config) |
| **Error** | Failed operations (job failed, detector error, DB error) |
| **Critical** | System-threatening (DB down, Vault unreachable, OOM) |

### Correlation IDs

```csharp
// Middleware to add correlation ID
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() 
            ?? Activity.Current?.Id 
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = correlationId;
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
        using (LogContext.PushProperty("SpanId", Activity.Current?.SpanId.ToString()))
        {
            await _next(context);
        }
    }
}
```

---

## Tracing

### OpenTelemetry Setup

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("KKBOT.*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        })
    )
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter()
    );
```

### Custom Activity Sources

```csharp
public static class ActivitySources
{
    public static readonly ActivitySource InstanceManager = new("KKBOT.InstanceManager");
    public static readonly ActivitySource JobExecutor = new("KKBOT.JobExecutor");
    public static readonly ActivitySource PerceptionPipeline = new("KKBOT.Perception");
    public static readonly ActivitySource PluginLoader = new("KKBOT.Plugins");
    public static readonly ActivitySource AIClient = new("KKBOT.AI");
}

// Usage
public async Task<Instance> StartInstanceAsync(Guid instanceId, CancellationToken ct)
{
    using var activity = ActivitySources.InstanceManager.StartActivity("StartInstance", ActivityKind.Internal);
    activity?.SetTag("instance.id", instanceId.ToString());
    activity?.SetTag("instance.name", instance.Name);
    
    try 
    {
        // ... start logic
        activity?.SetStatus(ActivityStatusCode.Ok);
        return instance;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

### Distributed Trace Context Propagation

```csharp
// HTTP client with trace context
var client = _httpClientFactory.CreateClient("ollama");
using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate");

// Inject trace context into headers
ActivityContext contextToInject = Activity.Current?.Context ?? default;
if (contextToInject != default)
{
    var propagator = new TraceContextPropagator();
    propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), 
        request.Headers, 
        (headers, key, value) => headers.TryAddWithoutValidation(key, value));
}

var response = await client.SendAsync(request, ct);
```

---

## Health Checks

### Built-in Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("kkbot-alive", () => HealthCheckResult.Healthy("KKBOT is alive"))
    .AddDbContextCheck<KkbotDbContext>("database")
    .AddUrlGroup(new Uri("http://ollama:11434/api/tags"), "ollama", timeout: TimeSpan.FromSeconds(5))
    .AddUrlGroup(new Uri("http://postgres:5432"), "postgres", timeout: TimeSpan.FromSeconds(5))
    .AddRedis("redis", configuration["ConnectionStrings:Redis"])
    .AddCheck("disk-space", () => 
    {
        var drive = new DriveInfo("/");
        var freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
        return freePercent > 10 
            ? HealthCheckResult.Healthy($"Disk free: {freePercent:F1}%")
            : HealthCheckResult.Unhealthy($"Disk critical: {freePercent:F1}%");
    });
```

### Health Check Response

```json
// GET /health
{
  "status": "Healthy",
  "results": {
    "kkbot-alive": { "status": "Healthy", "description": "KKBOT is alive" },
    "database": { "status": "Healthy", "description": "" },
    "ollama": { "status": "Healthy", "description": "" },
    "disk-space": { "status": "Healthy", "description": "Disk free: 45.2%" }
  },
  "totalDuration": "00:00:00.0012345"
}
```

### Kubernetes Probes

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 40
  periodSeconds: 30
  timeoutSeconds: 10
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 10
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

---

## Alerting

### Alertmanager Configuration

```yaml
# monitoring/alertmanager.yml
global:
  resolve_timeout: 5m
  smtp_smarthost: 'smtp.example.com:587'
  smtp_from: 'alerts@kkbot.example.com'
  smtp_auth_username: 'alerts'
  smtp_auth_password: '${SMTP_PASSWORD}'

route:
  group_by: ['alertname', 'cluster', 'service']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  receiver: 'default'
  routes:
  - match:
      severity: critical
    receiver: 'critical-alerts'
    continue: true
  - match:
      severity: warning
    receiver: 'warning-alerts'

receivers:
- name: 'default'
  email_configs:
  - to: 'team@kkbot.example.com'
    send_resolved: true
- name: 'critical-alerts'
  email_configs:
  - to: 'oncall@kkbot.example.com'
    send_resolved: true
  pagerduty_configs:
  - service_key: '${PAGERDUTY_KEY}'
    severity: critical
- name: 'warning-alerts'
  email_configs:
  - to: 'team@kkbot.example.com'
    send_resolved: true
  slack_configs:
  - api_url: '${SLACK_WEBHOOK}'
    channel: '#kkbot-alerts'
    send_resolved: true

inhibit_rules:
- source_match:
    severity: 'critical'
  target_match:
    severity: 'warning'
  equal: ['alertname', 'cluster', 'service']
```

### Prometheus Alert Rules

```yaml
# monitoring/rules/kkbot.yml
groups:
- name: kkbot.rules
  interval: 30s
  rules:
  # Instance Alerts
  - alert: KKBOTInstanceDown
    expr: up{job="kkbot"} == 0
    for: 2m
    labels:
      severity: critical
      service: kkbot
    annotations:
      summary: "KKBOT instance {{ $labels.instance }} is down"
      description: "KKBOT has been down for more than 2 minutes."
      runbook_url: "https://wiki.kkbot.example.com/runbooks/instance-down"

  - alert: KKBOTInstanceError
    expr: kkbot_instances_status{status="Error"} > 0
    for: 1m
    labels:
      severity: critical
      service: kkbot
    annotations:
      summary: "KKBOT instance in error state"
      description: "Instance {{ $labels.instance_id }} ({{ $labels.name }}) is in Error state."

  - alert: KKBOTHighInstanceCount
    expr: kkbot_instances_total > 50
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High instance count"
      description: "{{ $value }} instances running, approaching limit."

  # Job Alerts
  - alert: KKBOTJobQueueBacklog
    expr: kkbot_jobs_pending > 100
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "Job queue backlog"
      description: "{{ $value }} jobs pending for more than 5 minutes."

  - alert: KKBOTJobFailureRate
    expr: |
      (
        rate(kkbot_jobs_total{status="Failed"}[5m]) 
        / 
        rate(kkbot_jobs_total[5m])
      ) > 0.1
    for: 3m
    labels:
      severity: warning
    annotations:
      summary: "High job failure rate"
      description: "Job failure rate is above 10% for 3 minutes."

  - alert: KKBOTJobDurationHigh
    expr: histogram_quantile(0.95, rate(kkbot_job_duration_seconds_bucket[5m])) > 300
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "High job duration (p95)"
      description: "Job p95 duration is {{ $value | humanizeDuration }}."

  # AI Alerts
  - alert: KKBOTAIErrorRate
    expr: |
      (
        rate(kkbot_ai_requests_total{status="error"}[5m])
        /
        rate(kkbot_ai_requests_total[5m])
      ) > 0.05
    for: 3m
    labels:
      severity: warning
    annotations:
      summary: "AI error rate high"
      description: "AI service error rate above 5%."

  - alert: KKBOTAILatencyHigh
    expr: histogram_quantile(0.95, rate(kkbot_ai_duration_seconds_bucket[5m])) > 30
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "AI latency high (p95)"
      description: "AI p95 latency is {{ $value }}s."

  # Resource Alerts
  - alert: KKBOTHighCPU
    expr: |
      (rate(container_cpu_usage_seconds_total{pod=~"kkbot-.*"}[5m]) * 100) > 80
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "KKBOT high CPU"
      description: "Pod {{ $labels.pod }} CPU > 80% for 5m."

  - alert: KKBOTHighMemory
    expr: |
      (container_memory_usage_bytes{pod=~"kkbot-.*"} 
       / container_spec_memory_limit_bytes{pod=~"kkbot-.*"}) * 100 > 85
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "KKBOT high memory"
      description: "Pod {{ $labels.pod }} memory > 85%."

  - alert: KKBOTDiskSpace
    expr: |
      (node_filesystem_avail_bytes{mountpoint="/app/data"} 
       / node_filesystem_size_bytes{mountpoint="/app/data"}) * 100 < 10
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "KKBOT disk space critical"
      description: "Data volume free space below 10%."
```

---

## Grafana Dashboards

### Dashboard 1: Overview

```json
{
  "title": "KKBOT Overview",
  "panels": [
    {
      "title": "Instances by Status",
      "type": "piechart",
      "targets": [
        { "expr": "kkbot_instances_total", "legendFormat": "{{status}}" }
      ]
    },
    {
      "title": "Jobs Rate (per minute)",
      "type": "graph",
      "targets": [
        { "expr": "rate(kkbot_jobs_total[5m])", "legendFormat": "{{type}} - {{status}}" }
      ]
    },
    {
      "title": "Job Duration (p50, p95, p99)",
      "type": "graph",
      "targets": [
        { "expr": "histogram_quantile(0.50, rate(kkbot_job_duration_seconds_bucket[5m]))", "legendFormat": "p50" },
        { "expr": "histogram_quantile(0.95, rate(kkbot_job_duration_seconds_bucket[5m]))", "legendFormat": "p95" },
        { "expr": "histogram_quantile(0.99, rate(kkbot_job_duration_seconds_bucket[5m]))", "legendFormat": "p99" }
      ]
    },
    {
      "title": "AI Requests & Latency",
      "type": "graph",
      "targets": [
        { "expr": "rate(kkbot_ai_requests_total[5m])", "legendFormat": "{{model}} - {{status}}" },
        { "expr": "histogram_quantile(0.95, rate(kkbot_ai_duration_seconds_bucket[5m]))", "legendFormat": "AI p95 latency" }
      ]
    },
    {
      "title": "System Resources",
      "type": "graph",
      "targets": [
        { "expr": "rate(process_cpu_seconds_total[5m]) * 100", "legendFormat": "CPU %" },
        { "expr": "process_resident_memory_bytes / 1024 / 1024", "legendFormat": "Memory (MB)" }
      ]
    }
  ]
}
```

### Dashboard 2: Instance Details

```json
{
  "title": "KKBOT Instance Details",
  "panels": [
    {
      "title": "Instance Status Timeline",
      "type": "statetimeline",
      "targets": [
        { "expr": "kkbot_instance_status", "legendFormat": "{{instance_id}} - {{name}}" }
      ]
    },
    {
      "title": "Jobs per Instance",
      "type": "bargauge",
      "targets": [
        { "expr": "sum by (instance_id) (kkbot_jobs_total)", "legendFormat": "{{instance_id}}" }
      ]
    }
  ]
}
```

### Dashboard 3: Business Metrics

```json
{
  "title": "KKBOT Business Metrics",
  "panels": [
    {
      "title": "Successful Actions per Hour",
      "type": "graph",
      "targets": [
        { "expr": "rate(kkbot_actions_total{result=\"success\"}[1h])", "legendFormat": "{{action}}" }
      ]
    },
    {
      "title": "Items Collected",
      "type": "stat",
      "targets": [
        { "expr": "increase(kkbot_items_collected_total[24h])", "legendFormat": "24h" }
      ]
    },
    {
      "title": "Gold Earned",
      "type": "graph",
      "targets": [
        { "expr": "rate(kkbot_gold_earned_total[5m])", "legendFormat": "Gold/min" }
      ]
    }
  ]
}
```

### Provisioning Dashboards

```yaml
# monitoring/grafana/dashboards.yaml
apiVersion: 1
providers:
- name: 'KKBOT'
  orgId: 1
  folder: 'KKBOT'
  type: file
  disableDeletion: false
  updateIntervalSeconds: 10
  allowUiUpdates: true
  options:
    path: /etc/grafana/provisioning/dashboards
```

---

## Log Aggregation

### Loki Configuration

```yaml
# monitoring/loki-config.yml
auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096

common:
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    instance_addr: 127.0.0.1
    kvstore:
      store: inmemory

schema_config:
  configs:
  - from: 2024-01-01
    store: boltdb-shipper
    object_store: filesystem
    schema: v11
    index:
      prefix: index_
      period: 24h

limits_config:
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  ingestion_rate_mb: 25
  ingestion_burst_size_mb: 50
```

### Promtail Configuration

```yaml
# monitoring/promtail-config.yml
server:
  http_listen_port: 9080
  grpc_listen_port: 9081

positions:
  filename: /tmp/positions.yaml

clients:
- url: http://loki:3100/loki/api/v1/push

scrape_configs:
- job_name: kkbot
  kubernetes_sd_configs:
  - role: pod
    namespaces:
      names:
      - kkbot
  relabel_configs:
  - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
    action: keep
    regex: true
  - source_labels: [__meta_kubernetes_pod_name]
    action: replace
    target_label: pod
  - source_labels: [__meta_kubernetes_namespace]
    action: replace
    target_label: namespace
  - source_labels: [__meta_kubernetes_pod_label_app_kubernetes_io_name]
    action: replace
    target_label: app
  pipeline_stages:
  - json:
      expressions:
        level: Level
        message: Message
        source: SourceContext
        timestamp: Timestamp
        correlationId: CorrelationId
  - labels:
      level:
      source:
  - timestamp:
      source: timestamp
      format: RFC3339Nano
```

### LogQL Queries

```logql
# All errors in last hour
{app="kkbot"} | level="Error" | json | line_format "{{.Message}}"

# Specific instance logs
{app="kkbot"} | json | instance_id="3fa85f64-5717-4562-b3fc-2c963f66afa6"

# Job execution logs
{app="kkbot"} | json | job_id="3fa85f64-5717-4562-b3fc-2c963f66afa6" | level!="Debug"

# AI request logs with duration
{app="kkbot"} | json | ai_model!="" | line_format "{{.ai_model}}: {{.duration_ms}}ms"

# Detector performance
{app="kkbot"} | json | detector_id!="" | line_format "{{.detector_id}}: {{.confidence}}"
```

---

## Performance Profiling

### Dotnet Diagnostics

```bash
# Install dotnet-trace
dotnet tool install -g dotnet-trace

# Collect trace from running process
dotnet-trace collect --process-id <PID> --format Speedscope --output kkbot.trace

# Analyze with Speedscope
npx speedscope kkbot.trace
```

### Continuous Profiling (Pyroscope)

```yaml
# Add to deployment
- name: pyroscope
  image: grafana/pyroscope:1.4.0
  args:
    - "--storage.path=/data"
  volumeMounts:
  - name: pyroscope-data
    mountPath: /data
  ports:
  - containerPort: 4040
---
# In KKBOT container, add:
# - name: DOTNET_EnableDiagnostics
#   value: "1"
# - name: COMPlus_Profiler
#   value: "{...}"
```

---

## Runbooks

### Runbook: High Job Failure Rate

**Alert**: `KKBOTJobFailureRate`

**Steps**:
1. Check job error details:
   ```bash
   # Query recent failed jobs
   curl -s "http://localhost:8080/api/v1/jobs?status=Failed" | jq '.[] | {id, errorMessage, instanceId}'
   ```

2. Check instance health:
   ```bash
   curl -s "http://localhost:8080/api/v1/instances" | jq '.[] | {id, name, status}'
   ```

3. Check plugin logs:
   ```bash
   # In Grafana Loki
   {app="kkbot"} | json | level="Error" | plugin_id!=""
   ```

4. Common causes:
   - Game update broke templates → Reload/update plugin
   - Device disconnected → Check ADB connection
   - AI service down → Check Ollama health

5. Resolution:
   - Restart affected instances
   - Update plugin templates
   - Scale down/up if resource issue

### Runbook: AI Service Unavailable

**Alert**: `KKBOTAIErrorRate`

**Steps**:
1. Check Ollama health:
   ```bash
   curl http://ollama:11434/api/tags
   ```

2. Check Ollama logs:
   ```bash
   kubectl logs -n kkbot deployment/ollama
   ```

3. Check GPU memory:
   ```bash
   nvidia-smi
   ```

4. Resolution:
   - Restart Ollama if OOM
   - Reduce model size if persistent
   - Enable fallback to OpenAI

### Runbook: Disk Space Critical

**Alert**: `KKBOTDiskSpace`

**Steps**:
1. Check disk usage:
   ```bash
   df -h /app/data
   ```

2. Clean old logs:
   ```bash
   find /app/logs -name "*.log" -mtime +30 -delete
   ```

3. Clean old database:
   ```bash
   # SQLite vacuum
   sqlite3 /app/data/kkbot.db "VACUUM;"
   ```

4. Increase PVC size:
   ```bash
   kubectl patch pvc kkbot-data -n kkbot -p '{"spec":{"resources":{"requests":{"storage":"20Gi"}}}}'
   ```

---

## Useful Queries

### Top 10 Slowest Jobs
```promql
topk(10, histogram_quantile(0.99, rate(kkbot_job_duration_seconds_bucket[1h])))
```

### Instance Uptime
```promql
time() - kkbot_instance_start_timestamp_seconds
```

### Plugin Load Success Rate
```promql
rate(kkbot_plugin_load_total{status="success"}[1h]) / rate(kkbot_plugin_load_total[1h])
```

### Detector Accuracy
```promql
avg by (detector) (kkbot_detector_confidence) > 0.8
```

---

## Maintenance

### Daily
- [ ] Review alert firing history
- [ ] Check disk usage trends
- [ ] Verify backup completion

### Weekly
- [ ] Review dashboard for anomalies
- [ ] Update alert thresholds if needed
- [ ] Check log retention compliance

### Monthly
- [ ] Review and tune alert rules
- [ ] Capacity planning (CPU, Memory, Disk, GPU)
- [ ] Security scan review
- [ ] Documentation updates