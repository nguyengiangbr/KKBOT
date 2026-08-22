// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;
using KKBOT.Persistence.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KKBOT.API.Controllers;

/// <summary>
/// Instance management API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[SwaggerTag("Manage KKBOT instances")]
public sealed class InstancesController : ControllerBase
{
    private readonly IInstanceRepository _repository;
    private readonly ILogger<InstancesController> _logger;

    public InstancesController(IInstanceRepository repository, ILogger<InstancesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all instances
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Instance>), 200)]
    public async Task<ActionResult<IEnumerable<Instance>>> GetAll(CancellationToken ct)
    {
        var instances = await _repository.GetAllAsync(ct);
        return Ok(instances);
    }

    /// <summary>
    /// Get instance by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Instance), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Instance>> GetById(Guid id, CancellationToken ct)
    {
        var instance = await _repository.GetAsync(id, ct);
        return instance is null ? NotFound() : Ok(instance);
    }

    /// <summary>
    /// Create a new instance
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Instance), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<Instance>> Create([FromBody] CreateInstanceRequest request, CancellationToken ct)
    {
        var instance = new Instance(Guid.NewGuid(), request.Name, InstanceStatus.Created, null)
        {
            DeviceId = request.DeviceId,
            ModuleId = request.ModuleId,
            Configuration = request.Configuration ?? "{}"
        };

        await _repository.AddAsync(instance, ct);
        _logger.LogInformation("Created instance {InstanceId} ({Name})", instance.Id, instance.Name);
        return CreatedAtAction(nameof(GetById), new { id = instance.Id }, instance);
    }

    /// <summary>
    /// Update an instance
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Instance), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<Instance>> Update(Guid id, [FromBody] UpdateInstanceRequest request, CancellationToken ct)
    {
        var instance = await _repository.GetAsync(id, ct);
        if (instance is null) return NotFound();

        instance = instance with
        {
            Name = request.Name ?? instance.Name,
            Status = request.Status ?? instance.Status,
            DeviceId = request.DeviceId ?? instance.DeviceId,
            ModuleId = request.ModuleId ?? instance.ModuleId,
            Configuration = request.Configuration ?? instance.Configuration,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.UpdateAsync(instance, ct);
        _logger.LogInformation("Updated instance {InstanceId}", id);
        return Ok(instance);
    }

    /// <summary>
    /// Delete an instance
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var instance = await _repository.GetAsync(id, ct);
        if (instance is null) return NotFound();

        await _repository.DeleteAsync(id, ct);
        _logger.LogInformation("Deleted instance {InstanceId}", id);
        return NoContent();
    }
}

public record CreateInstanceRequest(
    string Name,
    Guid? DeviceId = null,
    Guid? ModuleId = null,
    string? Configuration = null
);

public record UpdateInstanceRequest(
    string? Name = null,
    InstanceStatus? Status = null,
    Guid? DeviceId = null,
    Guid? ModuleId = null,
    string? Configuration = null
);