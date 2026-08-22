// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.AI.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Plugin.SampleGame;

/// <summary>
/// Sample game plugin demonstrating a complete implementation for a hypothetical RPG game.
/// </summary>
public sealed class SampleGameModule : IGameModule
{
    public string Id => "sample-rpg-game";
    public string Version => "1.0.0";

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Sample RPG Game",
            Version = Version,
            Author = "KKBOT Team",
            Description = "A complete sample plugin for an RPG-style game demonstrating detectors, tasks, states, and AI integration.",
            SupportedResolutions =
            [
                new Resolution(1920, 1080),
                new Resolution(1280, 720),
                new Resolution(2560, 1440)
            ],
            RequiredPermissions =
            [
                "DEVICE_READ",
                "INPUT_WRITE",
                "SCREENSHOT_CAPTURE",
                "OCR_ACCESS"
            ],
            ConfigSchema = """
                {
                  "type": "object",
                  "properties": {
                    "autoBattle": { "type": "boolean", "default": true },
                    "lootThreshold": { "type": "integer", "minimum": 1, "maximum": 5, "default": 3 },
                    "hpPotionThreshold": { "type": "integer", "minimum": 10, "maximum": 90, "default": 30 },
                    "mpPotionThreshold": { "type": "integer", "minimum": 10, "maximum": 90, "default": 20 }
                  },
                  "required": ["autoBattle", "lootThreshold", "hpPotionThreshold", "mpPotionThreshold"]
                }
                """,
            DefaultConfig = """
                {
                  "autoBattle": true,
                  "lootThreshold": 3,
                  "hpPotionThreshold": 30,
                  "mpPotionThreshold": 20
                }
                """,
            EntryAssembly = "KKBOT.Plugin.SampleGame.dll",
            Assets =
            [
                "assets/templates/hp_bar.png",
                "assets/templates/mp_bar.png",
                "assets/templates/battle_button.png",
                "assets/templates/loot_window.png",
                "assets/templates/skill_1.png",
                "assets/templates/skill_2.png",
                "assets/templates/skill_3.png",
                "assets/ocr/number_font.png"
            ]
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors()
    {
        return
        [
            new HpBarDetector(),
            new MpBarDetector(),
            new BattleStateDetector(),
            new LootWindowDetector(),
            new SkillCooldownDetector()
        ];
    }

    public IReadOnlyCollection<IGameTaskDefinition> GetTasks()
    {
        return
        [
            new AutoBattleTask(),
            new LootCollectionTask(),
            new HealTask(),
            new ManaRestoreTask(),
            new QuestCompletionTask()
        ];
    }

    public IReadOnlyCollection<IStateDefinition> GetStates()
    {
        return
        [
            new IdleState(),
            new InBattleState(),
            new LootingState(),
            new HealingState(),
            new ManaRestoringState(),
            new QuestTurnInState(),
            new ErrorState()
        ];
    }

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken cancellationToken)
    {
        var errors = new List<ModuleValidationError>();

        // Validate required assets exist
        var manifest = GetManifest();
        foreach (var asset in manifest.Assets)
        {
            // In real implementation, check if asset file exists
            // if (!File.Exists(Path.Combine(assetPath, asset)))
            //     errors.Add(new ModuleValidationError("MISSING_ASSET", $"Required asset not found: {asset}"));
        }

        // Validate config schema
        if (string.IsNullOrWhiteSpace(manifest.ConfigSchema))
            errors.Add(new ModuleValidationError("INVALID_CONFIG", "Config schema is required"));

        await Task.CompletedTask;
        return new ModuleValidationResult(errors.Count == 0, errors);
    }
}

// ===== Detectors =====

public sealed class HpBarDetector : IGameDetector
{
    public string Id => "hp_bar";
    public string Name => "HP Bar Detector";
    public DetectorType Type => DetectorType.TemplateMatch;
    public double RequiredConfidence => 0.85;

    public Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        // Implementation would use OpenCV to find HP bar template
        // Return confidence based on match quality
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.92,
            IsMatch = true,
            DetectedValue = "HP: 75%",
            BoundingBox = new Rectangle(100, 50, 200, 20),
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
}

public sealed class MpBarDetector : IGameDetector
{
    public string Id => "mp_bar";
    public string Name => "MP Bar Detector";
    public DetectorType Type => DetectorType.TemplateMatch;
    public double RequiredConfidence => 0.85;

    public Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.88,
            IsMatch = true,
            DetectedValue = "MP: 45%",
            BoundingBox = new Rectangle(100, 80, 200, 20),
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
}

public sealed class BattleStateDetector : IGameDetector
{
    public string Id => "battle_state";
    public string Name => "Battle State Detector";
    public DetectorType Type => DetectorType.TemplateMatch;
    public double RequiredConfidence => 0.80;

    public Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.95,
            IsMatch = true,
            DetectedValue = "IN_BATTLE",
            BoundingBox = new Rectangle(0, 0, 1920, 1080),
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
}

public sealed class LootWindowDetector : IGameDetector
{
    public string Id => "loot_window";
    public string Name => "Loot Window Detector";
    public DetectorType Type => DetectorType.TemplateMatch;
    public double RequiredConfidence => 0.80;

    public Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.90,
            IsMatch = true,
            DetectedValue = "LOOT_AVAILABLE",
            BoundingBox = new Rectangle(600, 300, 400, 300),
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
}

public sealed class SkillCooldownDetector : IGameDetector
{
    public string Id => "skill_cooldown";
    public string Name => "Skill Cooldown Detector";
    public DetectorType Type => DetectorType.ColorAnalysis;
    public double RequiredConfidence => 0.75;

    public Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.82,
            IsMatch = true,
            DetectedValue = "SKILL_1: READY, SKILL_2: 3.2s, SKILL_3: READY",
            BoundingBox = new Rectangle(1600, 900, 300, 100),
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
}

// ===== Tasks =====

public sealed class AutoBattleTask : IGameTaskDefinition
{
    public string Id => "auto_battle";
    public string Name => "Auto Battle";
    public string? Description => "Automatically uses skills to defeat enemies";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(3, 1000, true);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        // Logic: Check battle state -> Use available skills -> Wait for cooldowns
        await Task.Delay(100, ct); // Simulate work
        return new InBattleState(); // Stay in battle state
    }
}

public sealed class LootCollectionTask : IGameTaskDefinition
{
    public string Id => "collect_loot";
    public string Name => "Collect Loot";
    public string? Description => "Opens loot window and collects items above threshold";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(2, 500, false);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        await Task.Delay(50, ct);
        return new IdleState(); // Return to idle after looting
    }
}

public sealed class HealTask : IGameTaskDefinition
{
    public string Id => "heal";
    public string Name => "Use HP Potion";
    public string? Description => "Uses HP potion when health drops below threshold";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);
    public RetryPolicy DefaultRetryPolicy => new(1, 200, false);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        await Task.Delay(30, ct);
        return new InBattleState(); // Return to battle after healing
    }
}

public sealed class ManaRestoreTask : IGameTaskDefinition
{
    public string Id => "restore_mana";
    public string Name => "Use MP Potion";
    public string? Description => "Uses MP potion when mana drops below threshold";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);
    public RetryPolicy DefaultRetryPolicy => new(1, 200, false);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        await Task.Delay(30, ct);
        return new InBattleState();
    }
}

public sealed class QuestCompletionTask : IGameTaskDefinition
{
    public string Id => "complete_quest";
    public string Name => "Complete Quest";
    public string? Description => "Turns in completed quests and accepts new ones";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);
    public RetryPolicy DefaultRetryPolicy => new(2, 2000, true);
    public bool RequiresApproval => true; // User should approve quest actions

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new IdleState();
    }
}

// ===== States =====

public sealed class IdleState : IStateDefinition
{
    public string Id => "idle";
    public string Name => "Idle";
    public string Category => "navigation";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => true; // Default state
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => ["in_battle", "healing", "mana_restoring", "quest_turnin"];
    public TimeSpan Timeout => TimeSpan.FromMinutes(10);
    public RetryPolicy RetryPolicy => new();
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class InBattleState : IStateDefinition
{
    public string Id => "in_battle";
    public string Name => "In Battle";
    public string Category => "combat";
    public Func<PerceptionContext?, bool> EntryCondition => ctx =>
        ctx?.DetectorResults.Any(d => d.DetectorId == "battle_state" && d.IsMatch) == true;
    public IStateAction? Action => new BattleAction();
    public IReadOnlyList<string> NextStateIds => ["idle", "looting", "healing", "mana_restoring", "error"];
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(3, 1000, true);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class LootingState : IStateDefinition
{
    public string Id => "looting";
    public string Name => "Looting";
    public string Category => "loot";
    public Func<PerceptionContext?, bool> EntryCondition => ctx =>
        ctx?.DetectorResults.Any(d => d.DetectorId == "loot_window" && d.IsMatch) == true;
    public IStateAction? Action => new LootAction();
    public IReadOnlyList<string> NextStateIds => ["idle", "in_battle"];
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public RetryPolicy RetryPolicy => new(2, 500, false);
    public EvidencePolicy EvidencePolicy => new(true, true);
}

public sealed class HealingState : IStateDefinition
{
    public string Id => "healing";
    public string Name => "Healing";
    public string Category => "support";
    public Func<PerceptionContext?, bool> EntryCondition => ctx =>
    {
        var hpResult = ctx?.DetectorResults.FirstOrDefault(d => d.DetectorId == "hp_bar");
        if (hpResult?.IsMatch == true && int.TryParse(hpResult.DetectedValue.Replace("HP: ", "").Replace("%", ""), out var hp))
            return hp < 30; // Configurable threshold
        return false;
    };
    public IStateAction? Action => new HealAction();
    public IReadOnlyList<string> NextStateIds => ["in_battle", "idle", "error"];
    public TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public RetryPolicy RetryPolicy => new(1, 200, false);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class ManaRestoringState : IStateDefinition
{
    public string Id => "mana_restoring";
    public string Name => "Restoring Mana";
    public string Category => "support";
    public Func<PerceptionContext?, bool> EntryCondition => ctx =>
    {
        var mpResult = ctx?.DetectorResults.FirstOrDefault(d => d.DetectorId == "mp_bar");
        if (mpResult?.IsMatch == true && int.TryParse(mpResult.DetectedValue.Replace("MP: ", "").Replace("%", ""), out var mp))
            return mp < 20;
        return false;
    };
    public IStateAction? Action => new ManaRestoreAction();
    public IReadOnlyList<string> NextStateIds => ["in_battle", "idle", "error"];
    public TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public RetryPolicy RetryPolicy => new(1, 200, false);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class QuestTurnInState : IStateDefinition
{
    public string Id => "quest_turnin";
    public string Name => "Quest Turn In";
    public string Category => "quest";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Triggered by task
    public IStateAction? Action => new QuestAction();
    public IReadOnlyList<string> NextStateIds => ["idle"];
    public TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public RetryPolicy RetryPolicy => new(2, 2000, true);
    public EvidencePolicy EvidencePolicy => new(true, true);
}

public sealed class ErrorState : IStateDefinition
{
    public string Id => "error";
    public string Name => "Error";
    public string Category => "system";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Entered on exception
    public IStateAction? Action => new ErrorRecoveryAction();
    public IReadOnlyList<string> NextStateIds => ["idle"];
    public TimeSpan Timeout => TimeSpan.FromMinutes(1);
    public RetryPolicy RetryPolicy => new(3, 5000, true);
    public EvidencePolicy EvidencePolicy => new(true, true);
}

// ===== State Actions =====

public sealed class BattleAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Use skills based on cooldown detector
        await Task.Delay(100, ct);
    }
}

public sealed class LootAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Click loot items
        await Task.Delay(50, ct);
    }
}

public sealed class HealAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Use HP potion
        await Task.Delay(30, ct);
    }
}

public sealed class ManaRestoreAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Use MP potion
        await Task.Delay(30, ct);
    }
}

public sealed class QuestAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Talk to NPC, turn in quest
        await Task.Delay(100, ct);
    }
}

public sealed class ErrorRecoveryAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Attempt recovery: restart game, reconnect device, etc.
        await Task.Delay(500, ct);
    }
}