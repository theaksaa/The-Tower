using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TheTower;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class TowerBattleController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Navigation")]
    [SerializeField] private string overviewSceneName = "RunOverviewScene";

    [Header("Turn Timing")]
    [SerializeField] private float heroAttackDelay = 1f;
    [SerializeField] private float monsterAttackDelay = 1f;
    [SerializeField] private float nextTurnDelay = 0.75f;

    [Header("BattleScene2 UI")]
    [SerializeField] private Sprite battleScene2HoverSelectorTopLeft;
    [SerializeField] private Sprite battleScene2HoverSelectorTopRight;
    [SerializeField] private Sprite battleScene2HoverSelectorBottomLeft;
    [SerializeField] private Sprite battleScene2HoverSelectorBottomRight;
    [SerializeField] private float battleScene2HoverSelectorCornerSize = 24f;
    [SerializeField] private Sprite battleScene2EndPanelPressedButtonSprite;
    [SerializeField] private Color battleScene2EndPanelHoverTint = new(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color battleScene2EndPanelPressedTint = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Vector2 battleScene2EndPanelPressedTextOffset = new(0f, -6f);

    private readonly string[] buttonObjectNames =
    {
        "SlashButton",
        "FireballButton",
        "Heavy StrikeButton",
        "Quick JabButton"
    };

    private readonly List<Button> moveButtons = new();
    private readonly List<Text> moveButtonLabels = new();
    private readonly List<Image> moveButtonIcons = new();
    private readonly List<GameObject> moveHoverSelectorRoots = new();
    private readonly List<ActiveModifier> heroModifiers = new();
    private readonly List<ActiveModifier> monsterModifiers = new();

    private TextMesh heroLabel;
    private TextMesh monsterLabel;
    private TextMesh heroHpWorldText;
    private TextMesh monsterHpWorldText;

    private Text statusText;
    private Text progressText;
    private Text effectsText;
    private TMP_Text levelText;
    private TMP_Text heroAttackText;
    private TMP_Text heroDefenseText;
    private TMP_Text heroMagicText;
    private TMP_Text monsterAttackText;
    private TMP_Text monsterDefenseText;
    private TMP_Text monsterMagicText;
    private Image heroHealthBarImage;
    private Image monsterHealthBarImage;
    private Image heroEffectAttackIcon;
    private Image heroEffectDefenseIcon;
    private Image heroEffectMagicIcon;
    private Image monsterEffectAttackIcon;
    private Image monsterEffectDefenseIcon;
    private Image monsterEffectMagicIcon;
    private GameObject endPanelRoot;
    private GameObject defeatPanelRoot;
    private TMP_Text endPanelTitleText;
    private TMP_Text reward1Text;
    private TMP_Text reward2Text;
    private TMP_Text reward3Text;
    private GameObject reward2Root;
    private Image reward1Icon;
    private Image reward2Icon;
    private Image reward3Icon;
    private Button reviveButton;
    private Button continueButton;
    private Button defeatReviveButton;
    private Button defeatMapButton;

    private RunConfig runConfig;
    private Monster currentMonster;
    private HeroRuntimeState hero;
    private int encounterIndex = -1;
    private int currentMonsterHp;
    private int turnNumber = 1;
    private string heroLastMoveId;
    private bool isBusy;
    private bool returnReady;
    private string returnLabel = "Back to map";
    private bool usingBattleScene2Ui;
    private int hoveredMoveIndex = -1;

    private readonly List<string> monsterMoveHistory = new();
    private readonly Color inactiveEffectColor = Color.black;
    private readonly Color buffEffectColor = new(0.85f, 1f, 0.3f, 1f);
    private readonly Color debuffEffectColor = new(1f, 0.45f, 0.45f, 1f);
    private readonly Color activeMoveIconColor = Color.white;
    private readonly Color inactiveMoveIconColor = new(0.35f, 0.35f, 0.35f, 1f);
    private VictoryRewards pendingVictoryRewards;

    private sealed class VictoryRewards
    {
        public string Summary;
        public string CoinsText;
        public string LearnedMoveText;
        public string XpText;
        public string LearnedMoveId;
        public bool HasLearnedMove;
    }

    private sealed class MoveHoverListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TowerBattleController owner;
        private int moveIndex;

        public void Initialize(TowerBattleController controller, int index)
        {
            owner = controller;
            moveIndex = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.HandleMoveHoverChanged(moveIndex, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HandleMoveHoverChanged(moveIndex, false);
        }

        private void OnDisable()
        {
            owner?.HandleMoveHoverChanged(moveIndex, false);
        }
    }

    private sealed class EndPanelButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button button;
        private Image backgroundImage;
        private RectTransform labelRect;
        private Sprite normalSprite;
        private Sprite pressedSprite;
        private Color normalColor;
        private Color hoverColor;
        private Color pressedColor;
        private Vector2 labelBasePosition;
        private Vector2 pressedLabelOffset;
        private bool isHovered;
        private bool isPressed;

        public void Initialize(
            Button targetButton,
            Image targetImage,
            RectTransform targetLabelRect,
            Sprite targetPressedSprite,
            Color targetHoverColor,
            Color targetPressedColor,
            Vector2 targetPressedLabelOffset)
        {
            button = targetButton;
            backgroundImage = targetImage;
            labelRect = targetLabelRect;
            pressedSprite = targetPressedSprite;
            hoverColor = targetHoverColor;
            pressedColor = targetPressedColor;
            pressedLabelOffset = targetPressedLabelOffset;

            if (backgroundImage != null)
            {
                normalSprite = backgroundImage.sprite;
                normalColor = backgroundImage.color;
            }

            if (labelRect != null)
            {
                labelBasePosition = labelRect.anchoredPosition;
            }

            ApplyVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            ApplyVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button == null || !button.IsInteractable())
            {
                return;
            }

            isPressed = true;
            ApplyVisualState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            ApplyVisualState();
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = isPressed && pressedSprite != null ? pressedSprite : normalSprite;
                backgroundImage.color = isPressed
                    ? pressedColor
                    : isHovered ? hoverColor : normalColor;
            }

            if (labelRect != null)
            {
                labelRect.anchoredPosition = labelBasePosition + (isPressed ? pressedLabelOffset : Vector2.zero);
            }
        }
    }

    private void Start()
    {
        AutoBindScene();
        CreateRuntimeHud();
        StartCoroutine(BootstrapEncounter());
    }

    public void UseMoveSlot(int slot)
    {
        if (returnReady)
        {
            ReturnToOverview();
            return;
        }

        if (isBusy || hero == null || slot < 0 || slot >= hero.EquippedMoves.Count)
        {
            return;
        }

        StartCoroutine(ResolveTurnSequence(hero.EquippedMoves[slot]));
    }

    private IEnumerator BootstrapEncounter()
    {
        isBusy = true;
        returnReady = false;
        SetButtonsInteractable(false);
        SetStatus("Loading battle...");

        if (!RunSession.HasActiveRun)
        {
            yield return RunDataService.LoadRunConfig(baseUrl, useLocalFallbackIfApiFails, (config, usingFallback) =>
            {
                runConfig = config;
                if (runConfig != null)
                {
                    RunSession.InitializeNewRun(runConfig, usingFallback);
                }
            });
        }

        if (!RunSession.HasActiveRun)
        {
            SetStatus("Unable to load run config.");
            PrepareReturn("Retry from map");
            yield break;
        }

        runConfig = RunSession.CurrentRunConfig;
        hero = RunSession.Hero;
        encounterIndex = RunSession.SelectedEncounterIndex >= 0
            ? RunSession.SelectedEncounterIndex
            : RunSession.GetFirstAvailableEncounterIndex();

        if (!RunSession.CanEnterEncounter(encounterIndex))
        {
            var fallbackEncounter = RunSession.GetFirstAvailableEncounterIndex();
            if (fallbackEncounter >= 0 && RunSession.CanEnterEncounter(fallbackEncounter))
            {
                encounterIndex = fallbackEncounter;
                RunSession.SelectEncounter(encounterIndex);
            }
        }

        if (!RunSession.CanEnterEncounter(encounterIndex))
        {
            SetStatus(RunSession.IsRunComplete()
                ? "The run is already complete. Return to the map to start again."
                : "No encounter is currently available.");
            PrepareReturn("Back to map");
            yield break;
        }

        RunSession.SelectEncounter(encounterIndex);
        StartEncounter(encounterIndex);
        isBusy = false;
    }

    private void StartEncounter(int index)
    {
        encounterIndex = index;
        currentMonster = runConfig.encounters[index];
        currentMonsterHp = currentMonster.stats.health;
        monsterModifiers.Clear();
        heroModifiers.Clear();
        monsterMoveHistory.Clear();
        heroLastMoveId = null;
        turnNumber = 1;
        returnReady = false;

        var intro = RunSession.UsingFallbackData
            ? "Offline fallback data loaded."
            : $"Run {GetRunLabel()} active.";
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
        var wasFinalEncounter = IsPendingEncounterFinal();
        var rewards = AwardVictoryRewards();
        RunSession.MarkEncounterComplete(encounterIndex, rewards.Summary);
        RestoreHeroToFullHealth();
        RefreshAllUi();
        pendingVictoryRewards = rewards;

        var statusBuilder = new StringBuilder();
        statusBuilder.AppendLine(heroTurnSummary);
        statusBuilder.AppendLine($"{currentMonster.name} was defeated.");
        statusBuilder.Append(rewards.Summary);
        if (wasFinalEncounter)
        {
            statusBuilder.AppendLine();
            statusBuilder.Append("Continue to finish the run.");
        }

        SetStatus(statusBuilder.ToString());

        if (usingBattleScene2Ui && endPanelRoot != null)
        {
            ShowVictoryEndPanel(rewards, wasFinalEncounter);
            return;
        }

        if (RunSession.IsRunComplete())
        {
            PrepareReturn("Back to map");
            return;
        }

        PrepareReturn("Continue on map");
    }

    private void HandleDefeat(string heroTurnSummary, string monsterTurnSummary)
    {
        if (RunSession.IsEncounterCompleted(encounterIndex))
        {
            RestoreHeroToFullHealth();
            RefreshAllUi();
            SetStatus($"{heroTurnSummary}\n{monsterTurnSummary}\nThe hero fell after the encounter was already cleared. Return to the map when you're ready.");
            PrepareReturn("Back to map");
            return;
        }

        RunSession.RegisterDefeat(encounterIndex);
        RefreshAllUi();
        SetStatus($"{heroTurnSummary}\n{monsterTurnSummary}\nThe hero fell on encounter {encounterIndex + 1}. Try again.");

        if (usingBattleScene2Ui && defeatPanelRoot != null)
        {
            SetButtonsInteractable(false);
            SetDefeatPanelVisible(true);
            return;
        }

        PrepareReturn("Back to map");
    }

    private VictoryRewards AwardVictoryRewards()
    {
        hero.Xp += currentMonster.xpReward;
        var xpSummary = $"+{currentMonster.xpReward} xp";

        while (hero.Level < runConfig.xpTable.Count && hero.Xp >= runConfig.xpTable[hero.Level])
        {
            hero.Level++;
        }

        var learnedMove = PickLearnableMove(currentMonster);
        string learnedMoveText = null;
        if (!string.IsNullOrEmpty(learnedMove))
        {
            learnedMoveText = RegisterLearnedMoveReward(learnedMove);
        }

        var summaryParts = new List<string> { xpSummary };
        if (!string.IsNullOrEmpty(learnedMoveText))
        {
            summaryParts.Add(learnedMoveText);
        }

        return new VictoryRewards
        {
            Summary = string.Join(" | ", summaryParts),
            CoinsText = "+0",
            LearnedMoveText = learnedMoveText,
            XpText = xpSummary,
            LearnedMoveId = learnedMove,
            HasLearnedMove = !string.IsNullOrEmpty(learnedMoveText)
        };
    }

    private void RestoreHeroToFullHealth()
    {
        if (hero == null)
        {
            return;
        }

        hero.CurrentHp = GetHeroBaseStats().health;
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

    private string RegisterLearnedMoveReward(string newMoveId)
    {
        var newMove = GetMove(newMoveId);
        if (newMove == null)
        {
            return "Unknown move";
        }

        RunSession.SetPendingLearnedMove(newMoveId);
        return newMove.name;
    }

    private void PrepareReturn(string label)
    {
        returnReady = true;
        returnLabel = label;
        isBusy = false;

        for (var index = 0; index < moveButtons.Count; index++)
        {
            moveButtons[index].interactable = index == 0;
            if (moveButtonLabels[index] != null)
            {
                moveButtonLabels[index].text = index == 0 ? returnLabel : "-";
            }
        }

        UpdateMoveHoverSelectors();
    }

    private void RefreshAllUi()
    {
        RefreshLabels();
        RefreshButtons();
        RefreshProgress();
        RefreshEffects();
    }

    private void ShowVictoryEndPanel(VictoryRewards rewards, bool wasFinalEncounter)
    {
        if (endPanelRoot == null || rewards == null)
        {
            return;
        }

        if (endPanelTitleText != null)
        {
            endPanelTitleText.text = wasFinalEncounter ? "Run Complete!" : "Victory!";
        }

        if (reward1Text != null)
        {
            reward1Text.text = rewards.CoinsText;
        }

        if (reward2Root != null)
        {
            reward2Root.SetActive(rewards.HasLearnedMove);
        }

        if (reward2Text != null)
        {
            reward2Text.text = rewards.LearnedMoveText;
        }

        if (reward3Text != null)
        {
            reward3Text.text = rewards.XpText;
        }

        if (reward2Icon != null)
        {
            reward2Icon.sprite = ResolveReward2Icon(rewards.LearnedMoveId);
            reward2Icon.color = Color.white;
        }

        SetButtonsInteractable(false);
        SetEndPanelVisible(true);
    }

    private Sprite ResolveReward2Icon(string learnedMoveId)
    {
        var learnedMove = GetMove(learnedMoveId);
        var icon = ResolveMoveIconSprite(learnedMove);
        return icon != null ? icon : reward2Icon?.sprite;
    }

    private bool IsPendingEncounterFinal()
    {
        return RunSession.CompletedEncounters != null &&
               RunSession.CompletedEncounters.Count > 0 &&
               RunSession.CompletedEncounters.Count(completed => completed) == RunSession.CompletedEncounters.Count - 1;
    }

    private void OnReviveButtonPressed()
    {
        if (pendingVictoryRewards == null)
        {
            return;
        }

        SetEndPanelVisible(false);
        StartEncounter(encounterIndex);
    }

    private void OnContinueButtonPressed()
    {
        pendingVictoryRewards = null;
        ReturnToOverview();
    }

    private void OnDefeatReviveButtonPressed()
    {
        if (runConfig == null)
        {
            return;
        }

        RunSession.InitializeNewRun(runConfig, RunSession.UsingFallbackData);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnDefeatMapButtonPressed()
    {
        ReturnToOverview();
    }

    private void SetEndPanelVisible(bool isVisible)
    {
        if (endPanelRoot == null)
        {
            return;
        }

        endPanelRoot.SetActive(isVisible);
        if (!isVisible)
        {
            pendingVictoryRewards = null;
        }
    }

    private void SetDefeatPanelVisible(bool isVisible)
    {
        if (defeatPanelRoot == null)
        {
            return;
        }

        defeatPanelRoot.SetActive(isVisible);
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

        if (levelText != null)
        {
            var encounterText = runConfig != null && encounterIndex >= 0
                ? $"Encounter {encounterIndex + 1}/{runConfig.encounters.Count}"
                : "Battle";
            levelText.text = hero != null
                ? $"Hero Lv.{hero.Level}  •  {encounterText}"
                : encounterText;
        }

        if (heroHealthBarImage != null && hero != null)
        {
            var heroMaxHp = Mathf.Max(1, GetHeroBaseStats().health);
            heroHealthBarImage.fillAmount = Mathf.Clamp01(hero.CurrentHp / (float)heroMaxHp);
        }

        if (monsterHealthBarImage != null)
        {
            var monsterMaxHp = Mathf.Max(1, currentMonster?.stats.health ?? 1);
            monsterHealthBarImage.fillAmount = Mathf.Clamp01(currentMonsterHp / (float)monsterMaxHp);
        }

        var heroStats = hero != null ? GetEffectiveHeroStats() : null;
        if (heroAttackText != null)
        {
            heroAttackText.text = $"{heroStats?.attack ?? 0}";
        }

        if (heroDefenseText != null)
        {
            heroDefenseText.text = $"{heroStats?.defense ?? 0}";
        }

        if (heroMagicText != null)
        {
            heroMagicText.text = $"{heroStats?.magic ?? 0}";
        }

        var monsterStats = currentMonster != null ? GetEffectiveMonsterStats() : null;
        if (monsterAttackText != null)
        {
            monsterAttackText.text = $"{monsterStats?.attack ?? 0}";
        }

        if (monsterDefenseText != null)
        {
            monsterDefenseText.text = $"{monsterStats?.defense ?? 0}";
        }

        if (monsterMagicText != null)
        {
            monsterMagicText.text = $"{monsterStats?.magic ?? 0}";
        }
    }

    private void RefreshButtons()
    {
        for (var index = 0; index < moveButtons.Count; index++)
        {
            var hasMove = hero != null && index < hero.EquippedMoves.Count;
            moveButtons[index].interactable = !isBusy && !returnReady && hasMove;
            if (moveButtonLabels[index] != null)
            {
                moveButtonLabels[index].text = hasMove ? GetMove(hero.EquippedMoves[index])?.name ?? hero.EquippedMoves[index] : "-";
            }

            if (index < moveButtonIcons.Count && moveButtonIcons[index] != null)
            {
                var move = hasMove ? GetMove(hero.EquippedMoves[index]) : null;
                moveButtonIcons[index].sprite = ResolveMoveIconSprite(move);
                moveButtonIcons[index].color = hasMove ? activeMoveIconColor : inactiveMoveIconColor;
            }
        }

        UpdateMoveHoverSelectors();
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
        RefreshEffectIcon(heroEffectAttackIcon, heroModifiers, "attack");
        RefreshEffectIcon(heroEffectDefenseIcon, heroModifiers, "defense");
        RefreshEffectIcon(heroEffectMagicIcon, heroModifiers, "magic");
        RefreshEffectIcon(monsterEffectAttackIcon, monsterModifiers, "attack");
        RefreshEffectIcon(monsterEffectDefenseIcon, monsterModifiers, "defense");
        RefreshEffectIcon(monsterEffectMagicIcon, monsterModifiers, "magic");

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

        UpdateMoveHoverSelectors();
    }

    private void ConfigureMoveHoverListener(GameObject moveRoot, int index)
    {
        if (moveRoot == null)
        {
            return;
        }

        var listener = moveRoot.GetComponent<MoveHoverListener>();
        if (listener == null)
        {
            listener = moveRoot.AddComponent<MoveHoverListener>();
        }

        listener.Initialize(this, index);
    }

    private GameObject CreateMoveHoverSelector(Transform moveRoot)
    {
        if (moveRoot == null ||
            battleScene2HoverSelectorTopLeft == null ||
            battleScene2HoverSelectorTopRight == null ||
            battleScene2HoverSelectorBottomLeft == null ||
            battleScene2HoverSelectorBottomRight == null)
        {
            return null;
        }

        var existing = moveRoot.Find("Hover Selector");
        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            existing.SetAsLastSibling();
            return existing.gameObject;
        }

        var selectorRoot = new GameObject("Hover Selector", typeof(RectTransform));
        selectorRoot.transform.SetParent(moveRoot, false);

        var selectorRect = selectorRoot.GetComponent<RectTransform>();
        selectorRect.anchorMin = Vector2.zero;
        selectorRect.anchorMax = Vector2.one;
        selectorRect.offsetMin = Vector2.zero;
        selectorRect.offsetMax = Vector2.zero;

        CreateSelectorCorner(selectorRoot.transform, "Top Left", battleScene2HoverSelectorTopLeft, new Vector2(0f, 1f));
        CreateSelectorCorner(selectorRoot.transform, "Top Right", battleScene2HoverSelectorTopRight, new Vector2(1f, 1f));
        CreateSelectorCorner(selectorRoot.transform, "Bottom Left", battleScene2HoverSelectorBottomLeft, new Vector2(0f, 0f));
        CreateSelectorCorner(selectorRoot.transform, "Bottom Right", battleScene2HoverSelectorBottomRight, new Vector2(1f, 0f));

        selectorRoot.transform.SetAsLastSibling();
        selectorRoot.SetActive(false);
        return selectorRoot;
    }

    private void CreateSelectorCorner(Transform parent, string name, Sprite sprite, Vector2 anchor)
    {
        var cornerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cornerObject.transform.SetParent(parent, false);

        var rect = cornerObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(battleScene2HoverSelectorCornerSize, battleScene2HoverSelectorCornerSize);

        var image = cornerObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void HandleMoveHoverChanged(int moveIndex, bool isHovered)
    {
        hoveredMoveIndex = isHovered
            ? moveIndex
            : hoveredMoveIndex == moveIndex ? -1 : hoveredMoveIndex;
        UpdateMoveHoverSelectors();
    }

    private void UpdateMoveHoverSelectors()
    {
        for (var index = 0; index < moveHoverSelectorRoots.Count; index++)
        {
            var selectorRoot = moveHoverSelectorRoots[index];
            if (selectorRoot == null)
            {
                continue;
            }

            var isActive = usingBattleScene2Ui &&
                index == hoveredMoveIndex &&
                index < moveButtons.Count &&
                moveButtons[index] != null &&
                moveButtons[index].interactable;

            selectorRoot.SetActive(isActive);
        }
    }

    private Move GetMove(string moveId)
    {
        return RunSession.GetMove(moveId);
    }

    private Stats GetHeroBaseStats()
    {
        return RunSession.GetHeroBaseStats();
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

    private void RefreshEffectIcon(Image icon, IEnumerable<ActiveModifier> modifiers, string stat)
    {
        if (icon == null)
        {
            return;
        }

        var total = modifiers
            .Where(modifier => string.Equals(modifier.Stat, stat, StringComparison.OrdinalIgnoreCase))
            .Sum(modifier => modifier.Value);

        icon.color = total switch
        {
            > 0 => buffEffectColor,
            < 0 => debuffEffectColor,
            _ => inactiveEffectColor
        };
    }

    private TextMesh FindTextMesh(string objectName)
    {
        var gameObject = GameObject.Find(objectName);
        return gameObject != null ? gameObject.GetComponent<TextMesh>() : null;
    }

    private static T FindComponent<T>(string objectPath) where T : Component
    {
        var gameObject = GameObject.Find(objectPath);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private static void ConfigureHealthBarImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.preserveAspect = false;
    }

    private Sprite ResolveMoveIconSprite(Move move)
    {
        if (move == null)
        {
            return null;
        }

        if (move.effect == "heal" || move.type == "magic" || string.Equals(move.statModifier?.stat, "magic", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectMagicIcon?.sprite ?? monsterEffectMagicIcon?.sprite;
        }

        if (string.Equals(move.statModifier?.stat, "defense", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectDefenseIcon?.sprite ?? monsterEffectDefenseIcon?.sprite;
        }

        return heroEffectAttackIcon?.sprite ?? monsterEffectAttackIcon?.sprite;
    }

    private void ReturnToOverview()
    {
        SceneManager.LoadScene(overviewSceneName);
    }

    private string GetRunLabel()
    {
        if (string.IsNullOrEmpty(runConfig?.runId))
        {
            return "run";
        }

        return runConfig.runId.Substring(0, Mathf.Min(8, runConfig.runId.Length));
    }

    private void AutoBindScene()
    {
        heroLabel = FindTextMesh("HeroLabel");
        monsterLabel = FindTextMesh("MonsterLabel");
        heroHpWorldText = FindTextMesh("HeroHPText");
        monsterHpWorldText = FindTextMesh("MonsterHPText");

        moveButtons.Clear();
        moveButtonLabels.Clear();
        moveButtonIcons.Clear();
        moveHoverSelectorRoots.Clear();
        hoveredMoveIndex = -1;

        for (var index = 0; index < buttonObjectNames.Length; index++)
        {
            var buttonObject = GameObject.Find(buttonObjectNames[index]);
            if (buttonObject == null)
            {
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
            moveButtonIcons.Add(null);
            moveHoverSelectorRoots.Add(null);
        }

        usingBattleScene2Ui = moveButtons.Count == 0;
        if (!usingBattleScene2Ui)
        {
            return;
        }

        levelText = FindComponent<TMP_Text>("Canvas/Level Text");
        heroAttackText = FindComponent<TMP_Text>("Canvas/Hero UI/Stats/Attack Text");
        heroDefenseText = FindComponent<TMP_Text>("Canvas/Hero UI/Stats/Defense Text");
        heroMagicText = FindComponent<TMP_Text>("Canvas/Hero UI/Stats/Magic Text");
        monsterAttackText = FindComponent<TMP_Text>("Canvas/Monster UI/Stats/Attack Text");
        monsterDefenseText = FindComponent<TMP_Text>("Canvas/Monster UI/Stats/Defense Text");
        monsterMagicText = FindComponent<TMP_Text>("Canvas/Monster UI/Stats/Magic Text");

        heroHealthBarImage = FindComponent<Image>("Canvas/Hero UI/Health Bar/Health Bar");
        monsterHealthBarImage = FindComponent<Image>("Canvas/Monster UI/Health Bar/Health Bar");
        ConfigureHealthBarImage(heroHealthBarImage);
        ConfigureHealthBarImage(monsterHealthBarImage);

        heroEffectAttackIcon = FindComponent<Image>("Canvas/Hero UI/Stat Effects/Sword Icon");
        heroEffectDefenseIcon = FindComponent<Image>("Canvas/Hero UI/Stat Effects/Shield Icon");
        heroEffectMagicIcon = FindComponent<Image>("Canvas/Hero UI/Stat Effects/Magic Icon");
        monsterEffectAttackIcon = FindComponent<Image>("Canvas/Monster UI/Stat Effects/Sword Icon");
        monsterEffectDefenseIcon = FindComponent<Image>("Canvas/Monster UI/Stat Effects/Shield Icon");
        monsterEffectMagicIcon = FindComponent<Image>("Canvas/Monster UI/Stat Effects/Magic Icon");
        endPanelRoot = GameObject.Find("Canvas/End Panel");
        defeatPanelRoot = GameObject.Find("Canvas/Defeat Panel");
        endPanelTitleText = FindComponent<TMP_Text>("Canvas/End Panel/Title Image/Title Text");
        reward2Root = GameObject.Find("Canvas/End Panel/Rewards/Reward 2");
        reward1Text = FindComponent<TMP_Text>("Canvas/End Panel/Rewards/Reward 1/Text");
        reward2Text = FindComponent<TMP_Text>("Canvas/End Panel/Rewards/Reward 2/Text");
        reward3Text = FindComponent<TMP_Text>("Canvas/End Panel/Rewards/Reward 3/Text");
        reward1Icon = FindComponent<Image>("Canvas/End Panel/Rewards/Reward 1/Reward Icon");
        reward2Icon = FindComponent<Image>("Canvas/End Panel/Rewards/Reward 2/Reward Icon");
        reward3Icon = FindComponent<Image>("Canvas/End Panel/Rewards/Reward 3/Reward Icon");
        reviveButton = FindComponent<Button>("Canvas/End Panel/Revive Button");
        continueButton = FindComponent<Button>("Canvas/End Panel/Continue Button");
        defeatReviveButton = FindComponent<Button>("Canvas/Defeat Panel/Revive Button");
        defeatMapButton = FindComponent<Button>("Canvas/Defeat Panel/Map Button");

        if (reviveButton != null)
        {
            ConfigureEndPanelButton(reviveButton, OnReviveButtonPressed);
        }

        if (continueButton != null)
        {
            ConfigureEndPanelButton(continueButton, OnContinueButtonPressed);
        }

        if (defeatReviveButton != null)
        {
            ConfigureEndPanelButton(defeatReviveButton, OnDefeatReviveButtonPressed);
        }

        if (defeatMapButton != null)
        {
            ConfigureEndPanelButton(defeatMapButton, OnDefeatMapButtonPressed);
        }

        SetEndPanelVisible(false);
        SetDefeatPanelVisible(false);

        for (var index = 0; index < 4; index++)
        {
            var movePath = $"Canvas/Moves Bar/Moves/Move {index + 1}";
            var moveRoot = GameObject.Find(movePath);
            if (moveRoot == null)
            {
                Debug.LogWarning($"Missing move slot '{movePath}'.");
                continue;
            }

            var button = moveRoot.GetComponent<Button>();
            if (button == null)
            {
                button = moveRoot.AddComponent<Button>();
            }

            var backgroundImage = FindComponent<Image>($"{movePath}/Move Background");
            if (backgroundImage != null)
            {
                button.targetGraphic = backgroundImage;
            }

            var iconImage = FindComponent<Image>($"{movePath}/Move Icon");
            var capturedIndex = index;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => UseMoveSlot(capturedIndex));

            moveButtons.Add(button);
            moveButtonLabels.Add(null);
            moveButtonIcons.Add(iconImage);
            moveHoverSelectorRoots.Add(CreateMoveHoverSelector(moveRoot.transform));
            ConfigureMoveHoverListener(moveRoot, capturedIndex);
        }

        UpdateMoveHoverSelectors();
    }

    private void ConfigureEndPanelButton(Button button, UnityEngine.Events.UnityAction clickAction)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(clickAction);
        button.transition = Selectable.Transition.None;

        var buttonImage = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (buttonImage != null)
        {
            button.targetGraphic = buttonImage;
        }

        var labelRect = button.GetComponentInChildren<TMP_Text>()?.rectTransform;
        var feedback = button.GetComponent<EndPanelButtonFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<EndPanelButtonFeedback>();
        }

        feedback.Initialize(
            button,
            buttonImage,
            labelRect,
            battleScene2EndPanelPressedButtonSprite,
            battleScene2EndPanelHoverTint,
            battleScene2EndPanelPressedTint,
            battleScene2EndPanelPressedTextOffset);
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
        ConfigureTextRect(
            statusText.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, usingBattleScene2Ui ? -92f : -26f),
            new Vector2(900f, 120f));

        if (!usingBattleScene2Ui)
        {
            progressText = CreateUiText("ProgressText", canvas.transform, font, 22, TextAnchor.UpperLeft);
            effectsText = CreateUiText("EffectsText", canvas.transform, font, 20, TextAnchor.UpperRight);

            ConfigureTextRect(progressText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(420f, 120f));
            ConfigureTextRect(effectsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -24f), new Vector2(420f, 160f));
        }
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

    private sealed class ActiveModifier
    {
        public string Stat;
        public int Value;
        public int RemainingUses;
        public bool SkipNextConsumption;
    }
}
