// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Domain.Models;

/// <summary>
/// Base class for all domain entities.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when the entity was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Version for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; protected set; }

    /// <summary>
    /// Updates the UpdatedAt timestamp.
    /// </summary>
    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Determines equality based on Id.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Id == Guid.Empty || other.Id == Guid.Empty) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity? left, Entity? right)
        => !(left == right);
}

/// <summary>
/// Base class for entities with string-based identifiers (e.g., profiles, modules).
/// </summary>
public abstract class StringEntity : Entity
{
    /// <summary>
    /// Human-readable identifier.
    /// </summary>
    [MaxLength(100)]
    public string Key { get; protected set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    public string DisplayName { get; protected set; } = string.Empty;

    /// <summary>
    /// Description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; protected set; }
}