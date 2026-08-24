// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;
using KKBOT.Persistence.Abstractions;
using KKBOT.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

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

    [HttpGet]
    [RequirePermission(KKBOTPermissions.InstanceRead)]
    [ProducesResponseType(typeof(IEnumerable<Instance>), 200)]
    [SwaggerOperation("Get all instances", "Requires instance.read permission")]
    public async Task<ActionResult<IEnumerable<Instance>>> GetAll(CancellationToken ct)
    {
        var instances = await _repository.GetAllAsync(ct);
        return Ok(instances);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(KKBOTPermissions.InstanceRead, "instance", "{id}")]
    [ProducesResponseType(typeof(Instance), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get instance by ID", "Requires instance.read permission")]
    public async Task<ActionResult<Instance>> GetById(Guid id, CancellationToken ct)
    {
        var instance = await _repository.GetAsync(id, ct);
        return instance is null ? NotFound() : Ok(instance);
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.InstanceWrite)]
    [ProducesResponseType(typeof(Instance), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new instance", "Requires instance.write permission")]
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

    [HttpPut("{id:guid}")]
    [RequirePermission(KKBOTPermissions.InstanceWrite, "instance", "{id}")]
    [ProducesResponseType(typeof(Instance), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Update an instance", "Requires instance.write permission")]
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

    [HttpDelete("{id:guid}")]
    [RequirePermission(KKBOTPermissions.InstanceDelete, "instance", "{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete an instance", "Requires instance.delete permission")]
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