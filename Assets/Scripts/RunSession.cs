using System.Collections.Generic;
using System.Linq;
using TheTower;
using UnityEngine;

[System.Serializable]
public sealed class HeroRuntimeState
{
    public int Level;
    public int Xp;
    public int CurrentHp;
    public List<string> EquippedMoves = new();
    public HashSet<string> KnownMoves = new();
}

public static class RunSession
{
    public static RunConfig CurrentRunConfig { get; private set; }
    public static HeroRuntimeState Hero { get; private set; }
    public static List<bool> CompletedEncounters { get; private set; }
    public static int SelectedEncounterIndex { get; private set; } = -1;
    public static bool UsingFallbackData { get; private set; }
    public static bool IsDefeated { get; private set; }
    public static string StatusMessage { get; private set; }
    public static string PendingLearnedMoveId { get; private set; }

    public static bool HasActiveRun => CurrentRunConfig != null && Hero != null && CompletedEncounters != null;
    public static bool HasPendingLearnedMove => !string.IsNullOrEmpty(PendingLearnedMoveId);

    public static void InitializeNewRun(RunConfig runConfig, bool usingFallbackData)
    {
        CurrentRunConfig = runConfig;
        UsingFallbackData = usingFallbackData;
        IsDefeated = false;
        PendingLearnedMoveId = null;
        var runLabel = string.IsNullOrEmpty(runConfig.runId)
            ? "run"
            : runConfig.runId.Substring(0, Mathf.Min(8, runConfig.runId.Length));
        StatusMessage = usingFallbackData
            ? "Offline fallback data loaded."
            : $"Run {runLabel} ready.";

        var defaults = runConfig.heroDefaults;
        Hero = new HeroRuntimeState
        {
            Level = 1,
            Xp = 0,
            CurrentHp = defaults.baseStats.health,
            EquippedMoves = defaults.moves.Take(4).ToList(),
            KnownMoves = new HashSet<string>(defaults.moves)
        };

        CompletedEncounters = Enumerable.Repeat(false, runConfig.encounters.Count).ToList();
        SelectedEncounterIndex = GetFirstAvailableEncounterIndex();
    }

    public static void SelectEncounter(int encounterIndex)
    {
        SelectedEncounterIndex = Mathf.Clamp(encounterIndex, 0, CurrentRunConfig.encounters.Count - 1);
    }

    public static int GetFirstAvailableEncounterIndex()
    {
        if (CurrentRunConfig == null || CompletedEncounters == null)
        {
            return -1;
        }

        for (var index = 0; index < CompletedEncounters.Count; index++)
        {
            if (!CompletedEncounters[index])
            {
                return index;
            }
        }

        return -1;
    }

    public static bool IsEncounterCompleted(int encounterIndex)
    {
        return CompletedEncounters != null &&
               encounterIndex >= 0 &&
               encounterIndex < CompletedEncounters.Count &&
               CompletedEncounters[encounterIndex];
    }

    public static bool CanEnterEncounter(int encounterIndex)
    {
        return HasActiveRun &&
               !IsDefeated &&
               encounterIndex >= 0 &&
               encounterIndex < CurrentRunConfig.encounters.Count &&
               !CompletedEncounters[encounterIndex];
    }

    public static void MarkEncounterComplete(int encounterIndex, string rewardSummary)
    {
        if (!IsEncounterCompleted(encounterIndex))
        {
            CompletedEncounters[encounterIndex] = true;
        }

        SelectedEncounterIndex = GetFirstAvailableEncounterIndex();
        StatusMessage = $"Encounter {encounterIndex + 1} cleared. {rewardSummary}";
    }

    public static void RegisterDefeat(int encounterIndex)
    {
        IsDefeated = true;
        StatusMessage = $"The hero fell on encounter {encounterIndex + 1}. Start a new run to try again.";
    }

    public static void SetPendingLearnedMove(string moveId)
    {
        PendingLearnedMoveId = moveId;
    }

    public static void ClearPendingLearnedMove()
    {
        PendingLearnedMoveId = null;
    }

    public static void SetStatus(string message)
    {
        StatusMessage = message;
    }

    public static bool IsRunComplete()
    {
        return CompletedEncounters != null && CompletedEncounters.All(completed => completed);
    }

    public static Move GetMove(string moveId)
    {
        if (CurrentRunConfig?.moveRegistry == null || string.IsNullOrEmpty(moveId))
        {
            return null;
        }

        CurrentRunConfig.moveRegistry.TryGetValue(moveId, out var move);
        return move;
    }

    public static Stats GetHeroBaseStats()
    {
        var baseStats = CurrentRunConfig.heroDefaults.baseStats.Clone();
        var levelBonus = Mathf.Max(0, Hero.Level - 1);

        baseStats.health += CurrentRunConfig.heroDefaults.statsPerLevel.health * levelBonus;
        baseStats.attack += CurrentRunConfig.heroDefaults.statsPerLevel.attack * levelBonus;
        baseStats.defense += CurrentRunConfig.heroDefaults.statsPerLevel.defense * levelBonus;
        baseStats.magic += CurrentRunConfig.heroDefaults.statsPerLevel.magic * levelBonus;
        return baseStats;
    }
}
