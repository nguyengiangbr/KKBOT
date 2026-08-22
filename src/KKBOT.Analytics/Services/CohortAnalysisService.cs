// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Analytics.Abstractions;
using KKBOT.Analytics.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Analytics.Services;

public sealed class CohortAnalysisService : ICohortAnalysisService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<CohortAnalysisService> _logger;

    public CohortAnalysisService(AnalyticsDbContext dbContext, ILogger<CohortAnalysisService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CohortAnalysis> CreateCohortAnalysisAsync(CreateCohortAnalysisRequest request, string userId, CancellationToken ct = default)
    {
        var analysis = new CohortAnalysis
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CohortDefinition = request.CohortDefinition,
            MetricDefinition = request.MetricDefinition,
            PeriodCount = request.PeriodCount,
            Granularity = request.Granularity
        };

        _dbContext.CohortAnalyses.Add(analysis);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created cohort analysis {AnalysisId}", analysis.Id);
        return analysis;
    }

    public async Task<CohortAnalysis?> GetCohortAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.CohortAnalyses
            .Include(a => a.Results)
                .ThenInclude(r => r.Periods)
            .FirstOrDefaultAsync(a => a.Id == analysisId, ct);
    }

    public async Task<List<CohortAnalysis>> GetCohortAnalysesAsync(string userId, CohortAnalysisFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.CohortAnalyses.AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(a => a.Type == filter.Type.Value);

        query = query.OrderByDescending(a => a.CreatedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<CohortAnalysis> RunCohortAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default)
    {
        var analysis = await _dbContext.CohortAnalyses.FirstOrDefaultAsync(a => a.Id == analysisId, ct);
        if (analysis == null)
            throw new InvalidOperationException("Cohort analysis not found");

        // Parse cohort definition
        var cohortDef = System.Text.Json.JsonSerializer.Deserialize<CohortDefinition>(analysis.CohortDefinition);
        var metricDef = System.Text.Json.JsonSerializer.Deserialize<MetricDefinition>(analysis.MetricDefinition);

        // In a real implementation, this would query the data warehouse
        // For now, generate mock results
        var results = await GenerateMockCohortResultsAsync(analysis, cohortDef, metricDef, ct);

        analysis.Results = results;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Ran cohort analysis {AnalysisId} with {CohortCount} cohorts", analysis.Id, results.Count);
        return analysis;
    }

    private async Task<List<CohortResult>> GenerateMockCohortResultsAsync(CohortAnalysis analysis, CohortDefinition cohortDef, MetricDefinition metricDef, CancellationToken ct)
    {
        var results = new List<CohortResult>();
        var random = new Random();

        // Generate mock cohorts
        var cohortCount = 12;
        for (int i = 0; i < cohortCount; i++)
        {
            var cohortDate = analysis.StartDate.AddMonths(i);
            var cohortSize = random.Next(100, 1000);
            
            var periods = new List<PeriodValue>();
            for (int p = 0; p < analysis.PeriodCount; p++)
            {
                var periodStart = cohortDate.AddMonths(p);
                var retained = (int)(cohortSize * Math.Pow(0.8, p) * (0.9 + random.NextDouble() * 0.2));
                
                periods.Add(new PeriodValue
                {
                    PeriodIndex = p,
                    PeriodStart = periodStart,
                    PeriodEnd = periodStart.AddMonths(1),
                    UsersCount = cohortSize,
                    RetainedUsers = retained,
                    RetentionRate = (double)retained / cohortSize,
                    Value = random.NextDouble() * 100
                });
            }

            results.Add(new CohortResult
            {
                CohortId = $"cohort_{i}",
                CohortLabel = cohortDate.ToString("yyyy-MM"),
                CohortSize = cohortSize,
                CohortDate = cohortDate,
                Periods = periods
            });
        }

        return results;
    }
}

public sealed class CohortDefinition
{
    public string Dimension { get; set; } = string.Empty; // acquisition_date, first_action, etc.
    public string FilterJson { get; set; } = "{}";
}