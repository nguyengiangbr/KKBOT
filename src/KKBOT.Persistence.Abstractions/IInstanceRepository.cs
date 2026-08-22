// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;

namespace KKBOT.Persistence.Abstractions;

public interface IInstanceRepository
{
    Task<Instance?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Instance>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Instance instance, CancellationToken cancellationToken);
    Task UpdateAsync(Instance instance, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}