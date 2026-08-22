# KKBOT API Reference

Base URL: `https://api.kkbot.example.com/api/v1`

## Authentication

Currently supports JWT Bearer tokens (to be implemented). For development, authentication is disabled.

```http
Authorization: Bearer <token>
```

## Common Headers

| Header | Value |
|--------|-------|
| Content-Type | application/json |
| Accept | application/json |

## Error Responses

All endpoints may return standard error responses:

| Status Code | Description |
|-------------|-------------|
| 200 | Success |
| 201 | Created |
| 400 | Bad Request - Validation error |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not Found |
| 500 | Internal Server Error |

Error format:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-...-00",
  "errors": {
    "fieldName": ["Error message"]
  }
}
```

---

## Instances

### Get All Instances

```http
GET /api/v1/instances
```

**Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Main Instance",
    "status": "Running",
    "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "moduleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "configuration": "{}",
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  }
]
```

### Get Instance by ID

```http
GET /api/v1/instances/{id}
```

**Parameters:**
- `id` (Guid, required) - Instance ID

**Response:** `200 OK` or `404 Not Found`

### Create Instance

```http
POST /api/v1/instances
```

**Request Body:**
```json
{
  "name": "New Instance",
  "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "moduleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configuration": "{\"autoBattle\": true}"
}
```

**Response:** `201 Created`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "New Instance",
  "status": "Created",
  "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "moduleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configuration": "{\"autoBattle\": true}",
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-01-15T10:30:00Z"
}
```

### Update Instance

```http
PUT /api/v1/instances/{id}
```

**Request Body:**
```json
{
  "name": "Updated Name",
  "status": "Running",
  "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "moduleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configuration": "{\"autoBattle\": false}"
}
```

**Response:** `200 OK` or `404 Not Found`

### Delete Instance

```http
DELETE /api/v1/instances/{id}
```

**Response:** `204 No Content` or `404 Not Found`

---

## Jobs

### Get All Jobs

```http
GET /api/v1/jobs
```

**Query Parameters:**
- `instanceId` (Guid, optional) - Filter by instance

**Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "instanceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "type": "OneShot",
    "status": "Completed",
    "priority": 0,
    "payload": "{\"action\": \"attack\"}",
    "result": "{\"damage\": 1500}",
    "errorMessage": null,
    "createdAt": "2024-01-15T10:30:00Z",
    "startedAt": "2024-01-15T10:30:01Z",
    "completedAt": "2024-01-15T10:30:05Z",
    "updatedAt": "2024-01-15T10:30:05Z"
  }
]
```

### Get Job by ID

```http
GET /api/v1/jobs/{id}
```

**Response:** `200 OK` or `404 Not Found`

### Create Job

```http
POST /api/v1/jobs
```

**Request Body:**
```json
{
  "instanceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "OneShot",
  "payload": "{\"action\": \"heal\"}",
  "priority": 10,
  "scheduledAt": "2024-01-15T11:00:00Z"
}
```

**Response:** `201 Created`

### Update Job

```http
PUT /api/v1/jobs/{id}
```

**Request Body:**
```json
{
  "status": "Running",
  "priority": 5,
  "payload": "{\"action\": \"attack\", \"target\": \"boss\"}",
  "result": "{\"damage\": 2000}",
  "errorMessage": null,
  "startedAt": "2024-01-15T10:30:01Z",
  "completedAt": "2024-01-15T10:30:05Z"
}
```

**Response:** `200 OK` or `404 Not Found`

### Delete Job

```http
DELETE /api/v1/jobs/{id}
```

**Response:** `204 No Content` or `404 Not Found`

---

## Plugins

### Get All Plugins

```http
GET /api/v1/plugins
```

**Query Parameters:**
- `path` (string, optional) - Plugins directory path (default: "plugins")

**Response:** `200 OK`
```json
[
  {
    "id": "sample-rpg-game",
    "name": "Sample RPG Game",
    "version": "1.0.0",
    "author": "KKBOT Team",
    "description": "A complete sample plugin for an RPG-style game...",
    "supportedResolutions": [
      { "width": 1920, "height": 1080 },
      { "width": 1280, "height": 720 }
    ],
    "requiredPermissions": ["DEVICE_READ", "INPUT_WRITE", "SCREENSHOT_CAPTURE", "OCR_ACCESS"],
    "detectors": [
      { "id": "hp_bar", "name": "HP Bar Detector" },
      { "id": "mp_bar", "name": "MP Bar Detector" }
    ],
    "tasks": [
      { "id": "auto_battle", "name": "Auto Battle", "type": "Repeating" },
      { "id": "collect_loot", "name": "Collect Loot", "type": "OneShot" }
    ],
    "states": [
      { "id": "idle", "name": "Idle", "category": "navigation" },
      { "id": "in_battle", "name": "In Battle", "category": "combat" }
    ]
  }
]
```

### Reload Plugins

```http
POST /api/v1/plugins/reload
```

**Query Parameters:**
- `path` (string, optional) - Plugins directory path (default: "plugins")

**Response:** `200 OK` - Returns updated plugin list

---

## Health Check

```http
GET /health
```

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "kkbot-alive",
      "status": "Healthy",
      "description": "KKBOT is alive"
    }
  ],
  "totalDuration": "00:00:00.0012345"
}
```

---

## Metrics (Prometheus)

```http
GET /metrics
```

Returns Prometheus-format metrics. Key metrics:

```
# HELP kkbot_instances_total Total number of instances
# TYPE kkbot_instances_total gauge
kkbot_instances_total 5

# HELP kkbot_instances_status Instances by status
# TYPE kkbot_instances_status gauge
kkbot_instances_status{status="Running"} 3
kkbot_instances_status{status="Stopped"} 1
kkbot_instances_status{status="Error"} 1

# HELP kkbot_jobs_total Total number of jobs
# TYPE kkbot_jobs_total counter
kkbot_jobs_total{type="OneShot",status="Completed"} 150

# HELP kkbot_job_duration_seconds Job execution duration
# TYPE kkbot_job_duration_seconds histogram
kkbot_job_duration_seconds_bucket{le="1"} 120
kkbot_job_duration_seconds_bucket{le="5"} 145
kkbot_job_duration_seconds_bucket{le="+Inf"} 150

# HELP kkbot_plugin_count Number of loaded plugins
# TYPE kkbot_plugin_count gauge
kkbot_plugin_count 3
```

---

## Models

### Instance

```json
{
  "id": "guid",
  "name": "string",
  "status": "Created|Running|Stopped|Error",
  "deviceId": "guid|null",
  "moduleId": "guid|null",
  "configuration": "string (JSON)",
  "createdAt": "datetime",
  "updatedAt": "datetime|null"
}
```

### Job

```json
{
  "id": "guid",
  "instanceId": "guid",
  "type": "OneShot|Repeating|Manual",
  "status": "Pending|Running|Completed|Failed|Cancelled",
  "priority": "integer",
  "payload": "string (JSON)",
  "result": "string (JSON)|null",
  "errorMessage": "string|null",
  "createdAt": "datetime",
  "startedAt": "datetime|null",
  "completedAt": "datetime|null",
  "updatedAt": "datetime"
}
```

### PluginInfo

```json
{
  "id": "string",
  "name": "string",
  "version": "string",
  "author": "string",
  "description": "string",
  "supportedResolutions": [
    { "width": "integer", "height": "integer" }
  ],
  "requiredPermissions": ["string"],
  "detectors": [
    { "id": "string", "name": "string" }
  ],
  "tasks": [
    { "id": "string", "name": "string", "type": "string" }
  ],
  "states": [
    { "id": "string", "name": "string", "category": "string" }
  ]
}
```

### Resolution

```json
{
  "width": 1920,
  "height": 1080
}
```

---

## Rate Limiting

Currently not implemented. Planned for future release.

## Versioning

API versioning via URL path: `/api/v1/`

## Changelog

### v1.0.0 (2024-01-15)
- Initial API release
- Instances, Jobs, Plugins endpoints
- Health check and metrics endpoints