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
    private const string DefaultMonsterSpriteKey = "";
    private const string DefaultHeroSpriteKey = "";

    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Navigation")]
    [SerializeField] private string overviewSceneName = "RunOverviewScene";

    [Header("Turn Timing")]
    [SerializeField] private float heroAttackDelay = 1f;
    [SerializeField] private float monsterAttackDelay = 1f;
    [SerializeField] private float nextTurnDelay = 0.75f;

    [Header("Battle Scene UI")]
    [SerializeField] private Sprite battleSceneHoverSelectorTopLeft;
    [SerializeField] private Sprite battleSceneHoverSelectorTopRight;
    [SerializeField] private Sprite battleSceneHoverSelectorBottomLeft;
    [SerializeField] private Sprite battleSceneHoverSelectorBottomRight;
    [SerializeField] private float battleSceneHoverSelectorCornerSize = 24f;
    [SerializeField] private Sprite battleSceneEndPanelPressedButtonSprite;
    [SerializeField] private Color battleSceneEndPanelHoverTint = new(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color battleSceneEndPanelPressedTint = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Vector2 battleSceneEndPanelPressedTextOffset = new(0f, -6f);

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
    private BattleCharacterPresenter heroCharacterPresenter;
    private BattleCharacterPresenter monsterCharacterPresenter;
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
    private bool usingBattleSceneUi;
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
        PreloadBattleAssets();
        ConfigureBattlePresenters();
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
        PlayActorAnimation(actorIsHero: true, move);
        RefreshAllUi();

        if (heroAttackDelay > 0f)
        {
            yield return new WaitForSeconds(heroAttackDelay);
        }

        var heroTurnSummary = ResolveMove(move, actorIsHero: true);
        PlayReactionAnimation(actorIsHero: true, move);
        heroLastMoveId = move.id;
        SetStatus(heroTurnSummary);
        RefreshAllUi();

        if (currentMonsterHp <= 0)
        {
            HandleVictory(heroTurnSummary);
            isBusy = false;
            yield break;
        }

        var monsterReactionDelay = GetReactionAnimationDelay(actorIsHero: true, move);
        if (monsterReactionDelay > 0f)
        {
            yield return new WaitForSeconds(monsterReactionDelay);
        }

        MonsterMoveResponse monsterResponse = null;
        yield return FetchMonsterMoveRoutine(response => monsterResponse = response);

        if (monsterResponse == null)
        {
            var fallbackMoveId = ChooseFallbackMonsterMoveId();
            monsterResponse = new MonsterMoveResponse
            {
                moveId = fallbackMoveId,
                move = GetMove(fallbackMoveId)
            };
        }

        SetStatus($"{heroTurnSummary}\n{currentMonster.name} prepares {monsterResponse.move?.name ?? "an attack"}...");
        PlayActorAnimation(actorIsHero: false, monsterResponse.move);

        if (monsterAttackDelay > 0f)
        {
            yield return new WaitForSeconds(monsterAttackDelay);
        }

        var monsterTurnSummary = ResolveMove(monsterResponse.move, actorIsHero: false);
        PlayReactionAnimation(actorIsHero: false, monsterResponse.move);
        monsterMoveHistory.Add(monsterResponse.move.id);
        turnNumber++;
        RefreshAllUi();

        if (hero.CurrentHp <= 0)
        {
            yield return StartCoroutine(HandleDefeatSequence(heroTurnSummary, monsterTurnSummary));
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

    private IEnumerator FetchMonsterMoveRoutine(Action<MonsterMoveResponse> onCompleted)
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
        MonsterMoveResponse response = null;

        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    response = JsonConvert.DeserializeObject<MonsterMoveResponse>(request.downloadHandler.text);
                    if (response != null)
                    {
                        response.move ??= GetMove(response.moveId);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Monster move response could not be parsed: {exception.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Monster move request failed: {request.error}");
            }
        }

        onCompleted?.Invoke(response);
    }

    private string ResolveMove(Move move, bool actorIsHero)
    {
        if (move == null)
        {
            return actorIsHero ? "Hero hesitated." : $"{currentMonster.name} hesitated.";
        }

        var actorName = actorIsHero ? RunSession.GetHeroDisplayName() : currentMonster.name;
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
        monsterCharacterPresenter?.PlayState(BattleAnimationState.Death);
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

        if (usingBattleSceneUi && endPanelRoot != null)
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

    private IEnumerator HandleDefeatSequence(string heroTurnSummary, string monsterTurnSummary)
    {
        RunSession.RegisterDefeat(encounterIndex);
        SetButtonsInteractable(false);
        heroCharacterPresenter?.PlayState(BattleAnimationState.Death);
        RefreshAllUi();
        SetStatus($"{heroTurnSummary}\n{monsterTurnSummary}\nThe hero fell on encounter {encounterIndex + 1}. Try again.");

        if (usingBattleSceneUi && defeatPanelRoot != null)
        {
            SetDefeatPanelVisible(true);
            yield break;
        }

        PrepareReturn("Back to map");
        yield break;
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

        if (usingBattleSceneUi)
        {
            SetButtonsInteractable(false);
            return;
        }

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

        RunSession.InitializeNewRun(runConfig, RunSession.UsingFallbackData, RunSession.SelectedHeroDefinition);
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

        if (isVisible)
        {
            endPanelRoot.transform.SetAsLastSibling();
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

        if (isVisible)
        {
            defeatPanelRoot.transform.SetAsLastSibling();
        }

        defeatPanelRoot.SetActive(isVisible);
    }

    private void RefreshLabels()
    {
        if (heroLabel != null)
        {
            heroLabel.text = $"{RunSession.GetHeroDisplayName()} Lv.{hero?.Level ?? 1}";
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
            battleSceneHoverSelectorTopLeft == null ||
            battleSceneHoverSelectorTopRight == null ||
            battleSceneHoverSelectorBottomLeft == null ||
            battleSceneHoverSelectorBottomRight == null)
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

        CreateSelectorCorner(selectorRoot.transform, "Top Left", battleSceneHoverSelectorTopLeft, new Vector2(0f, 1f));
        CreateSelectorCorner(selectorRoot.transform, "Top Right", battleSceneHoverSelectorTopRight, new Vector2(1f, 1f));
        CreateSelectorCorner(selectorRoot.transform, "Bottom Left", battleSceneHoverSelectorBottomLeft, new Vector2(0f, 0f));
        CreateSelectorCorner(selectorRoot.transform, "Bottom Right", battleSceneHoverSelectorBottomRight, new Vector2(1f, 0f));

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
        rect.sizeDelta = new Vector2(battleSceneHoverSelectorCornerSize, battleSceneHoverSelectorCornerSize);

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

            var isActive = usingBattleSceneUi &&
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
        var gameObject = FindGameObject(objectName);
        return gameObject != null ? gameObject.GetComponent<TextMesh>() : null;
    }

    private static T FindComponent<T>(string objectPath) where T : Component
    {
        var gameObject = FindGameObject(objectPath);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private static GameObject FindGameObject(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return null;
        }

        var directMatch = GameObject.Find(objectPath);
        if (directMatch != null)
        {
            return directMatch;
        }

        var pathSegments = objectPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length == 0)
        {
            return null;
        }

        var activeScene = SceneManager.GetActiveScene();
        foreach (var root in activeScene.GetRootGameObjects())
        {
            if (!string.Equals(root.name, pathSegments[0], StringComparison.Ordinal))
            {
                continue;
            }

            var current = root.transform;
            var found = true;
            for (var index = 1; index < pathSegments.Length; index++)
            {
                current = current.Find(pathSegments[index]);
                if (current == null)
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return current.gameObject;
            }
        }

        return null;
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

        var keyedSprite = SpriteKeyLookup.LoadMoveSprite(move.spriteKey);
        if (keyedSprite != null)
        {
            return keyedSprite;
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
        heroCharacterPresenter = FindComponent<BattleCharacterPresenter>("Hero Character");
        monsterCharacterPresenter = FindComponent<BattleCharacterPresenter>("Monster Character");
        endPanelRoot = FindGameObject("Canvas/End Panel");
        defeatPanelRoot = FindGameObject("Canvas/Defeat Panel");
        usingBattleSceneUi = FindGameObject("Canvas/Moves Bar/Moves/Move 1") != null ||
                             endPanelRoot != null ||
                             defeatPanelRoot != null;

        moveButtons.Clear();
        moveButtonLabels.Clear();
        moveButtonIcons.Clear();
        moveHoverSelectorRoots.Clear();
        hoveredMoveIndex = -1;

        if (!usingBattleSceneUi)
        {
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
        }

        if (!usingBattleSceneUi)
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
        endPanelTitleText = FindComponent<TMP_Text>("Canvas/End Panel/Title Image/Title Text");
        reward2Root = FindGameObject("Canvas/End Panel/Rewards/Reward 2");
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
            var moveRoot = FindGameObject(movePath);
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

    private void ConfigureBattlePresenters()
    {
        if (heroCharacterPresenter != null)
        {
            heroCharacterPresenter.SetCharacter(ResolveHeroSpriteKey(), CharacterSpriteKind.Hero, BattleAnimationState.Idle);
        }

        if (monsterCharacterPresenter != null)
        {
            monsterCharacterPresenter.SetCharacter(ResolveMonsterSpriteKey(), CharacterSpriteKind.Monster, BattleAnimationState.Idle);
        }
    }

    private void PreloadBattleAssets()
    {
        var heroSpriteKey = ResolveHeroSpriteKey();
        var monsterSpriteKey = ResolveMonsterSpriteKey();
        PreloadAnimationStates(heroSpriteKey, CharacterSpriteKind.Hero);
        PreloadAnimationStates(monsterSpriteKey, CharacterSpriteKind.Monster);

        if (hero?.EquippedMoves != null)
        {
            foreach (var moveId in hero.EquippedMoves)
            {
                var move = GetMove(moveId);
                if (!string.IsNullOrWhiteSpace(move?.spriteKey))
                {
                    SpriteKeyLookup.LoadMoveSprite(move.spriteKey);
                }
            }
        }

        if (currentMonster?.moves == null)
        {
            return;
        }

        foreach (var moveId in currentMonster.moves)
        {
            var move = GetMove(moveId);
            if (!string.IsNullOrWhiteSpace(move?.spriteKey))
            {
                SpriteKeyLookup.LoadMoveSprite(move.spriteKey);
            }
        }
    }

    private static void PreloadAnimationStates(string spriteKey, CharacterSpriteKind kind)
    {
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Idle, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Attack, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Hurt, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Defend, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Death, kind);
    }

    private void PlayActorAnimation(bool actorIsHero, Move move)
    {
        var actorPresenter = actorIsHero ? heroCharacterPresenter : monsterCharacterPresenter;
        var actorState = ResolveAnimationStateForMove(move);

        actorPresenter?.PlayTemporaryState(actorState, returnState: BattleAnimationState.Idle);
    }

    private void PlayReactionAnimation(bool actorIsHero, Move move)
    {
        var targetPresenter = actorIsHero ? monsterCharacterPresenter : heroCharacterPresenter;
        if (targetPresenter == null || move == null)
        {
            return;
        }

        if (move.effect is "damage" or "damage_and_stat_modifier" or "drain")
        {
            targetPresenter.PlayTemporaryState(BattleAnimationState.Hurt, 0.05f, BattleAnimationState.Idle);
            return;
        }

        if (move.target == "self" && move.effect == "stat_modifier")
        {
            var actorPresenter = actorIsHero ? heroCharacterPresenter : monsterCharacterPresenter;
            actorPresenter?.PlayTemporaryState(BattleAnimationState.Defend, returnState: BattleAnimationState.Idle);
        }
    }

    private float GetReactionAnimationDelay(bool actorIsHero, Move move)
    {
        var targetPresenter = actorIsHero ? monsterCharacterPresenter : heroCharacterPresenter;
        if (targetPresenter == null || move == null)
        {
            return 0f;
        }

        if (move.effect is "damage" or "damage_and_stat_modifier" or "drain")
        {
            return targetPresenter.GetStateDuration(BattleAnimationState.Hurt, 0.05f);
        }

        if (move.target == "self" && move.effect == "stat_modifier")
        {
            var actorPresenter = actorIsHero ? heroCharacterPresenter : monsterCharacterPresenter;
            return actorPresenter != null
                ? actorPresenter.GetStateDuration(BattleAnimationState.Defend)
                : 0f;
        }

        return 0f;
    }

    private string ResolveHeroSpriteKey()
    {
        var selectedHero = RunSession.SelectedHeroDefinition;
        if (!string.IsNullOrWhiteSpace(selectedHero?.spriteKey) &&
            SpriteKeyLookup.HasCharacterAnimation(selectedHero.spriteKey))
        {
            return selectedHero.spriteKey;
        }

        if (!string.IsNullOrWhiteSpace(selectedHero?.portraitKey) &&
            SpriteKeyLookup.HasCharacterAnimation(selectedHero.portraitKey))
        {
            return selectedHero.portraitKey;
        }

        return DefaultHeroSpriteKey;
    }

    private string ResolveMonsterSpriteKey()
    {
        if (SpriteKeyLookup.HasCharacterAnimation(currentMonster?.spriteKey))
        {
            return currentMonster.spriteKey;
        }

        if (SpriteKeyLookup.HasCharacterAnimation(currentMonster?.id))
        {
            return currentMonster.id;
        }

        return DefaultMonsterSpriteKey;
    }

    private static BattleAnimationState ResolveAnimationStateForMove(Move move)
    {
        if (move == null)
        {
            return BattleAnimationState.Idle;
        }

        if (move.effect == "heal" || move.target == "self")
        {
            return BattleAnimationState.Defend;
        }

        return BattleAnimationState.Attack;
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
            battleSceneEndPanelPressedButtonSprite,
            battleSceneEndPanelHoverTint,
            battleSceneEndPanelPressedTint,
            battleSceneEndPanelPressedTextOffset);
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
            new Vector2(0f, usingBattleSceneUi ? -92f : -26f),
            new Vector2(900f, 120f));

        if (!usingBattleSceneUi)
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
