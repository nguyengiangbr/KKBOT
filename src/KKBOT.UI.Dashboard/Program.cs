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
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseKkbotTelemetry();

// Services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Persistence
builder.Services.AddKkbotPersistence(builder.Configuration);

// Security
builder.Services.AddKkbotSecurity(builder.Configuration);

// Identity & Authentication
builder.Services.AddKkbotIdentity(builder.Configuration);

// Plugin Marketplace
builder.Services.AddKkbotPluginMarketplace(builder.Configuration);

// AI Pipeline
builder.Services.AddKkbotAIPipeline(builder.Configuration);

// Analytics
builder.Services.AddKkbotAnalytics(builder.Configuration);

// Collaboration
builder.Services.AddKkbotCollaboration(builder.Configuration);

// Enterprise
builder.Services.AddKkbotEnterprise(builder.Configuration);

// Visual Scripting
builder.Services.AddKkbotVisualScripting(builder.Configuration);

// Orchestration
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<DagScheduler>();
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<InstanceOrchestrator>();

// Application (MediatR, etc.)
builder.Services.AddKkbotApplication();

// Health checks
builder.Services.AddKkbotHealthChecks();

var app = builder.Build();

// Ensure database created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KkbotDbContext>();
    db.Database.EnsureCreated();
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSerilogRequestLogging();

// Tenant resolution middleware
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();