// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using KKBOT.Agent.Abstractions;
using KKBOT.Agent.Core;
using KKBOT.Identity.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/agents")]
[Produces("application/json")]
[SwaggerTag("Game Agent Management")]
public sealed class AgentsController : ControllerBase
{
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentFactory agentFactory, ILogger<AgentsController> logger)
    {
        _agentFactory = agentFactory;
        _logger = logger;
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.AIUse)] // Changed from InstanceWrite to AIUse
    [ProducesResponseType(typeof(IGameAgent), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new game agent", "Requires ai.use permission")]
    public async Task<ActionResult<AgentResponse>> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var agent = await _agentFactory.CreateAgentAsync(request.Type, request.Configuration, ct);
        return CreatedAtAction(nameof(GetAgent), new { id = agent.AgentId }, new AgentResponse(agent));
    }

    [HttpGet]
    [RequirePermission(KKBOTPermissions.AIUse)]
    [ProducesResponseType(typeof(IEnumerable<AgentResponse>), 200)]
    [SwaggerOperation("List all agents", "Requires ai.use permission")]
    public async Task<ActionResult<IEnumerable<AgentResponse>>> GetAgents(CancellationToken ct)
    {
        // In real implementation, query from repository
        return Ok(Array.Empty<AgentResponse>());
    }

    [HttpGet("{id}")]
    [RequirePermission(KKBOTPermissions.AIUse, "agent", "{id}")]
    [ProducesResponseType(typeof(AgentResponse), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get agent by ID", "Requires ai.use permission")]
    public async Task<ActionResult<AgentResponse>> GetAgent(string id, CancellationToken ct)
    {
        // In real implementation, query from repository
        return NotFound();
    }

    [HttpPost("{id}/goals")]
    [RequirePermission(KKBOTPermissions.AIUse, "agent", "{id}")]
    [ProducesResponseType(typeof(GoalExecutionResponse), 202)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Submit a goal for execution", "Requires ai.use permission")]
    public async Task<ActionResult<GoalExecutionResponse>> ExecuteGoal(string id, [FromBody] ExecuteGoalRequest request, CancellationToken ct)
    {
        // In real implementation, get agent and execute goal
        // For now, return accepted
        var executionId = Guid.NewGuid().ToString("N");
        return Accepted(new GoalExecutionResponse(executionId, "accepted", request.Goal.Id));
    }

    [HttpGet("{id}/goals/{goalId}")]
    [RequirePermission(KKBOTPermissions.AIUse, "agent", "{id}")]
    [ProducesResponseType(typeof(GoalStatusResponse), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get goal execution status", "Requires ai.use permission")]
    public async Task<ActionResult<GoalStatusResponse>> GetGoalStatus(string id, string goalId, CancellationToken ct)
    {
        return NotFound();
    }

    [HttpDelete("{id}/goals/{goalId}")]
    [RequirePermission(KKBOTPermissions.AIUse, "agent", "{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Cancel goal execution", "Requires ai.use permission")]
    public async Task<IActionResult> CancelGoal(string id, string goalId, CancellationToken ct)
    {
        return NoContent();
    }

    [HttpGet("{id}/goals/{goalId}/trajectory")]
    [RequirePermission(KKBOTPermissions.AIUse, "agent", "{id}")]
    [ProducesResponseType(typeof(Trajectory), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get execution trajectory", "Requires ai.use permission")]
    public async Task<ActionResult<Trajectory>> GetTrajectory(string id, string goalId, CancellationToken ct)
    {
        return NotFound();
    }

    [HttpPost("{id}/learn/demonstrations")]
    [RequirePermission(KKBOTPermissions.AITrain, "agent", "{id}")]
    [ProducesResponseType(typeof(LearningResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Learn from demonstrations", "Requires ai.train permission")]
    public async Task<ActionResult<LearningResult>> LearnFromDemonstrations(string id, [FromBody] LearnFromDemosRequest request, CancellationToken ct)
    {
        return NotFound();
    }

    [HttpPost("{id}/learn/feedback")]
    [RequirePermission(KKBOTPermissions.AITrain, "agent", "{id}")]
    [ProducesResponseType(typeof(LearningResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Learn from human feedback", "Requires ai.train permission")]
    public async Task<ActionResult<LearningResult>> LearnFromFeedback(string id, [FromBody] LearnFromFeedbackRequest request, CancellationToken ct)
    {
        return NotFound();
    }

    [HttpGet("{id}/skills")]
    [RequirePermission(KKBOTPermissions.AIUse, "agent", "{id}")]
    [ProducesResponseType(typeof(IEnumerable<Skill>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("List agent skills", "Requires ai.use permission")]
    public async Task<ActionResult<IEnumerable<Skill>>> GetSkills(string id, CancellationToken ct)
    {
        return Ok(Array.Empty<Skill>());
    }

    [HttpPost("{id}/skills")]
    [RequirePermission(KKBOTPermissions.AITrain, "agent", "{id}")]
    [ProducesResponseType(typeof(Skill), 201)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create skill manually", "Requires ai.train permission")]
    public async Task<ActionResult<Skill>> CreateSkill(string id, [FromBody] CreateSkillRequest request, CancellationToken ct)
    {
        return NotFound();
    }
}

public record CreateAgentRequest(
    AgentType Type,
    AgentConfiguration Configuration
);

public record AgentResponse(
    IGameAgent Agent
);

public record ExecuteGoalRequest(
    GameGoal Goal,
    bool StreamResults = false
);

public record GoalExecutionResponse(
    string ExecutionId,
    string Status,
    string GoalId
);

public record GoalStatusResponse(
    string ExecutionId,
    string Status,
    GameGoal Goal,
    Plan? CurrentPlan,
    int CurrentStep,
    AgentDecision? LastDecision,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt
);

public record LearnFromDemosRequest(
    IEnumerable<Trajectory> Demonstrations,
    LearningConfig? Config = null
);

public record LearnFromFeedbackRequest(
    IEnumerable<FeedbackSample> Feedback,
    LearningConfig? Config = null
);

public record CreateSkillRequest(
    string Name,
    string Description,
    SkillCategory Category,
    SkillParameter[] Parameters,
    Plan TemplatePlan,
    SkillPrecondition[] Preconditions,
    SkillPostcondition[] Postconditions
);