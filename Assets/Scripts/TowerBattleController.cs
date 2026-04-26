using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TheTower;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class TowerBattleController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Turn Timing")]
    [SerializeField] private float heroAttackDelay = 1f;
    [SerializeField] private float monsterAttackDelay = 1f;
    [SerializeField] private float nextTurnDelay = 0.75f;

    private readonly string[] buttonObjectNames =
    {
        "SlashButton",
        "FireballButton",
        "Heavy StrikeButton",
        "Quick JabButton"
    };

    private readonly List<Button> moveButtons = new();
    private readonly List<Text> moveButtonLabels = new();
    private readonly List<ActiveModifier> heroModifiers = new();
    private readonly List<ActiveModifier> monsterModifiers = new();

    private TextMesh heroLabel;
    private TextMesh monsterLabel;
    private TextMesh heroHpWorldText;
    private TextMesh monsterHpWorldText;

    private Text statusText;
    private Text progressText;
    private Text effectsText;

    private RunConfig runConfig;
    private Monster currentMonster;
    private HeroRuntime hero;
    private int encounterIndex;
    private int currentMonsterHp;
    private int turnNumber = 1;
    private string heroLastMoveId;
    private bool isBusy;
    private bool restartReady;
    private bool usingFallbackData;

    private readonly List<string> monsterMoveHistory = new();

    private void Start()
    {
        AutoBindScene();
        CreateRuntimeHud();
        StartCoroutine(BootstrapRun());
    }

    public void UseMoveSlot(int slot)
    {
        if (restartReady)
        {
            StartCoroutine(BootstrapRun());
            return;
        }

        if (isBusy || hero == null || slot < 0 || slot >= hero.EquippedMoves.Count)
        {
            return;
        }

        StartCoroutine(ResolveTurnSequence(hero.EquippedMoves[slot]));
    }

    private IEnumerator BootstrapRun()
    {
        isBusy = true;
        restartReady = false;
        SetButtonsInteractable(false);
        SetStatus("Loading run config...");

        yield return LoadRunConfig();

        if (runConfig == null)
        {
            SetStatus("Unable to load run config.");
            PrepareRestart("Retry loading run");
            yield break;
        }

        InitializeHero();
        encounterIndex = 0;
        StartEncounter(encounterIndex);
        isBusy = false;
    }

    private void AutoBindScene()
    {
        heroLabel = FindTextMesh("HeroLabel");
        monsterLabel = FindTextMesh("MonsterLabel");
        heroHpWorldText = FindTextMesh("HeroHPText");
        monsterHpWorldText = FindTextMesh("MonsterHPText");

        moveButtons.Clear();
        moveButtonLabels.Clear();

        for (var index = 0; index < buttonObjectNames.Length; index++)
        {
            var buttonObject = GameObject.Find(buttonObjectNames[index]);
            if (buttonObject == null)
            {
                Debug.LogWarning($"Missing button object '{buttonObjectNames[index]}'.");
                continue;
            }

            var button = buttonObject.GetComponent<Button>();
            var label = buttonObject.GetComponentInChildren<Text>();
            if (button == null || label == null)
            {
                Debug.LogWarning($"Button '{buttonObjectNames[index]}' is missing a Button or Text component.");
                continue;
            }

            var capturedIndex = index;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => UseMoveSlot(capturedIndex));

            moveButtons.Add(button);
            moveButtonLabels.Add(label);
        }
    }

    private void CreateRuntimeHud()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("TowerBattleController must be attached to the BattleUI canvas.");
            return;
        }

        var font = moveButtonLabels.FirstOrDefault(label => label != null)?.font;
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        statusText = CreateUiText("StatusText", canvas.transform, font, 28, TextAnchor.UpperCenter);
        progressText = CreateUiText("ProgressText", canvas.transform, font, 22, TextAnchor.UpperLeft);
        effectsText = CreateUiText("EffectsText", canvas.transform, font, 20, TextAnchor.UpperRight);

        ConfigureTextRect(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(820f, 120f));
        ConfigureTextRect(progressText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(420f, 120f));
        ConfigureTextRect(effectsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -24f), new Vector2(420f, 160f));
    }

    private IEnumerator LoadRunConfig()
    {
        runConfig = null;
        usingFallbackData = false;

        using var request = UnityWebRequest.Get($"{baseUrl}/run/config");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                runConfig = JsonConvert.DeserializeObject<RunConfig>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to deserialize run config: {exception}");
            }
        }

        if (runConfig != null)
        {
            yield break;
        }

        if (!useLocalFallbackIfApiFails)
        {
            yield break;
        }

        usingFallbackData = true;
        runConfig = BuildFallbackRunConfig();
    }

    private void InitializeHero()
    {
        heroModifiers.Clear();
        hero = new HeroRuntime
        {
            Level = 1,
            Xp = 0,
            CurrentHp = Mathf.Min(100, runConfig.heroDefaults.baseStats.health),
            EquippedMoves = runConfig.heroDefaults.moves.Take(4).ToList(),
            KnownMoves = new HashSet<string>(runConfig.heroDefaults.moves)
        };
    }

    private void StartEncounter(int index)
    {
        if (index >= runConfig.encounters.Count)
        {
            CompleteRun();
            return;
        }

        encounterIndex = index;
        currentMonster = runConfig.encounters[index];
        currentMonsterHp = currentMonster.stats.health;
        monsterModifiers.Clear();
        heroModifiers.Clear();
        monsterMoveHistory.Clear();
        heroLastMoveId = null;
        turnNumber = 1;
        restartReady = false;

        var intro = usingFallbackData
            ? "Offline fallback data loaded."
            : $"Run {runConfig.runId[..8]} active.";
        SetStatus($"{intro}\nEncounter {encounterIndex + 1}/{runConfig.encounters.Count}: {currentMonster.name}\n{currentMonster.description}");
        RefreshAllUi();
        SetButtonsInteractable(true);
    }

    private IEnumerator ResolveTurnSequence(string moveId)
    {
        var move = GetMove(moveId);
        if (move == null)
        {
            SetStatus($"Missing move '{moveId}'.");
            yield break;
        }

        isBusy = true;
        SetButtonsInteractable(false);
        SetStatus($"Hero prepares {move.name}...");
        RefreshAllUi();

        if (heroAttackDelay > 0f)
        {
            yield return new WaitForSeconds(heroAttackDelay);
        }

        var heroTurnSummary = ResolveMove(move, actorIsHero: true);
        heroLastMoveId = move.id;
        SetStatus(heroTurnSummary);
        RefreshAllUi();

        if (currentMonsterHp <= 0)
        {
            HandleVictory(heroTurnSummary);
            isBusy = false;
            yield break;
        }

        SetStatus($"{heroTurnSummary}\n{currentMonster.name} prepares an attack...");

        if (monsterAttackDelay > 0f)
        {
            yield return new WaitForSeconds(monsterAttackDelay);
        }

        var monsterResponse = FetchMonsterMove();

        if (monsterResponse == null)
        {
            var fallbackMoveId = ChooseFallbackMonsterMoveId();
            monsterResponse = new MonsterMoveResponse
            {
                moveId = fallbackMoveId,
                move = GetMove(fallbackMoveId)
            };
        }

        var monsterTurnSummary = ResolveMove(monsterResponse.move, actorIsHero: false);
        monsterMoveHistory.Add(monsterResponse.move.id);
        turnNumber++;
        RefreshAllUi();

        if (hero.CurrentHp <= 0)
        {
            HandleDefeat(heroTurnSummary, monsterTurnSummary);
            isBusy = false;
            yield break;
        }

        SetStatus($"{heroTurnSummary}\n{monsterTurnSummary}");

        if (nextTurnDelay > 0f)
        {
            yield return new WaitForSeconds(nextTurnDelay);
        }

        isBusy = false;
        SetStatus($"{heroTurnSummary}\n{monsterTurnSummary}\nChoose your next move.");
        SetButtonsInteractable(true);
        RefreshAllUi();
    }

    private MonsterMoveResponse FetchMonsterMove()
    {
        var payload = new BattleState
        {
            monsterId = currentMonster.id,
            monsterCurrentHp = currentMonsterHp,
            heroCurrentHp = hero.CurrentHp,
            heroMaxHp = GetHeroBaseStats().health,
            heroStats = GetEffectiveHeroStats(),
            turnNumber = turnNumber,
            heroLastMoveId = heroLastMoveId,
            monsterMoveHistory = new List<string>(monsterMoveHistory)
        };

        var json = JsonConvert.SerializeObject(payload);
        var encodedState = Uri.EscapeDataString(json);
        var url = $"{baseUrl}/battle/monster-move?state={encodedState}";

        try
        {
            using var client = new System.Net.WebClient();
            var responseText = client.DownloadString(url);
            var response = JsonConvert.DeserializeObject<MonsterMoveResponse>(responseText);
            response.move ??= GetMove(response.moveId);
            return response;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Monster move request failed: {exception.Message}");
            return null;
        }
    }

    private string ResolveMove(Move move, bool actorIsHero)
    {
        if (move == null)
        {
            return actorIsHero ? "Hero hesitated." : $"{currentMonster.name} hesitated.";
        }

        var actorName = actorIsHero ? "Hero" : currentMonster.name;
        var sourceStats = actorIsHero ? GetEffectiveHeroStats() : GetEffectiveMonsterStats();
        var targetStats = actorIsHero ? GetEffectiveMonsterStats() : GetEffectiveHeroStats();

        if (move.hpCost.HasValue && move.hpCost.Value > 0)
        {
            ApplyDamage(actorIsHero, move.hpCost.Value);
        }

        string summary;

        switch (move.effect)
        {
            case "damage":
            {
                var damage = ComputeDamage(move, sourceStats, targetStats);
                ApplyDamage(!actorIsHero, damage);
                summary = $"{actorName} used {move.name} for {damage} damage.";
                break;
            }
            case "heal":
            {
                var healAmount = ComputeHeal(move, sourceStats);
                ApplyHeal(actorIsHero, healAmount);
                summary = $"{actorName} used {move.name} and healed {healAmount} HP.";
                break;
            }
            case "drain":
            {
                var drainAmount = ComputeDamage(move, sourceStats, targetStats);
                ApplyDamage(!actorIsHero, drainAmount);
                ApplyHeal(actorIsHero, drainAmount);
                summary = $"{actorName} drained {drainAmount} HP with {move.name}.";
                break;
            }
            case "stat_modifier":
            {
                var targetIsHero = move.target == "self" ? actorIsHero : !actorIsHero;
                ApplyModifier(targetIsHero, move.statModifier);
                summary = $"{actorName} used {move.name}. {BuildModifierSummary(targetIsHero, move.statModifier)}";
                break;
            }
            case "damage_and_stat_modifier":
            {
                var hybridDamage = ComputeDamage(move, sourceStats, targetStats);
                ApplyDamage(!actorIsHero, hybridDamage);
                var modifierTargetIsHero = move.target == "self" ? actorIsHero : !actorIsHero;
                ApplyModifier(modifierTargetIsHero, move.statModifier);
                summary = $"{actorName} used {move.name} for {hybridDamage} damage. {BuildModifierSummary(modifierTargetIsHero, move.statModifier)}";
                break;
            }
            default:
                summary = $"{actorName} used {move.name}.";
                break;
        }

        ConsumeMoveStatModifiers(actorIsHero, move);
        return summary;
    }

    private int ComputeDamage(Move move, Stats attacker, Stats defender)
    {
        var attackStat = move.type == "magic" ? attacker.magic : attacker.attack;
        var defenseStat = move.type == "magic" ? defender.magic : defender.defense;
        var raw = move.basePower + attackStat * move.statMultiplier - defenseStat * 0.55f;
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    private int ComputeHeal(Move move, Stats sourceStats)
    {
        var stat = move.type == "magic" ? sourceStats.magic : sourceStats.attack;
        var raw = move.basePower + stat * move.statMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    private void ApplyDamage(bool targetIsHero, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (targetIsHero)
        {
            hero.CurrentHp = Mathf.Max(0, hero.CurrentHp - amount);
            ConsumeDefenseModifiers(targetIsHero: true);
            return;
        }

        currentMonsterHp = Mathf.Max(0, currentMonsterHp - amount);
        ConsumeDefenseModifiers(targetIsHero: false);
    }

    private void ApplyHeal(bool targetIsHero, int amount)
    {
        if (targetIsHero)
        {
            hero.CurrentHp = Mathf.Min(GetHeroBaseStats().health, hero.CurrentHp + amount);
            return;
        }

        currentMonsterHp = Mathf.Min(currentMonster.stats.health, currentMonsterHp + amount);
    }

    private void ApplyModifier(bool targetIsHero, StatModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        var collection = targetIsHero ? heroModifiers : monsterModifiers;
        collection.Add(new ActiveModifier
        {
            Stat = modifier.stat,
            Value = modifier.value,
            RemainingUses = Mathf.Max(1, modifier.durationTurns),
            SkipNextConsumption = ShouldDelayFirstConsumption(modifier.stat)
        });
    }

    private static bool ShouldDelayFirstConsumption(string stat)
    {
        return string.Equals(stat, "attack", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stat, "magic", StringComparison.OrdinalIgnoreCase);
    }

    private void ConsumeMoveStatModifiers(bool actorIsHero, Move move)
    {
        var stat = GetScalingStat(move);
        if (string.IsNullOrEmpty(stat))
        {
            return;
        }

        ConsumeModifierUse(actorIsHero ? heroModifiers : monsterModifiers, stat);
    }

    private static string GetScalingStat(Move move)
    {
        if (move == null)
        {
            return null;
        }

        return move.type switch
        {
            "physical" => "attack",
            "magic" => "magic",
            _ => null
        };
    }

    private void ConsumeDefenseModifiers(bool targetIsHero)
    {
        ConsumeModifierUse(targetIsHero ? heroModifiers : monsterModifiers, "defense");
    }

    private static void ConsumeModifierUse(List<ActiveModifier> modifiers, string stat)
    {
        for (var index = modifiers.Count - 1; index >= 0; index--)
        {
            if (!string.Equals(modifiers[index].Stat, stat, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (modifiers[index].SkipNextConsumption)
            {
                modifiers[index].SkipNextConsumption = false;
                continue;
            }

            modifiers[index].RemainingUses--;
            if (modifiers[index].RemainingUses <= 0)
            {
                modifiers.RemoveAt(index);
            }
        }
    }

    private void HandleVictory(string heroTurnSummary)
    {
        var rewardSummary = AwardVictoryRewards();
        RefreshAllUi();

        if (encounterIndex + 1 >= runConfig.encounters.Count)
        {
            SetStatus($"{heroTurnSummary}\n{currentMonster.name} was defeated.\n{rewardSummary}\nThe tower is clear.");
            PrepareRestart("Restart run");
            return;
        }

        SetStatus($"{heroTurnSummary}\n{currentMonster.name} was defeated.\n{rewardSummary}");
        StartEncounter(encounterIndex + 1);
    }

    private void HandleDefeat(string heroTurnSummary, string monsterTurnSummary)
    {
        RefreshAllUi();
        SetStatus($"{heroTurnSummary}\n{monsterTurnSummary}\nThe hero fell on encounter {encounterIndex + 1}. Try again.");
        PrepareRestart("Restart run");
    }

    private string AwardVictoryRewards()
    {
        hero.Xp += currentMonster.xpReward;
        var rewardParts = new List<string> { $"+{currentMonster.xpReward} XP" };

        while (hero.Level < runConfig.xpTable.Count && hero.Xp >= runConfig.xpTable[hero.Level])
        {
            hero.Level++;
            hero.CurrentHp = Mathf.Min(GetHeroBaseStats().health, hero.CurrentHp + runConfig.heroDefaults.statsPerLevel.health);
            rewardParts.Add($"Level up to {hero.Level}");
        }

        var learnedMove = PickLearnableMove(currentMonster);
        if (!string.IsNullOrEmpty(learnedMove))
        {
            rewardParts.Add(EquipLearnedMove(learnedMove));
        }

        return string.Join(" | ", rewardParts);
    }

    private string PickLearnableMove(Monster monster)
    {
        foreach (var moveId in monster.learnableMoves)
        {
            if (hero.KnownMoves.Add(moveId))
            {
                return moveId;
            }
        }

        return null;
    }

    private string EquipLearnedMove(string newMoveId)
    {
        var newMove = GetMove(newMoveId);
        if (newMove == null)
        {
            return "Learned an unknown move";
        }

        if (hero.EquippedMoves.Contains(newMoveId))
        {
            return $"{newMove.name} was already equipped";
        }

        if (hero.EquippedMoves.Count < 4)
        {
            hero.EquippedMoves.Add(newMoveId);
            return $"Learned {newMove.name}";
        }

        var weakestIndex = 0;
        var weakestScore = ScoreMove(GetMove(hero.EquippedMoves[0]));

        for (var index = 1; index < hero.EquippedMoves.Count; index++)
        {
            var candidateScore = ScoreMove(GetMove(hero.EquippedMoves[index]));
            if (candidateScore < weakestScore)
            {
                weakestScore = candidateScore;
                weakestIndex = index;
            }
        }

        var learnedScore = ScoreMove(newMove);
        if (learnedScore <= weakestScore)
        {
            return $"Learned {newMove.name}, but kept the current loadout";
        }

        var replacedMoveName = GetMove(hero.EquippedMoves[weakestIndex])?.name ?? hero.EquippedMoves[weakestIndex];
        hero.EquippedMoves[weakestIndex] = newMoveId;
        return $"Swapped {replacedMoveName} for {newMove.name}";
    }

    private float ScoreMove(Move move)
    {
        if (move == null)
        {
            return 0f;
        }

        var modifierValue = move.statModifier == null ? 0f : Mathf.Abs(move.statModifier.value) * 2f;
        var sustainValue = move.effect == "heal" || move.effect == "drain" ? 12f : 0f;
        return move.basePower + move.statMultiplier * 10f + modifierValue + sustainValue;
    }

    private void CompleteRun()
    {
        PrepareRestart("Restart run");
    }

    private void PrepareRestart(string label)
    {
        restartReady = true;
        isBusy = false;

        for (var index = 0; index < moveButtons.Count; index++)
        {
            moveButtons[index].interactable = index == 0;
            if (moveButtonLabels[index] != null)
            {
                moveButtonLabels[index].text = index == 0 ? label : "-";
            }
        }
    }

    private void RefreshAllUi()
    {
        RefreshLabels();
        RefreshButtons();
        RefreshProgress();
        RefreshEffects();
    }

    private void RefreshLabels()
    {
        if (heroLabel != null)
        {
            heroLabel.text = $"Hero Lv.{hero?.Level ?? 1}";
        }

        if (monsterLabel != null)
        {
            monsterLabel.text = currentMonster != null ? currentMonster.name : "Monster";
        }

        if (heroHpWorldText != null && hero != null)
        {
            heroHpWorldText.text = $"HP: {hero.CurrentHp}/{GetHeroBaseStats().health}";
        }

        if (monsterHpWorldText != null)
        {
            var maxMonsterHp = currentMonster?.stats.health ?? 0;
            monsterHpWorldText.text = $"HP: {currentMonsterHp}/{maxMonsterHp}";
        }
    }

    private void RefreshButtons()
    {
        for (var index = 0; index < moveButtons.Count; index++)
        {
            var hasMove = hero != null && index < hero.EquippedMoves.Count;
            moveButtons[index].interactable = !isBusy && !restartReady && hasMove;
            moveButtonLabels[index].text = hasMove ? GetMove(hero.EquippedMoves[index])?.name ?? hero.EquippedMoves[index] : "-";
        }
    }

    private void RefreshProgress()
    {
        if (progressText == null || hero == null || runConfig == null)
        {
            return;
        }

        var nextThreshold = hero.Level < runConfig.xpTable.Count ? runConfig.xpTable[hero.Level] : -1;
        var heroStats = GetEffectiveHeroStats();
        var thresholdText = nextThreshold >= 0 ? $"{hero.Xp}/{nextThreshold}" : $"{hero.Xp}/MAX";
        progressText.text =
            $"Run: {encounterIndex + 1}/{runConfig.encounters.Count}\n" +
            $"XP: {thresholdText}\n" +
            $"ATK {heroStats.attack} DEF {heroStats.defense} MAG {heroStats.magic}";
    }

    private void RefreshEffects()
    {
        if (effectsText == null)
        {
            return;
        }

        var heroEffects = heroModifiers.Count == 0
            ? "Hero: none"
            : "Hero: " + string.Join(", ", heroModifiers.Select(FormatModifier));

        var monsterEffects = monsterModifiers.Count == 0
            ? "Monster: none"
            : "Monster: " + string.Join(", ", monsterModifiers.Select(FormatModifier));

        effectsText.text =
            $"Turn {turnNumber}\n" +
            $"{heroEffects}\n" +
            $"{monsterEffects}";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (var button in moveButtons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }

    private Move GetMove(string moveId)
    {
        if (runConfig?.moveRegistry == null || string.IsNullOrEmpty(moveId))
        {
            return null;
        }

        runConfig.moveRegistry.TryGetValue(moveId, out var move);
        return move;
    }

    private Stats GetHeroBaseStats()
    {
        var baseStats = runConfig.heroDefaults.baseStats.Clone();
        var levelBonus = Mathf.Max(0, hero.Level - 1);

        baseStats.health += runConfig.heroDefaults.statsPerLevel.health * levelBonus;
        baseStats.attack += runConfig.heroDefaults.statsPerLevel.attack * levelBonus;
        baseStats.defense += runConfig.heroDefaults.statsPerLevel.defense * levelBonus;
        baseStats.magic += runConfig.heroDefaults.statsPerLevel.magic * levelBonus;
        return baseStats;
    }

    private Stats GetEffectiveHeroStats()
    {
        return ApplyModifiers(GetHeroBaseStats(), heroModifiers);
    }

    private Stats GetEffectiveMonsterStats()
    {
        return ApplyModifiers(currentMonster.stats.Clone(), monsterModifiers);
    }

    private static Stats ApplyModifiers(Stats stats, IEnumerable<ActiveModifier> modifiers)
    {
        foreach (var modifier in modifiers)
        {
            switch (modifier.Stat)
            {
                case "attack":
                    stats.attack += modifier.Value;
                    break;
                case "defense":
                    stats.defense += modifier.Value;
                    break;
                case "magic":
                    stats.magic += modifier.Value;
                    break;
            }
        }

        stats.attack = Mathf.Max(1, stats.attack);
        stats.defense = Mathf.Max(1, stats.defense);
        stats.magic = Mathf.Max(1, stats.magic);
        return stats;
    }

    private string ChooseFallbackMonsterMoveId()
    {
        var availableMoves = currentMonster.moves
            .Select(GetMove)
            .Where(move => move != null)
            .ToList();

        if (availableMoves.Count == 0)
        {
            return currentMonster.moves.FirstOrDefault();
        }

        if (currentMonsterHp <= currentMonster.stats.health * 0.4f)
        {
            var sustainMove = availableMoves.FirstOrDefault(move => move.effect is "heal" or "drain" || move.target == "self");
            if (sustainMove != null)
            {
                return sustainMove.id;
            }
        }

        var filteredMoves = availableMoves.Where(move => move.id != monsterMoveHistory.LastOrDefault()).ToList();
        var pool = filteredMoves.Count > 0 ? filteredMoves : availableMoves;
        return pool[Random.Range(0, pool.Count)].id;
    }

    private string BuildModifierSummary(bool targetIsHero, StatModifier modifier)
    {
        if (modifier == null)
        {
            return "No lasting effect.";
        }

        var who = targetIsHero ? "Hero" : currentMonster.name;
        var direction = modifier.value >= 0 ? "gained" : "lost";
        var timing = ShouldDelayFirstConsumption(modifier.stat)
            ? "starting from the next action"
            : "starting immediately";
        return $"{who} {direction} {Mathf.Abs(modifier.value)} {modifier.stat} for {modifier.durationTurns} uses, {timing}.";
    }

    private static string FormatModifier(ActiveModifier modifier)
    {
        var sign = modifier.Value >= 0 ? "+" : string.Empty;
        var timing = modifier.SkipNextConsumption ? ", next use not counted" : string.Empty;
        return $"{modifier.Stat} {sign}{modifier.Value} ({modifier.RemainingUses} uses{timing})";
    }

    private TextMesh FindTextMesh(string objectName)
    {
        var gameObject = GameObject.Find(objectName);
        return gameObject != null ? gameObject.GetComponent<TextMesh>() : null;
    }

    private static Text CreateUiText(string name, Transform parent, Font font, int fontSize, TextAnchor anchor)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        gameObject.transform.SetParent(parent, false);

        var text = gameObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.black;
        return text;
    }

    private static void ConfigureTextRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private RunConfig BuildFallbackRunConfig()
    {
        const string fallbackJson = @"
{
  ""runId"": ""offline-fallback"",
  ""encounters"": [
    {
      ""id"": ""training_dummy"",
      ""name"": ""Training Dummy"",
      ""description"": ""A stand-in opponent used when the local API is unavailable."",
      ""stats"": { ""health"": 60, ""attack"": 10, ""defense"": 6, ""magic"": 4 },
      ""moves"": [""rusty_blade"", ""dirty_kick""],
      ""learnableMoves"": [""rusty_blade"", ""dirty_kick""],
      ""xpReward"": 60,
      ""spriteKey"": ""training_dummy""
    },
    {
      ""id"": ""emberscale"",
      ""name"": ""Emberscale"",
      ""description"": ""A young drake with a nasty flame breath."",
      ""stats"": { ""health"": 110, ""attack"": 16, ""defense"": 12, ""magic"": 16 },
      ""moves"": [""flame_breath"", ""claw_swipe"", ""dragon_scales""],
      ""learnableMoves"": [""flame_breath"", ""dragon_scales""],
      ""xpReward"": 140,
      ""spriteKey"": ""emberscale""
    }
  ],
    ""heroDefaults"": {
    ""baseStats"": { ""health"": 100, ""attack"": 18, ""defense"": 10, ""magic"": 12 },
    ""statsPerLevel"": { ""health"": 18, ""attack"": 4, ""defense"": 3, ""magic"": 3 },
    ""moves"": [""slash"", ""shield_up"", ""battle_cry"", ""second_wind""]
  },
  ""xpTable"": [0, 100, 250, 450, 700],
  ""moveRegistry"": {
    ""slash"": { ""id"": ""slash"", ""name"": ""Slash"", ""description"": ""Moderate physical damage."", ""type"": ""physical"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 15, ""statMultiplier"": 1.0, ""statModifier"": null, ""hpCost"": null },
    ""shield_up"": { ""id"": ""shield_up"", ""name"": ""Shield Up"", ""description"": ""Raises the user's Defense for two turns."", ""type"": ""status"", ""effect"": ""stat_modifier"", ""target"": ""self"", ""basePower"": 0, ""statMultiplier"": 0, ""statModifier"": { ""stat"": ""defense"", ""value"": 6, ""durationTurns"": 2 }, ""hpCost"": null },
    ""battle_cry"": { ""id"": ""battle_cry"", ""name"": ""Battle Cry"", ""description"": ""Raises the user's Attack for two turns."", ""type"": ""status"", ""effect"": ""stat_modifier"", ""target"": ""self"", ""basePower"": 0, ""statMultiplier"": 0, ""statModifier"": { ""stat"": ""attack"", ""value"": 6, ""durationTurns"": 2 }, ""hpCost"": null },
    ""second_wind"": { ""id"": ""second_wind"", ""name"": ""Second Wind"", ""description"": ""Moderate heal that scales with Magic."", ""type"": ""magic"", ""effect"": ""heal"", ""target"": ""self"", ""basePower"": 18, ""statMultiplier"": 0.8, ""statModifier"": null, ""hpCost"": null },
    ""rusty_blade"": { ""id"": ""rusty_blade"", ""name"": ""Rusty Blade"", ""description"": ""Moderate physical damage."", ""type"": ""physical"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 14, ""statMultiplier"": 1, ""statModifier"": null, ""hpCost"": null },
    ""dirty_kick"": { ""id"": ""dirty_kick"", ""name"": ""Dirty Kick"", ""description"": ""Light damage and lowers the target's Defense."", ""type"": ""physical"", ""effect"": ""damage_and_stat_modifier"", ""target"": ""opponent"", ""basePower"": 8, ""statMultiplier"": 0.7, ""statModifier"": { ""stat"": ""defense"", ""value"": -5, ""durationTurns"": 2 }, ""hpCost"": null },
    ""flame_breath"": { ""id"": ""flame_breath"", ""name"": ""Flame Breath"", ""description"": ""Heavy magic damage."", ""type"": ""magic"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 30, ""statMultiplier"": 1.3, ""statModifier"": null, ""hpCost"": null },
    ""claw_swipe"": { ""id"": ""claw_swipe"", ""name"": ""Claw Swipe"", ""description"": ""Moderate physical damage."", ""type"": ""physical"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 18, ""statMultiplier"": 1, ""statModifier"": null, ""hpCost"": null },
    ""dragon_scales"": { ""id"": ""dragon_scales"", ""name"": ""Dragon Scales"", ""description"": ""Raises the user's Defense for two turns."", ""type"": ""status"", ""effect"": ""stat_modifier"", ""target"": ""self"", ""basePower"": 0, ""statMultiplier"": 0, ""statModifier"": { ""stat"": ""defense"", ""value"": 8, ""durationTurns"": 2 }, ""hpCost"": null }
  }
}";

        return JsonConvert.DeserializeObject<RunConfig>(fallbackJson);
    }

    private sealed class HeroRuntime
    {
        public int Level;
        public int Xp;
        public int CurrentHp;
        public List<string> EquippedMoves;
        public HashSet<string> KnownMoves;
    }

    private sealed class ActiveModifier
    {
        public string Stat;
        public int Value;
        public int RemainingUses;
        public bool SkipNextConsumption;
    }
}
