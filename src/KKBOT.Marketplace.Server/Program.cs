// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.OpenApi.Models;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using KKBOT.Security;
using KKBOT.Observability;
using KKBOT.Identity;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseKkbotTelemetry(builder.Configuration);

// Controllers & API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KKBOT Marketplace API",
        Version = "v1",
        Description = "Plugin Marketplace Backend for KKBOT Game Automation Platform",
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

// Database
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MarketplaceDb") 
        ?? builder.Configuration.GetConnectionString("KkbotDb") 
        ?? "DataSource=marketplace.db";
    options.UseSqlite(connectionString);
});

// Security & Identity
builder.Services.AddKkbotSecurity(builder.Configuration);
builder.Services.AddKkbotIdentity(builder.Configuration);

// Marketplace Services
builder.Services.AddSingleton<IStorageService, LocalStorageService>(); // or MinioStorageService
builder.Services.AddSingleton<ISigningService, SigningService>();
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();

// Advanced Marketplace Services
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ISentimentAnalysisService, SentimentAnalysisService>();
builder.Services.AddScoped<ISecurityScanningService, SecurityScanningService>();
builder.Services.AddScoped<ILicenseComplianceService, LicenseComplianceService>();

// ML.NET for sentiment analysis
builder.Services.AddSingleton<MLContext>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MarketplaceDbContext>()
    .AddCheck("marketplace-alive", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Marketplace is alive"));

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

// SignalR for real-time updates
builder.Services.AddSignalR();

var app = builder.Build();

// Ensure database created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    db.Database.EnsureCreated();
    
    // Seed default data
    await SeedDefaultDataAsync(db);
}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KKBOT Marketplace API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<MarketplaceHub>("/hubs/marketplace");

app.Run();

// Seed default data
static async Task SeedDefaultDataAsync(MarketplaceDbContext db)
{
    // Check if already seeded
    if (await db.Publishers.AnyAsync())
        return;

    // Create system publisher
    var systemPublisher = new Publisher
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        UserId = "system",
        Name = "KKBOT Official",
        Slug = "kkbot-official",
        Description = "Official KKBOT plugins and tools",
        Email = "official@kkbot.example.com",
        VerificationStatus = "verified",
        RevenueSharePercent = 100,
        IsActive = true
    };
    db.Publishers.Add(systemPublisher);
    await db.SaveChangesAsync();
}