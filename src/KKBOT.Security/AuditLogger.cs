// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using KKBOT.Security.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Security;

/// <summary>
/// Simple audit logger that writes structured logs via ILogger.
/// Replace with a dedicated audit store (DB, Elasticsearch, etc.) in production.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ILogger<AuditLogger> logger) => _logger = logger;

    public Task LogAsync(string action, string userId, string details, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AUDIT | Action: {Action} | User: {UserId} | Details: {Details}", action, userId, details);
        return Task.CompletedTask;
    }
}