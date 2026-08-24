// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Performance.Optimization;

/// <summary>
/// EF Core query optimization and compiled query management.
/// </summary>
public sealed class EfCoreQueryOptimizer : IDisposable
{
    private readonly ILogger<EfCoreQueryOptimizer> _logger;
    private readonly EfCoreOptimizationOptions _options;
    private readonly ConcurrentDictionary<string, CompiledQueryCacheEntry> _compiledQueryCache = new();
    private readonly ConcurrentDictionary<string, QueryPlan> _queryPlanCache = new();
    private readonly IMetricsRegistry _metrics;
    private readonly Timer? _cacheCleanupTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public EfCoreQueryOptimizer(
        ILogger<EfCoreQueryOptimizer> logger,
        IOptions<EfCoreOptimizationOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        if (_options.EnableCompiledQueryCache)
        {
            _cacheCleanupTimer = new Timer(CleanupCacheAsync, null, 
                TimeSpan.FromMinutes(_options.CacheCleanupIntervalMinutes), 
                TimeSpan.FromMinutes(_options.CacheCleanupIntervalMinutes));
        }
    }

    /// <summary>
    /// Gets or creates a compiled query for the given query expression.
    /// </summary>
    public TResult ExecuteCompiledQuery<TContext, TResult>(
        TContext context,
        Expression<Func<TContext, TResult>> query,
        string cacheKey = null) 
        where TContext : DbContext
    {
        var key = cacheKey ?? GenerateCacheKey(query);
        
        if (_options.EnableCompiledQueryCache && 
            _compiledQueryCache.TryGetValue(key, out var cached))
        {
            _metrics.IncrementHttpRequests(); // Reuse metric
            return cached.CompiledQuery.Invoke(context);
        }

        // Compile the query
        var compiledQuery = EF.CompileQuery(query);
        
        if (_options.EnableCompiledQueryCache)
        {
            _compiledQueryCache[key] = new CompiledQueryCacheEntry
            {
                CompiledQuery = compiledQuery,
                CreatedAt = DateTimeOffset.UtcNow,
                AccessCount = 1,
                LastAccessed = DateTimeOffset.UtcNow
            };
        }

        return compiledQuery.Invoke(context);
    }

    /// <summary>
    /// Gets or creates a compiled async query.
    /// </summary>
    public async Task<TResult> ExecuteCompiledQueryAsync<TContext, TResult>(
        TContext context,
        Expression<Func<TContext, Task<TResult>>> query,
        string cacheKey = null,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var key = cacheKey ?? GenerateCacheKey(query);
        
        if (_options.EnableCompiledQueryCache && 
            _compiledQueryCache.TryGetValue(key, out var cached))
        {
            _metrics.IncrementHttpRequests();
            return await cached.AsyncCompiledQuery.Invoke(context, cancellationToken);
        }

        var compiledQuery = EF.CompileAsyncQuery(query);
        
        if (_options.EnableCompiledQueryCache)
        {
            _compiledQueryCache[key] = new CompiledQueryCacheEntry
            {
                AsyncCompiledQuery = compiledQuery,
                CreatedAt = DateTimeOffset.UtcNow,
                AccessCount = 1,
                LastAccessed = DateTimeOffset.UtcNow
            };
        }

        return await compiledQuery.Invoke(context, cancellationToken);
    }

    /// <summary>
    /// Analyzes a query and returns optimization recommendations.
    /// </summary>
    public QueryAnalysisResult AnalyzeQuery<TContext>(Expression<Func<TContext, object>> query)
        where TContext : DbContext
    {
        var analysis = new QueryAnalysisResult
        {
            QueryExpression = query.ToString(),
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        // Analyze query expression tree
        var visitor = new QueryExpressionVisitor();
        visitor.Visit(query);
        
        analysis.HasSelectNPlusOne = visitor.HasSelectNPlusOne;
        analysis.HasCartesianProduct = visitor.HasCartesianProduct;
        analysis.HasUnfilteredQuery = visitor.HasUnfilteredQuery;
        analysis.UsedTables = visitor.UsedTables.ToList();
        analysis.UsedColumns = visitor.UsedColumns.ToList();
        analysis.JoinCount = visitor.JoinCount;
        analysis.WhereClauseCount = visitor.WhereClauseCount;
        analysis.OrderByCount = visitor.OrderByCount;
        analysis.SkipTakeUsed = visitor.SkipTakeUsed;

        // Generate recommendations
        if (analysis.HasSelectNPlusOne)
        {
            analysis.Recommendations.Add("N+1 query detected. Use Include() or projection to eager load related data.");
        }
        if (analysis.HasCartesianProduct)
        {
            analysis.Recommendations.Add("Cartesian product detected. Review join conditions.");
        }
        if (analysis.HasUnfilteredQuery)
        {
            analysis.Recommendations.Add("Query without WHERE clause detected. Add filtering to limit results.");
        }
        if (!analysis.SkipTakeUsed)
        {
            analysis.Recommendations.Add("Consider adding pagination (Skip/Take) for large result sets.");
        }
        if (analysis.JoinCount > 5)
        {
            analysis.Recommendations.Add($"High join count ({analysis.JoinCount}). Consider denormalization or materialized views.");
        }

        return analysis;
    }

    /// <summary>
    /// Creates a pre-compiled query for common patterns.
    /// </summary>
    public void PrecompileCommonQueries<TContext>() where TContext : DbContext
    {
        // Common query patterns to pre-compile
        var commonQueries = GetCommonQueryPatterns<TContext>();
        
        foreach (var query in commonQueries)
        {
            var key = GenerateCacheKey(query);
            if (!_compiledQueryCache.ContainsKey(query.ToString()))
            {
                try
                {
                    if (query.ReturnType.IsGenericType && 
                        query.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        // Async query - would need async compilation
                    }
                    else
                    {
                        EF.CompileQuery((Expression<Func<DbContext, object>>)query);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to pre-compile query: {Query}", query);
                }
            }
        }
    }

    /// <summary>
    /// Gets query execution statistics.
    /// </summary>
    public QueryStatistics GetStatistics()
    {
        return new QueryStatistics
        {
            CompiledQueryCount = _compiledQueryCache.Count,
            QueryPlanCacheCount = _queryPlanCache.Count,
            TotalExecutions = _compiledQueryCache.Values.Sum(c => c.AccessCount),
            CacheHitRate = CalculateCacheHitRate(),
            AverageExecutionTimeMs = _queryPlanCache.Values.Any() 
                ? _queryPlanCache.Values.Average(p => p.AverageExecutionTimeMs) 
                : 0,
            SlowestQueries = _queryPlanCache.Values
                .OrderByDescending(p => p.AverageExecutionTimeMs)
                .Take(10)
                .Select(p => new SlowQueryInfo
                {
                    QueryKey = p.QueryKey,
                    AverageTimeMs = p.AverageExecutionTimeMs,
                    ExecutionCount = p.ExecutionCount,
                    LastExecuted = p.LastExecuted
                }).ToList()
        };
    }

    private string GenerateCacheKey(Expression query)
    {
        // Generate deterministic cache key from expression
        return query.ToString().GetHashCode().ToString("x8");
    }

    private async void CleanupCacheAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(_options.CacheExpirationMinutes);
        var keysToRemove = _compiledQueryCache
            .Where(kvp => kvp.Value.LastAccessed < cutoff && kvp.Value.AccessCount < _options.MinAccessCountForRetention)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _compiledQueryCache.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} cached queries", keysToRemove.Count);
        }
    }

    private List<Expression> GetCommonQueryPatterns<TContext>() where TContext : DbContext
    {
        // Return common query patterns for pre-compilation
        // This would be customized per application
        return new List<Expression>();
    }

    private double CalculateCacheHitRate()
    {
        var total = _compiledQueryCache.Values.Sum(c => c.AccessCount);
        var hits = _compiledQueryCache.Values.Sum(c => c.AccessCount - 1); // First access is a miss
        return total > 0 ? (double)hits / total : 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cacheCleanupTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}

/// <summary>
/// Query expression analyzer for finding common issues.
/// </summary>
internal sealed class QueryExpressionVisitor : ExpressionVisitor
{
    public bool HasSelectNPlusOne { get; private set; }
    public bool HasCartesianProduct { get; private set; }
    public bool HasUnfilteredQuery { get; private set; }
    public HashSet<string> UsedTables { get; } = new();
    public HashSet<string> UsedColumns { get; } = new();
    public int JoinCount { get; private set; }
    public int WhereClauseCount { get; private set; }
    public int OrderByCount { get; private set; }
    public bool SkipTakeUsed { get; private set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == "Select" && node.Arguments.Count == 2)
        {
            var lambda = node.Arguments[1] as LambdaExpression;
            if (lambda?.Body is MemberExpression || lambda?.Body is NewExpression)
            {
                // Check for navigation property access in Select
                var visitor = new NavigationPropertyVisitor();
                visitor.Visit(lambda.Body);
                if (visitor.HasNavigationProperty)
                {
                    HasSelectNPlusOne = true;
                }
            }
        }

        if (node.Method.Name == "Where")
        {
            WhereClauseCount++;
        }

        if (node.Method.Name == "OrderBy" || node.Method.Name == "OrderByDescending" ||
            node.Method.Name == "ThenBy" || node.Method.Name == "ThenByDescending")
        {
            OrderByCount++;
        }

        if (node.Method.Name == "Skip" || node.Method.Name == "Take")
        {
            SkipTakeUsed = true;
        }

        if (node.Method.Name == "Join" || node.Method.Name == "GroupJoin")
        {
            JoinCount++;
        }

        if (node.Method.Name == "SelectMany")
        {
            // Potential Cartesian product
            HasCartesianProduct = true;
        }

        if (node.Method.Name == "ToList" || node.Method.Name == "ToListAsync" ||
            node.Method.Name == "ToArray" || node.Method.Name == "ToArrayAsync")
        {
            // Check if there's no Where clause before materialization
            // This is simplified - real implementation would walk the tree
            HasUnfilteredQuery = true;
        }

        return base.VisitMethodCall(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression)
        {
            UsedColumns.Add(node.Member.Name);
        }
        return base.VisitMember(node);
    }
}

internal sealed class NavigationPropertyVisitor : ExpressionVisitor
{
    public bool HasNavigationProperty { get; private set; }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression)
        {
            // Check if property is a navigation property (would need entity metadata)
            // Simplified check: if property type is a class/entity
            if (node.Member.GetType().IsClass && node.Member.GetType() != typeof(string))
            {
                HasNavigationProperty = true;
            }
        }
        return base.VisitMember(node);
    }
}

/// <summary>
/// Cached compiled query entry.
/// </summary>
internal sealed class CompiledQueryCacheEntry
{
    public Func<DbContext, object> CompiledQuery { get; init; }
    public Func<DbContext, CancellationToken, Task<object>> AsyncCompiledQuery { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int AccessCount { get; set; }
    public DateTimeOffset LastAccessed { get; set; }
}

/// <summary>
/// Query execution plan cache.
/// </summary>
internal sealed class QueryPlan
{
    public string QueryKey { get; init; } = string.Empty;
    public string ExecutionPlan { get; init; } = string.Empty;
    public double AverageExecutionTimeMs { get; set; }
    public int ExecutionCount { get; set; }
    public DateTimeOffset LastExecuted { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Query analysis result.
/// </summary>
public sealed record QueryAnalysisResult
{
    public string QueryExpression { get; init; } = string.Empty;
    public DateTimeOffset AnalyzedAt { get; init; }
    public bool HasSelectNPlusOne { get; init; }
    public bool HasCartesianProduct { get; init; }
    public bool HasUnfilteredQuery { get; init; }
    public IReadOnlyList<string> UsedTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UsedColumns { get; init; } = Array.Empty<string>();
    public int JoinCount { get; init; }
    public int WhereClauseCount { get; init; }
    public int OrderByCount { get; init; }
    public bool SkipTakeUsed { get; init; }
    public List<string> Recommendations { get; init; } = new();
}

/// <summary>
/// Query statistics.
/// </summary>
public sealed record QueryStatistics
{
    public int CompiledQueryCount { get; init; }
    public int QueryPlanCacheCount { get; init; }
    public long TotalExecutions { get; init; }
    public double CacheHitRate { get; init; }
    public double AverageExecutionTimeMs { get; init; }
    public IReadOnlyList<SlowQueryInfo> SlowestQueries { get; init; } = Array.Empty<SlowQueryInfo>();
}

public sealed record SlowQueryInfo
{
    public string QueryKey { get; init; } = string.Empty;
    public double AverageTimeMs { get: init; }
    public long ExecutionCount { get: init; }
    public DateTimeOffset LastExecuted { get: init; }
}

/// <summary>
/// EF Core optimization options.
/// </summary>
public sealed class EfCoreOptimizationOptions
{
    public bool EnableCompiledQueryCache { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 60;
    public int MinAccessCountForRetention { get; set; } = 2;
    public int CacheCleanupIntervalMinutes { get; set; } = 30;
    public bool EnableQueryPlanCaching { get; set; } = true;
    public bool EnableQueryAnalysis { get; set; } = true;
    public bool AutoPrecompileCommonQueries { get; set; } = false;
    public int MaxCompiledQueries { get; set; } = 1000;
}
