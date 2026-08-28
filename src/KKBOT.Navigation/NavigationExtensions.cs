// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Navigation;

/// <summary>
/// DI registration for the navigation engine.
/// </summary>
public static class NavigationExtensions
{
    /// <summary>Registers <see cref="INavigationService"/> and options.</summary>
    public static IServiceCollection AddKkbotNavigation(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<NavigationOptions>(configuration.GetSection("Navigation"));
        }
        else
        {
            services.Configure<NavigationOptions>(_ => { });
        }

        services.AddSingleton<INavigationService, NavigationService>();
        return services;
    }
}
