// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Contracts.Dashboard;
using KKBOT.Contracts.Devices;
using KKBOT.Contracts.Instances;
using KKBOT.Contracts.Jobs;
using KKBOT.Domain.Models;
using MediatR;

namespace KKBOT.Application.Queries;

/// <summary>
/// Query to get all instances with optional filtering.
/// </summary>
public record GetInstancesQuery(
    string? State = null,
    Guid? ModuleId = null,
    Guid? DeviceId = null,
    string? Search = null,
    string[]? Tags = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResponse<InstanceSummaryDto>>;

/// <summary>
/// Query to get a specific instance by ID.
/// </summary>
public record GetInstanceQuery(Guid InstanceId) : IRequest<InstanceDetailDto?>;

/// <summary>
/// Query to get instance state history.
/// </summary>
public record GetInstanceStateHistoryQuery(Guid InstanceId, int Limit = 50) : IRequest<IReadOnlyList<StateTransitionDto>>;

/// <summary>
/// Query to get devices with optional filtering.
/// </summary>
public record GetDevicesQuery(
    bool? IsConnected = null,
    bool? IsAvailable = null,
    string? Type = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResponse<DeviceSummaryDto>>;

/// <summary>
/// Query to get a specific device by ID.
/// </summary>
public record GetDeviceQuery(Guid DeviceId) : IRequest<DeviceDetailDto?>;

/// <summary>
/// Query to get jobs with optional filtering.
/// </summary>
public record GetJobsQuery(
    Guid? ModuleId = null,
    bool? IsEnabled = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResponse<JobSummaryDto>>;

/// <summary>
/// Query to get job runs with optional filtering.
/// </summary>
public record GetJobRunsQuery(
    Guid? JobId = null,
    Guid? InstanceId = null,
    string? State = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResponse<JobRunSummaryDto>>;

/// <summary>
/// Query to get dashboard summary.
/// </summary>
public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

/// <summary>
/// Query to get system status.
/// </summary>
public record GetSystemStatusQuery : IRequest<SystemStatusDto>;

/// <summary>
/// Query to check if an instance is healthy.
/// </summary>
public record GetInstanceHealthQuery(Guid InstanceId) : IRequest<bool>;

/// <summary>
/// Query to get audit events with filtering.
/// </summary>
public record GetAuditEventsQuery(
    string? Actor = null,
    string? Action = null,
    string? TargetType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResponse<AuditEvent>>;

/// <summary>
/// Query to validate a profile.
/// </summary>
public record ValidateProfileQuery(Guid ProfileId) : IRequest<Domain.Common.Result<bool>>;

/// <summary>
/// Query to validate a module.
/// </summary>
public record ValidateModuleQuery(Guid ModuleId) : IRequest<Domain.Common.Result<bool>>;

/// <summary>
/// Query to get health snapshots for an instance.
/// </summary>
public record GetHealthSnapshotsQuery(Guid InstanceId, int Limit = 10) : IRequest<IReadOnlyList<KkbOT.Contracts.Instances.HealthSnapshotDto>>;