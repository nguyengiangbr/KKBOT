// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
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

    [HttpGet]
    [RequirePermission(KKBOTPermissions.JobRead)]
    [ProducesResponseType(typeof(IEnumerable<Job>), 200)]
    [SwaggerOperation("Get all jobs", "Requires job.read permission")]
    public async Task<ActionResult<IEnumerable<Job>>> GetAll([FromQuery] Guid? instanceId, CancellationToken ct)
    {
        if (instanceId.HasValue)
        {
            var jobs = await _repository.GetByInstanceAsync(instanceId.Value, ct);
            return Ok(jobs);
        }

        var instances = await _instanceRepository.GetAllAsync(ct);
        var allJobs = new List<Job>();
        foreach (var inst in instances)
        {
            var jobs = await _repository.GetByInstanceAsync(inst.Id, ct);
            allJobs.AddRange(jobs);
        }
        return Ok(allJobs.OrderByDescending(j => j.CreatedAt));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(KKBOTPermissions.JobRead, "job", "{id}")]
    [ProducesResponseType(typeof(Job), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get job by ID", "Requires job.read permission")]
    public async Task<ActionResult<Job>> GetById(Guid id, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.JobWrite)]
    [ProducesResponseType(typeof(Job), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new job", "Requires job.write permission")]
    public async Task<ActionResult<Job>> Create([FromBody] CreateJobRequest request, CancellationToken ct)
    {
        var job = new Job(Guid.NewGuid(), request.Name, request.ModuleId, request.TaskId)
        {
            Parameters = request.Parameters ?? "{}",
            Priority = request.Priority,
            MaxConcurrency = request.MaxConcurrency,
            Timeout = request.Timeout
        };

        await _repository.AddAsync(job, ct);
        _logger.LogInformation("Created job {JobId} ({Name})", job.Id, job.Name);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(KKBOTPermissions.JobWrite, "job", "{id}")]
    [ProducesResponseType(typeof(Job), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Update a job", "Requires job.write permission")]
    public async Task<ActionResult<Job>> Update(Guid id, [FromBody] UpdateJobRequest request, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        if (job is null) return NotFound();

        job = job with
        {
            Name = request.Name ?? job.Name,
            TaskId = request.TaskId ?? job.TaskId,
            Parameters = request.Parameters ?? job.Parameters,
            Priority = request.Priority ?? job.Priority,
            MaxConcurrency = request.MaxConcurrency ?? job.MaxConcurrency,
            Timeout = request.Timeout ?? job.Timeout,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.UpdateAsync(job, ct);
        _logger.LogInformation("Updated job {JobId}", id);
        return Ok(job);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(KKBOTPermissions.JobCancel, "job", "{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a job", "Requires job.cancel permission")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        if (job is null) return NotFound();

        await _repository.DeleteAsync(id, ct);
        _logger.LogInformation("Deleted job {JobId}", id);
        return NoContent();
    }

    [HttpPost("{id:guid}/execute")]
    [RequirePermission(KKBOTPermissions.JobExecute, "job", "{id}")]
    [ProducesResponseType(typeof(JobRun), 202)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Execute a job", "Requires job.execute permission")]
    public async Task<ActionResult<JobRun>> Execute(Guid id, [FromBody] ExecuteJobRequest? request, CancellationToken ct)
    {
        var job = await _repository.GetAsync(id, ct);
        if (job is null) return NotFound();

        var jobRun = new JobRun(Guid.NewGuid(), id, request?.InstanceId ?? Guid.Empty)
        {
            Parameters = request?.Parameters ?? "{}",
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _repository.AddJobRunAsync(jobRun, ct);
        _logger.LogInformation("Started job run {JobRunId} for job {JobId}", jobRun.Id, id);
        return AcceptedAtAction(nameof(GetJobRun), new { id = jobRun.Id }, jobRun);
    }

    [HttpGet("runs/{runId:guid}")]
    [RequirePermission(KKBOTPermissions.JobRead)]
    [ProducesResponseType(typeof(JobRun), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get job run by ID", "Requires job.read permission")]
    public async Task<ActionResult<JobRun>> GetJobRun(Guid runId, CancellationToken ct)
    {
        var jobRun = await _repository.GetJobRunAsync(runId, ct);
        return jobRun is null ? NotFound() : Ok(jobRun);
    }
}

public record CreateJobRequest(
    string Name,
    Guid ModuleId,
    string TaskId,
    string? Parameters = null,
    int Priority = 0,
    int MaxConcurrency = 1,
    TimeSpan? Timeout = null
);

public record UpdateJobRequest(
    string? Name = null,
    string? TaskId = null,
    string? Parameters = null,
    int? Priority = null,
    int? MaxConcurrency = null,
    TimeSpan? Timeout = null
);

public record ExecuteJobRequest(
    Guid? InstanceId = null,
    string? Parameters = null
);