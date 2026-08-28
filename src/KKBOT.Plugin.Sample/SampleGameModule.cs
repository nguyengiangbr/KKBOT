using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;

namespace KKBOT.Plugin.Sample;

/// <summary>
/// A minimal sample game module demonstrating the required contract.
/// </summary>
public sealed class SampleGameModule : IGameModule
{
    public string Id => "sample-game";
    public string Version => "1.0.0";

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Sample Game Module",
            Version = Version,
            Author = "KKBOT",
            Description = "A minimal sample module for demonstration purposes.",
            SupportedResolutions = new[] { new Resolution(1280, 720) },
            RequiredPermissions = new[] { "DEVICE_READ", "INPUT_WRITE" },
            ConfigSchema = "{ \"type\": \"object\" }",
            DefaultConfig = "{}",
            EntryAssembly = "KKBOT.Plugin.Sample.dll",
            Assets = new[] { "assets/sample.png" }
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors()
    {
        // No detectors for sample, returns empty collection.
        return Array.Empty<IGameDetector>();
    }

    public IReadOnlyCollection<IGameTaskDefinition> GetTasks()
    {
        // Provide a simple placeholder task.
        return new[] { new SampleTaskDefinition() };
    }

    public IReadOnlyCollection<IStateDefinition> GetStates()
    {
        // Simple two-state FSM: idle -> running -> idle.
        var idleState = new SimpleStateDefinition("idle", "Idle", "idle", ctx => true, null, new[] { "running" }, TimeSpan.FromSeconds(30), new(), new());
        var runningState = new SimpleStateDefinition("running", "Running", "action", ctx => true, null, new[] { "idle" }, TimeSpan.FromSeconds(180), new(), new());
        return new[] { idleState, runningState };
    }

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken cancellationToken)
    {
        // Sample validation always succeeds.
        await Task.CompletedTask;
        return new ModuleValidationResult(true, Array.Empty<ModuleValidationError>());
    }
}

/// <summary>
/// Simple task definition returning the idle state.
/// </summary>
public sealed class SampleTaskDefinition : IGameTaskDefinition
{
    public string Id => "sample-task";
    public string Name => "Sample Task";
    public string? Description => "A no-op sample task.";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(60);
    public RetryPolicy DefaultRetryPolicy => new();
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken cancellationToken)
    {
        // Simply return the idle state.
        await Task.CompletedTask;
        return new SimpleStateDefinition("idle", "Idle", "idle", _ => true, null, Array.Empty<string>(), TimeSpan.Zero, new(), new());
    }
}

/// <summary>
/// Minimal implementation of IStateDefinition used for sample.
/// </summary>
public sealed class SimpleStateDefinition : IStateDefinition
{
    public SimpleStateDefinition(
        string id,
        string name,
        string category,
        Func<PerceptionContext?, bool> entryCondition,
        IStateAction? action,
        IEnumerable<string> nextStateIds,
        TimeSpan timeout,
        RetryPolicy retryPolicy,
        EvidencePolicy evidencePolicy)
    {
        Id = id;
        Name = name;
        Category = category;
        EntryCondition = entryCondition;
        Action = action;
        NextStateIds = (nextStateIds as IReadOnlyList<string>) ?? nextStateIds.ToList();
        NextStates = nextStateIds.Select(id => (IStateDefinition)new SimpleStateDefinitionPlaceholder(id)).ToArray();
        Timeout = timeout;
        RetryPolicy = retryPolicy;
        EvidencePolicy = evidencePolicy;
    }

    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public Func<PerceptionContext?, bool> EntryCondition { get; }
    public IStateAction? Action { get; }
    public IReadOnlyList<string> NextStateIds { get; }
    public IReadOnlyList<IStateDefinition> NextStates { get; }
    public TimeSpan Timeout { get; }
    public RetryPolicy RetryPolicy { get; }
    public EvidencePolicy EvidencePolicy { get; }
}

/// <summary>
/// Placeholder for state definitions referenced only by ID.
/// </summary>
public sealed class SimpleStateDefinitionPlaceholder : IStateDefinition
{
    public SimpleStateDefinitionPlaceholder(string id) => Id = id;
    public string Id { get; }
    public string Name => Id;
    public string Category => "placeholder";
    public Func<PerceptionContext?, bool> EntryCondition => _ => false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => Array.Empty<string>();
    public IReadOnlyList<IStateDefinition> NextStates => Array.Empty<IStateDefinition>();
    public TimeSpan Timeout => TimeSpan.Zero;
    public RetryPolicy RetryPolicy => new();
    public EvidencePolicy EvidencePolicy => new();
}