// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using KKBOT.PluginMarketplace;
using KKBOT.Plugins.Loader;

namespace KKBOT.PluginMarketplace;

public static class MarketplaceExtensions
{
    public static IServiceCollection AddKkbotPluginMarketplace(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<MarketplaceOptions>(configuration.GetSection("Marketplace"));

        // Registry
        services.AddSingleton<IPluginRegistry, LocalPluginRegistry>();

        // Repository
        services.AddHttpClient<IPluginRepository, HttpPluginRepository>();

        // Installer
        services.AddSingleton<IPluginInstaller, LocalPluginInstaller>();

        // Ensure PluginLoader is registered (from Plugins.Loader)
        // services.AddSingleton<PluginLoader>(); // Already registered in Orchestration

        return services;
    }
}