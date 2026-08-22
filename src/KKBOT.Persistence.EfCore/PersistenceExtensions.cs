// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Persistence.Abstractions;
using KKBOT.Persistence.EfCore;

namespace KKBOT.Persistence.EfCore;

public static class PersistenceExtensions
{
    public static IServiceCollection AddKkbotPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KkbotDb") ?? "DataSource=kkbot.db";
        services.AddDbContext<KkbotDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IInstanceRepository, EfInstanceRepository>();
        services.AddScoped<IJobRepository, EfJobRepository>();
        return services;
    }
}