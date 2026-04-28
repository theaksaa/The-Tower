using System.Collections.Generic;
using System.Linq;
using TheTower;
using UnityEngine;

[System.Serializable]
public sealed class HeroRuntimeState
{
    public string HeroId;
    public string HeroName;
    public int Level;
    public int Xp;
    public int CurrentHp;
    public int BonusAttack;
    public int BonusDefense;
    public int BonusMagic;
    public List<string> EquippedMoves = new();
    public HashSet<string> KnownMoves = new();
}

public static class RunSession
{
    public static RunConfig CurrentRunConfig { get; private set; }
    public static HeroRuntimeState Hero { get; private set; }
    public static HeroDefinition SelectedHeroDefinition { get; private set; }
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
        InitializeNewRun(runConfig, usingFallbackData, GetAvailableHeroes(runConfig).FirstOrDefault());
    }

    public static void InitializeNewRun(RunConfig runConfig, bool usingFallbackData, HeroDefinition selectedHero)
    {
        CurrentRunConfig = runConfig;
        UsingFallbackData = usingFallbackData;
        IsDefeated = false;
        PendingLearnedMoveId = null;
        SelectedHeroDefinition = selectedHero ?? GetAvailableHeroes(runConfig).FirstOrDefault();
        var runLabel = string.IsNullOrEmpty(runConfig.runId)
            ? "run"
            : runConfig.runId.Substring(0, Mathf.Min(8, runConfig.runId.Length));
        var heroName = string.IsNullOrEmpty(SelectedHeroDefinition?.name)
            ? "Hero"
            : SelectedHeroDefinition.name;
        StatusMessage = usingFallbackData
            ? "Offline fallback data loaded."
            : $"Run {runLabel} ready. {heroName} selected.";

        var defaults = SelectedHeroDefinition;
        if (defaults == null || defaults.baseStats == null || defaults.moves == null)
        {
            Hero = null;
            CompletedEncounters = null;
            SelectedEncounterIndex = -1;
            StatusMessage = "No hero configuration was found in the run config.";
            return;
        }

        Hero = new HeroRuntimeState
        {
            HeroId = defaults.id,
            HeroName = defaults.name,
            Level = 1,
            Xp = 0,
            CurrentHp = defaults.baseStats.health,
            EquippedMoves = defaults.moves.Take(4).ToList(),
            KnownMoves = new HashSet<string>(defaults.moves)
        };

        CompletedEncounters = Enumerable.Repeat(false, runConfig.encounters.Count).ToList();
        SelectedEncounterIndex = GetFirstAvailableEncounterIndex();
    }

    public static IReadOnlyList<HeroDefinition> GetAvailableHeroes(RunConfig runConfig)
    {
        if (runConfig?.heroes != null && runConfig.heroes.Count > 0)
        {
            return runConfig.heroes;
        }

        if (runConfig?.heroDefaults == null)
        {
            return new List<HeroDefinition>();
        }

        return new List<HeroDefinition>
        {
            new()
            {
                id = string.IsNullOrEmpty(runConfig.heroDefaults.id) ? "hero" : runConfig.heroDefaults.id,
                name = string.IsNullOrEmpty(runConfig.heroDefaults.name) ? "Hero" : runConfig.heroDefaults.name,
                description = runConfig.heroDefaults.description,
                spriteKey = runConfig.heroDefaults.spriteKey,
                baseStats = runConfig.heroDefaults.baseStats?.Clone(),
                statsPerLevel = runConfig.heroDefaults.statsPerLevel?.Clone(),
                moves = runConfig.heroDefaults.moves?.ToList() ?? new List<string>()
            }
        };
    }

    public static string GetHeroDisplayName()
    {
        if (!string.IsNullOrEmpty(Hero?.HeroName))
        {
            return Hero.HeroName;
        }

        return !string.IsNullOrEmpty(SelectedHeroDefinition?.name)
            ? SelectedHeroDefinition.name
            : "Hero";
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
        var nextEncounterIndex = GetFirstAvailableEncounterIndex();
        return HasActiveRun &&
               !IsDefeated &&
               encounterIndex >= 0 &&
               encounterIndex < CurrentRunConfig.encounters.Count &&
               !CompletedEncounters[encounterIndex] &&
               encounterIndex == nextEncounterIndex;
    }

    public static bool CanEnterEncounterFromMap(int encounterIndex)
    {
        if (!HasActiveRun || IsDefeated || CurrentRunConfig == null || CompletedEncounters == null)
        {
            return false;
        }

        if (encounterIndex < 0 || encounterIndex >= CurrentRunConfig.encounters.Count)
        {
            return false;
        }

        if (CompletedEncounters[encounterIndex])
        {
            return true;
        }

        return encounterIndex == GetFirstAvailableEncounterIndex();
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

    public static int GetNextLevelXpThreshold()
    {
        if (!HasActiveRun || CurrentRunConfig?.xpTable == null || Hero == null)
        {
            return -1;
        }

        return Hero.Level < CurrentRunConfig.xpTable.Count
            ? CurrentRunConfig.xpTable[Hero.Level]
            : -1;
    }

    public static bool CanLevelUp()
    {
        var nextThreshold = GetNextLevelXpThreshold();
        return nextThreshold >= 0 && Hero.Xp >= nextThreshold;
    }

    public static int GetAvailableLevelUpCount()
    {
        if (!HasActiveRun || CurrentRunConfig?.xpTable == null || Hero == null)
        {
            return 0;
        }

        var count = 0;
        var simulatedLevel = Hero.Level;
        while (simulatedLevel < CurrentRunConfig.xpTable.Count &&
               Hero.Xp >= CurrentRunConfig.xpTable[simulatedLevel])
        {
            count++;
            simulatedLevel++;
        }

        return count;
    }

    public static bool TrySpendLevelUp(string stat)
    {
        if (!CanLevelUp() || string.IsNullOrWhiteSpace(stat))
        {
            return false;
        }

        var selectedHero = SelectedHeroDefinition ?? GetAvailableHeroes(CurrentRunConfig).FirstOrDefault();
        if (selectedHero?.statsPerLevel == null || Hero == null)
        {
            return false;
        }

        var healthGain = Mathf.Max(0, selectedHero.statsPerLevel.health);
        switch (stat.Trim().ToLowerInvariant())
        {
            case "attack":
                Hero.BonusAttack += Mathf.Max(0, selectedHero.statsPerLevel.attack);
                break;
            case "defense":
                Hero.BonusDefense += Mathf.Max(0, selectedHero.statsPerLevel.defense);
                break;
            case "magic":
                Hero.BonusMagic += Mathf.Max(0, selectedHero.statsPerLevel.magic);
                break;
            default:
                return false;
        }

        Hero.Level++;
        Hero.CurrentHp += healthGain;
        Hero.CurrentHp = Mathf.Min(Hero.CurrentHp, GetHeroBaseStats().health);
        StatusMessage = $"{GetHeroDisplayName()} leveled up {stat.Trim().ToUpperInvariant()}.";
        return true;
    }

    public static Stats GetHeroBaseStats()
    {
        var selectedHero = SelectedHeroDefinition ?? GetAvailableHeroes(CurrentRunConfig).FirstOrDefault();
        if (selectedHero?.baseStats == null || selectedHero.statsPerLevel == null || Hero == null)
        {
            return new Stats();
        }

        var baseStats = selectedHero.baseStats.Clone();
        var levelBonus = Mathf.Max(0, Hero.Level - 1);

        baseStats.health += selectedHero.statsPerLevel.health * levelBonus;
        baseStats.attack += Hero.BonusAttack;
        baseStats.defense += Hero.BonusDefense;
        baseStats.magic += Hero.BonusMagic;
        return baseStats;
    }
}
