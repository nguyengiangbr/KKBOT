using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Persistence.Repositories;

public sealed class InstanceRepository : Repository<Instance>, IInstanceRepository
{
    private readonly KkbBotDbContext _context;
    public InstanceRepository(KkbBotDbContext context) : base(context) => _context = context;

    public async Task<IReadOnlyList<Instance>> GetByStateAsync(InstanceState state, CancellationToken cancellationToken)
    {
        return await _context.Instances.Where(i => i.State == state).ToListAsync(cancellationToken);
    }
}