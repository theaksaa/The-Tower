using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TheTower;
using UnityEngine;

[System.Serializable]
public sealed class HeroRuntimeState
{
    public string HeroId;
    public string HeroName;
    public int Level;
    public int Xp;
    public int Coins;
    public int CurrentHp;
    public int BonusHealth;
    public int BonusAttack;
    public int BonusDefense;
    public int BonusMagic;
    public List<string> EquippedMoves = new();
    public HashSet<string> KnownMoves = new();
    public Dictionary<string, int> MonsterKillCounts = new();
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
    public static string CurrentSaveId { get; private set; }
    public static string CurrentMode { get; private set; } = "Story";
    public static long CreatedAtUtcTicks { get; private set; }
    public static long LastUpdatedAtUtcTicks { get; private set; }

    public static bool HasActiveRun => CurrentRunConfig != null && Hero != null && CompletedEncounters != null;
    public static bool HasPendingLearnedMove => !string.IsNullOrEmpty(PendingLearnedMoveId);

    public static void InitializeNewRun(RunConfig runConfig, bool usingFallbackData)
    {
        InitializeNewRun(runConfig, usingFallbackData, GetAvailableHeroes(runConfig).FirstOrDefault(), "Story");
    }

    public static void InitializeNewRun(RunConfig runConfig, bool usingFallbackData, HeroDefinition selectedHero)
    {
        InitializeNewRun(runConfig, usingFallbackData, selectedHero, "Story");
    }

    public static void InitializeNewRun(RunConfig runConfig, bool usingFallbackData, HeroDefinition selectedHero, string mode)
    {
        CurrentRunConfig = runConfig;
        UsingFallbackData = usingFallbackData;
        IsDefeated = false;
        PendingLearnedMoveId = null;
        CurrentMode = string.IsNullOrWhiteSpace(mode) ? "Story" : mode;
        CurrentSaveId = Guid.NewGuid().ToString("N");
        CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
        LastUpdatedAtUtcTicks = CreatedAtUtcTicks;
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
            ClearSaveMetadata();
            return;
        }

        Hero = new HeroRuntimeState
        {
            HeroId = defaults.id,
            HeroName = defaults.name,
            Level = 1,
            Xp = 0,
            Coins = 0,
            CurrentHp = defaults.baseStats.health,
            BonusHealth = 0,
            EquippedMoves = defaults.moves.Take(4).ToList(),
            KnownMoves = new HashSet<string>(defaults.moves),
            MonsterKillCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        };

        CompletedEncounters = Enumerable.Repeat(false, runConfig.encounters.Count).ToList();
        SelectedEncounterIndex = GetFirstAvailableEncounterIndex();
        RunSaveService.SaveCurrentRun();
    }

    public static bool RestoreSavedRun(RunSaveService.PersistedRunData runData)
    {
        if (runData?.RunConfig == null || runData.Hero == null)
        {
            return false;
        }

        CurrentRunConfig = runData.RunConfig;
        UsingFallbackData = runData.UsingFallbackData;
        IsDefeated = runData.IsDefeated;
        StatusMessage = runData.StatusMessage;
        PendingLearnedMoveId = runData.PendingLearnedMoveId;
        CurrentSaveId = runData.SaveId;
        CurrentMode = string.IsNullOrWhiteSpace(runData.Mode) ? "Story" : runData.Mode;
        CreatedAtUtcTicks = runData.CreatedAtUtcTicks > 0 ? runData.CreatedAtUtcTicks : DateTime.UtcNow.Ticks;
        LastUpdatedAtUtcTicks = runData.UpdatedAtUtcTicks > 0 ? runData.UpdatedAtUtcTicks : CreatedAtUtcTicks;

        SelectedHeroDefinition = ResolveSavedHeroDefinition(runData);
        Hero = new HeroRuntimeState
        {
            HeroId = runData.Hero.HeroId,
            HeroName = string.IsNullOrWhiteSpace(runData.Hero.HeroName)
                ? SelectedHeroDefinition?.name ?? "Hero"
                : runData.Hero.HeroName,
            Level = Mathf.Max(1, runData.Hero.Level),
            Xp = Mathf.Max(0, runData.Hero.Xp),
            Coins = Mathf.Max(0, runData.Hero.Coins),
            CurrentHp = Mathf.Max(0, runData.Hero.CurrentHp),
            BonusHealth = runData.Hero.BonusHealth,
            BonusAttack = runData.Hero.BonusAttack,
            BonusDefense = runData.Hero.BonusDefense,
            BonusMagic = runData.Hero.BonusMagic,
            EquippedMoves = runData.Hero.EquippedMoves?.ToList() ?? new List<string>(),
            KnownMoves = runData.Hero.KnownMoves != null
                ? new HashSet<string>(runData.Hero.KnownMoves)
                : new HashSet<string>(),
            MonsterKillCounts = runData.Hero.MonsterKillCounts != null
                ? new Dictionary<string, int>(runData.Hero.MonsterKillCounts, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        };

        if (Hero.KnownMoves.Count == 0 && SelectedHeroDefinition?.moves != null)
        {
            Hero.KnownMoves = new HashSet<string>(SelectedHeroDefinition.moves);
        }

        RestoreHeroToFullHealth();

        CompletedEncounters = runData.CompletedEncounters?.ToList() ?? new List<bool>();
        NormalizeCompletedEncounters();

        var firstAvailableEncounter = GetFirstAvailableEncounterIndex();
        SelectedEncounterIndex = runData.SelectedEncounterIndex;
        if (SelectedEncounterIndex < 0 || SelectedEncounterIndex >= CompletedEncounters.Count)
        {
            SelectedEncounterIndex = firstAvailableEncounter;
        }

        if (SelectedEncounterIndex >= 0 && IsEncounterCompleted(SelectedEncounterIndex))
        {
            SelectedEncounterIndex = firstAvailableEncounter;
        }

        return true;
    }

    public static void ClearActiveRun()
    {
        CurrentRunConfig = null;
        Hero = null;
        SelectedHeroDefinition = null;
        CompletedEncounters = null;
        SelectedEncounterIndex = -1;
        UsingFallbackData = false;
        IsDefeated = false;
        StatusMessage = null;
        PendingLearnedMoveId = null;
        ClearSaveMetadata();
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

    public static void ClearDefeatState(string statusMessage = null)
    {
        IsDefeated = false;
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            StatusMessage = statusMessage;
        }
    }

    public static void ReturnToMapAfterDefeat(int encounterIndex)
    {
        if (!HasActiveRun || Hero == null)
        {
            return;
        }

        ClearDefeatState();
        RestoreHeroToFullHealth();

        var nextEncounterIndex = encounterIndex >= 0 && encounterIndex < CurrentRunConfig.encounters.Count
            ? encounterIndex
            : GetFirstAvailableEncounterIndex();

        if (nextEncounterIndex >= 0)
        {
            SelectedEncounterIndex = nextEncounterIndex;
        }

        StatusMessage = $"The hero retreated from encounter {encounterIndex + 1}. Choose your next fight from the map.";
        RunSaveService.SaveCurrentRun();
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

    public static void AddCoins(int amount)
    {
        if (Hero == null || amount <= 0)
        {
            return;
        }

        Hero.Coins += amount;
    }

    public static bool TryPurchaseStatBoost(string stat, int amount, int coinCost)
    {
        if (Hero == null || string.IsNullOrWhiteSpace(stat) || amount <= 0 || coinCost < 0)
        {
            return false;
        }

        if (Hero.Coins < coinCost)
        {
            return false;
        }

        switch (stat.Trim().ToLowerInvariant())
        {
            case "health":
                Hero.BonusHealth += amount;
                RestoreHeroToFullHealth();
                break;
            case "attack":
                Hero.BonusAttack += amount;
                break;
            case "defense":
                Hero.BonusDefense += amount;
                break;
            case "magic":
                Hero.BonusMagic += amount;
                break;
            default:
                return false;
        }

        Hero.Coins -= coinCost;
        StatusMessage = $"{GetHeroDisplayName()} bought {stat.Trim().ToUpperInvariant()} +{amount}.";
        RunSaveService.SaveCurrentRun();
        return true;
    }

    public static bool TryPurchaseMoveUnlock(string moveId, int coinCost)
    {
        if (Hero == null || string.IsNullOrWhiteSpace(moveId) || coinCost < 0)
        {
            return false;
        }

        moveId = moveId.Trim();
        if (Hero.Coins < coinCost || Hero.KnownMoves.Contains(moveId) || GetMove(moveId) == null)
        {
            return false;
        }

        Hero.KnownMoves.Add(moveId);
        Hero.Coins -= coinCost;
        PendingLearnedMoveId = moveId;

        var moveName = GetMove(moveId)?.name ?? moveId;
        StatusMessage = $"{GetHeroDisplayName()} learned {moveName}.";
        RunSaveService.SaveCurrentRun();
        return true;
    }

    public static int GetMonsterKillCount(string monsterId)
    {
        if (Hero?.MonsterKillCounts == null || string.IsNullOrWhiteSpace(monsterId))
        {
            return 0;
        }

        return Hero.MonsterKillCounts.TryGetValue(monsterId, out var count)
            ? Mathf.Max(0, count)
            : 0;
    }

    public static void RegisterMonsterKill(string monsterId)
    {
        if (Hero == null || string.IsNullOrWhiteSpace(monsterId))
        {
            return;
        }

        Hero.MonsterKillCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Hero.MonsterKillCounts.TryGetValue(monsterId, out var existingCount);
        Hero.MonsterKillCounts[monsterId] = Mathf.Max(0, existingCount) + 1;
    }

    public static int CalculateCoinReward(Monster monster)
    {
        var baseReward = Mathf.Max(0, monster?.coinReward ?? 0);
        if (baseReward <= 0)
        {
            return 0;
        }

        var repeatKillCount = GetMonsterKillCount(monster.id);
        if (repeatKillCount <= 0)
        {
            return baseReward;
        }

        var rewardScaling = CurrentRunConfig?.coinRewardScaling;
        if (rewardScaling == null)
        {
            return baseReward;
        }

        var multiplierPerKill = rewardScaling.multiplierPerKill > 0f
            ? rewardScaling.multiplierPerKill
            : 1f;
        var scaledReward = Mathf.FloorToInt(baseReward * Mathf.Pow(multiplierPerKill, repeatKillCount));
        return Mathf.Max(Mathf.Max(0, rewardScaling.minimumReward), scaledReward);
    }

    public static int CalculateXpReward(Monster monster)
    {
        var baseReward = Mathf.Max(0, monster?.xpReward ?? 0);
        if (baseReward <= 0)
        {
            return 0;
        }

        var repeatKillCount = GetMonsterKillCount(monster.id);
        if (repeatKillCount <= 0)
        {
            return baseReward;
        }

        var rewardScaling = CurrentRunConfig?.xpRewardScaling;
        if (rewardScaling == null)
        {
            return baseReward;
        }

        var multiplierPerKill = rewardScaling.multiplierPerKill > 0f
            ? rewardScaling.multiplierPerKill
            : 1f;
        var scaledReward = Mathf.FloorToInt(baseReward * Mathf.Pow(multiplierPerKill, repeatKillCount));
        return Mathf.Max(Mathf.Max(0, rewardScaling.minimumReward), scaledReward);
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
        ClampHeroCurrentHp();
        StatusMessage = $"{GetHeroDisplayName()} leveled up {stat.Trim().ToUpperInvariant()}.";
        RunSaveService.SaveCurrentRun();
        return true;
    }

    public static int GetHeroMaxHealth()
    {
        return Mathf.Max(1, GetHeroBaseStats().health);
    }

    public static void ClampHeroCurrentHp()
    {
        if (Hero == null)
        {
            return;
        }

        Hero.CurrentHp = Mathf.Clamp(Hero.CurrentHp, 0, GetHeroMaxHealth());
    }

    public static void RestoreHeroToFullHealth()
    {
        if (Hero == null)
        {
            return;
        }

        Hero.CurrentHp = GetHeroMaxHealth();
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

        baseStats.health += selectedHero.statsPerLevel.health * levelBonus + Hero.BonusHealth;
        baseStats.attack += Hero.BonusAttack;
        baseStats.defense += Hero.BonusDefense;
        baseStats.magic += Hero.BonusMagic;
        return baseStats;
    }

    public static void SetLastUpdatedAt(long updatedAtUtcTicks)
    {
        LastUpdatedAtUtcTicks = Math.Max(0L, updatedAtUtcTicks);
    }

    private static HeroDefinition ResolveSavedHeroDefinition(RunSaveService.PersistedRunData runData)
    {
        if (runData?.SelectedHeroDefinition != null)
        {
            return new HeroDefinition
            {
                id = runData.SelectedHeroDefinition.id,
                name = runData.SelectedHeroDefinition.name,
                description = runData.SelectedHeroDefinition.description,
                portraitKey = runData.SelectedHeroDefinition.portraitKey,
                spriteKey = runData.SelectedHeroDefinition.spriteKey,
                baseStats = runData.SelectedHeroDefinition.baseStats?.Clone(),
                statsPerLevel = runData.SelectedHeroDefinition.statsPerLevel?.Clone(),
                moves = runData.SelectedHeroDefinition.moves?.ToList() ?? new List<string>()
            };
        }

        var matchingHero = GetAvailableHeroes(runData?.RunConfig)
            .FirstOrDefault(hero => string.Equals(hero.id, runData?.Hero?.HeroId, StringComparison.OrdinalIgnoreCase));

        return matchingHero != null
            ? new HeroDefinition
            {
                id = matchingHero.id,
                name = matchingHero.name,
                description = matchingHero.description,
                portraitKey = matchingHero.portraitKey,
                spriteKey = matchingHero.spriteKey,
                baseStats = matchingHero.baseStats?.Clone(),
                statsPerLevel = matchingHero.statsPerLevel?.Clone(),
                moves = matchingHero.moves?.ToList() ?? new List<string>()
            }
            : null;
    }

    private static void NormalizeCompletedEncounters()
    {
        var encounterCount = CurrentRunConfig?.encounters?.Count ?? 0;
        if (CompletedEncounters == null)
        {
            CompletedEncounters = Enumerable.Repeat(false, encounterCount).ToList();
            return;
        }

        if (CompletedEncounters.Count < encounterCount)
        {
            CompletedEncounters.AddRange(Enumerable.Repeat(false, encounterCount - CompletedEncounters.Count));
        }
        else if (CompletedEncounters.Count > encounterCount)
        {
            CompletedEncounters = CompletedEncounters.Take(encounterCount).ToList();
        }
    }

    private static void ClearSaveMetadata()
    {
        CurrentSaveId = null;
        CurrentMode = "Story";
        CreatedAtUtcTicks = 0L;
        LastUpdatedAtUtcTicks = 0L;
    }
}

public static class RunSaveService
{
    private const string SavesFolderName = "SavedRuns";

    [Serializable]
    public sealed class PersistedRunData
    {
        public string SaveId;
        public string Mode;
        public long CreatedAtUtcTicks;
        public long UpdatedAtUtcTicks;
        public RunConfig RunConfig;
        public HeroRuntimeState Hero;
        public HeroDefinition SelectedHeroDefinition;
        public List<bool> CompletedEncounters = new();
        public int SelectedEncounterIndex = -1;
        public bool UsingFallbackData;
        public bool IsDefeated;
        public string StatusMessage;
        public string PendingLearnedMoveId;
    }

    public sealed class RunSaveSummary
    {
        public string SaveId;
        public string Mode;
        public string HeroName;
        public string HeroSpriteKey;
        public int CompletedEncounterCount;
        public int TotalEncounterCount;
        public bool IsComplete;
        public bool IsDefeated;
        public DateTime LastUpdatedUtc;
    }

    public static IReadOnlyList<RunSaveSummary> GetAllRuns()
    {
        EnsureSavesDirectory();

        var saves = new List<RunSaveSummary>();
        foreach (var filePath in Directory.GetFiles(GetSavesDirectory(), "*.json"))
        {
            if (!TryReadRun(filePath, out var runData) || runData == null)
            {
                continue;
            }

            saves.Add(BuildSummary(runData));
        }

        return saves
            .OrderByDescending(save => save.LastUpdatedUtc)
            .ToList();
    }

    public static bool SaveCurrentRun()
    {
        if (!RunSession.HasActiveRun || string.IsNullOrWhiteSpace(RunSession.CurrentSaveId))
        {
            return false;
        }

        EnsureSavesDirectory();

        var updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        RunSession.SetLastUpdatedAt(updatedAtUtcTicks);

        var runData = new PersistedRunData
        {
            SaveId = RunSession.CurrentSaveId,
            Mode = RunSession.CurrentMode,
            CreatedAtUtcTicks = RunSession.CreatedAtUtcTicks,
            UpdatedAtUtcTicks = updatedAtUtcTicks,
            RunConfig = RunSession.CurrentRunConfig,
            Hero = CloneHero(RuntimeHeroOrNull()),
            SelectedHeroDefinition = CloneHeroDefinition(RunSession.SelectedHeroDefinition),
            CompletedEncounters = RunSession.CompletedEncounters?.ToList() ?? new List<bool>(),
            SelectedEncounterIndex = RunSession.SelectedEncounterIndex,
            UsingFallbackData = RunSession.UsingFallbackData,
            IsDefeated = RunSession.IsDefeated,
            StatusMessage = RunSession.StatusMessage,
            PendingLearnedMoveId = RunSession.PendingLearnedMoveId
        };

        try
        {
            var json = JsonConvert.SerializeObject(runData, Formatting.Indented);
            File.WriteAllText(GetSaveFilePath(runData.SaveId), json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to save run '{RunSession.CurrentSaveId}': {exception.Message}");
            return false;
        }
    }

    public static bool TryLoadRun(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
        {
            return false;
        }

        return TryReadRun(GetSaveFilePath(saveId), out var runData) &&
               runData != null &&
               RunSession.RestoreSavedRun(runData);
    }

    public static bool DeleteRun(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
        {
            return false;
        }

        var filePath = GetSaveFilePath(saveId);
        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Delete(filePath);
        if (string.Equals(RunSession.CurrentSaveId, saveId, StringComparison.Ordinal))
        {
            RunSession.ClearActiveRun();
        }

        return true;
    }

    private static HeroRuntimeState RuntimeHeroOrNull()
    {
        return RunSession.Hero == null
            ? null
            : new HeroRuntimeState
            {
                HeroId = RunSession.Hero.HeroId,
                HeroName = RunSession.Hero.HeroName,
                Level = RunSession.Hero.Level,
                Xp = RunSession.Hero.Xp,
                Coins = RunSession.Hero.Coins,
                CurrentHp = RunSession.Hero.CurrentHp,
                BonusHealth = RunSession.Hero.BonusHealth,
                BonusAttack = RunSession.Hero.BonusAttack,
                BonusDefense = RunSession.Hero.BonusDefense,
                BonusMagic = RunSession.Hero.BonusMagic,
                EquippedMoves = RunSession.Hero.EquippedMoves?.ToList() ?? new List<string>(),
                KnownMoves = RunSession.Hero.KnownMoves != null
                    ? new HashSet<string>(RunSession.Hero.KnownMoves)
                    : new HashSet<string>(),
                MonsterKillCounts = RunSession.Hero.MonsterKillCounts != null
                    ? new Dictionary<string, int>(RunSession.Hero.MonsterKillCounts, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            };
    }

    private static HeroRuntimeState CloneHero(HeroRuntimeState hero)
    {
        return hero == null
            ? null
            : new HeroRuntimeState
            {
                HeroId = hero.HeroId,
                HeroName = hero.HeroName,
                Level = hero.Level,
                Xp = hero.Xp,
                Coins = hero.Coins,
                CurrentHp = hero.CurrentHp,
                BonusHealth = hero.BonusHealth,
                BonusAttack = hero.BonusAttack,
                BonusDefense = hero.BonusDefense,
                BonusMagic = hero.BonusMagic,
                EquippedMoves = hero.EquippedMoves?.ToList() ?? new List<string>(),
                KnownMoves = hero.KnownMoves != null
                    ? new HashSet<string>(hero.KnownMoves)
                    : new HashSet<string>(),
                MonsterKillCounts = hero.MonsterKillCounts != null
                    ? new Dictionary<string, int>(hero.MonsterKillCounts, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            };
    }

    private static HeroDefinition CloneHeroDefinition(HeroDefinition hero)
    {
        if (hero == null)
        {
            return null;
        }

        return new HeroDefinition
        {
            id = hero.id,
            name = hero.name,
            description = hero.description,
            portraitKey = hero.portraitKey,
            spriteKey = hero.spriteKey,
            baseStats = hero.baseStats?.Clone(),
            statsPerLevel = hero.statsPerLevel?.Clone(),
            moves = hero.moves?.ToList() ?? new List<string>()
        };
    }

    private static RunSaveSummary BuildSummary(PersistedRunData runData)
    {
        var totalEncounters = runData.RunConfig?.encounters?.Count ?? 0;
        var completedCount = runData.CompletedEncounters?.Count(completed => completed) ?? 0;
        var heroName = !string.IsNullOrWhiteSpace(runData.Hero?.HeroName)
            ? runData.Hero.HeroName
            : !string.IsNullOrWhiteSpace(runData.SelectedHeroDefinition?.name)
                ? runData.SelectedHeroDefinition.name
                : "Hero";

        return new RunSaveSummary
        {
            SaveId = runData.SaveId,
            Mode = string.IsNullOrWhiteSpace(runData.Mode) ? "Story" : runData.Mode,
            HeroName = heroName,
            HeroSpriteKey = runData.SelectedHeroDefinition?.spriteKey,
            CompletedEncounterCount = completedCount,
            TotalEncounterCount = totalEncounters,
            IsComplete = totalEncounters > 0 && completedCount >= totalEncounters,
            IsDefeated = runData.IsDefeated,
            LastUpdatedUtc = new DateTime(Math.Max(0L, runData.UpdatedAtUtcTicks), DateTimeKind.Utc)
        };
    }

    private static bool TryReadRun(string filePath, out PersistedRunData runData)
    {
        runData = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            runData = JsonConvert.DeserializeObject<PersistedRunData>(json);
            return runData != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to read run save '{filePath}': {exception.Message}");
            return false;
        }
    }

    private static void EnsureSavesDirectory()
    {
        Directory.CreateDirectory(GetSavesDirectory());
    }

    private static string GetSavesDirectory()
    {
        return Path.Combine(Application.persistentDataPath, SavesFolderName);
    }

    private static string GetSaveFilePath(string saveId)
    {
        return Path.Combine(GetSavesDirectory(), $"{saveId}.json");
    }
}
