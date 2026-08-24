// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KKBOT.Agent.Abstractions;
using KKBOT.Persistence.EfCore;

namespace KKBOT.Agent.Memory;

/// <summary>
/// EF Core implementation of agent memory.
/// </summary>
public sealed class EfCoreAgentMemory : IAgentMemory
{
    private readonly ILogger<EfCoreAgentMemory> _logger;
    private readonly IDbContextFactory<KkbotDbContext> _dbContextFactory;
    private readonly ConcurrentDictionary<string, Dictionary<string, object>> _workingMemory = new();
    private readonly ConcurrentDictionary<string, Skill> _skillCache = new();

    public EfCoreAgentMemory(
        ILogger<EfCoreAgentMemory> logger,
        IDbContextFactory<KkbotDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task StoreEpisodeAsync(Episode episode)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var entity = new AgentEpisodeEntity
        {
            Id = Guid.Parse(episode.EpisodeId),
            AgentId = Guid.Parse(episode.Context.GetValueOrDefault("agentId")?.ToString() ?? Guid.Empty.ToString()),
            GoalId = episode.Goal.Id,
            GoalDescription = episode.Goal.Description,
            GoalType = episode.Goal.Type.ToString(),
            TrajectoryJson = System.Text.Json.JsonSerializer.Serialize(episode.Trajectory),
            RewardValue = episode.Outcome.Value,
            RewardSource = episode.Outcome.Source.ToString(),
            Success = episode.Trajectory.Success,
            DurationMs = episode.Trajectory.TotalDuration.TotalMilliseconds,
            CreatedAt = episode.Timestamp,
            ContextJson = System.Text.Json.JsonSerializer.Serialize(episode.Context)
        };

        db.AgentEpisodes.Add(entity);
        await db.SaveChangesAsync();
        
        _logger.LogDebug("Stored episode {EpisodeId}", episode.EpisodeId);
    }

    public async Task<IReadOnlyList<Episode>> RetrieveEpisodesAsync(EpisodeQuery query, int limit = 10)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var q = db.AgentEpisodes.AsQueryable();

        if (!string.IsNullOrEmpty(query.GoalId))
            q = q.Where(e => e.GoalId == query.GoalId);

        if (!string.IsNullOrEmpty(query.AgentId))
            q = q.Where(e => e.AgentId == Guid.Parse(query.AgentId));

        if (query.FromDate.HasValue)
            q = q.Where(e => e.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(e => e.CreatedAt <= query.ToDate.Value);

        if (query.SuccessOnly == true)
            q = q.Where(e => e.Success);

        if (query.MinReward.HasValue)
            q = q.Where(e => e.RewardValue >= query.MinReward.Value);

        var entities = await q
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return entities.Select(e => new Episode
        {
            EpisodeId = e.Id.ToString(),
            Goal = new GameGoal { Id = e.GoalId, Description = e.GoalDescription, Type = Enum.Parse<GoalType>(e.GoalType) },
            Trajectory = System.Text.Json.JsonSerializer.Deserialize<Trajectory>(e.TrajectoryJson) ?? new Trajectory(),
            Outcome = new RewardSignal { Value = e.RewardValue, Source = Enum.Parse<RewardSource>(e.RewardSource) },
            Context = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(e.ContextJson) ?? new(),
            Timestamp = e.CreatedAt
        }).ToList();
    }

    public async Task StoreFactAsync(SemanticFact fact)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var entity = new AgentFactEntity
        {
            Id = Guid.Parse(fact.FactId),
            Subject = fact.Subject,
            Predicate = fact.Predicate,
            Object = fact.Object,
            Confidence = fact.Confidence,
            Source = fact.Source,
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(fact.Metadata),
            CreatedAt = fact.CreatedAt,
            ExpiresAt = fact.ExpiresAt
        };

        db.AgentFacts.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<SemanticFact>> QueryFactsAsync(string query, int limit = 10)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Simple text search - in production use vector search
        var entities = await db.AgentFacts
            .Where(f => f.Subject.Contains(query) || f.Predicate.Contains(query) || f.Object.Contains(query))
            .OrderByDescending(f => f.Confidence)
            .Take(limit)
            .ToListAsync();

        return entities.Select(e => new SemanticFact
        {
            FactId = e.Id.ToString(),
            Subject = e.Subject,
            Predicate = e.Predicate,
            Object = e.Object,
            Confidence = e.Confidence,
            Source = e.Source,
            Metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(e.MetadataJson) ?? new(),
            CreatedAt = e.CreatedAt,
            ExpiresAt = e.ExpiresAt
        }).ToList();
    }

    public Task UpdateWorkingMemoryAsync(string key, object value)
    {
        var memory = _workingMemory.GetOrAdd(key, _ => new ConcurrentDictionary<string, object>());
        memory[key] = value;
        return Task.CompletedTask;
    }

    public Task<T?> GetWorkingMemoryAsync<T>(string key)
    {
        if (_workingMemory.TryGetValue(key, out var memory) && memory.TryGetValue(key, out var value))
        {
            return Task.FromResult((T?)value);
        }
        return Task.FromResult<T?>(default);
    }

    public async Task StoreSkillAsync(Skill skill)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var entity = new AgentSkillEntity
        {
            Id = Guid.Parse(skill.SkillId),
            Name = skill.Name,
            Description = skill.Description,
            Category = skill.Category.ToString(),
            ParametersJson = System.Text.Json.JsonSerializer.Serialize(skill.Parameters),
            TemplatePlanJson = System.Text.Json.JsonSerializer.Serialize(skill.TemplatePlan),
            PreconditionsJson = System.Text.Json.JsonSerializer.Serialize(skill.Preconditions),
            PostconditionsJson = System.Text.Json.JsonSerializer.Serialize(skill.Postconditions),
            SuccessRate = skill.SuccessRate,
            UsageCount = skill.UsageCount,
            LearnedFrom = skill.LearnedFrom,
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(skill.Metadata),
            CreatedAt = skill.CreatedAt
        };

        db.AgentSkills.Add(entity);
        await db.SaveChangesAsync();
        
        _skillCache[skill.SkillId] = skill;
    }

    public async Task<Skill?> GetSkillAsync(string skillId)
    {
        if (_skillCache.TryGetValue(skillId, out var cached))
            return cached;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var entity = await db.AgentSkills.FirstOrDefaultAsync(s => s.Id == Guid.Parse(skillId));
        if (entity == null) return null;

        var skill = new Skill
        {
            SkillId = entity.Id.ToString(),
            Name = entity.Name,
            Description = entity.Description,
            Category = Enum.Parse<SkillCategory>(entity.Category),
            Parameters = System.Text.Json.JsonSerializer.Deserialize<SkillParameter[]>(entity.ParametersJson) ?? Array.Empty<SkillParameter>(),
            TemplatePlan = System.Text.Json.JsonSerializer.Deserialize<Plan>(entity.TemplatePlanJson) ?? new Plan(),
            Preconditions = System.Text.Json.JsonSerializer.Deserialize<SkillPrecondition[]>(entity.PreconditionsJson) ?? Array.Empty<SkillPrecondition>(),
            Postconditions = System.Text.Json.JsonSerializer.Deserialize<SkillPostcondition[]>(entity.PostconditionsJson) ?? Array.Empty<SkillPostcondition>(),
            SuccessRate = entity.SuccessRate,
            UsageCount = entity.UsageCount,
            LearnedFrom = entity.LearnedFrom,
            Metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(entity.MetadataJson) ?? new(),
            CreatedAt = entity.CreatedAt
        };

        _skillCache[skillId] = skill;
        return skill;
    }

    public async Task<IReadOnlyList<Skill>> ListSkillsAsync(SkillCategory? category = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var q = db.AgentSkills.AsQueryable();
        if (category.HasValue)
            q = q.Where(s => s.Category == category.Value.ToString());

        var entities = await q.OrderByDescending(s => s.SuccessRate).ToListAsync();

        return entities.Select(e => new Skill
        {
            SkillId = e.Id.ToString(),
            Name = e.Name,
            Description = e.Description,
            Category = Enum.Parse<SkillCategory>(e.Category),
            Parameters = System.Text.Json.JsonSerializer.Deserialize<SkillParameter[]>(e.ParametersJson) ?? Array.Empty<SkillParameter>(),
            TemplatePlan = System.Text.Json.JsonSerializer.Deserialize<Plan>(e.TemplatePlanJson) ?? new Plan(),
            Preconditions = System.Text.Json.JsonSerializer.Deserialize<SkillPrecondition[]>(e.PreconditionsJson) ?? Array.Empty<SkillPrecondition>(),
            Postconditions = System.Text.Json.JsonSerializer.Deserialize<SkillPostcondition[]>(e.PostconditionsJson) ?? Array.Empty<SkillPostcondition>(),
            SuccessRate = e.SuccessRate,
            UsageCount = e.UsageCount,
            LearnedFrom = e.LearnedFrom,
            Metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(e.MetadataJson) ?? new(),
            CreatedAt = e.CreatedAt
        }).ToList();
    }
}

// Entity classes for EF Core
public sealed class AgentEpisodeEntity
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string GoalId { get; set; } = string.Empty;
    public string GoalDescription { get; set; } = string.Empty;
    public string GoalType { get; set; } = string.Empty;
    public string TrajectoryJson { get; set; } = string.Empty;
    public double RewardValue { get; set; }
    public string RewardSource { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ContextJson { get; set; } = string.Empty;
}

public sealed class AgentFactEntity
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class AgentSkillEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = string.Empty;
    public string TemplatePlanJson { get; set; } = string.Empty;
    public string PreconditionsJson { get; set; } = string.Empty;
    public string PostconditionsJson { get; set; } = string.Empty
    public double SuccessRate { get; set; }
    public int UsageCount { get; set; }
    public string LearnedFrom { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}