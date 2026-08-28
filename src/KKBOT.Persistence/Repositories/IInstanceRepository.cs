using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KKBOT.Persistence.Repositories;

public interface IInstanceRepository : IRepository<Instance>
{
    Task<IReadOnlyList<Instance>> GetByStateAsync(InstanceState state, CancellationToken cancellationToken);
}