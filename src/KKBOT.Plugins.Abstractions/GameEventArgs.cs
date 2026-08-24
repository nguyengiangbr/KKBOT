// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Base class for all game event arguments.
/// </summary>
public abstract class GameEventArgs : EventArgs
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Game instance ID where the event occurred.
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// Game plugin ID (e.g., "volam", "mu").
    /// </summary>
    public string GameId { get; init; } = string.Empty;
}

/// <summary>
/// Event arguments for login process started.
/// </summary>
public sealed class LoginEventArgs : GameEventArgs
{
    /// <summary>
    /// Username/account being used for login.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Server/region being connected to.
    /// </summary>
    public string Server { get; init; } = string.Empty;

    /// <summary>
    /// Character/role selected for login.
    /// </summary>
    public string CharacterName { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is an auto-login attempt.
    /// </summary>
    public bool IsAutoLogin { get; init; } = false;

    /// <summary>
    /// Additional login parameters (game-specific).
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Event arguments for successful login.
/// </summary>
public sealed class LoginSuccessEventArgs : GameEventArgs
{
    /// <summary>
    /// Username that logged in successfully.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Character name that entered the game.
    /// </summary>
    public string CharacterName { get; init; } = string.Empty;

    /// <summary>
    /// Character level.
    /// </summary>
    public int CharacterLevel { get; init; } = 0;

    /// <summary>
    /// Character class/job.
    /// </summary>
    public string CharacterClass { get; init; } = string.Empty;

    /// <summary>
    /// Current map/zone name.
    /// </summary>
    public string CurrentMap { get; init; } = string.Empty;

    /// <summary>
    /// Session ID assigned by game server.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Time taken to login (milliseconds).
    /// </summary>
    public long LoginDurationMs { get; init; } = 0;
}

/// <summary>
/// Event arguments for failed login.
/// </summary>
public sealed class LoginFailedEventArgs : GameEventArgs
{
    /// <summary>
    /// Username that failed to login.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Error code from game server.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is a retryable failure.
    /// </summary>
    public bool IsRetryable { get; init; } = true;

    /// <summary>
    /// Suggested retry delay.
    /// </summary>
    public TimeSpan SuggestedRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of retry attempts so far.
    /// </summary>
    public int RetryCount { get; init; } = 0;
}

/// <summary>
/// Event arguments for character entering game world.
/// </summary>
public sealed class CharacterEnterWorldEventArgs : GameEventArgs
{
    /// <summary>
    /// Character name.
    /// </summary>
    public string CharacterName { get; init; } = string.Empty;

    /// <summary>
    /// Character level.
    /// </summary>
    public int Level { get; init; } = 0;

    /// <summary>
    /// Character class/job.
    /// </summary>
    public string CharacterClass { get; init; } = string.Empty;

    /// <summary>
    /// Current map/zone.
    /// </summary>
    public string MapName { get; init; } = string.Empty;

    /// <summary>
    /// Current coordinates (X, Y, Z).
    /// </summary>
    public (int X, int Y, int Z) Position { get; init; }

    /// <summary>
    /// Current HP/MP.
    /// </summary>
    public (int Current, int Max) Health { get; init; }

    /// <summary>
    /// Current stamina/energy.
    /// </summary>
    public (int Current, int Max) Stamina { get; init; }

    /// <summary>
    /// Whether this is a relogin (reconnection).
    /// </summary>
    public bool IsRelogin { get; init; } = false;
}

/// <summary>
/// Event arguments for combat start.
/// </summary>
public sealed class CombatStartEventArgs : GameEventArgs
{
    /// <summary>
    /// Target entity ID.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Target name (monster/player name).
    /// </summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>
    /// Target level.
    /// </summary>
    public int TargetLevel { get; init; } = 0;

    /// <summary>
    /// Target type (monster, player, boss, etc.).
    /// </summary>
    public string TargetType { get; init; } = string.Empty;

    /// <summary>
    /// Distance to target.
    /// </summary>
    public float Distance { get; init; } = 0;

    /// <summary>
    /// Whether combat was initiated by player.
    /// </summary>
    public bool IsPlayerInitiated { get; init; } = true;

    /// <summary>
    /// Skill used to initiate combat.
    /// </summary>
    public string InitiatingSkill { get; init; } = string.Empty;
}

/// <summary>
/// Event arguments for combat end.
/// </summary>
public sealed class CombatEndEventArgs : GameEventArgs
{
    /// <summary>
    /// Target entity ID.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Target name.
    /// </summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>
    /// Whether combat ended in victory.
    /// </summary>
    public bool IsVictory { get; init; } = false;

    /// <summary>
    /// Combat duration.
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Damage dealt.
    /// </summary>
    public long DamageDealt { get; init; } = 0;

    /// <summary>
    /// Damage received.
    /// </summary>
    public long DamageReceived { get; init; } = 0;

    /// <summary>
    /// Experience gained.
    /// </summary>
    public long ExperienceGained { get; init; } = 0;

    /// <summary>
    /// Items dropped.
    /// </summary>
    public IReadOnlyList<string> DroppedItems { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Event arguments for skill usage.
/// </summary>
public sealed class SkillUsedEventArgs : GameEventArgs
{
    /// <summary>
    /// Skill ID.
    /// </summary>
    public string SkillId { get; init; } = string.Empty;

    /// <summary>
    /// Skill name.
    /// </summary>
    public string SkillName { get; init; } = string.Empty;

    /// <summary>
    /// Skill level.
    /// </summary>
    public int SkillLevel { get; init; } = 1;

    /// <summary>
    /// Target entity ID.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Whether skill was successful.
    /// </summary>
    public bool IsSuccessful { get; init; } = true;

    /// <summary>
    /// Mana/energy cost.
    /// </summary>
    public int ManaCost { get; init; } = 0;

    /// <summary>
    /// Cooldown remaining.
    /// </summary>
    public TimeSpan CooldownRemaining { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Whether skill was used on self.
    /// </summary>
    public bool IsSelfCast { get; init; } = false;
}

/// <summary>
/// Event arguments for damage taken.
/// </summary>
public sealed class DamageTakenEventArgs : GameEventArgs
{
    /// <summary>
    /// Damage amount.
    /// </summary>
    public long Damage { get; init; } = 0;

    /// <summary>
    /// Damage type (physical, magical, poison, etc.).
    /// </summary>
    public string DamageType { get; init; } = string.Empty;

    /// <summary>
    /// Attacker entity ID.
    /// </summary>
    public string AttackerId { get; init; } = string.Empty;

    /// <summary>
    /// Attacker name.
    /// </summary>
    public string AttackerName { get; init; } = string.Empty;

    /// <summary>
    /// Whether this was a critical hit.
    /// </summary>
    public bool IsCritical { get; init; } = false;

    /// <summary>
    /// Remaining HP after damage.
    /// </summary>
    public int RemainingHp { get; init; } = 0;

    /// <summary>
    /// Whether character died from this damage.
    /// </summary>
    public bool IsFatal { get; init; } = false;
}

/// <summary>
/// Event arguments for character death.
/// </summary>
public sealed class DeadEventArgs : GameEventArgs
{
    /// <summary>
    /// Killer entity ID.
    /// </summary>
    public string KillerId { get; init; } = string.Empty;

    /// <summary>
    /// Killer name.
    /// </summary>
    public string KillerName { get; init; } = string.Empty;

    /// <summary>
    /// Death cause (monster, player, environment, etc.).
    /// </summary>
    public string DeathCause { get; init; } = string.Empty;

    /// <summary>
    /// Location of death.
    /// </summary>
    public (int X, int Y, int Z) Position { get; init; }

    /// <summary>
    /// Experience lost.
    /// </summary>
    public long ExperienceLost { get; init; } = 0;

    /// <summary>
    /// Items dropped on death.
    /// </summary>
    public IReadOnlyList<string> DroppedItems { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether this was a PvP death.
    /// </summary>
    public bool IsPvpDeath { get; init; } = false;
}

/// <summary>
/// Event arguments for revive/respawn.
/// </summary>
public sealed class ReviveEventArgs : GameEventArgs
{
    /// <summary>
    /// Revive type (town, spot, scroll, priest, etc.).
    /// </summary>
    public string ReviveType { get; init; } = string.Empty;

    /// <summary>
    /// Revive location.
    /// </summary>
    public (int X, int Y, int Z) RevivePosition { get; init; }

    /// <summary>
    /// HP/MP after revive.
    /// </summary>
    public (int CurrentHp, int MaxHp, int CurrentMp, int MaxMp) Stats { get; init; }

    /// <summary>
    /// Experience restored.
    /// </summary>
    public long ExperienceRestored { get; init; } = 0;

    /// <summary>
    /// Whether items were lost.
    /// </summary>
    public bool ItemsLost { get; init; } = false;
}

/// <summary>
/// Event arguments for level up.
/// </summary>
public sealed class LevelUpEventArgs : GameEventArgs
{
    /// <summary>
    /// New level.
    /// </summary>
    public int NewLevel { get; init; } = 0;

    /// <summary>
    /// Previous level.
    /// </summary>
    public int PreviousLevel { get; init; } = 0;

    /// <summary>
    /// Stat points gained.
    /// </summary>
    public int StatPointsGained { get; init; } = 0;

    /// <summary>
    /// Skill points gained.
    /// </summary>
    public int SkillPointsGained { get; init; } = 0;

    /// <summary>
    /// New skills unlocked.
    /// </summary>
    public IReadOnlyList<string> UnlockedSkills { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Event arguments for quest completion.
/// </summary>
public sealed class QuestCompleteEventArgs : GameEventArgs
{
    /// <summary>
    /// Quest ID.
    /// </summary>
    public string QuestId { get; init; } = string.Empty;

    /// <summary>
    /// Quest name.
    /// </summary>
    public string QuestName { get; init; } = string.Empty;

    /// <summary>
    /// Quest type (main, side, daily, event).
    /// </summary>
    public string QuestType { get; init; } = string.Empty;

    /// <summary>
    /// Experience reward.
    /// </summary>
    public long ExperienceReward { get; init; } = 0;

    /// <summary>
    /// Gold/money reward.
    /// </summary>
    public long GoldReward { get; init; } = 0;

    /// <summary>
    /// Item rewards.
    /// </summary>
    public IReadOnlyList<string> ItemRewards { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Event arguments for inventory full.
/// </summary>
public sealed class InventoryFullEventArgs : GameEventArgs
{
    /// <summary>
    /// Current item count.
    /// </summary>
    public int CurrentCount { get; init; } = 0;

    /// <summary>
    /// Maximum capacity.
    /// </summary>
    public int MaxCapacity { get; init; } = 0;

    /// <summary>
    /// Item that triggered the full condition.
    /// </summary>
    public string TriggerItem { get; init; } = string.Empty;

    /// <summary>
    /// Number of free slots remaining.
    /// </summary>
    public int FreeSlots { get; init; } = 0;
}

/// <summary>
/// Event arguments for low stamina/energy.
/// </summary>
public sealed class StaminaLowEventArgs : GameEventArgs
{
    /// <summary>
    /// Current stamina.
    /// </summary>
    public int CurrentStamina { get; init; } = 0;

    /// <summary>
    /// Maximum stamina.
    /// </summary>
    public int MaxStamina { get; init; } = 0;

    /// <summary>
    /// Stamina percentage.
    /// </summary>
    public double Percentage { get; init; } = 0;

    /// <summary>
    /// Whether auto-pot should be used.
    /// </summary>
    public bool ShouldAutoPot { get; init; } = true;
}

/// <summary>
/// Event arguments for character stuck.
/// </summary>
public sealed class StuckEventArgs : GameEventArgs
{
    /// <summary>
    /// Duration stuck (milliseconds).
    /// </summary>
    public long StuckDurationMs { get; init; } = 0;

    /// <summary>
    /// Current position.
    /// </summary>
    public (int X, int Y, int Z) Position { get; init; }

    /// <summary>
    /// Number of stuck occurrences.
    /// </summary>
    public int StuckCount { get; init; } = 0;

    /// <summary>
    /// Last attempted action.
    /// </summary>
    public string LastAction { get; init; } = string.Empty;

    /// <summary>
    /// Whether auto-unstuck should be attempted.
    /// </summary>
    public bool ShouldAutoUnstuck { get; init; } = true;
}

/// <summary>
/// Event arguments for boss spawn.
/// </summary>
public sealed class BossSpawnEventArgs : GameEventArgs
{
    /// <summary>
    /// Boss entity ID.
    /// </summary>
    public string BossId { get; init; } = string.Empty;

    /// <summary>
    /// Boss name.
    /// </summary>
    public string BossName { get; init; } = string.Empty;

    /// <summary>
    /// Boss level.
    /// </summary>
    public int Level { get; init; } = 0;

    /// <summary>
    /// Boss type (world boss, dungeon boss, field boss, etc.).
    /// </summary>
    public string BossType { get; init; } = string.Empty;

    /// <summary>
    /// Boss position.
    /// </summary>
    public (int X, int Y, int Z) Position { get; init; }

    /// <summary>
    /// Whether boss is elite/rare.
    /// </summary>
    public bool IsElite { get; init; } = false;

    /// <summary>
    /// Estimated HP.
    /// </summary>
    public long EstimatedHp { get; init; } = 0;
}

/// <summary>
/// Event arguments for player nearby.
/// </summary>
public sealed class PlayerNearbyEventArgs : GameEventArgs
{
    /// <summary>
    /// Player entity ID.
    /// </summary>
    public string PlayerId { get; init; } = string.Empty;

    /// <summary>
    /// Player name.
    /// </summary>
    public string PlayerName { get; init; } = string.Empty;

    /// <summary>
    /// Player level.
    /// </summary>
    public int Level { get; init; } = 0;

    /// <summary>
    /// Player class/job.
    /// </summary>
    public string CharacterClass { get; init; } = string.Empty;

    /// <summary>
    /// Distance to player.
    /// </summary>
    public float Distance { get; init; } = 0;

    /// <summary>
    /// Whether player is in same party/guild.
    /// </summary>
    public bool IsFriendly { get; init; } = false;

    /// <summary>
    /// Whether player is hostile (PvP flagged).
    /// </summary>
    public bool IsHostile { get; init; } = false;
}

/// <summary>
/// Event arguments for item drop.
/// </summary>
public sealed class ItemDropEventArgs : GameEventArgs
{
    /// <summary>
    /// Item entity ID.
    /// </summary>
    public string ItemId { get; init; } = string.Empty;

    /// <summary>
    /// Item name.
    /// </summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>
    /// Item quality/rarity.
    /// </summary>
    public string Quality { get; init; } = string.Empty;

    /// <summary>
    /// Item position.
    /// </summary>
    public (int X, int Y, int Z) Position { get; init; }

    /// <summary>
    /// Quantity.
    /// </summary>
    public int Quantity { get; init; } = 1;

    /// <summary>
    /// Whether item is from boss.
    /// </summary>
    public bool IsFromBoss { get; init; } = false;

    /// <summary>
    /// Time until item despawns.
    /// </summary>
    public TimeSpan DespawnTime { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Event arguments for chat message.
/// </summary>
public sealed class ChatMessageEventArgs : GameEventArgs
{
    /// <summary>
    /// Sender name.
    /// </summary>
    public string SenderName { get; init; } = string.Empty;

    /// <summary>
    /// Message content.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Chat channel (world, party, guild, whisper, system).
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// Sender level.
    /// </summary>
    public int SenderLevel { get; init; } = 0;

    /// <summary>
    /// Whether message is from GM/admin.
    /// </summary>
    public bool IsSystemMessage { get; init; } = false;

    /// <summary>
    /// Whether message is a command.
    /// </summary>
    public bool IsCommand { get; init; } = false;
}

/// <summary>
/// Event arguments for party/guild invite.
/// </summary>
public sealed class PartyInviteEventArgs : GameEventArgs
{
    /// <summary>
    /// Inviter name.
    /// </summary>
    public string InviterName { get; init; } = string.Empty;

    /// <summary>
    /// Inviter level.
    /// </summary>
    public int InviterLevel { get; init; } = 0;

    /// <summary>
    /// Invite type (party, guild, raid, etc.).
    /// </summary>
    public string InviteType { get; init; } = string.Empty;

    /// <summary>
    /// Whether auto-accept is enabled.
    /// </summary>
    public bool AutoAccept { get; init; } = false;

    /// <summary>
    /// Time to respond.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Event arguments for trade request.
/// </summary>
public sealed class TradeRequestEventArgs : GameEventArgs
{
    /// <summary>
    /// Requester name.
    /// </summary>
    public string RequesterName { get; init; } = string.Empty;

    /// <summary>
    /// Requester level.
    /// </summary>
    public int RequesterLevel { get; init; } = 0;

    /// <summary>
    /// Items offered by requester.
    /// </summary>
    public IReadOnlyList<string> OfferedItems { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gold/money offered.
    /// </summary>
    public long GoldOffered { get; init; } = 0;

    /// <summary>
    /// Time to respond.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Event arguments for window focus lost.
/// </summary>
public sealed class WindowFocusLostEventArgs : GameEventArgs
{
    /// <summary>
    /// Process ID of game window.
    /// </summary>
    public int ProcessId { get; init; } = 0;

    /// <summary>
    /// Window title.
    /// </summary>
    public string WindowTitle { get; init; } = string.Empty;
}

/// <summary>
/// Event arguments for window focus gained.
/// </summary>
public sealed class WindowFocusGainedEventArgs : GameEventArgs
{
    /// <summary>
    /// Process ID of game window.
    /// </summary>
    public int ProcessId { get; init; } = 0;

    /// <summary>
    /// Window title.
    /// </summary>
    public string WindowTitle { get; init; } = string.Empty;

    /// <summary>
    /// Time since focus was lost.
    /// </summary>
    public TimeSpan TimeUnfocused { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// Event arguments for network disconnection.
/// </summary>
public sealed class DisconnectedEventArgs : GameEventArgs
{
    /// <summary>
    /// Disconnection reason.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Error code.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Whether reconnection should be attempted.
    /// </summary>
    public bool ShouldReconnect { get; init; } = true;

    /// <summary>
    /// Time since last successful packet.
    /// </summary>
    public TimeSpan LastPacketAge { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// Event arguments for network reconnection.
/// </summary>
public sealed class ReconnectedEventArgs : GameEventArgs
{
    /// <summary>
    /// Time taken to reconnect.
    /// </summary>
    public TimeSpan ReconnectDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Whether reconnection was successful.
    /// </summary>
    public bool IsSuccessful { get; init; } = true;

    /// <summary>
    /// Number of reconnection attempts.
    /// </summary>
    public int AttemptCount { get; init; } = 0;
}

/// <summary>
/// Event arguments for periodic tick.
/// </summary>
public sealed class TickEventArgs : GameEventArgs
{
    /// <summary>
    /// Tick number (increments each tick).
    /// </summary>
    public long TickNumber { get; init; } = 0;

    /// <summary>
    /// Time since last tick.
    /// </summary>
    public TimeSpan DeltaTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Current game time.
    /// </summary>
    public DateTimeOffset GameTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether this is a fixed update (physics) tick.
    /// </summary>
    public bool IsFixedUpdate { get; init; } = false;
}