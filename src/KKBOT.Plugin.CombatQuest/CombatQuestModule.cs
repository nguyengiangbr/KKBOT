// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;
using MathNet.Numerics;

namespace KKBOT.Plugin.CombatQuest;

/// <summary>
/// Combat style/role for the character.
/// </summary>
public enum CombatRole
{
    Melee,
    Ranged,
    Caster,
    Tank,
    Support,
    Hybrid
}

/// <summary>
/// Skill configuration for combat.
/// </summary>
public sealed record SkillConfig
{
    public string SkillId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Hotkey { get; init; } = string.Empty;
    public SkillType Type { get; init; } = SkillType.Active;
    public SkillTargetType TargetType { get; init; } = SkillTargetType.Enemy;
    public double CooldownSeconds { get; init; } = 0;
    public double CastTimeSeconds { get; init; } = 0;
    public double Range { get; init; } = 0;
    public double AoERadius { get; init; } = 0;
    public int ManaCost { get; init; } = 0;
    public int Priority { get; init; } = 0; // Higher = more important
    public SkillCondition[] Conditions { get; init; } = Array.Empty<SkillCondition>();
    public bool Enabled { get; init; } = true;
    public string AnimationName { get; init; } = string.Empty; // For animation cancel detection
}

public enum SkillType
{
    Active,
    Passive,
    Toggle,
    Channelled
}

public enum SkillTargetType
{
    Enemy,
    Self,
    Ally,
    Ground,
    Directional
}

/// <summary>
/// Condition for skill usage.
/// </summary>
public sealed record SkillCondition
{
    public ConditionType Type { get; init; }
    public string Parameter { get; init; } = string.Empty;
    public double Value { get; init; } = 0;
    public ComparisonOperator Operator { get; init; } = ComparisonOperator.GreaterThanOrEqual;
}

public enum ConditionType
{
    HPPercent,
    ManaPercent,
    EnemyCount,
    EnemyDistance,
    EnemyHPPercent,
    HasBuff,
    HasDebuff,
    IsInCombat,
    IsMoving,
    HasTarget,
    TargetHasBuff,
    TargetHasDebuff,
    SkillReady,
    Custom
}

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// Target priority configuration.
/// </summary>
public sealed record TargetPriority
{
    public int Priority { get; init; } = 0; // Higher = higher priority
    public TargetType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string[] Tags { get; init; } = Array.Empty<string>(); // e.g., "boss", "elite", "healer"
    public double MaxDistance { get; init; } = double.MaxValue;
    public double MinHPPercent { get; init; } = 0;
    public double MaxHPPercent { get; init; } = 1.0;
    public bool RequireLineOfSight { get; init; } = true;
}

public enum TargetType
{
    Any,
    Boss,
    Elite,
    Normal,
    Healer,
    Caster,
    Melee,
    LowestHP,
    HighestThreat,
    Custom
}

/// <summary>
/// Combat rotation/rotation step.
/// </summary>
public sealed record RotationStep
{
    public int StepOrder { get; init; }
    public string SkillId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public RotationCondition[] Conditions { get; init; } = Array.Empty<RotationCondition>();
    public bool IsOptional { get; init; } = false;
    public int MaxRetries { get; init; } = 3;
}

public sealed record RotationCondition
{
    public ConditionType Type { get; init; }
    public string Parameter { get; init; } = string.Empty;
    public double Value { get; init; } = 0;
    public ComparisonOperator Operator { get; init; } = ComparisonOperator.GreaterThanOrEqual;
}

/// <summary>
/// Quest step definition.
/// </summary>
public sealed record QuestStep
{
    public string StepId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public QuestStepType Type { get; init; }
    public QuestObjective[] Objectives { get; init; } = Array.Empty<QuestObjective>();
    public string[] Prerequisites { get; init; } = Array.Empty<string>();
    public Reward Reward { get; init; } = new();
    public string NextStepId { get; init; } = string.Empty;
    public bool IsOptional { get; init; } = false;
    public TimeSpan? Timeout { get; init; }
}

public enum QuestStepType
{
    TalkToNPC,
    KillMobs,
    CollectItems,
    ReachLocation,
    UseItem,
    CompleteDungeon,
    DefeatBoss,
    EscortNPC,
    DefendObjective,
    Custom
}

public sealed record QuestObjective
{
    public string ObjectiveId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ObjectiveType Type { get; init; }
    public string Target { get; init; } = string.Empty; // NPC name, item name, location, etc.
    public int RequiredCount { get; init; } = 1;
    public int CurrentCount { get; init; } = 0;
    public string Location { get; init; } = string.Empty;
    public double Radius { get; init; } = 0;
}

public enum ObjectiveType
{
    Talk,
    Kill,
    Collect,
    Interact,
    Reach,
    Use,
    Wait,
    Protect
}

public sealed record Reward
{
    public long Experience { get; init; } = 0;
    public long Gold { get; init; } = 0;
    public string[] Items { get; init; } = Array.Empty<string>();
    public long Reputation { get; init; } = 0;
}

/// <summary>
/// Combat & Quest Engine configuration.
/// </summary>
public sealed class CombatQuestOptions
{
    // Combat
    public CombatRole Role { get; set; } = CombatRole.Melee;
    public List<SkillConfig> Skills { get; set; } = new();
    public List<RotationStep> Rotation { get; set; } = new();
    public List<TargetPriority> TargetPriorities { get; set; } = new();
    public double GlobalCooldownMs { get; set; } = 1000;
    public double MaxCombatDistance { get; set; } = 30;
    public bool EnableAutoTargeting { get; set; } = true;
    public bool EnableKiting { get; set; } = true;
    public double KitingDistance { get; set; } = 15;
    public bool EnableAoEAvoidance { get; set; } = true;
    public double AoEAvoidanceDistance { get; set; } = 5;
    public bool EnableInterrupts { get; set; } = true;
    public string[] InterruptibleCasts { get; set; } = Array.Empty<string>();
    public bool EnablePotionsInCombat { get; set; } = true;
    public double CombatHPThreshold { get; set; } = 0.3;
    public double CombatManaThreshold { get; set; } = 0.2;

    // Targeting
    public bool EnableAutoTargeting { get; set; } = true;
    public TargetingMode TargetingMode { get; set; } = TargetingMode.Priority;
    public double TabTargetRange { get; set; } = 40;
    public bool IgnoreDeadTargets { get; set; } = true;
    public bool IgnoreInvulnerableTargets { get; set; } = true;

    // Movement
    public bool EnablePathfinding { get; set; } = true;
    public double MaxPathDistance { get; set; } = 100;
    public bool AvoidHazards { get; set; } = true;
    public string[] HazardTemplates { get; set; } = Array.Empty<string>(); // Fire, poison, etc.
    public bool EnableJumping { get: set; } = true;
    public bool EnableFlying { get; set; } = false; // If game supports

    // Questing
    public bool EnableAutoQuesting { get; set; } = true;
    public string ActiveQuestId { get; set; } = string.Empty;
    public List<QuestStep> QuestChain { get; set; } = new();
    public bool AutoAcceptQuests { get; set; } = true;
    public bool AutoTurnInQuests { get; set; } = true;
    public string[] QuestNPCs { get; set; } = Array.Empty<string>();
    public bool SkipCinematics { get; set; } = true;
    public bool AutoNavigateToObjectives { get; set; } = true;

    // Party
    public bool EnablePartyMode { get: set; } = false;
    public PartyRole PartyRole { get; set; } = PartyRole.DPS;
    public string MasterName { get; set; } = string.Empty;
    public double FollowDistance { get: set; } = 5;
    public bool AutoFollow { get: set; } = true;
    public bool AutoAssist { get; set; } = true;
    public bool ShareLoot { get: set; } = true;

    // Anti-KS
    public bool EnableAntiKS { get; set; } = true;
    public int MaxNearbyPlayers { get; set; } = 3;
    public double KSThresholdDistance { get: set; } = 20;
    public bool AutoChangeChannel { get: set; } = true;
    public string[] SafeChannels { get: set; } = Array.Empty<string>();

    // Safety
    public double FleeHPPercent { get; set; } = 0.15;
    public double FleeManaPercent { get; set; } = 0.1;
    public string[] FleeSpells { get: set; } = Array.Empty<string>();
    public bool EnableFeignDeath { get; set; } = false;
    public double MaxDeathsPerHour { get: set; } = 3;
}

public enum TargetingMode
{
    Priority,      // Use target priority list
    Nearest,       // Nearest enemy
    Weakest,       // Lowest HP%
    HighestThreat, // Highest threat on player
    AssistMaster,  // Assist party master
    Custom         // Custom logic
}

public enum PartyRole
{
    Tank,
    Healer,
    DPS,
    Support,
    Puller
}

/// <summary>
/// Combat state for an instance.
/// </summary>
public sealed class CombatState
{
    public string AccountId { get; set; } = string.Empty;
    public IDeviceSession? Session { get; set; }
    public CombatStateType CurrentState { get; set; } = CombatStateType.Idle;
    public CombatStateType PreviousState { get; set; } = CombatStateType.Idle;
    public CombatTarget? CurrentTarget { get; set; }
    public List<CombatTarget> NearbyEnemies { get; set; } = new();
    public List<ActiveBuff> ActiveBuffs { get; set; } = new();
    public List<ActiveDebuff> ActiveDebuffs { get; set; } = new();
    public List<ActiveSkill> SkillCooldowns { get; set; } = new();
    public CombatRotation CurrentRotation { get; set; } = new();
    public QuestProgress? ActiveQuest { get; set; }
    public CombatTarget? QuestTarget { get; set; }
    public PartyInfo? PartyInfo { get; set; }
    public Point3D? LastKnownPosition { get; set; }
    public DateTimeOffset LastCombatTime { get; set; } = DateTimeOffset.MinValue;
    public int DeathCount { get; set; } = 0;
    public DateTimeOffset LastDeathTime { get; set; } = DateTimeOffset.MinValue;
    public bool IsInCombat => CurrentState == CombatStateType.InCombat;
    public bool IsCasting => CurrentState == CombatStateType.Casting;
    public bool IsChanneling => CurrentState == CombatStateType.Channeling;
    public DateTimeOffset LastSkillTime { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastTargetChange { get; set; } = DateTimeOffset.MinValue;
    public int ComboPoints { get; set; } = 0;
    public int ComboStep { get; init; } = 0;
}

public enum CombatStateType
{
    Idle,
    InCombat,
    Casting,
    Channeling,
    Moving,
    Fleeing,
    Looting,
    WaitingForRespawn,
    Dead,
    Questing,
    Following
}

public sealed record CombatTarget
{
    public string TargetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TargetType Type { get; init; }
    public double HPPercent { get; init; } = 1.0;
    public double Distance { get; init; } = 0;
    public Point3D Position { get; init; } = Point3D.Zero;
    public bool IsInLineOfSight { get; init; } = true;
    public bool IsAlive => HPPercent > 0;
    public TargetType Type { get; init; }
    public double ThreatLevel { get; init; } = 0;
    public List<string> Buffs { get; init; } = new();
    public List<string> Debuffs { get; init; } = new();
}

public sealed record ActiveBuff
{
    public string BuffId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TimeSpan RemainingDuration { get; init; }
    public int Stacks { get; init; } = 1;
    public DateTimeOffset AppliedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ActiveDebuff
{
    public string DebuffId { get: init; } = string.Empty;
    public string Name { get: init; } = string.Empty;
    public TimeSpan RemainingDuration { get; init; }
    public int Stacks { get; init; } = 1;
    public DebuffType Type { get: init; } = DebuffType.None;
    public DateTimeOffset AppliedAt { get: init; } = DateTimeOffset.UtcNow;
}

public enum DebuffType
{
    None,
    Stun,
    Root,
    Slow,
    Silence,
    Disarm,
    Poison,
    Disease,
    Curse,
    Bleed,
    Burn,
    Fear,
    Charm,
    Sleep
}

public sealed record ActiveSkill
{
    public string SkillId { get: init; } = string.Empty;
    public string Name { get: init; } = string.Empty;
    public DateTimeOffset LastUsed { get: init; } = DateTimeOffset.MinValue;
    public TimeSpan CooldownRemaining => LastUsed + Cooldown - DateTimeOffset.UtcNow;
    public TimeSpan Cooldown { get; init; } = TimeSpan.Zero;
    public bool IsReady => DateTimeOffset.UtcNow >= LastUsed + Cooldown;
    public TimeSpan Cooldown { get; init; } = TimeSpan.Zero;
}

public sealed record CombatRotation
{
    public string RotationId { get; init; } = string.Empty;
    public int CurrentStep { get; init; } = 0;
    public List<RotationStep> Steps { get; init; } = new();
    public bool IsLooping { get; init; } = true;
    public DateTimeOffset StartedAt { get: init; } = DateTimeOffset.UtcNow;
}

public sealed record QuestProgress
{
    public string QuestId { get; init; } = string.Empty;
    public string QuestName { get: init; } = string.Empty;
    public int CurrentStepIndex { get; init; } = 0;
    public QuestStep? CurrentStep => CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;
    public List<QuestStep> Steps { get: init; } = new();
    public Dictionary<string, int> ObjectiveProgress { get: init; } = new();
    public QuestStatus Status { get: init; } = QuestStatus.NotStarted;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
}

public enum QuestStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    TurnedIn
}

public sealed record PartyInfo
{
    public string PartyId { get: init; } = string.Empty;
    public string LeaderName { get: init; } = string.Empty;
    public PartyRole MyRole { get: init; } = PartyRole.DPS;
    public List<PartyMember> Members { get: init; } = new();
    public bool IsLeader => MyRole == PartyRole.Tank || MyRole == PartyRole.Healer || Members.FirstOrDefault(m => m.Name == "Self")?.IsLeader == true;
}

public sealed record PartyMember
{
    public string Name { get: init; } = string.Empty;
    public PartyRole Role { get: init; } = PartyRole.DPS;
    public double HPPercent { get; init; } = 1.0;
    public double ManaPercent { get: init; } = 1.0;
    public double Distance { get: init; } = 0;
    public bool IsLeader { get; init; } = false;
    public bool IsOnline { get: init; } = true;
}

/// <summary>
/// Combat & Quest Engine module.
/// </summary>
public sealed class CombatQuestModule : IGameModule
{
    private readonly ILogger<CombatQuestModule> _logger;
    private readonly CombatQuestOptions _options;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline _perceptionPipeline;
    private readonly IAiService _aiService;
    private readonly IInstanceManager _instanceManager;
    private readonly IMetricsRegistry _metrics;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBus _messageBus;

    private readonly ConcurrentDictionary<string, CombatState> _combatStates = new();
    private readonly ConcurrentDictionary<string, QuestProgress> _questProgress = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastTargetScan = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSkillUse = new();
    private readonly Timer? _combatTimer;
    private readonly Timer? _targetScanTimer;
    private readonly Timer? _questTimer;
    private readonly Timer? _partyTimer;
    private readonly Timer? _safetyTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Random _random = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public string Id => "combat-quest";
    public string Version => "1.0.0";

    public CombatQuestModule(
        ILogger<CombatQuestModule> logger,
        IOptions<CombatQuestOptions> options,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline perceptionPipeline,
        IAiService aiService,
        IInstanceManager instanceManager,
        IMetricsRegistry metrics,
        IAgentRegistry agentRegistry,
        IMessageBus messageBus)
    {
        _logger = logger;
        _options = options.Value;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        _aiService = aiService;
        _instanceManager = instanceManager;
        _metrics = metrics;
        _agentRegistry = agentRegistry;
        _messageBus = messageBus;

        _combatTimer = new Timer(CombatLoopAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        _targetScanTimer = new Timer(ScanTargetsAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        _questTimer = new Timer(UpdateQuestsAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        _partyTimer = new Timer(UpdatePartyAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        _safetyTimer = new Timer(SafetyCheckAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Combat & Quest Engine",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Advanced combat rotation, targeting, quest automation, and party coordination",
            SupportedResolutions = new[] { new Resolution(1920, 1080), new Resolution(1280, 720) },
            RequiredPermissions = new[] 
            { 
                "DEVICE_READ", "DEVICE_WRITE", "INPUT_WRITE", 
                "SCREENSHOT_CAPTURE", "PERCEPTION_USE", "AI_USE",
                "PARTY_MANAGE", "QUEST_MANAGE"
            },
            ConfigSchema = JsonSerializer.Serialize(new CombatQuestOptions()),
            DefaultConfig = JsonSerializer.Serialize(new CombatQuestOptions()),
            EntryAssembly = "KKBOT.Plugin.CombatQuest.dll",
            Assets = new[] 
            { 
                "templates/skill_icons/",
                "templates/target_frames/",
                "templates/quest_icons/",
                "templates/party_frames/",
                "templates/hazard_zones/"
            }
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] 
    { 
        new CombatRotationTask(), 
        new AutoTargetTask(), 
        new QuestingTask(), 
        new PartyAssistTask(),
        new AntiKSTask(),
        new FleeTask()
    };
    public IReadOnlyCollection<IStateDefinition> GetStates() => new[] 
    { 
        new IdleState(), 
        new InCombatState(), 
        new CastingState(), 
        new ChannelingState(), 
        new MovingState(), 
        new FleeingState(), 
        new LootingState(),
        new QuestingState(),
        new FollowingState(),
        new DeadState()
    };

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();

        if (_options.Skills.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_SKILLS", "No skills configured for combat"));
        }

        if (_options.Rotation.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_ROTATION", "No combat rotation configured"));
        }

        if (_options.TargetPriorities.Count == 0 && _options.EnableAutoTargeting)
        {
            errors.Add(new ModuleValidationError("NO_TARGET_PRIORITIES", "Auto-targeting enabled but no target priorities configured"));
        }

        if (_options.EnableAutoQuesting && _options.QuestChain.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_QUEST_CHAIN", "Auto-questing enabled but no quest chain configured"));
        }

        if (_options.EnablePartyMode && string.IsNullOrEmpty(_options.MasterName))
        {
            errors.Add(new ModuleValidationError("NO_MASTER", "Party mode enabled but no master name configured"));
        }

        return new ModuleValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Registers an instance for combat management.
    /// </summary>
    public void RegisterInstance(string accountId, IDeviceSession session)
    {
        _combatStates[accountId] = new CombatState
        {
            AccountId = accountId,
            Session = session,
            CurrentState = CombatStateType.Idle,
            NearbyEnemies = new List<CombatTarget>(),
            ActiveBuffs = new List<ActiveBuff>(),
            ActiveDebuffs = new List<ActiveDebuff>(),
            SkillCooldowns = _options.Skills.Select(s => new ActiveSkill
            {
                SkillId = s.SkillId,
                Name = s.Name,
                Cooldown = TimeSpan.FromSeconds(s.CooldownSeconds)
            }).ToList()
        };

        _logger.LogInformation("Registered instance {AccountId} for combat", accountId);
    }

    /// <summary>
    /// Unregisters an instance.
    /// </summary>
    public void UnregisterInstance(string accountId)
    {
        _combatStates.TryRemove(accountId, out _);
        _questProgress.TryRemove(accountId, out _);
        _lastTargetScan.TryRemove(accountId, out _);
        _lastSkillUse.TryRemove(accountId, out _);
    }

    /// <summary>
    /// Sets the active quest for an instance.
    /// </summary>
    public void SetActiveQuest(string accountId, string questId)
    {
        if (_options.QuestChain.Any(q => q.StepId == questId))
        {
            var quest = _options.QuestChain.First(q => q.StepId == questId);
            _questProgress[accountId] = new QuestProgress
            {
                QuestId = questId,
                QuestName = quest.Name,
                Steps = quest.QuestChain?.Select(q => q.StepId).ToList() ?? new List<QuestStep>(),
                Status = QuestStatus.InProgress,
                StartedAt = DateTimeOffset.UtcNow
            };
            _logger.LogInformation("Started quest {QuestName} ({QuestId}) for account {AccountId}", quest.Name, questId, accountId);
        }
    }

    /// <summary>
    /// Completes a quest step.
    /// </summary>
    public void CompleteQuestStep(string accountId, string stepId, string objectiveId = "")
    {
        if (_questProgress.TryGetValue(accountId, out var progress))
        {
            var step = progress.Steps.FirstOrDefault(s => s.StepId == stepId);
            if (step != null)
            {
                if (!string.IsNullOrEmpty(objectiveId))
                {
                    var obj = step.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
                    if (obj != null)
                    {
                        obj.CurrentCount++;
                        _logger.LogInformation("Quest objective progress: {Objective} ({Current}/{Required})", 
                            obj.Description, obj.CurrentCount, obj.RequiredCount);
                    }
                }
                else
                {
                    progress.CurrentStepIndex++;
                    _logger.LogInformation("Completed quest step {StepId} for account {AccountId}", stepId, accountId);
                    
                    if (progress.CurrentStepIndex >= step.Objectives.Count)
                    {
                        progress.Status = QuestStatus.Completed;
                        progress.CompletedAt = DateTimeOffset.UtcNow;
                        _logger.LogInformation("Quest {QuestName} completed for account {AccountId}", progress.QuestName, accountId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Main combat loop running at ~10Hz.
    /// </summary>
    private async void CombatLoopAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var kvp in _combatStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;

                if (state.Session == null) continue;

                await ProcessCombatStateAsync(accountId, state, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in combat loop");
        }
    }

    /// <summary>
    /// Processes combat state machine for an instance.
    /// </summary>
    private async Task ProcessCombatStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        var session = state.Session;
        if (session == null) return;

        // Update perception
        var frame = await session.CaptureAsync(new CaptureOptions(), ct);
        var perceptions = await _perceptionPipeline.ProcessAsync(frame, ct);

        // Update state based on perceptions
        UpdateCombatState(state, perceptions);

        // State machine
        switch (state.CurrentState)
        {
            case CombatStateType.Idle:
                await HandleIdleStateAsync(accountId, state, ct);
                break;
            case CombatStateType.InCombat:
                await HandleCombatStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Casting:
                await HandleCastingStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Channeling:
                await HandleChannelingStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Moving:
                await HandleMovingStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Fleeing:
                await HandleFleeingStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Looting:
                await HandleLootingStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Dead:
                await HandleDeadStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Questing:
                await HandleQuestingStateAsync(accountId, state, ct);
                break;
            case CombatStateType.Following:
                await HandleFollowingStateAsync(accountId, state, ct);
                break;
        }
    }

    private void UpdateCombatState(CombatState state, List<DetectionResult> perceptions)
    {
        // Update target info
        if (state.CurrentTarget != null)
        {
            var targetPerception = perceptions.FirstOrDefault(p => 
                p.DetectorName.Contains(state.CurrentTarget.Name, StringComparison.OrdinalIgnoreCase) ||
                p.DetectorName.Contains("target", StringComparison.OrdinalIgnoreCase));
            
            if (targetPerception != null)
            {
                state.CurrentTarget = state.CurrentTarget with
                {
                    HPPercent = targetPerception.Confidence,
                    Position = targetPerception.BoundingBox != null 
                        ? new Point3D(targetPerception.BoundingBox.Value.X, targetPerception.BoundingBox.Value.Y, 0)
                        : state.CurrentTarget.Position,
                    IsInLineOfSight = targetPerception.IsMatch
                };
            }
        }

        // Update nearby enemies
        state.NearbyEnemies = perceptions
            .Where(p => p.IsMatch && IsEnemyDetector(p.DetectorName))
            .Select(p => new CombatTarget
            {
                TargetId = p.DetectorId,
                Name = p.DetectorName,
                Type = InferTargetType(p.DetectorName),
                HPPercent = p.Confidence,
                Distance = CalculateDistance(p.BoundingBox),
                Position = p.BoundingBox != null 
                    ? new Point3D(p.BoundingBox.Value.X, p.BoundingBox.Value.Y, 0)
                    : Point3D.Zero,
                IsInLineOfSight = p.IsMatch
            }).ToList();

        // Update buffs/debuffs
        state.ActiveBuffs = perceptions
            .Where(p => p.IsMatch && IsBuffDetector(p.DetectorName))
            .Select(p => new ActiveBuff
            {
                BuffId = p.DetectorId,
                Name = p.DetectorName,
                RemainingDuration = TimeSpan.FromSeconds(p.Confidence * 60), // Rough estimate
                AppliedAt = DateTimeOffset.UtcNow
            }).ToList();

        state.ActiveDebuffs = perceptions
            .Where(p => p.IsMatch && IsDebuffDetector(p.DetectorName))
            .Select(p => new ActiveDebuff
            {
                DebuffId = p.DetectorId,
                Name = p.DetectorName,
                RemainingDuration = TimeSpan.FromSeconds(p.Confidence * 30),
                Type = InferDebuffType(p.DetectorName),
                AppliedAt = DateTimeOffset.UtcNow
            }).ToList();
    }

    private async Task HandleIdleStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Check for quest objectives
        if (_options.EnableAutoQuesting)
        {
            await CheckQuestObjectivesAsync(accountId, ct);
        }

        // Auto-target if enabled
        if (_options.EnableAutoTargeting && state.NearbyEnemies.Any())
        {
            var target = SelectBestTarget(state.NearbyEnemies);
            if (target != null)
            {
                await AcquireTargetAsync(accountId, target, CancellationToken.None);
            }
        }

        // Check party follow
        if (_options.EnablePartyMode && _options.AutoFollow)
        {
            await FollowMasterAsync(accountId, ct);
        }
    }

    private async Task HandleCombatStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        var session = state.Session;
        if (session == null) return;

        // Check if target is still valid
        if (state.CurrentTarget == null || !state.CurrentTarget.IsAlive || state.CurrentTarget.Distance > _options.MaxCombatDistance)
        {
            // Need new target
            var newTarget = SelectBestTarget(state.NearbyEnemies);
            if (newTarget != null)
            {
                await AcquireTargetAsync(accountId, newTarget, ct);
            }
            else
            {
                // No valid targets, exit combat
                state.CurrentState = CombatStateType.Idle;
                state.CurrentTarget = null;
                return;
            }
        }

        // Execute combat rotation
        await ExecuteRotationAsync(accountId, state, ct);

        // Check for interrupts
        if (_options.EnableInterrupts)
        {
            await CheckInterruptsAsync(accountId, state, ct);
        }

        // Check flee conditions
        await CheckFleeConditionsAsync(accountId, state, ct);

        // Check party assist
        if (_options.EnablePartyMode && _options.AutoAssist)
        {
            await AssistPartyMembersAsync(accountId, ct);
        }
    }

    private async Task ExecuteRotationAsync(string accountId, CombatState state, CancellationToken ct)
    {
        var rotation = GetActiveRotation();
        if (rotation == null || rotation.Steps.Count == 0) return;

        var step = rotation.Steps[rotation.CurrentStep];
        var skill = _options.Skills.FirstOrDefault(s => s.SkillId == step.SkillId);
        if (skill == null) return;

        // Check conditions
        if (!await CheckSkillConditionsAsync(skill, step, ct)) return;

        // Check global cooldown
        var timeSinceLastSkill = DateTimeOffset.UtcNow - _lastSkillUse.GetValueOrDefault(step.SkillId, DateTimeOffset.MinValue);
        if (timeSinceLastSkill.TotalMilliseconds < _options.GlobalCooldownMs) return;

        // Check skill cooldown
        var skillCooldown = state.SkillCooldowns.FirstOrDefault(s => s.SkillId == skill.SkillId);
        if (skillCooldown != null && !skillCooldown.IsReady) return;

        // Check conditions
        if (!await CheckConditionsAsync(step.Conditions, ct)) return;

        // Execute skill
        await CastSkillAsync(accountId, skill, ct);

        // Update rotation
        rotation.CurrentStep = (rotation.CurrentStep + 1) % rotation.Steps.Count;
    }

    private CombatRotation GetActiveRotation()
    {
        // In real implementation, would select rotation based on situation
        return new CombatRotation
        {
            RotationId = "default",
            Steps = _options.Rotation
        };
    }

    private async Task<bool> CheckSkillConditionsAsync(SkillConfig skill, RotationStep step, CancellationToken ct)
    {
        // Check mana
        if (skill.ManaCost > 0)
        {
            // Would check current mana
        }

        // Check range
        if (skill.Range > 0)
        {
            // Would check distance to target
        }

        // Check step conditions
        foreach (var condition in step.Conditions)
        {
            if (!await CheckConditionAsync(condition)) return false;
        }

        return true;
    }

    private async Task CastSkillAsync(string accountId, SkillConfig skill, CancellationToken ct)
    {
        var state = _combatStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        _logger.LogDebug("Casting skill {SkillName} ({SkillId}) for account {AccountId}", skill.Name, skill.SkillId, accountId);

        // Send input
        await state.Session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = skill.Hotkey
        }, ct);

        // Update cooldown tracking
        var skillCooldown = state.SkillCooldowns.FirstOrDefault(s => s.SkillId == skill.SkillId);
        if (skillCooldown != null)
        {
            var index = state.SkillCooldowns.IndexOf(skillCooldown);
            state.SkillCooldowns[index] = skillCooldown with { LastUsed = DateTimeOffset.UtcNow };
        }

        _lastSkillUse[skill.SkillId] = DateTimeOffset.UtcNow;
        _metrics.IncrementHttpRequests(); // Reuse metric

        // Wait for cast time
        if (skill.CastTimeSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(skill.CastTimeSeconds), ct);
        }
    }

    private async Task CheckInterruptsAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Check if target is casting interruptible spell
        // Would use perception to detect cast bars
    }

    private async Task CheckFleeConditionsAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Check HP threshold
        var frame = await state.Session!.CaptureAsync(new CaptureOptions(), ct);
        var results = await _perceptionPipeline.ProcessAsync(frame, ct);

        var hpResult = results.FirstOrDefault(r => r.DetectorName.Contains("hp") || r.DetectorName.Contains("health"));
        if (hpResult != null && hpResult.IsMatch && hpResult.Confidence < _options.FleeHPPercent)
        {
            await FleeAsync(accountId, ct);
        }

        // Check mana
        var manaResult = results.FirstOrDefault(r => r.DetectorName.Contains("mana") || r.DetectorName.Contains("mp"));
        if (manaResult != null && manaResult.IsMatch && manaResult.Confidence < _options.FleeManaPercent)
        {
            await FleeAsync(accountId, ct);
        }

        // Check death count
        var stateObj = _combatStates.GetValueOrDefault(accountId);
        if (stateObj != null && stateObj.DeathCount >= _options.MaxDeathsPerHour &&
            DateTimeOffset.UtcNow - stateObj.LastDeathTime < TimeSpan.FromHours(1))
        {
            _logger.LogWarning("Max deaths per hour reached for {AccountId}, stopping", accountId);
            await StopAsync(accountId, ct);
        }
    }

    private async Task FleeAsync(string accountId, CancellationToken ct)
    {
        var state = _combatStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        _logger.LogWarning("Account {AccountId} fleeing!", accountId);
        
        var stateObj = _combatStates.GetValueOrDefault(accountId);
        if (stateObj != null)
        {
            stateObj.CurrentState = CombatStateType.Fleeing;
        }

        // Use flee spell if available
        if (_options.FleeSpells.Length > 0)
        {
            foreach (var spell in _options.FleeSpells)
            {
                var skill = _options.Skills.FirstOrDefault(s => s.Name == spell);
                if (skill != null)
                {
                    await CastSkillAsync(accountId, skill, CancellationToken.None);
                    break;
                }
            }
        }

        // Move away from target
        if (stateObj?.CurrentTarget != null)
        {
            await MoveAwayFromTargetAsync(accountId, ct);
        }
    }

    private async Task MoveAwayFromTargetAsync(string accountId, CancellationToken ct)
    {
        // Move away from target
        await Task.Delay(1000, ct);
    }

    private async Task FollowMasterAsync(string accountId, CancellationToken ct)
    {
        // Follow party master
    }

    private async Task AssistPartyMembersAsync(string accountId, CancellationToken ct)
    {
        // Assist party members
    }

    private async Task CheckQuestObjectivesAsync(string accountId, CancellationToken ct)
    {
        // Check and update quest objectives
    }

    private async Task UpdateQuestsAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var kvp in _questProgress)
        {
            var accountId = kvp.Key;
            var progress = kvp.Value;

            if (progress.Status == QuestStatus.InProgress)
            {
                await CheckQuestProgressAsync(accountId, progress, CancellationToken.None);
            }
        }
    }

    private async Task CheckQuestProgressAsync(string accountId, QuestProgress progress, CancellationToken ct)
    {
        var step = progress.CurrentStep;
        if (step == null) return;

        // Check objectives
        foreach (var objective in step.Objectives)
        {
            var current = progress.ObjectiveProgress.GetValueOrDefault(objective.ObjectiveId, 0);
            if (current >= objective.RequiredCount)
            {
                // Objective complete
            }
        }

        // Check if step complete
        if (step.Objectives.All(o => progress.ObjectiveProgress.GetValueOrDefault(o.ObjectiveId, 0) >= o.RequiredCount))
        {
            // Step complete, move to next
            // Handled in CompleteQuestStep
        }
    }

    private async void UpdatePartyAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;
        // Update party info
    }

    private async void SafetyCheckAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var kvp in _combatStates)
        {
            var accountId = kvp.Key;
            var state = kvp.Value;

            // Check death count
            if (state.DeathCount >= _options.MaxDeathsPerHour &&
                DateTimeOffset.UtcNow - state.LastDeathTime < TimeSpan.FromHours(1))
            {
                await StopAsync(accountId, CancellationToken.None);
            }
        }
    }

    private async Task StopAsync(string accountId, CancellationToken ct)
    {
        if (_combatStates.TryGetValue(accountId, out var state))
        {
            state.CurrentState = CombatStateType.Idle;
            state.CurrentTarget = null;
            _logger.LogInformation("Stopped combat for account {AccountId}", accountId);
        }
    }

    // Helper methods
    private CombatTarget? SelectBestTarget(List<CombatTarget> enemies)
    {
        if (!enemies.Any()) return null;

        return _options.TargetingMode switch
        {
            TargetingMode.Priority => enemies
                .OrderByDescending(e => GetTargetPriority(e))
                .FirstOrDefault(),
            TargetingMode.Nearest => enemies
                .OrderBy(e => e.Distance)
                .FirstOrDefault(),
            TargetingMode.Weakest => enemies
                .OrderBy(e => e.HPPercent)
                .FirstOrDefault(),
            TargetingMode.HighestThreat => enemies
                .OrderByDescending(e => e.ThreatLevel)
                .FirstOrDefault(),
            TargetingMode.AssistMaster => {
                // Would assist master's target
                return enemies.FirstOrDefault();
            },
            _ => enemies.FirstOrDefault()
        };
    }

    private int GetTargetPriority(CombatTarget target)
    {
        var priority = _options.TargetPriorities
            .FirstOrDefault(p => 
                (p.Type == TargetType.Any || p.Type == target.Type) &&
                target.Name.Contains(p.Name, StringComparison.OrdinalIgnoreCase) ||
                (p.Tags?.Any(t => target.Name.Contains(t, StringComparison.OrdinalIgnoreCase)) ?? false));

        return priority?.Priority ?? 0;
    }

    private async Task AcquireTargetAsync(string accountId, CombatTarget target, CancellationToken ct)
    {
        var state = _combatStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        state.CurrentTarget = target;
        state.CurrentState = CombatStateType.InCombat;
        state.LastTargetChange = DateTimeOffset.UtcNow;

        _logger.LogDebug("Account {AccountId} acquired target {TargetName}", accountId, target.Name);

        // Send target acquisition input (e.g., Tab or click)
        await state.Session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = "Tab" // Tab targeting
        }, CancellationToken.None);
    }

    private async Task MoveAwayFromTargetAsync(string accountId, CancellationToken ct)
    {
        // Move away from current target
    }

    private async Task HandleCastingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Wait for cast to complete
        await Task.Delay(100, ct);
        if (state.CurrentState == CombatStateType.Casting)
        {
            state.CurrentState = CombatStateType.InCombat;
        }
    }

    private async Task HandleChannelingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Handle channeled spells
    }

    private async Task HandleMovingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Handle movement state
    }

    private async Task HandleFleeingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Handle fleeing state
        await Task.Delay(100, ct);
        if (state.CurrentState == CombatStateType.Fleeing)
        {
            state.CurrentState = CombatStateType.Idle;
        }
    }

    private async Task HandleLootingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Handle looting
    }

    private async Task HandleDeadStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        var stateObj = _combatStates.GetValueOrDefault(accountId);
        if (stateObj != null)
        {
            stateObj.DeathCount++;
            stateObj.LastDeathTime = DateTimeOffset.UtcNow;
            stateObj.CurrentState = CombatStateType.Idle;
            stateObj.CurrentTarget = null;
        }

        // Release spirit, resurrect, etc.
        _logger.LogInformation("Account {AccountId} died (death #{Count})", accountId, stateObj.DeathCount);
    }

    private async Task HandleQuestingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Handle questing state
    }

    private async Task HandleFollowingStateAsync(string accountId, CombatState state, CancellationToken ct)
    {
        // Handle following state
    }

    private bool IsEnemyDetector(string name)
    {
        var lower = name.ToLower();
        return lower.Contains("enemy") || lower.Contains("mob") || lower.Contains("mob_") || 
               lower.Contains("boss") || lower.Contains("elite");
    }

    private bool IsBuffDetector(string name)
    {
        var lower = name.ToLower();
        return lower.Contains("buff") || lower.Contains("aura") || lower.Contains("blessing");
    }

    private bool IsDebuffDetector(string name)
    {
        var lower = name.ToLower();
        return lower.Contains("debuff") || lower.Contains("debuff_") || 
               lower.Contains("poison") || lower.Contains("curse") || 
               lower.Contains("disease") || lower.Contains("slow") ||
               lower.Contains("stun") || lower.Contains("root");
    }

    private TargetType InferTargetType(string name)
    {
        var lower = name.ToLower();
        if (lower.Contains("boss")) return TargetType.Boss;
        if (lower.Contains("elite")) return TargetType.Elite;
        if (lower.Contains("healer")) return TargetType.Healer;
        if (lower.Contains("caster") || lower.Contains("mage") || lower.Contains("wizard")) return TargetType.Caster;
        return TargetType.Normal;
    }

    private DebuffType InferDebuffType(string name)
    {
        var lower = name.ToLower();
        if (lower.Contains("stun")) return DebuffType.Stun;
        if (lower.Contains("root") || lower.Contains("immobilize")) return DebuffType.Root;
        if (lower.Contains("slow")) return DebuffType.Slow;
        if (lower.Contains("silence")) return DebuffType.Silence;
        if (lower.Contains("disarm")) return DebuffType.Disarm;
        if (lower.Contains("poison")) return DebuffType.Poison;
        if (lower.Contains("disease")) return DebuffType.Disease;
        if (lower.Contains("curse")) return DebuffType.Curse;
        if (lower.Contains("bleed")) return DebuffType.Bleed;
        if (lower.Contains("burn") || lower.Contains("fire")) return DebuffType.Burn;
        if (lower.Contains("fear")) return DebuffType.Fear;
        if (lower.Contains("charm")) return DebuffType.Charm;
        if (lower.Contains("sleep")) return DebuffType.Sleep;
        return DebuffType.None;
    }

    private double CalculateDistance(Rectangle? box)
    {
        if (!box.HasValue) return double.MaxValue;
        // Simplified distance calculation
        return Math.Sqrt(box.Value.X * box.Value.X + box.Value.Y * box.Value.Y);
    }

    private async Task<bool> CheckConditionsAsync(RotationCondition[] conditions, CancellationToken ct)
    {
        foreach (var condition in conditions)
        {
            if (!await CheckConditionAsync(condition)) return false;
        }
        return true;
    }

    private async Task<bool> CheckConditionAsync(RotationCondition condition)
    {
        // In real implementation, would check actual game state
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _combatTimer?.Dispose();
        _targetScanTimer?.Dispose();
        _questTimer?.Dispose();
        _partyTimer?.Dispose();
        _safetyTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}

// ==================== TASKS & STATES ====================

public sealed class CombatRotationTask : IGameTaskDefinition
{
    public string Id => "combat-rotation";
    public string Name => "Combat Rotation";
    public string? Description => "Executes combat skill rotation";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(1, 1000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new InCombatState();
    }
}

public sealed class AutoTargetTask : IGameTaskDefinition
{
    public string Id => "auto-target";
    public string Name => "Auto Target";
    public string? Description => "Automatically acquires and manages targets";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);
    public RetryPolicy DefaultRetryPolicy => new(2, 1000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new InCombatState();
    }
}

public sealed class QuestingTask : IGameTaskDefinition
{
    public string Id => "questing";
    public string Name => "Auto Questing";
    public string? Description => "Automatically completes quest objectives";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(2, 30000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new QuestingState();
    }
}

public sealed class PartyAssistTask : IGameTaskDefinition
{
    public string Id => "party-assist";
    public string Name => "Party Assist";
    public string? Description => "Assists party members in combat";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);
    public RetryPolicy DefaultRetryPolicy => new(1, 2000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new InCombatState();
    }
}

public sealed class AntiKSTask : IGameTaskDefinition
{
    public string Id => "anti-ks";
    public string Name => "Anti-KS";
    public string? Description => "Prevents kill-stealing and changes channel if needed";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(1, 10000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new InCombatState();
    }
}

public sealed class FleeTask : IGameTaskDefinition
{
    public string Id => "flee";
    public string Name => "Flee";
    public string? Description => "Emergency flee when HP/mana critical";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);
    public RetryPolicy DefaultRetryPolicy => new(1, 5000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new FleeingState();
    }
}

public sealed class IdleState : IStateDefinition
{
    public string Id => "idle";
    public string Name => "Idle";
    public string Category => "combat";
    public Func<PerceptionContext?, bool> EntryCondition => _ => true;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "in_combat", "questing", "following" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class InCombatState : IStateDefinition
{
    public string Id => "in_combat";
    public string Name => "In Combat";
    public string Category => "combat";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("enemy") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "casting", "channeling", "fleeing", "looting", "dead" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(10);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(true, true);
}

public sealed class CastingState : IStateDefinition
{
    public string Id => "casting";
    public string Name => "Casting";
    public string Category => "combat";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("casting") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "in_combat", "channeling" };
    public TimeSpan Timeout => TimeSpan.FromSeconds(5);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class ChannelingState : IStateDefinition
{
    public string Id => "channeling";
    public string Name => "Channeling";
    public string Category => "combat";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("channeling") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "in_combat", "interrupted" };
    public TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class MovingState : IStateDefinition
{
    public string Id => "moving";
    public string Name => "Moving";
    public string Category => "movement";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("moving") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat", "questing" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class FleeingState : IStateDefinition
{
    public string Id => "fleeing";
    public string Name => "Fleeing";
    public string Category => "survival";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => (d.DetectorName.Contains("hp") || d.DetectorName.Contains("health")) && d.Confidence < 0.2) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "dead" };
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public RetryPolicy RetryPolicy => new(1, 5000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class LootingState : IStateDefinition
{
    public string Id => "looting";
    public string Name => "Looting";
    public string Category => "logistics";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("loot") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat" };
    public TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public RetryPolicy RetryPolicy => new(2, 2000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class QuestingState : IStateDefinition
{
    public string Id => "questing";
    public string Name => "Questing";
    public string Category => "quest";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("quest") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat", "moving" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(30);
    public RetryPolicy RetryPolicy => new(1, 30000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class FollowingState : IStateDefinition
{
    public string Id => "following";
    public string Name => "Following";
    public string Category => "party";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("party_leader") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat", "questing" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(30);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class DeadState : IStateDefinition
{
    public string Id => "dead";
    public string Name => "Dead";
    public string Category => "death";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("dead") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle" };
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

/// <summary>
/// DI Extensions for Combat Quest module.
/// </summary>
public static class CombatQuestExtensions
{
    public static IServiceCollection AddKkbotCombatQuest(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CombatQuestOptions>(configuration.GetSection("CombatQuest"));

        services.AddSingleton<CombatQuestModule>();
        services.AddScoped<ICombatQuestService, CombatQuestService>();

        return services;
    }
}

/// <summary>
/// Service interface for combat quest operations.
/// </summary>
public interface ICombatQuestService
{
    void RegisterInstance(string accountId, IDeviceSession session);
    void UnregisterInstance(string accountId);
    void SetActiveQuest(string accountId, string questId);
    void CompleteQuestStep(string accountId, string stepId, string objectiveId = "");
    Task<bool> SetTargetAsync(string accountId, string targetId, CancellationToken ct = default);
    Task<bool> ExecuteSkillAsync(string accountId, string skillId, CancellationToken ct = default);
    Task<bool> FleeAsync(string accountId, CancellationToken ct = default);
    Task<bool> FollowTargetAsync(string accountId, string targetName, CancellationToken ct = default);
    Task<bool> StartQuestAsync(string accountId, string questId, CancellationToken ct = default);
    Task<bool> CompleteQuestAsync(string accountId, string questId, CancellationToken ct = default);
    CombatState? GetCombatState(string accountId);
    QuestProgress? GetQuestProgress(string accountId);
}

/// <summary>
/// Service implementation for combat quest operations.
/// </summary>
public sealed class CombatQuestService : ICombatQuestService
{
    private readonly CombatQuestModule _module;

    public CombatQuestService(CombatQuestModule module)
    {
        _module = module;
    }

    public void RegisterInstance(string accountId, IDeviceSession session)
        => _module.RegisterInstance(accountId, session);

    public void UnregisterInstance(string accountId)
        => _module.UnregisterInstance(accountId);

    public void SetActiveQuest(string accountId, string questId)
        => _module.SetActiveQuest(accountId, questId);

    public void CompleteQuestStep(string accountId, string stepId, string objectiveId = "")
        => _module.CompleteQuestStep(accountId, stepId, objectiveId);

    public async Task<bool> SetTargetAsync(string accountId, string targetId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> ExecuteSkillAsync(string accountId, string skillId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> FleeAsync(string accountId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> FollowTargetAsync(string accountId, string targetName, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> StartQuestAsync(string accountId, string questId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> CompleteQuestAsync(string accountId, string questId, CancellationToken ct = default)
    {
        return true;
    }

    public CombatState? GetCombatState(string accountId)
    {
        return null;
    }

    public QuestProgress? GetQuestProgress(string accountId)
    {
        return null;
    }
}