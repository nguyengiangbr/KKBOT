// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;
using KKBOT.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace KKBOT.Persistence.EfCore;

public sealed class EfInstanceRepository : IInstanceRepository
{
    private readonly KkbotDbContext _context;

    public EfInstanceRepository(KkbotDbContext context) => _context = context;

    public async Task<Instance?> GetAsync(Guid id, CancellationToken cancellationToken)
        => await _context.Instances.FindAsync(new object[] { id }, cancellationToken);

    public async Task<IReadOnlyList<Instance>> GetAllAsync(CancellationToken cancellationToken)
        => await _context.Instances.AsNoTracking().ToListAsync(cancellationToken);

    public async Task AddAsync(Instance instance, CancellationToken cancellationToken)
    {
        await _context.Instances.AddAsync(instance, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Instance instance, CancellationToken cancellationToken)
    {
        _context.Instances.Update(instance);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Instances.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            _context.Instances.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class EfJobRepository : IJobRepository
{
    private readonly KkbotDbContext _context;

    public EfJobRepository(KkbotDbContext context) => _context = context;

    public async Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken)
        => await _context.Jobs.FindAsync(new object[] { id }, cancellationToken);

    public async Task<IReadOnlyList<Job>> GetByInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
        => await _context.Jobs.AsNoTracking().Where(j => j.Runs.Any(r => r.InstanceId == instanceId)).ToListAsync(cancellationToken);

    public async Task AddAsync(Job job, CancellationToken cancellationToken)
    {
        await _context.Jobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Jobs.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            _context.Jobs.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}