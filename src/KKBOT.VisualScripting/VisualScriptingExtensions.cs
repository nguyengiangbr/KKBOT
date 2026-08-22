// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.VisualScripting.Engine;

namespace KKBOT.VisualScripting;

public static class VisualScriptingExtensions
{
    public static IServiceCollection AddKkbotVisualScripting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VisualScriptingOptions>(configuration.GetSection("VisualScripting"));
        services.AddSingleton<IVisualScriptingEngine, VisualScriptingEngine>();
        return services;
    }
}