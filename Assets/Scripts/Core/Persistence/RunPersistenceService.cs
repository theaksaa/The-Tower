using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TheTower;
using UnityEngine;

public static class RunPersistenceService
{
    private const string SavesFolderName = "SavedRuns";

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
            EndSceneShown = RunSession.EndSceneShown,
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
            Debug.LogError($"Failed to save run '{RunSession.CurrentSaveId}': {exception.Message}");
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

        try
        {
            File.Delete(filePath);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to delete run '{saveId}': {exception.Message}");
            return false;
        }

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
                EquippedItems = RunSession.Hero.EquippedItems?.ToList() ?? new List<string>(),
                InventoryItems = RunSession.Hero.InventoryItems?.ToList() ?? new List<string>(),
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
                EquippedItems = hero.EquippedItems?.ToList() ?? new List<string>(),
                InventoryItems = hero.InventoryItems?.ToList() ?? new List<string>(),
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
        return hero == null
            ? null
            : new HeroDefinition
            {
                id = hero.id,
                name = hero.name,
                description = hero.description,
                spriteKey = hero.spriteKey,
                portraitKey = hero.portraitKey,
                baseStats = hero.baseStats?.Clone(),
                statsPerLevel = hero.statsPerLevel?.Clone(),
                moves = hero.moves?.ToList() ?? new List<string>(),
                equippedItems = hero.equippedItems?.ToList() ?? new List<string>(),
                inventoryItems = hero.inventoryItems?.ToList() ?? new List<string>()
            };
    }

    private static RunSaveSummary BuildSummary(PersistedRunData runData)
    {
        var completedEncounterCount = runData.CompletedEncounters?.Count(flag => flag) ?? 0;
        var totalEncounterCount = runData.CompletedEncounters?.Count ?? runData.RunConfig?.encounters?.Count ?? 0;
        var heroName = string.IsNullOrWhiteSpace(runData.Hero?.HeroName)
            ? runData.SelectedHeroDefinition?.name ?? "Hero"
            : runData.Hero.HeroName;
        var heroSpriteKey = runData.SelectedHeroDefinition?.spriteKey;

        return new RunSaveSummary
        {
            SaveId = runData.SaveId,
            Mode = string.IsNullOrWhiteSpace(runData.Mode) ? "Story" : runData.Mode,
            HeroName = heroName,
            HeroSpriteKey = heroSpriteKey,
            CompletedEncounterCount = completedEncounterCount,
            TotalEncounterCount = totalEncounterCount,
            IsComplete = totalEncounterCount > 0 && completedEncounterCount >= totalEncounterCount,
            IsDefeated = runData.IsDefeated,
            LastUpdatedUtc = runData.UpdatedAtUtcTicks > 0
                ? new DateTime(runData.UpdatedAtUtcTicks, DateTimeKind.Utc)
                : new DateTime(runData.CreatedAtUtcTicks, DateTimeKind.Utc)
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
            Debug.LogError($"Failed to read run save '{filePath}': {exception.Message}");
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
