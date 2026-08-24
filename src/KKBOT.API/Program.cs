// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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
using KKBOT.Analytics;
using KKBOT.Collaboration;
using KKBOT.Enterprise;
using KKBOT.Agent;
using KKBOT.Identity.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Complete telemetry stack: Serilog + OpenTelemetry + Health Checks + Correlation IDs
builder.Host.UseKkbotTelemetry(builder.Configuration, "KKBOT.API");

// Controllers & API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KKBOT API",
        Version = "v1",
        Description = "REST API for KKBOT Game Automation Platform",
        Contact = new OpenApiContact
        {
            Name = "KKBOT Team",
            Email = "team@kkbot.example.com",
            Url = new Uri("https://github.com/your-org/kkbot")
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // Security scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

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

// Game Agent Framework
builder.Services.AddKkbotAgent(builder.Configuration);

// Orchestration
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<DagScheduler>();
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<InstanceOrchestrator>();

// Application
builder.Services.AddKkbotApplication();

// Health checks
builder.Services.AddKkbotHealthChecks();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
    
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded. Please try again later." });
    };
});

var app = builder.Build();

// Ensure database created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KkbotDbContext>();
    db.Database.EnsureCreated();
}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KKBOT API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

// Full telemetry pipeline: Correlation ID + Serilog Request Logging + Prometheus Metrics
app.UseKkbotTelemetryPipeline();

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Rate limiting
app.UseRateLimiter();

// Tenant resolution middleware (must be before authentication)
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

// Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();