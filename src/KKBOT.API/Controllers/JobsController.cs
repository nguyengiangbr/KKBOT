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
/// Job management API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[SwaggerTag("Manage KKBOT jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly IJobRepository _repository;
    private readonly IInstanceRepository _instanceRepository;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobRepository repository, IInstanceRepository instanceRepository, ILogger<JobsController> logger)
    {
        _repository = repository;
        _instanceRepository = instanceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all jobs (optionally filtered by instance)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Job>), 200)]
    public async Task<ActionResult<IEnumerable<Job>>> GetAll([FromQuery] Guid? instanceId, CancellationToken ct)
    {
        if (instanceId.HasValue)
        {
            var jobs = await _repository.GetByInstanceAsync(instanceId.Value, ct);
            return Ok(jobs);
        }

        // Get all jobs across all instances
        var instances = await _instanceRepository.GetAllAsync(ct);
        var allJobs = new List<Job>();
        foreach (var inst in instances)
        {
            var jobs = await _repository.GetByInstanceAsync(inst.Id, ct);
            allJobs.AddRange(jobs);
        }
        return Ok(allJobs.OrderByDescending(j => j.CreatedAt));
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Job), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Job>> GetById(Guid id, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>
    /// Create a new job
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Job), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<Job>> Create([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        var job = new Job(Guid.NewGuid(), request.InstanceId, request.Type, JobStatus.Pending, request.Payload ?? "{}")
        {
            Priority = request.Priority,
            ScheduledAt = request.ScheduledAt
        };

        await _repository.AddAsync(job, ct);
        _logger.LogInformation("Created job {JobId} for instance {InstanceId}", job.Id, request.InstanceId);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    /// <summary>
    /// Update a job
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Job), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<Job>> Update(Guid id, [FromBody] UpdateJobRequest request, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        if (job is null) return NotFound();

        job = job with
        {
            Status = request.Status ?? job.Status,
            Priority = request.Priority ?? job.Priority,
            Payload = request.Payload ?? job.Payload,
            Result = request.Result ?? job.Result,
            ErrorMessage = request.ErrorMessage ?? job.ErrorMessage,
            StartedAt = request.StartedAt ?? job.StartedAt,
            CompletedAt = request.CompletedAt ?? job.CompletedAt,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.UpdateAsync(job, ct);
        _logger.LogInformation("Updated job {JobId}", id);
        return Ok(job);
    }

    /// <summary>
    /// Delete a job
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        if (job is null) return NotFound();

        await _repository.DeleteAsync(id, ct);
        _logger.LogInformation("Deleted job {JobId}", id);
        return NoContent();
    }
}

public record CreateJobRequest(
    Guid InstanceId,
    JobType Type,
    string? Payload = null,
    int Priority = 0,
    DateTime? ScheduledAt = null
);

public record UpdateJobRequest(
    JobStatus? Status = null,
    int? Priority = null,
    string? Payload = null,
    string? Result = null,
    string? ErrorMessage = null,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null
);