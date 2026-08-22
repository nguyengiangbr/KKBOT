// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Domain.Models;
using KKBOT.Contracts.Instances;

namespace KKBOT.Domain.Extensions;

public static class InstanceExtensions
{
    /// <summary>
    /// Convert an <see cref="Instance"/> entity to its DTO representation.
    /// </summary>
    public static InstanceDto ToDto(this Instance instance) => new(
        instance.Id,
        instance.Key,
        instance.State.ToString(),
        instance.DeviceId ?? Guid.Empty,
        instance.ModuleId ?? Guid.Empty,
        instance.ProfileId ?? Guid.Empty);

    /// <summary>
    /// Update an existing <see cref="Instance"/> with values from a DTO.
    /// Only mutable fields are updated (Key, DisplayName, Description, IsEnabled, AutoStart, Priority, Tags, ConfigOverride).
    /// </summary>
    public static void ApplyDto(this Instance instance, InstanceDto dto)
    {
        instance.Key = dto.Key;
        // Note: the DTO currently does not contain all the mutable fields – extend as needed.
    }
}
