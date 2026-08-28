// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using MediatR;
using KKBOT.Domain.Models;
using KKBOT.Domain.Common;
using KKBOT.Application.Commands.Instances;
using KKBOT.Persistence.Repositories;

namespace KKBOT.Application.Handlers.Instances;

public sealed class StartInstanceHandler : IRequestHandler<StartInstanceCommand, Result>
{
    private readonly IInstanceRepository _repo;
    public StartInstanceHandler(IInstanceRepository repo) => _repo = repo;
    public async Task<Result> Handle(StartInstanceCommand request, CancellationToken ct)
    {
        var instance = await _repo.GetByIdAsync(request.InstanceId, ct);
        if (instance == null)
            return Result.Failure(Error.NotFound("INSTANCE_NOT_FOUND", $"Instance {request.InstanceId} not found"));
        instance.SetState(KKBOT.Domain.Enums.InstanceState.Running);
        await _repo.UpdateAsync(instance, ct);
        return Result.Success();
    }
}