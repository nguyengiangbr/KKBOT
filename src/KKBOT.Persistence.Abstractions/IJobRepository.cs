// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;

namespace KKBOT.Persistence.Abstractions;

public interface IJobRepository
{
    Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Job>> GetByInstanceAsync(Guid instanceId, CancellationToken cancellationToken);
    Task AddAsync(Job job, CancellationToken cancellationToken);
    Task UpdateAsync(Job job, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}