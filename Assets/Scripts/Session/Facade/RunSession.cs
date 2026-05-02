using System;
using System.Collections.Generic;
using System.Linq;
using TheTower;
using UnityEngine;

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
    public static string PendingMode { get; private set; } = "Story";
    public static long CreatedAtUtcTicks { get; private set; }
    public static long LastUpdatedAtUtcTicks { get; private set; }
    public static bool EndSceneShown { get; private set; }

    public static bool HasActiveRun => CurrentRunConfig != null && Hero != null && CompletedEncounters != null;
    public static bool HasPendingLearnedMove => !string.IsNullOrEmpty(PendingLearnedMoveId);
    public static bool IsEndlessMode => IsEndlessModeConfigured(CurrentRunConfig, CurrentMode);

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
        PendingMode = CurrentMode;
        CurrentSaveId = Guid.NewGuid().ToString("N");
        CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
        LastUpdatedAtUtcTicks = CreatedAtUtcTicks;
        EndSceneShown = false;
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
            EquippedItems = defaults.equippedItems?.ToList() ?? new List<string>(),
            InventoryItems = defaults.inventoryItems?.ToList() ?? new List<string>(),
            KnownMoves = new HashSet<string>(defaults.moves),
            MonsterKillCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        };

        CompletedEncounters = IsEndlessMode
            ? new List<bool> { false }
            : Enumerable.Repeat(false, runConfig.encounters.Count).ToList();
        SelectedEncounterIndex = GetFirstAvailableEncounterIndex();
        RunPersistenceService.SaveCurrentRun();
    }

    public static bool RestoreSavedRun(PersistedRunData runData)
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
        PendingMode = CurrentMode;
        CreatedAtUtcTicks = runData.CreatedAtUtcTicks > 0 ? runData.CreatedAtUtcTicks : DateTime.UtcNow.Ticks;
        LastUpdatedAtUtcTicks = runData.UpdatedAtUtcTicks > 0 ? runData.UpdatedAtUtcTicks : CreatedAtUtcTicks;
        EndSceneShown = runData.EndSceneShown;

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
            EquippedItems = runData.Hero.EquippedItems?.ToList() ?? new List<string>(),
            InventoryItems = runData.Hero.InventoryItems?.ToList() ?? new List<string>(),
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
        EndSceneShown = false;
        ClearSaveMetadata();
    }

    public static bool ShouldShowEndScene()
    {
        return !IsEndlessMode && !EndSceneShown;
    }

    public static void MarkEndSceneShown()
    {
        EndSceneShown = true;
    }

    public static void SetPendingMode(string mode)
    {
        PendingMode = string.IsNullOrWhiteSpace(mode) ? "Story" : mode.Trim();
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
                moves = runConfig.heroDefaults.moves?.ToList() ?? new List<string>(),
                equippedItems = runConfig.heroDefaults.equippedItems?.ToList() ?? new List<string>(),
                inventoryItems = runConfig.heroDefaults.inventoryItems?.ToList() ?? new List<string>()
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
        if (CurrentRunConfig == null)
        {
            SelectedEncounterIndex = -1;
            return;
        }

        var maxIndex = IsEndlessMode
            ? Mathf.Max(0, (CompletedEncounters?.Count ?? 1) - 1)
            : CurrentRunConfig.encounters.Count - 1;
        SelectedEncounterIndex = Mathf.Clamp(encounterIndex, 0, maxIndex);
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
        if (IsEndlessMode)
        {
            return HasActiveRun &&
                   !IsDefeated &&
                   encounterIndex >= 0 &&
                   encounterIndex < CompletedEncounters.Count &&
                   encounterIndex == GetFirstAvailableEncounterIndex();
        }

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

        if (IsEndlessMode)
        {
            return encounterIndex >= 0 &&
                   encounterIndex < CompletedEncounters.Count &&
                   encounterIndex == GetFirstAvailableEncounterIndex();
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
        if (CompletedEncounters == null || encounterIndex < 0)
        {
            return;
        }

        while (encounterIndex >= CompletedEncounters.Count)
        {
            CompletedEncounters.Add(false);
        }

        if (!IsEncounterCompleted(encounterIndex))
        {
            CompletedEncounters[encounterIndex] = true;
        }

        if (IsEndlessMode && CompletedEncounters.All(completed => completed))
        {
            CompletedEncounters.Add(false);
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

        var nextEncounterIndex = IsEndlessMode
            ? GetFirstAvailableEncounterIndex()
            : encounterIndex >= 0 && encounterIndex < CurrentRunConfig.encounters.Count
            ? encounterIndex
            : GetFirstAvailableEncounterIndex();

        if (nextEncounterIndex >= 0)
        {
            SelectedEncounterIndex = nextEncounterIndex;
        }

        StatusMessage = IsEndlessMode
            ? $"The hero recovered after encounter {encounterIndex + 1}. Continue to the next encounter."
            : $"The hero retreated from encounter {encounterIndex + 1}. Choose your next fight from the map.";
        RunPersistenceService.SaveCurrentRun();
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

    public static List<string> TransferMonsterEquippedItemsToHero(Monster monster)
    {
        var transferredItems = CollectNonEmptyItems(monster?.equippedItems);
        if (Hero == null || transferredItems.Count == 0)
        {
            return transferredItems;
        }

        Hero.InventoryItems ??= new List<string>();
        Hero.InventoryItems.AddRange(transferredItems);

        monster.equippedItems?.Clear();
        return transferredItems;
    }

    public static List<string> DropAllHeroItems()
    {
        if (Hero == null)
        {
            return new List<string>();
        }

        var droppedItems = CollectNonEmptyItems(Hero.EquippedItems);
        Hero.EquippedItems?.Clear();
        return droppedItems;
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
        RunPersistenceService.SaveCurrentRun();
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
        RunPersistenceService.SaveCurrentRun();
        return true;
    }

    public static bool TryPurchaseItem(string itemId, int coinCost, bool allowDuplicatePurchase = false)
    {
        if (Hero == null || string.IsNullOrWhiteSpace(itemId) || coinCost < 0)
        {
            return false;
        }

        itemId = itemId.Trim();
        if (Hero.Coins < coinCost || GetItem(itemId) == null)
        {
            return false;
        }

        Hero.EquippedItems ??= new List<string>();
        Hero.InventoryItems ??= new List<string>();

        var alreadyOwned = Hero.EquippedItems.Contains(itemId) || Hero.InventoryItems.Contains(itemId);
        if (alreadyOwned && !allowDuplicatePurchase)
        {
            return false;
        }

        Hero.InventoryItems.Add(itemId);
        Hero.Coins -= coinCost;

        var itemName = GetItem(itemId)?.name ?? itemId;
        StatusMessage = $"{GetHeroDisplayName()} bought {itemName}.";
        RunPersistenceService.SaveCurrentRun();
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
        if (IsEndlessMode)
        {
            return false;
        }

        return CompletedEncounters != null && CompletedEncounters.All(completed => completed);
    }

    public static int GetClearedEncounterCount()
    {
        return CompletedEncounters?.Count(completed => completed) ?? 0;
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

    public static ItemDefinition GetItem(string itemId)
    {
        if (CurrentRunConfig?.itemRegistry == null || string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        CurrentRunConfig.itemRegistry.TryGetValue(itemId, out var item);
        return item;
    }

    public static int GetNextLevelXpThreshold()
    {
        if (!HasActiveRun || Hero == null)
        {
            return -1;
        }

        return GetXpThresholdForLevel(Hero.Level + 1);
    }

    public static bool CanLevelUp()
    {
        var nextThreshold = GetNextLevelXpThreshold();
        return nextThreshold >= 0 && Hero.Xp >= nextThreshold;
    }

    public static int GetAvailableLevelUpCount()
    {
        if (!HasActiveRun || Hero == null)
        {
            return 0;
        }

        var count = 0;
        var simulatedLevel = Hero.Level;
        while (true)
        {
            var nextThreshold = GetXpThresholdForLevel(simulatedLevel + 1);
            if (nextThreshold < 0 || Hero.Xp < nextThreshold)
            {
                break;
            }

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
        RunPersistenceService.SaveCurrentRun();
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

    private static List<string> CollectNonEmptyItems(IEnumerable<string> itemIds)
    {
        if (itemIds == null)
        {
            return new List<string>();
        }

        return itemIds
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .ToList();
    }

    public static int GetXpThresholdForLevel(int level)
    {
        if (level <= 1)
        {
            return 0;
        }

        if (CurrentRunConfig == null)
        {
            return -1;
        }

        if (CurrentRunConfig.xpTable != null && CurrentRunConfig.xpTable.Count > 0)
        {
            var xpTableIndex = level - 1;
            return xpTableIndex >= 0 && xpTableIndex < CurrentRunConfig.xpTable.Count
                ? CurrentRunConfig.xpTable[xpTableIndex]
                : -1;
        }

        var progression = CurrentRunConfig.levelProgression;
        if (progression == null)
        {
            return -1;
        }

        var baseXp = Mathf.Max(0, progression.baseXpForNextLevel);
        var additionalXp = Mathf.Max(0, progression.additionalXpPerLevel);
        var increments = level - 1L;
        var additionalSteps = level - 2L;
        var totalXp = increments * baseXp + (additionalSteps * increments * additionalXp) / 2L;
        return totalXp > int.MaxValue ? int.MaxValue : (int)totalXp;
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

    private static HeroDefinition ResolveSavedHeroDefinition(PersistedRunData runData)
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
                moves = runData.SelectedHeroDefinition.moves?.ToList() ?? new List<string>(),
                equippedItems = runData.SelectedHeroDefinition.equippedItems?.ToList() ?? new List<string>(),
                inventoryItems = runData.SelectedHeroDefinition.inventoryItems?.ToList() ?? new List<string>()
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
                moves = matchingHero.moves?.ToList() ?? new List<string>(),
                equippedItems = matchingHero.equippedItems?.ToList() ?? new List<string>(),
                inventoryItems = matchingHero.inventoryItems?.ToList() ?? new List<string>()
            }
            : null;
    }

    private static void NormalizeCompletedEncounters()
    {
        if (IsEndlessMode)
        {
            if (CompletedEncounters == null || CompletedEncounters.Count == 0)
            {
                CompletedEncounters = new List<bool> { false };
                return;
            }

            if (CompletedEncounters.All(completed => completed))
            {
                CompletedEncounters.Add(false);
            }

            return;
        }

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
        PendingMode = "Story";
        CreatedAtUtcTicks = 0L;
        LastUpdatedAtUtcTicks = 0L;
        EndSceneShown = false;
    }

    private static bool IsEndlessModeConfigured(RunConfig runConfig, string mode)
    {
        return string.Equals(mode, "Endless", StringComparison.OrdinalIgnoreCase) &&
               runConfig?.endlessMode?.enabled == true;
    }
}

