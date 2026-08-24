// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using KKBOT.Domain.Models;
using KKBOT.Persistence.EfCore;
using KKBOT.Persistence.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Plugins.Loader;
using KKBOT.Security;
using KKBOT.Observability;
using KKBOT.Application;
using KKBOT.PluginMarketplace;
using KKBOT.Identity;
using KKBOT.AI.Pipeline;
using KKBOT.VisualScripting;
using KKBOT.Analytics;
using KKBOT.Collaboration;
using KKBOT.Enterprise;
using KKBOT.Device.Simulator;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.UI.Dashboard.Services;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseKkbotTelemetry();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

builder.Services.AddKkbotPersistence(builder.Configuration);
builder.Services.AddKkbotSecurity(builder.Configuration);
builder.Services.AddKkbotIdentity(builder.Configuration);
builder.Services.AddKkbotPluginMarketplace(builder.Configuration);
builder.Services.AddKkbotAIPipeline(builder.Configuration);
builder.Services.AddKkbotAnalytics(builder.Configuration);
builder.Services.AddKkbotCollaboration(builder.Configuration);
builder.Services.AddKkbotEnterprise(builder.Configuration);
builder.Services.AddKkbotVisualScripting(builder.Configuration);

builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<DagScheduler>();
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<InstanceOrchestrator>();

builder.Services.AddSingleton<IDeviceSessionFactory, KKBOT.Device.Simulator.SimulatedDeviceSessionFactory>();
builder.Services.AddSingleton<IPerceptionPipeline, KKBOT.Perception.Abstractions.PerceptionPipeline>();

builder.Services.AddScoped<IScenarioService, ScenarioService>();
builder.Services.AddScoped<ScenarioParser>();
builder.Services.AddScoped<ScenarioRunner>();

builder.Services.AddKkbotApplication();
builder.Services.AddKkbotHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KkbotDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSerilogRequestLogging();

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();