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

public sealed class FunnelAnalysisService : IFunnelAnalysisService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<FunnelAnalysisService> _logger;

    public FunnelAnalysisService(AnalyticsDbContext dbContext, ILogger<FunnelAnalysisService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<FunnelAnalysis> CreateFunnelAnalysisAsync(CreateFunnelAnalysisRequest request, string userId, CancellationToken ct = default)
    {
        var analysis = new FunnelAnalysis
        {
            Name = request.Name,
            Description = request.Description,
            Steps = request.Steps,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            FilterJson = request.FilterJson
        };

        _dbContext.FunnelAnalyses.Add(analysis);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created funnel analysis {AnalysisId}", analysis.Id);
        return analysis;
    }

    public async Task<FunnelAnalysis?> GetFunnelAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.FunnelAnalyses
            .Include(f => f.Steps)
            .Include(f => f.Result)
                .ThenInclude(r => r.StepResults)
            .FirstOrDefaultAsync(f => f.Id == analysisId, ct);
    }

    public async Task<List<FunnelAnalysis>> GetFunnelAnalysesAsync(string userId, FunnelAnalysisFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.FunnelAnalyses
            .Include(f => f.Steps)
            .Include(f => f.Result)
            .AsQueryable();

        query = query.OrderByDescending(f => f.CreatedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<FunnelAnalysis> RunFunnelAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default)
    {
        var analysis = await _dbContext.FunnelAnalyses
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.Id == analysisId, ct);

        if (analysis == null)
            throw new InvalidOperationException("Funnel analysis not found");

        // In a real implementation, this would query event data
        // For now, generate mock results
        var result = await GenerateMockFunnelResultAsync(analysis, ct);

        analysis.Result = result;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Ran funnel analysis {AnalysisId} with {StepCount} steps", analysis.Id, analysis.Steps.Count);
        return analysis;
    }

    private async Task<FunnelResult> GenerateMockFunnelResultAsync(FunnelAnalysis analysis, CancellationToken ct)
    {
        var stepResults = new List<StepResult>();
        var random = new Random();
        var totalEntrances = 10000;
        var currentUsers = totalEntrances;

        for (int i = 0; i < analysis.Steps.Count; i++)
        {
            var step = analysis.Steps[i];
            var conversionRate = 0.7 + random.NextDouble() * 0.2; // 70-90%
            var completed = (int)(currentUsers * conversionRate);

            stepResults.Add(new StepResult
            {
                StepOrder = step.Order,
                StepName = step.Name,
                UsersEntered = currentUsers,
                UsersCompleted = completed,
                ConversionRate = conversionRate,
                DropOffRate = 1 - conversionRate,
                AverageTimeInStep = random.NextDouble() * 300,
                DropOffReasons = new Dictionary<string, int>
                {
                    ["Timeout"] = (int)(currentUsers * (1 - conversionRate) * 0.4),
                    ["Error"] = (int)(currentUsers * (1 - conversionRate) * 0.3),
                    ["User Cancelled"] = (int)(currentUsers * (1 - conversionRate) * 0.2),
                    ["Other"] = (int)(currentUsers * (1 - conversionRate) * 0.1)
                }
            });

            currentUsers = completed;
        }

        return new FunnelResult
        {
            StepResults = stepResults,
            OverallConversionRate = stepResults.LastOrDefault()?.ConversionRate ?? 0,
            AverageTimeToConvert = stepResults.Sum(s => s.AverageTimeInStep),
            TotalEntrances = totalEntrances,
            TotalConversions = stepResults.LastOrDefault()?.UsersCompleted ?? 0,
            CalculatedAt = DateTime.UtcNow
        };
    }

    public async Task<FunnelComparison> CompareFunnelsAsync(List<Guid> analysisIds, string userId, CancellationToken ct = default)
    {
        var funnels = await _dbContext.FunnelAnalyses
            .Include(f => f.Steps)
            .Include(f => f.Result)
                .ThenInclude(r => r.StepResults)
            .Where(f => analysisIds.Contains(f.Id))
            .ToListAsync(ct);

        var comparison = new FunnelComparison
        {
            Funnels = funnels,
            Comparison = new Dictionary<string, object>
            {
                ["funnelCount"] = funnels.Count,
                ["averageConversionRate"] = funnels.Where(f => f.Result != null).Average(f => f.Result.OverallConversionRate),
                ["bestFunnel"] = funnels.OrderByDescending(f => f.Result?.OverallConversionRate ?? 0).FirstOrDefault()?.Name ?? "",
                ["worstFunnel"] = funnels.OrderBy(f => f.Result?.OverallConversionRate ?? 1).FirstOrDefault()?.Name ?? ""
            }
        };

        return comparison;
    }
}