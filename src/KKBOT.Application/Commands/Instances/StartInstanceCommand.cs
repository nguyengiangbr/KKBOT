// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Domain.Common;
using FluentValidation;
using MediatR;

namespace KKBOT.Application.Commands.Instances;

/// <summary>
/// Command to start an instance.
/// </summary>
public record StartInstanceCommand(Guid InstanceId) : IRequest<Result>;

/// <summary>
/// Validator for StartInstanceCommand.
/// </summary>
public class StartInstanceCommandValidator : FluentValidation.AbstractValidator<StartInstanceCommand>
{
    public StartInstanceCommandValidator()
    {
        RuleFor(x => x.InstanceId)
            .NotEmpty().WithMessage("Instance ID is required");
    }
}

/// <summary>
/// Command to stop an instance.
/// </summary>
public record StopInstanceCommand(Guid InstanceId, string? Reason = null) : IRequest<Result>;

/// <summary>
/// Command to pause an instance.
/// </summary>
public record PauseInstanceCommand(Guid InstanceId, string? Reason = null) : IRequest<Result>;

/// <summary>
/// Command to resume an instance.
/// </summary>
public record ResumeInstanceCommand(Guid InstanceId) : IRequest<Result>;

/// <summary>
/// Command to create an instance.
/// </summary>
public record CreateInstanceCommand(
    string Key,
    string DisplayName,
    string? Description,
    Guid DeviceId,
    Guid ModuleId,
    Guid ProfileId,
    bool AutoStart = false,
    int Priority = 0,
    string[]? Tags = null) : IRequest<Result<Guid>>;

/// <summary>
/// Command to update an instance.
/// </summary>
public record UpdateInstanceCommand(
    Guid InstanceId,
    string? DisplayName = null,
    string? Description = null,
    Guid? DeviceId = null,
    Guid? ProfileId = null,
    bool? IsEnabled = null,
    bool? AutoStart = null,
    int? Priority = null,
    string[]? Tags = null) : IRequest<Result>;

/// <summary>
/// Command to delete an instance.
/// </summary>
public record DeleteInstanceCommand(Guid InstanceId) : IRequest<Result>;

/// <summary>
/// Command to assign a profile to an instance.
/// </summary>
public record AssignProfileCommand(Guid InstanceId, Guid ProfileId) : IRequest<Result>;

/// <summary>
/// Command to trigger recovery on an instance.
/// </summary>
public record TriggerRecoveryCommand(Guid InstanceId) : IRequest<Result>;

/// <summary>
/// Command to retry the current task.
/// </summary>
public record RetryTaskCommand(Guid InstanceId) : IRequest<Result>;

/// <summary>
/// Command to cancel the current task.
/// </summary>
public record CancelTaskCommand(Guid InstanceId, string? Reason = null) : IRequest<Result>;

/// <summary>
/// Command to approve a blocked task.
/// </summary>
public record ApproveTaskCommand(Guid InstanceId) : IRequest<Result>;

/// <summary>
/// Command to run a job on an instance.
/// </summary>
public record RunJobCommand(Guid JobId, Guid InstanceId, Dictionary<string, object?>? ParameterOverrides = null) : IRequest<Result<Guid>>;

/// <summary>
/// Command to cancel a job run.
/// </summary>
public record CancelJobCommand(Guid JobRunId, string? Reason = null) : IRequest<Result>;

/// <summary>
/// Command to trigger emergency stop for all instances.
/// </summary>
public record EmergencyStopCommand(string? Reason = null, string? TriggeredBy = null) : IRequest<Result>;