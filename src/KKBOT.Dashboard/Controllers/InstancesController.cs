using Microsoft.AspNetCore.Mvc;
using KKBOT.Application.Queries;
using KKBOT.Contracts.Dashboard;
using KKBOT.Contracts.Instances;
using MediatR;

namespace KKBOT.Dashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstancesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InstancesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<PagedResponse<InstanceSummaryDto>> Get(CancellationToken ct) =>
        await _mediator.Send(new GetInstancesQuery(), ct);
}
