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
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class TowerBattleController : MonoBehaviour
{
    private const string DefaultMonsterSpriteKey = "";
    private const string DefaultHeroSpriteKey = "";
    private const string PauseExitBattleHoverText = "Exit to map without saving the current battle.";
    private const string PauseExitMainMenuHoverText = "Exit to main menu without saving the current battle.";

    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Navigation")]
    [SerializeField] private string overviewSceneName = "RunOverviewScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

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
    [SerializeField] private float currentMoveStatsAnimationDuration = 0.18f;
    [SerializeField] private float currentMoveStatsHideDelay = 0.25f;
    [SerializeField] private float healthBarSmoothSpeed = 4f;
    [SerializeField] private Sprite coinDropSprite;
    [SerializeField] private Sprite keyDropSprite;
    [SerializeField] private Sprite xpDropSprite;

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
    private readonly List<bool> moveButtonHasMoveStates = new();
    private readonly List<GameObject> moveHoverSelectorRoots = new();
    private readonly List<ActiveModifier> heroModifiers = new();
    private readonly List<ActiveModifier> monsterModifiers = new();

    private TMP_Text heroNameText;
    private TMP_Text monsterNameText;
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
    private Image moveStatsIcon;
    private GameObject currentMoveStatsRoot;
    private TMP_Text moveStatsTitleText;
    private TMP_Text moveStatsTypeText;
    private TMP_Text moveStatsDescriptionText;
    private TMP_Text moveStatsAttackText;
    private TMP_Text moveStatsHealText;
    private TMP_Text heroAttackEffectText;
    private TMP_Text heroDefenseEffectText;
    private TMP_Text heroMagicEffectText;
    private TMP_Text monsterAttackEffectText;
    private TMP_Text monsterDefenseEffectText;
    private TMP_Text monsterMagicEffectText;
    private TMP_Text moveStatsEffectsAmountText;
    private RectTransform currentMoveStatsRect;
    private CanvasGroup currentMoveStatsCanvasGroup;
    private Vector2 currentMoveStatsBasePosition;
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
    private GameObject battleLogPanelRoot;
    private GameObject pauseMenuPanelRoot;
    private TMP_Text battleLogTemplateText;
    private TMP_Text pauseMenuHoverText;
    private ScrollRect battleLogScrollRect;
    private RectTransform battleLogContentRoot;
    private Scrollbar battleLogScrollbar;
    private RectTransform battleLogScrollbarSlidingAreaRect;
    private RectTransform battleLogScrollbarTrackRect;
    private RectTransform battleLogScrollbarHandleRect;
    private float battleLogScrollbarHandleHeight;
    private Button battleLogOpenButton;
    private Button battleLogCloseButton;
    private bool battleLogVisible;
    private bool suppressBattleLogScrollbarCallbacks;
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
    private Button continueArrowButton;
    private Button backArrowButton;
    private Button pauseMenuResumeButton;
    private Button pauseMenuExitBattleButton;
    private Button pauseMenuExitToMainMenuButton;
    private RectTransform monsterDropsRoot;
    private RectTransform monsterDropTemplate;
    private RectTransform canvasRect;
    private RectTransform heroRootRect;
    private RectTransform monsterRootRect;
    private RectTransform heroCharacterRect;
    private RectTransform monsterCharacterRect;
    private ParticleSystem heroHealParticleSystem;
    private ParticleSystem monsterHealParticleSystem;
    private Texture heroParticleDefaultTexture;
    private Texture monsterParticleDefaultTexture;
    private ParticleSystem.MinMaxGradient heroParticleDefaultColor;
    private ParticleSystem.MinMaxGradient monsterParticleDefaultColor;

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
    private bool pauseMenuOpen;
    private bool requestedMoveButtonsInteractable;
    private bool reviveSequencePlaying;
    private bool encounterKeyDropConsumed;
    private UiSpriteSheetAnimator heroUiAnimator;
    private UiSpriteSheetAnimator monsterUiAnimator;
    private Coroutine currentMoveStatsAnimation;
    private Coroutine currentMoveStatsHideDelayRoutine;
    private bool currentMoveStatsVisible;
    private float heroHealthBarTargetFill = 1f;
    private float monsterHealthBarTargetFill = 1f;
    private bool pendingInstantHealthBarSync = true;

    private readonly List<string> monsterMoveHistory = new();
    private readonly Color inactiveEffectColor = Color.black;
    private readonly Color buffEffectColor = new(0.85f, 1f, 0.3f, 1f);
    private readonly Color debuffEffectColor = new(1f, 0.45f, 0.45f, 1f);
    private readonly Color activeMoveIconColor = Color.white;
    private readonly Color inactiveMoveIconColor = new(0.35f, 0.35f, 0.35f, 1f);
    private readonly Color disabledMoveIconColor = new(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
    private VictoryRewards pendingVictoryRewards;
    private EncounterSnapshot encounterSnapshot;
    private readonly List<GameObject> runtimeDropObjects = new();
    private bool heroReviveAvailable;
    private bool monsterReviveAvailable;

    private sealed class BattleLogDescriptor
    {
        public bool ShowActorIcon;
        public bool ActorIsHero;
        public string PrefixText;
        public string MoveName;
        public Sprite MoveSprite;
        public bool ShowTargetIcon;
        public bool TargetIsHero;
        public string SuffixText;
        public bool ShowSecondaryTargetIcon;
        public bool SecondaryTargetIsHero;
        public Sprite SecondaryDetailSprite;
        public string SecondaryText;
    }

    private sealed class VictoryRewards
    {
        public string Summary;
        public string CoinsText;
        public string KeyText;
        public string LearnedMoveText;
        public string XpText;
        public string LearnedMoveId;
        public int CoinsAmount;
        public int KeyAmount;
        public int XpAmount;
        public bool HasLearnedMove;
        public bool IsCommitted;
    }

    private sealed class EncounterSnapshot
    {
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

    private sealed class PauseMenuHoverTextTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TowerBattleController owner;
        private string hoverMessage;

        public void Initialize(TowerBattleController controller, string message)
        {
            owner = controller;
            hoverMessage = message;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.SetPauseMenuHoverMessage(hoverMessage);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.SetPauseMenuHoverMessage(null);
        }

        private void OnDisable()
        {
            owner?.SetPauseMenuHoverMessage(null);
        }
    }

    private void Start()
    {
        AutoBindScene();
        CreateRuntimeHud();
        PrepareHealParticleSystems();
        StartCoroutine(BootstrapEncounter());
    }

    private void Update()
    {
        UpdateHealthBarAnimations();

        if (!usingBattleSceneUi || pauseMenuPanelRoot == null)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (pauseMenuOpen)
        {
            ResumeBattleFromPauseMenu();
            return;
        }

        if (CanOpenPauseMenu())
        {
            OpenPauseMenu();
        }
    }

    private void OnDisable()
    {
        ReleasePauseState();
    }

    private void OnDestroy()
    {
        ReleasePauseState();
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

        if (!RunSession.CanEnterEncounterFromMap(encounterIndex))
        {
            var fallbackEncounter = RunSession.GetFirstAvailableEncounterIndex();
            if (fallbackEncounter >= 0 && RunSession.CanEnterEncounterFromMap(fallbackEncounter))
            {
                encounterIndex = fallbackEncounter;
                RunSession.SelectEncounter(encounterIndex);
            }
        }

        if (!RunSession.CanEnterEncounterFromMap(encounterIndex))
        {
            SetStatus(RunSession.IsRunComplete()
                ? "The run is already complete. Return to the map to start again."
                : "No encounter is currently available.");
            PrepareReturn("Back to map");
            yield break;
        }

        RunSession.SelectEncounter(encounterIndex);
        yield return LoadAndStartEncounter(encounterIndex);
        isBusy = false;
    }

    private IEnumerator LoadAndStartEncounter(int index, bool preserveEncounterFlags = false, bool reloadEndlessMonster = true)
    {
        Monster encounterMonster = currentMonster;

        if (RunSession.IsEndlessMode && reloadEndlessMonster)
        {
            encounterMonster = null;
            SetStatus("Loading next endless encounter...");
            yield return RunDataService.LoadNextEndlessEncounter(
                baseUrl,
                RunSession.GetClearedEncounterCount(),
                loadedMonster => encounterMonster = loadedMonster);

            if (encounterMonster == null)
            {
                SetStatus("Unable to load the next endless encounter.");
                PrepareReturn("Back to overview");
                yield break;
            }
        }
        else if (encounterMonster == null)
        {
            encounterMonster = runConfig.encounters[index];
        }

        StartEncounter(index, encounterMonster, preserveEncounterFlags);
    }

    private void StartEncounter(int index, Monster encounterMonster, bool preserveEncounterFlags = false)
    {
        encounterIndex = index;
        currentMonster = encounterMonster;
        currentMonsterHp = currentMonster.stats.health;
        monsterModifiers.Clear();
        heroModifiers.Clear();
        monsterMoveHistory.Clear();
        heroLastMoveId = null;
        turnNumber = 1;
        returnReady = false;
        pendingInstantHealthBarSync = true;
        heroReviveAvailable = false;
        monsterReviveAvailable = false;
        pendingVictoryRewards = null;
        if (!preserveEncounterFlags)
        {
            encounterKeyDropConsumed = RunSession.IsEncounterCompleted(index);
        }
        ClearMonsterDrops();
        SetContinueArrowVisible(false);
        SetBackArrowVisible(false);
        CaptureEncounterSnapshot();

        var intro = RunSession.UsingFallbackData
            ? "Offline fallback data loaded."
            : $"Run {GetRunLabel()} active.";
        SetStatus($"{intro}\n{GetEncounterLabel()}: {currentMonster.name}\n{currentMonster.description}");
        ClearBattleLog();
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
        var heroRecoveryDelay = 0f;
        var heroImpactDelay = PlayActorAnimation(actorIsHero: true, move, out heroRecoveryDelay);
        PlayMoveStartEffects(actorIsHero: true, move);
        RefreshAllUi();

        var resolveMoveImmediately = ShouldResolveMoveImmediately(move);
        var heroActionDelay = heroImpactDelay > 0f
            ? heroImpactDelay
            : resolveMoveImmediately
                ? 0f
                : heroAttackDelay;

        if (!resolveMoveImmediately && heroActionDelay > 0f)
        {
            yield return new WaitForSeconds(heroActionDelay);
        }

        var heroTurnSummary = ResolveMove(move, actorIsHero: true);
        PlayReactionAnimation(actorIsHero: true, move);
        heroLastMoveId = move.id;
        SetStatus(heroTurnSummary);
        RefreshAllUi();

        if (resolveMoveImmediately && heroRecoveryDelay > 0f)
        {
            yield return new WaitForSeconds(heroRecoveryDelay);
        }

        if (currentMonsterHp <= 0)
        {
            HandleVictory(heroTurnSummary);
            isBusy = false;
            yield break;
        }

        var monsterReactionDelay = resolveMoveImmediately
            ? GetReactionAnimationDelay(actorIsHero: true, move)
            : Mathf.Max(GetReactionAnimationDelay(actorIsHero: true, move), heroRecoveryDelay);
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
        var monsterRecoveryDelay = 0f;
        var monsterImpactDelay = PlayActorAnimation(actorIsHero: false, monsterResponse.move, out monsterRecoveryDelay);
        PlayMoveStartEffects(actorIsHero: false, monsterResponse.move);

        var monsterActionDelay = monsterImpactDelay > 0f ? monsterImpactDelay : monsterAttackDelay;
        if (monsterActionDelay > 0f)
        {
            yield return new WaitForSeconds(monsterActionDelay);
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

        var heroReactionDelay = GetReactionAnimationDelay(actorIsHero: false, monsterResponse.move);
        var postTurnDelay = Mathf.Max(nextTurnDelay, Mathf.Max(monsterRecoveryDelay, heroReactionDelay));
        if (postTurnDelay > 0f)
        {
            yield return new WaitForSeconds(postTurnDelay);
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
            heroMaxHp = RunSession.GetHeroMaxHealth(),
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
            yield return WaitWhilePauseMenuOpen();

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
                AddBattleLogMove(new BattleLogDescriptor
                {
                    ShowActorIcon = true,
                    ActorIsHero = actorIsHero,
                    PrefixText = "used",
                    MoveName = move.name,
                    MoveSprite = ResolveMoveIconSprite(move),
                    ShowTargetIcon = true,
                    TargetIsHero = !actorIsHero,
                    SuffixText = BuildBattleLogDamageSummary(move, damage)
                });
                break;
            }
            case "heal":
            {
                var healAmount = ComputeHeal(move, sourceStats);
                ApplyHeal(actorIsHero, healAmount);
                summary = $"{actorName} used {move.name} and healed {healAmount} HP.";
                AddBattleLogMove(new BattleLogDescriptor
                {
                    ShowActorIcon = true,
                    ActorIsHero = actorIsHero,
                    PrefixText = "used",
                    MoveName = move.name,
                    MoveSprite = ResolveMoveIconSprite(move),
                    SuffixText = $"healed {healAmount} HP."
                });
                break;
            }
            case "drain":
            {
                var drainAmount = ComputeDamage(move, sourceStats, targetStats);
                ApplyDamage(!actorIsHero, drainAmount);
                ApplyHeal(actorIsHero, drainAmount);
                summary = $"{actorName} drained {drainAmount} HP with {move.name}.";
                AddBattleLogMove(new BattleLogDescriptor
                {
                    ShowActorIcon = true,
                    ActorIsHero = actorIsHero,
                    PrefixText = "used",
                    MoveName = move.name,
                    MoveSprite = ResolveMoveIconSprite(move),
                    ShowTargetIcon = true,
                    TargetIsHero = !actorIsHero,
                    SuffixText = $"{BuildBattleLogDamageSummary(move, drainAmount)} Restored {drainAmount} HP."
                });
                break;
            }
            case "stat_modifier":
            {
                var targetIsHero = move.target == "self" ? actorIsHero : !actorIsHero;
                ApplyModifier(targetIsHero, move.statModifier);
                summary = $"{actorName} used {move.name}. {BuildModifierSummary(targetIsHero, move.statModifier)}";
                AddBattleLogMove(new BattleLogDescriptor
                {
                    ShowActorIcon = true,
                    ActorIsHero = actorIsHero,
                    PrefixText = "used",
                    MoveName = move.name,
                    MoveSprite = ResolveMoveIconSprite(move),
                    ShowTargetIcon = true,
                    TargetIsHero = targetIsHero,
                    SuffixText = "applied effect,",
                    SecondaryDetailSprite = ResolveBattleLogStatSprite(move.statModifier?.stat),
                    SecondaryText = BuildModifierBattleLogSummary(move.statModifier),
                    ShowSecondaryTargetIcon = true,
                    SecondaryTargetIsHero = targetIsHero
                });
                break;
            }
            case "damage_and_stat_modifier":
            {
                var hybridDamage = ComputeDamage(move, sourceStats, targetStats);
                ApplyDamage(!actorIsHero, hybridDamage);
                var modifierTargetIsHero = move.target == "self" ? actorIsHero : !actorIsHero;
                ApplyModifier(modifierTargetIsHero, move.statModifier);
                summary = $"{actorName} used {move.name} for {hybridDamage} damage. {BuildModifierSummary(modifierTargetIsHero, move.statModifier)}";
                AddBattleLogMove(new BattleLogDescriptor
                {
                    ShowActorIcon = true,
                    ActorIsHero = actorIsHero,
                    PrefixText = "used",
                    MoveName = move.name,
                    MoveSprite = ResolveMoveIconSprite(move),
                    ShowTargetIcon = true,
                    TargetIsHero = !actorIsHero,
                    SuffixText = BuildBattleLogDamageSummary(move, hybridDamage, true),
                    SecondaryDetailSprite = ResolveBattleLogStatSprite(move.statModifier?.stat),
                    SecondaryText = BuildModifierBattleLogSummary(move.statModifier),
                    ShowSecondaryTargetIcon = true,
                    SecondaryTargetIsHero = modifierTargetIsHero
                });
                break;
            }
            default:
                summary = $"{actorName} used {move.name}.";
                AddBattleLogMove(new BattleLogDescriptor
                {
                    ShowActorIcon = true,
                    ActorIsHero = actorIsHero,
                    PrefixText = "used",
                    MoveName = move.name,
                    MoveSprite = ResolveMoveIconSprite(move)
                });
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
        if (amount <= 0)
        {
            return;
        }

        if (targetIsHero)
        {
            hero.CurrentHp = Mathf.Min(RunSession.GetHeroMaxHealth(), hero.CurrentHp + amount);
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
        var wasFinalEncounter = !RunSession.IsEndlessMode && IsPendingEncounterFinal();
        var rewards = AwardVictoryRewards();
        monsterCharacterPresenter?.PlayState(BattleAnimationState.Death);
        CommitVictoryRewards(rewards);
        pendingVictoryRewards = rewards;
        RefreshAllUi();

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
            EnterVictoryState(rewards, wasFinalEncounter);
            return;
        }

        if (RunSession.IsRunComplete())
        {
            PrepareReturn("Back to map");
            return;
        }

        PrepareReturn(RunSession.IsEndlessMode ? "Next encounter" : "Continue on map");
    }

    private IEnumerator HandleDefeatSequence(string heroTurnSummary, string monsterTurnSummary)
    {
        SetButtonsInteractable(false);
        heroCharacterPresenter?.PlayState(BattleAnimationState.Death);
        RefreshAllUi();
        var defeatStatus = $"{heroTurnSummary}\n{monsterTurnSummary}\nThe hero fell on {GetEncounterLabel().ToLowerInvariant()}. Try again.";
        SetStatus(defeatStatus);
        RunSession.RegisterDefeat(encounterIndex);
        RunSession.SetStatus(defeatStatus);
        RunSaveService.SaveCurrentRun();

        if (usingBattleSceneUi && defeatPanelRoot != null)
        {
            EnterDefeatState();
            yield break;
        }

        PrepareReturn("Back to map");
        yield break;
    }

    private VictoryRewards AwardVictoryRewards()
    {
        var xpAmount = RunSession.CalculateXpReward(currentMonster);
        var coinAmount = RunSession.CalculateCoinReward(currentMonster);
        var keyAmount = RunSession.IsEndlessMode || encounterKeyDropConsumed || RunSession.IsEncounterCompleted(encounterIndex) ? 0 : 1;
        var learnedMoveId = PickLearnableMovePreview(currentMonster);
        var learnedMove = GetMove(learnedMoveId);
        var learnedMoveText = learnedMove != null ? learnedMove.name : null;

        var summaryParts = new List<string>
        {
            $"+{coinAmount} coins",
            $"+{xpAmount} xp"
        };

        if (keyAmount > 0)
        {
            summaryParts.Add($"+{keyAmount} key");
        }

        if (!string.IsNullOrEmpty(learnedMoveText))
        {
            summaryParts.Add($"learned {learnedMoveText}");
        }

        var nextThreshold = RunSession.GetNextLevelXpThreshold();
        if (nextThreshold >= 0 && hero != null && hero.Xp + xpAmount >= nextThreshold)
        {
            summaryParts.Add("level up ready");
        }

        return new VictoryRewards
        {
            Summary = string.Join(" | ", summaryParts),
            CoinsText = $"+{coinAmount}",
            KeyText = keyAmount > 0 ? $"+{keyAmount} key" : null,
            LearnedMoveText = learnedMoveText,
            XpText = $"+{xpAmount} xp",
            LearnedMoveId = learnedMoveId,
            CoinsAmount = coinAmount,
            KeyAmount = keyAmount,
            XpAmount = xpAmount,
            HasLearnedMove = !string.IsNullOrEmpty(learnedMoveText)
        };
    }

    private string PickLearnableMovePreview(Monster monster)
    {
        if (monster?.learnableMoves == null || hero?.KnownMoves == null)
        {
            return null;
        }

        foreach (var moveId in monster.learnableMoves)
        {
            if (!hero.KnownMoves.Contains(moveId))
            {
                return moveId;
            }
        }

        return null;
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
        RefreshCurrentMoveStats();
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

    private void EnterVictoryState(VictoryRewards rewards, bool wasFinalEncounter)
    {
        pendingVictoryRewards = rewards;
        if (rewards != null && rewards.KeyAmount > 0)
        {
            encounterKeyDropConsumed = true;
        }
        returnReady = true;
        isBusy = false;
        monsterReviveAvailable = !RunSession.IsEndlessMode;
        heroReviveAvailable = false;
        SetButtonsInteractable(false);
        SetEndPanelVisible(false);
        SetDefeatPanelVisible(false);
        SetContinueArrowVisible(true);
        SetBackArrowVisible(false);
        SpawnMonsterDrops(rewards);
        RefreshLabels();
    }

    private void EnterDefeatState()
    {
        pendingVictoryRewards = null;
        returnReady = true;
        isBusy = false;
        heroReviveAvailable = true;
        monsterReviveAvailable = false;
        ClearMonsterDrops();
        SetEndPanelVisible(false);
        SetDefeatPanelVisible(RunSession.IsEndlessMode);
        SetContinueArrowVisible(false);
        SetBackArrowVisible(!RunSession.IsEndlessMode);
        SetDefeatMapButtonLabel(RunSession.IsEndlessMode ? "Exit" : "Map");
        RefreshLabels();
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
        if (!reviveSequencePlaying)
        {
            StartCoroutine(RestartEncounterFromReviveRoutine(isHeroRevive: false));
        }
    }

    private void OnContinueButtonPressed()
    {
        CommitVictoryRewardsAndReturn();
        ReturnToOverview();
    }

    private void OnDefeatReviveButtonPressed()
    {
        if (!reviveSequencePlaying)
        {
            StartCoroutine(RestartEncounterFromReviveRoutine(isHeroRevive: true));
        }
    }

    private void OnDefeatMapButtonPressed()
    {
        if (RunSession.IsEndlessMode)
        {
            var saveId = RunSession.CurrentSaveId;
            if (!string.IsNullOrWhiteSpace(saveId))
            {
                RunSaveService.DeleteRun(saveId);
            }
            else
            {
                RunSession.ClearActiveRun();
            }

            ReleasePauseState();
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        RunSession.ReturnToMapAfterDefeat(encounterIndex);
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
        if (!isVisible && !monsterReviveAvailable && !RunSession.IsEndlessMode)
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
        if (heroNameText != null)
        {
            heroNameText.text = heroReviveAvailable ? "Revive" : RunSession.GetHeroDisplayName();
        }

        if (monsterNameText != null)
        {
            monsterNameText.text = monsterReviveAvailable
                ? "Revive"
                : currentMonster != null ? currentMonster.name : "Monster";
        }

        if (heroHpWorldText != null && hero != null)
        {
            heroHpWorldText.text = $"HP: {hero.CurrentHp}/{RunSession.GetHeroMaxHealth()}";
        }

        if (monsterHpWorldText != null)
        {
            var maxMonsterHp = currentMonster?.stats.health ?? 0;
            monsterHpWorldText.text = $"HP: {currentMonsterHp}/{maxMonsterHp}";
        }

        if (levelText != null)
        {
            var encounterText = runConfig != null && encounterIndex >= 0
                ? GetEncounterLabel()
                : "Battle";
            levelText.text = hero != null
                ? $"Hero Lv.{hero.Level}  •  {encounterText}"
                : encounterText;
        }

        if (heroHealthBarImage != null && hero != null)
        {
            var heroMaxHp = RunSession.GetHeroMaxHealth();
            SetHeroHealthBarTarget(Mathf.Clamp01(hero.CurrentHp / (float)heroMaxHp), pendingInstantHealthBarSync);
        }

        if (monsterHealthBarImage != null)
        {
            var monsterMaxHp = Mathf.Max(1, currentMonster?.stats.health ?? 1);
            SetMonsterHealthBarTarget(Mathf.Clamp01(currentMonsterHp / (float)monsterMaxHp), pendingInstantHealthBarSync);
        }

        pendingInstantHealthBarSync = false;

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
            var button = moveButtons[index];
            if (button != null)
            {
                button.interactable = !isBusy && !returnReady && hasMove;
            }

            if (index < moveButtonHasMoveStates.Count)
            {
                moveButtonHasMoveStates[index] = hasMove;
            }

            if (moveButtonLabels[index] != null)
            {
                moveButtonLabels[index].text = hasMove ? GetMove(hero.EquippedMoves[index])?.name ?? hero.EquippedMoves[index] : "-";
            }

            if (index < moveButtonIcons.Count && moveButtonIcons[index] != null)
            {
                var move = hasMove ? GetMove(hero.EquippedMoves[index]) : null;
                moveButtonIcons[index].sprite = ResolveMoveIconSprite(move);
            }

            RefreshMoveButtonIconState(index);
        }

        UpdateMoveHoverSelectors();
    }

    private void RefreshProgress()
    {
        if (progressText == null || hero == null || runConfig == null)
        {
            return;
        }

        var nextThreshold = RunSession.GetNextLevelXpThreshold();
        var heroStats = GetEffectiveHeroStats();
        var thresholdText = nextThreshold >= 0 ? $"{hero.Xp}/{nextThreshold}" : $"{hero.Xp}/MAX";
        var runText = RunSession.IsEndlessMode
            ? $"Run: Encounter {Mathf.Max(1, encounterIndex + 1)}"
            : $"Run: {encounterIndex + 1}/{runConfig.encounters.Count}";
        progressText.text =
            $"{runText}\n" +
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
        requestedMoveButtonsInteractable = interactable;
        ApplyMoveButtonsInteractableState();
    }

    private void ApplyMoveButtonsInteractableState()
    {
        var canInteract = requestedMoveButtonsInteractable && !pauseMenuOpen;
        for (var index = 0; index < moveButtons.Count; index++)
        {
            var button = moveButtons[index];
            if (button != null)
            {
                button.interactable = canInteract;
            }

            RefreshMoveButtonIconState(index);
        }

        UpdateMoveHoverSelectors();
    }

    private void RefreshMoveButtonIconState(int index)
    {
        if (index < 0 || index >= moveButtonIcons.Count)
        {
            return;
        }

        var icon = moveButtonIcons[index];
        if (icon == null)
        {
            return;
        }

        var hasMove = index < moveButtonHasMoveStates.Count && moveButtonHasMoveStates[index];
        var button = index < moveButtons.Count ? moveButtons[index] : null;
        icon.color = !hasMove
            ? inactiveMoveIconColor
            : button != null && button.IsInteractable()
                ? activeMoveIconColor
                : disabledMoveIconColor;
    }

    private void ConfigurePauseMenuButtons()
    {
        SetPauseMenuHoverMessage(null);

        if (pauseMenuResumeButton != null)
        {
            ConfigureEndPanelButton(pauseMenuResumeButton, ResumeBattleFromPauseMenu);
            ConfigurePauseMenuHoverTextTarget(pauseMenuResumeButton, null);
        }

        if (pauseMenuExitBattleButton != null)
        {
            ConfigureEndPanelButton(pauseMenuExitBattleButton, ExitBattleWithoutSaving);
            ConfigurePauseMenuHoverTextTarget(pauseMenuExitBattleButton, PauseExitBattleHoverText);
        }

        if (pauseMenuExitToMainMenuButton != null)
        {
            ConfigureEndPanelButton(pauseMenuExitToMainMenuButton, ExitToMainMenuWithoutSaving);
            ConfigurePauseMenuHoverTextTarget(pauseMenuExitToMainMenuButton, PauseExitMainMenuHoverText);
        }
    }

    private void ConfigurePauseMenuHoverTextTarget(Button button, string message)
    {
        if (button == null)
        {
            return;
        }

        var hoverTarget = button.GetComponent<PauseMenuHoverTextTarget>();
        if (hoverTarget == null)
        {
            hoverTarget = button.gameObject.AddComponent<PauseMenuHoverTextTarget>();
        }

        hoverTarget.Initialize(this, message);
    }

    private void SetPauseMenuHoverMessage(string message)
    {
        if (pauseMenuHoverText == null)
        {
            return;
        }

        var hasMessage = !string.IsNullOrWhiteSpace(message);
        pauseMenuHoverText.text = hasMessage ? message : string.Empty;
        pauseMenuHoverText.gameObject.SetActive(hasMessage && pauseMenuOpen);
    }

    private bool CanOpenPauseMenu()
    {
        if (!usingBattleSceneUi || pauseMenuPanelRoot == null || returnReady)
        {
            return false;
        }

        if (endPanelRoot != null && endPanelRoot.activeSelf)
        {
            return false;
        }

        if (defeatPanelRoot != null && defeatPanelRoot.activeSelf)
        {
            return false;
        }

        return true;
    }

    private void OpenPauseMenu()
    {
        SetPauseMenuVisible(true);
    }

    private void ResumeBattleFromPauseMenu()
    {
        SetPauseMenuVisible(false);
    }

    private void ExitBattleWithoutSaving()
    {
        RestoreSavedRunState();
        ReleasePauseState();
        SceneManager.LoadScene(overviewSceneName);
    }

    private void ExitToMainMenuWithoutSaving()
    {
        RestoreSavedRunState();
        ReleasePauseState();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void RestoreSavedRunState()
    {
        if (!RunSession.HasActiveRun || string.IsNullOrWhiteSpace(RunSession.CurrentSaveId))
        {
            return;
        }

        if (!RunSaveService.TryLoadRun(RunSession.CurrentSaveId))
        {
            Debug.LogWarning($"Could not restore saved run '{RunSession.CurrentSaveId}' before exiting the battle.");
        }
    }

    private IEnumerator WaitWhilePauseMenuOpen()
    {
        while (pauseMenuOpen)
        {
            yield return null;
        }
    }

    private void SetPauseMenuVisible(bool visible)
    {
        pauseMenuOpen = visible && pauseMenuPanelRoot != null;

        if (pauseMenuPanelRoot != null)
        {
            pauseMenuPanelRoot.SetActive(pauseMenuOpen);
            if (pauseMenuOpen)
            {
                pauseMenuPanelRoot.transform.SetAsLastSibling();
            }
        }

        SetPauseMenuHoverMessage(null);

        ApplyPauseState();
    }

    private void ApplyPauseState()
    {
        Time.timeScale = pauseMenuOpen ? 0f : 1f;

        if (heroUiAnimator != null)
        {
            heroUiAnimator.enabled = !pauseMenuOpen;
        }

        if (monsterUiAnimator != null)
        {
            monsterUiAnimator.enabled = !pauseMenuOpen;
        }

        ApplyMoveButtonsInteractableState();
    }

    private void ReleasePauseState()
    {
        pauseMenuOpen = false;
        Time.timeScale = 1f;

        if (pauseMenuPanelRoot != null)
        {
            pauseMenuPanelRoot.SetActive(false);
        }

        if (heroUiAnimator != null)
        {
            heroUiAnimator.enabled = true;
        }

        if (monsterUiAnimator != null)
        {
            monsterUiAnimator.enabled = true;
        }

        ApplyMoveButtonsInteractableState();
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
        RefreshCurrentMoveStats();
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

    private void RefreshCurrentMoveStats()
    {
        RefreshMoveStats(GetHoveredMove());
    }

    private Move GetHoveredMove()
    {
        if (!usingBattleSceneUi ||
            hero?.EquippedMoves == null ||
            hoveredMoveIndex < 0 ||
            hoveredMoveIndex >= hero.EquippedMoves.Count ||
            hoveredMoveIndex >= moveButtons.Count)
        {
            return null;
        }

        var button = moveButtons[hoveredMoveIndex];
        if (button == null || !button.interactable)
        {
            return null;
        }

        return GetMove(hero.EquippedMoves[hoveredMoveIndex]);
    }

    private void RefreshMoveStats(Move move)
    {
        SetCurrentMoveStatsVisible(move != null);

        if (move == null)
        {
            return;
        }

        if (moveStatsIcon != null)
        {
            moveStatsIcon.sprite = ResolveMoveIconSprite(move) ?? moveStatsIcon.sprite;
            moveStatsIcon.color = Color.white;
            moveStatsIcon.preserveAspect = true;
        }

        if (moveStatsTitleText != null)
        {
            moveStatsTitleText.text = move.name;
        }

        if (moveStatsTypeText != null)
        {
            moveStatsTypeText.text = $"{move.type?.ToUpperInvariant() ?? "UNKNOWN"}  |  {move.target?.ToUpperInvariant() ?? "TARGET"}";
        }

        if (moveStatsDescriptionText != null)
        {
            moveStatsDescriptionText.text = BuildMoveDescription(move);
        }

        if (moveStatsAttackText != null)
        {
            moveStatsAttackText.text = BuildAttackValue(move);
        }

        if (moveStatsHealText != null)
        {
            moveStatsHealText.text = BuildHealValue(move);
        }

        SetEffectValue(heroAttackEffectText, move, "attack", targetIsHero: true);
        SetEffectValue(heroDefenseEffectText, move, "defense", targetIsHero: true);
        SetEffectValue(heroMagicEffectText, move, "magic", targetIsHero: true);
        SetEffectValue(monsterAttackEffectText, move, "attack", targetIsHero: false);
        SetEffectValue(monsterDefenseEffectText, move, "defense", targetIsHero: false);
        SetEffectValue(monsterMagicEffectText, move, "magic", targetIsHero: false);

        if (moveStatsEffectsAmountText != null)
        {
            moveStatsEffectsAmountText.text = BuildEffectDurationLine(move);
        }
    }

    private void ConfigureCurrentMoveStatsAnimation()
    {
        if (currentMoveStatsRoot == null)
        {
            return;
        }

        currentMoveStatsRect = currentMoveStatsRoot.GetComponent<RectTransform>();
        if (currentMoveStatsRect != null)
        {
            currentMoveStatsBasePosition = currentMoveStatsRect.anchoredPosition;
        }

        currentMoveStatsCanvasGroup = currentMoveStatsRoot.GetComponent<CanvasGroup>();
        if (currentMoveStatsCanvasGroup == null)
        {
            currentMoveStatsCanvasGroup = currentMoveStatsRoot.AddComponent<CanvasGroup>();
        }

        SetCurrentMoveStatsVisibleImmediate(false);
    }

    private void SetCurrentMoveStatsVisible(bool visible)
    {
        if (currentMoveStatsRoot == null || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (currentMoveStatsHideDelayRoutine != null)
        {
            StopCoroutine(currentMoveStatsHideDelayRoutine);
            currentMoveStatsHideDelayRoutine = null;
        }

        if (!visible)
        {
            currentMoveStatsHideDelayRoutine = StartCoroutine(HideCurrentMoveStatsAfterDelay());
            return;
        }

        var hadActiveAnimation = currentMoveStatsAnimation != null;
        if (currentMoveStatsAnimation != null)
        {
            StopCoroutine(currentMoveStatsAnimation);
            currentMoveStatsAnimation = null;
        }

        if (visible == currentMoveStatsVisible)
        {
            if (hadActiveAnimation || visible != currentMoveStatsRoot.activeSelf)
            {
                ApplyCurrentMoveStatsState(visible);
                currentMoveStatsRoot.SetActive(visible);
            }

            return;
        }

        currentMoveStatsAnimation = StartCoroutine(AnimateCurrentMoveStatsVisibility(visible));
    }

    private IEnumerator HideCurrentMoveStatsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(currentMoveStatsHideDelay);
        currentMoveStatsHideDelayRoutine = null;

        var hadActiveAnimation = currentMoveStatsAnimation != null;
        if (currentMoveStatsAnimation != null)
        {
            StopCoroutine(currentMoveStatsAnimation);
            currentMoveStatsAnimation = null;
        }

        if (!currentMoveStatsVisible)
        {
            if (hadActiveAnimation || currentMoveStatsRoot.activeSelf)
            {
                ApplyCurrentMoveStatsState(false);
                currentMoveStatsRoot.SetActive(false);
            }

            yield break;
        }

        currentMoveStatsAnimation = StartCoroutine(AnimateCurrentMoveStatsVisibility(false));
    }

    private IEnumerator AnimateCurrentMoveStatsVisibility(bool visible)
    {
        currentMoveStatsVisible = visible;

        if (currentMoveStatsRect == null || currentMoveStatsCanvasGroup == null)
        {
            currentMoveStatsRoot.SetActive(visible);
            currentMoveStatsAnimation = null;
            yield break;
        }

        var hiddenPosition = GetCurrentMoveStatsHiddenPosition();
        var startPosition = visible ? hiddenPosition : currentMoveStatsRect.anchoredPosition;
        var targetPosition = visible ? currentMoveStatsBasePosition : hiddenPosition;
        var startAlpha = visible ? 0f : currentMoveStatsCanvasGroup.alpha;
        var targetAlpha = visible ? 1f : 0f;

        currentMoveStatsRoot.SetActive(true);

        if (visible)
        {
            currentMoveStatsRect.anchoredPosition = startPosition;
            currentMoveStatsCanvasGroup.alpha = startAlpha;
        }

        currentMoveStatsCanvasGroup.blocksRaycasts = visible;
        currentMoveStatsCanvasGroup.interactable = visible;

        var elapsed = 0f;
        while (elapsed < currentMoveStatsAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / currentMoveStatsAnimationDuration);
            var eased = 1f - Mathf.Pow(1f - progress, 3f);

            currentMoveStatsRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
            currentMoveStatsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        currentMoveStatsRect.anchoredPosition = targetPosition;
        currentMoveStatsCanvasGroup.alpha = targetAlpha;

        if (!visible)
        {
            currentMoveStatsRoot.SetActive(false);
        }

        currentMoveStatsAnimation = null;
    }

    private void SetCurrentMoveStatsVisibleImmediate(bool visible)
    {
        if (currentMoveStatsHideDelayRoutine != null)
        {
            StopCoroutine(currentMoveStatsHideDelayRoutine);
            currentMoveStatsHideDelayRoutine = null;
        }

        if (currentMoveStatsAnimation != null)
        {
            StopCoroutine(currentMoveStatsAnimation);
            currentMoveStatsAnimation = null;
        }

        currentMoveStatsVisible = visible;
        ApplyCurrentMoveStatsState(visible);

        if (currentMoveStatsRoot != null)
        {
            currentMoveStatsRoot.SetActive(visible);
        }
    }

    private void ApplyCurrentMoveStatsState(bool visible)
    {
        if (currentMoveStatsRect == null || currentMoveStatsCanvasGroup == null)
        {
            return;
        }

        currentMoveStatsRect.anchoredPosition = visible
            ? currentMoveStatsBasePosition
            : GetCurrentMoveStatsHiddenPosition();

        currentMoveStatsCanvasGroup.alpha = visible ? 1f : 0f;
        currentMoveStatsCanvasGroup.blocksRaycasts = visible;
        currentMoveStatsCanvasGroup.interactable = visible;
    }

    private Vector2 GetCurrentMoveStatsHiddenPosition()
    {
        if (currentMoveStatsRect == null)
        {
            return currentMoveStatsBasePosition;
        }

        var parentRect = currentMoveStatsRect.parent as RectTransform;
        if (parentRect == null)
        {
            return currentMoveStatsBasePosition;
        }

        var hiddenY = (parentRect.rect.height * 0.5f) + (currentMoveStatsRect.rect.height * 0.5f) + 24f;
        return new Vector2(currentMoveStatsBasePosition.x, hiddenY);
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

    private static string BuildMoveDescription(Move move)
    {
        return string.IsNullOrWhiteSpace(move?.description) ? "-" : move.description;
    }

    private static string BuildAttackValue(Move move)
    {
        if (move == null || move.effect is not "damage" and not "damage_and_stat_modifier" and not "drain")
        {
            return "-";
        }

        return BuildActionValue(move);
    }

    private static string BuildHealValue(Move move)
    {
        if (move == null || !string.Equals(move.effect, "heal"))
        {
            return "-";
        }

        return BuildActionValue(move);
    }

    private static string BuildActionValue(Move move)
    {
        if (move.basePower > 0 && move.statMultiplier > 0f)
        {
            return $"{move.basePower} x{move.statMultiplier:0.##}";
        }

        if (move.basePower > 0)
        {
            return move.basePower.ToString();
        }

        if (move.statMultiplier > 0f)
        {
            return $"x{move.statMultiplier:0.##}";
        }

        return "-";
    }

    private static void SetEffectValue(TMP_Text targetText, Move move, string statName, bool targetIsHero)
    {
        if (targetText == null)
        {
            return;
        }

        targetText.text = BuildModifierValue(move, statName, targetIsHero);
    }

    private static string BuildModifierValue(Move move, string statName, bool targetIsHero)
    {
        if (move?.statModifier == null)
        {
            return "0";
        }

        if (!string.Equals(move.statModifier.stat, statName, StringComparison.OrdinalIgnoreCase))
        {
            return "0";
        }

        var moveTargetsHero = string.Equals(move.target, "self", StringComparison.OrdinalIgnoreCase);
        if (moveTargetsHero != targetIsHero)
        {
            return "0";
        }

        var sign = move.statModifier.value >= 0 ? "+" : string.Empty;
        return $"{sign}{move.statModifier.value}";
    }

    private static string BuildEffectDurationLine(Move move)
    {
        if (move?.statModifier == null)
        {
            return "No effects will be applied.";
        }

        var rounds = Mathf.Max(1, move.statModifier.durationTurns);
        var roundLabel = rounds == 1 ? "round" : "rounds";
        return $"Effects will be applied for {rounds} {roundLabel}.";
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

    private void SetHeroHealthBarTarget(float fillAmount, bool instant)
    {
        heroHealthBarTargetFill = fillAmount;
        if (instant && heroHealthBarImage != null)
        {
            heroHealthBarImage.fillAmount = fillAmount;
        }
    }

    private void SetMonsterHealthBarTarget(float fillAmount, bool instant)
    {
        monsterHealthBarTargetFill = fillAmount;
        if (instant && monsterHealthBarImage != null)
        {
            monsterHealthBarImage.fillAmount = fillAmount;
        }
    }

    private void UpdateHealthBarAnimations()
    {
        if (heroHealthBarImage != null)
        {
            heroHealthBarImage.fillAmount = Mathf.MoveTowards(
                heroHealthBarImage.fillAmount,
                heroHealthBarTargetFill,
                healthBarSmoothSpeed * Time.deltaTime);
        }

        if (monsterHealthBarImage != null)
        {
            monsterHealthBarImage.fillAmount = Mathf.MoveTowards(
                monsterHealthBarImage.fillAmount,
                monsterHealthBarTargetFill,
                healthBarSmoothSpeed * Time.deltaTime);
        }
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
        ReleasePauseState();
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

    private string GetEncounterLabel()
    {
        if (RunSession.IsEndlessMode)
        {
            return encounterIndex >= 0
                ? $"Encounter {encounterIndex + 1}"
                : $"Encounter {RunSession.GetClearedEncounterCount() + 1}";
        }

        return runConfig != null && encounterIndex >= 0
            ? $"Encounter {encounterIndex + 1}/{runConfig.encounters.Count}"
            : "Battle";
    }

    private void SetDefeatMapButtonLabel(string label)
    {
        if (defeatMapButton == null || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var tmpText = defeatMapButton.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
            return;
        }

        var uiText = defeatMapButton.GetComponentInChildren<Text>(true);
        if (uiText != null)
        {
            uiText.text = label;
        }
    }

    private Button ConfigureImageButton(string path, UnityEngine.Events.UnityAction callback)
    {
        var buttonRoot = FindGameObject(path);
        if (buttonRoot == null)
        {
            return null;
        }

        var button = buttonRoot.GetComponent<Button>();
        if (button == null)
        {
            button = buttonRoot.AddComponent<Button>();
        }

        var image = buttonRoot.GetComponent<Image>();
        if (button.targetGraphic == null && image != null)
        {
            button.targetGraphic = image;
        }

        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
        return button;
    }

    private void ConfigureClickableName(TMP_Text label, bool isHeroLabel)
    {
        if (label == null)
        {
            return;
        }

        var button = label.GetComponent<Button>();
        if (button == null)
        {
            button = label.gameObject.AddComponent<Button>();
        }

        button.targetGraphic = label;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.75f, 1f);
        colors.pressedColor = new Color(0.9f, 0.85f, 0.65f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnNameClicked(isHeroLabel));
    }

    private void OnNameClicked(bool isHeroLabel)
    {
        if (reviveSequencePlaying)
        {
            return;
        }

        if ((isHeroLabel && !heroReviveAvailable) || (!isHeroLabel && !monsterReviveAvailable))
        {
            return;
        }

        StartCoroutine(RestartEncounterFromReviveRoutine(isHeroLabel));
    }

    private void OnContinueArrowPressed()
    {
        if (pendingVictoryRewards == null || reviveSequencePlaying)
        {
            return;
        }

        StartCoroutine(ContinueArrowRoutine());
    }

    private void OnBackArrowPressed()
    {
        RunSession.ReturnToMapAfterDefeat(encounterIndex);
        ReturnToOverview();
    }

    private void RestartEncounterFromRevive()
    {
        if (hero == null || encounterSnapshot == null)
        {
            return;
        }

        RunSession.RestoreHeroToFullHealth();
        pendingVictoryRewards = null;
        heroReviveAvailable = false;
        monsterReviveAvailable = false;
        returnReady = false;
        ClearMonsterDrops();
        SetContinueArrowVisible(false);
        SetBackArrowVisible(false);
        SetEndPanelVisible(false);
        SetDefeatPanelVisible(false);
        RunSession.ClearDefeatState($"Battle resumed on {GetEncounterLabel().ToLowerInvariant()}.");
        RunSaveService.SaveCurrentRun();
        StartCoroutine(LoadAndStartEncounter(encounterIndex, preserveEncounterFlags: true, reloadEndlessMonster: false));
    }

    private System.Collections.IEnumerator ContinueArrowRoutine()
    {
        reviveSequencePlaying = true;
        SetContinueArrowVisible(false);
        SetButtonsInteractable(false);

        if (heroCharacterPresenter != null)
        {
            var travelDuration = heroCharacterPresenter.PlayMoveToOffset(
                new Vector3(260f, 0f, 0f),
                moveState: BattleAnimationState.Move,
                endState: BattleAnimationState.Idle);
            yield return new WaitForSecondsRealtime(travelDuration);
        }

        CommitVictoryRewardsAndReturn();
        reviveSequencePlaying = false;
        ReturnToOverview();
    }

    private System.Collections.IEnumerator RestartEncounterFromReviveRoutine(bool isHeroRevive)
    {
        if (hero == null || encounterSnapshot == null || reviveSequencePlaying)
        {
            yield break;
        }

        reviveSequencePlaying = true;
        SetContinueArrowVisible(false);
        SetBackArrowVisible(false);
        SetButtonsInteractable(false);

        if (isHeroRevive)
        {
            yield return PlayHeroReviveAnimation();
        }
        else
        {
            yield return PlayMonsterReviveAnimation();
        }

        RestartEncounterFromRevive();
        reviveSequencePlaying = false;
    }

    private System.Collections.IEnumerator PlayMonsterReviveAnimation()
    {
        if (heroCharacterPresenter == null || monsterCharacterPresenter == null)
        {
            yield break;
        }

        yield return FadeOutMonsterDrops(0.22f);

        var timing = heroCharacterPresenter.PlayMoveLunge(
            monsterCharacterPresenter,
            moveState: BattleAnimationState.Move,
            returnState: BattleAnimationState.Idle);

        yield return new WaitForSecondsRealtime(timing.ImpactDelay);

        monsterCharacterPresenter.PlayReverseState(BattleAnimationState.Death, BattleAnimationState.Idle);
        yield return new WaitForSecondsRealtime(monsterCharacterPresenter.GetStateDuration(BattleAnimationState.Death));

        var remainingReturn = Mathf.Max(0f, timing.TotalDuration - timing.ImpactDelay);
        if (remainingReturn > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingReturn);
        }
    }

    private System.Collections.IEnumerator PlayHeroReviveAnimation()
    {
        if (heroCharacterPresenter == null)
        {
            yield break;
        }

        heroCharacterPresenter.PlayReverseState(BattleAnimationState.Death, BattleAnimationState.Idle);
        yield return new WaitForSecondsRealtime(heroCharacterPresenter.GetStateDuration(BattleAnimationState.Death));
    }

    private void CommitVictoryRewards(VictoryRewards rewards)
    {
        if (rewards == null || hero == null || rewards.IsCommitted)
        {
            return;
        }

        RunSession.AddCoins(rewards.CoinsAmount);
        hero.Xp += rewards.XpAmount;

        if (!string.IsNullOrWhiteSpace(rewards.LearnedMoveId))
        {
            hero.KnownMoves.Add(rewards.LearnedMoveId);
            RunSession.SetPendingLearnedMove(rewards.LearnedMoveId);
        }

        RunSession.RestoreHeroToFullHealth();
        RunSession.RegisterMonsterKill(currentMonster?.id);
        RunSession.MarkEncounterComplete(encounterIndex, rewards.Summary);
        RunSaveService.SaveCurrentRun();
        rewards.IsCommitted = true;
    }

    private void CommitVictoryRewardsAndReturn()
    {
        if (pendingVictoryRewards == null)
        {
            return;
        }

        CommitVictoryRewards(pendingVictoryRewards);
        pendingVictoryRewards = null;
        heroReviveAvailable = false;
        monsterReviveAvailable = false;
        ClearMonsterDrops();
        SetContinueArrowVisible(false);
        SetBackArrowVisible(false);
    }

    private void CaptureEncounterSnapshot()
    {
        if (hero == null)
        {
            encounterSnapshot = null;
            return;
        }

        encounterSnapshot = new EncounterSnapshot();
    }

    private void SetContinueArrowVisible(bool isVisible)
    {
        if (continueArrowButton != null)
        {
            continueArrowButton.gameObject.SetActive(isVisible);
        }
    }

    private void SetBackArrowVisible(bool isVisible)
    {
        if (backArrowButton != null)
        {
            backArrowButton.gameObject.SetActive(isVisible);
        }
    }

    private void ClearMonsterDrops()
    {
        for (var index = 0; index < runtimeDropObjects.Count; index++)
        {
            if (runtimeDropObjects[index] != null)
            {
                Destroy(runtimeDropObjects[index]);
            }
        }

        runtimeDropObjects.Clear();

        if (monsterDropsRoot == null)
        {
            return;
        }

        for (var index = 0; index < monsterDropsRoot.childCount; index++)
        {
            monsterDropsRoot.GetChild(index).gameObject.SetActive(false);
        }
    }

    private void SpawnMonsterDrops(VictoryRewards rewards)
    {
        ClearMonsterDrops();
        if (monsterDropsRoot == null || monsterDropTemplate == null || rewards == null)
        {
            return;
        }

        var anchorPosition = ResolveDropAnchorPosition();
        if (anchorPosition.HasValue)
        {
            monsterDropsRoot.anchoredPosition = anchorPosition.Value;
        }

        var drops = new List<(Sprite sprite, string label)>
        {
            (ResolveDropSprite(coinDropSprite, reward1Icon), rewards.CoinsText),
            (ResolveDropSprite(xpDropSprite, reward3Icon), rewards.XpText)
        };

        if (rewards.KeyAmount > 0)
        {
            drops.Add((ResolveDropSprite(keyDropSprite, null), rewards.KeyText));
        }

        if (rewards.HasLearnedMove)
        {
            drops.Add((ResolveReward2Icon(rewards.LearnedMoveId), rewards.LearnedMoveText));
        }

        var offsets = BuildDropOffsets(drops.Count, 64f, 118f, 26f);
        for (var index = 0; index < drops.Count; index++)
        {
            var dropObject = Instantiate(monsterDropTemplate.gameObject, monsterDropsRoot);
            dropObject.name = $"Object ({index})";
            dropObject.SetActive(true);
            runtimeDropObjects.Add(dropObject);

            var dropRect = dropObject.GetComponent<RectTransform>();
            if (dropRect != null)
            {
                dropRect.anchoredPosition = offsets[index];
                dropRect.localScale = Vector3.one;
            }

            ConfigureDropVisual(dropObject, drops[index].sprite, drops[index].label);
        }
    }

    private void ConfigureDropVisual(GameObject dropObject, Sprite iconSprite, string label)
    {
        if (dropObject == null)
        {
            return;
        }

        var images = dropObject.GetComponentsInChildren<Image>(true);
        Image iconImage = null;
        for (var index = 0; index < images.Length; index++)
        {
            var imageName = images[index].gameObject.name;
            if (imageName.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                iconImage = images[index];
                break;
            }
        }

        if (iconImage == null && images.Length > 0)
        {
            iconImage = images[images.Length - 1];
        }

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
        }

        var labelText = dropObject.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if (labelText != null)
        {
            labelText.text = label;
        }
    }

    private Sprite ResolveDropSprite(Sprite preferredSprite, Image fallbackImage)
    {
        if (preferredSprite != null)
        {
            return preferredSprite;
        }

        return fallbackImage != null ? fallbackImage.sprite : null;
    }

    private System.Collections.IEnumerator FadeOutMonsterDrops(float duration)
    {
        if (runtimeDropObjects.Count == 0)
        {
            yield break;
        }

        var images = new List<Image>();
        var texts = new List<TMP_Text>();
        var imageColors = new List<Color>();
        var textColors = new List<Color>();

        for (var index = 0; index < runtimeDropObjects.Count; index++)
        {
            var dropObject = runtimeDropObjects[index];
            if (dropObject == null)
            {
                continue;
            }

            var childImages = dropObject.GetComponentsInChildren<Image>(true);
            for (var imageIndex = 0; imageIndex < childImages.Length; imageIndex++)
            {
                images.Add(childImages[imageIndex]);
                imageColors.Add(childImages[imageIndex].color);
            }

            var childTexts = dropObject.GetComponentsInChildren<TMP_Text>(true);
            for (var textIndex = 0; textIndex < childTexts.Length; textIndex++)
            {
                texts.Add(childTexts[textIndex]);
                textColors.Add(childTexts[textIndex].color);
            }
        }

        if (duration <= 0f)
        {
            ClearMonsterDrops();
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);

            for (var index = 0; index < images.Count; index++)
            {
                if (images[index] == null)
                {
                    continue;
                }

                var color = imageColors[index];
                color.a = Mathf.Lerp(imageColors[index].a, 0f, eased);
                images[index].color = color;
            }

            for (var index = 0; index < texts.Count; index++)
            {
                if (texts[index] == null)
                {
                    continue;
                }

                var color = textColors[index];
                color.a = Mathf.Lerp(textColors[index].a, 0f, eased);
                texts[index].color = color;
            }

            yield return null;
        }

        ClearMonsterDrops();
    }

    private Vector2? ResolveDropAnchorPosition()
    {
        var parentRect = monsterDropsRoot != null ? monsterDropsRoot.parent as RectTransform : null;
        var anchorRect = monsterRootRect != null ? monsterRootRect : monsterCharacterRect;
        if (parentRect == null || anchorRect == null)
        {
            return null;
        }

        var worldCenter = anchorRect.TransformPoint(anchorRect.rect.center);
        var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out var localPoint))
        {
            return localPoint + new Vector2(0f, -26f);
        }

        return null;
    }

    private static List<Vector2> BuildDropOffsets(int count, float minRadius, float maxRadius, float jitter)
    {
        var offsets = new List<Vector2>(count);
        if (count <= 0)
        {
            return offsets;
        }

        var baseAngle = Random.Range(0f, Mathf.PI * 2f);
        for (var index = 0; index < count; index++)
        {
            var t = (index + 0.5f) / count;
            var angle = baseAngle + (Mathf.PI * 2f * index / count) + Random.Range(-0.18f, 0.18f);
            var radius = Mathf.Lerp(minRadius, maxRadius, t) + Random.Range(-jitter, jitter);
            offsets.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        return offsets;
    }

    private void AutoBindScene()
    {
        canvasRect = FindComponent<RectTransform>("Canvas");
        heroRootRect = FindComponent<RectTransform>("Canvas/Hero UI/Hero") ??
                       FindComponent<RectTransform>("Canvas/Hero") ??
                       FindComponent<RectTransform>("Hero");
        monsterRootRect = FindComponent<RectTransform>("Canvas/Monster UI/Monster") ??
                          FindComponent<RectTransform>("Canvas/Monster") ??
                          FindComponent<RectTransform>("Monster");
        heroNameText = FindComponent<TMP_Text>("Canvas/Hero/Hero Name") ??
                       FindComponent<TMP_Text>("Hero/Hero Name");
        monsterNameText = FindComponent<TMP_Text>("Canvas/Monster/Monster Name") ??
                          FindComponent<TMP_Text>("Monster/Monster Name");
        heroHpWorldText = FindTextMesh("HeroHPText");
        monsterHpWorldText = FindTextMesh("MonsterHPText");
        heroCharacterPresenter = FindComponent<BattleCharacterPresenter>("Canvas/Hero/Hero Character") ??
                                 FindComponent<BattleCharacterPresenter>("Hero/Hero Character") ??
                                 FindComponent<BattleCharacterPresenter>("Hero Character");
        monsterCharacterPresenter = FindComponent<BattleCharacterPresenter>("Canvas/Monster/Monster Character") ??
                                    FindComponent<BattleCharacterPresenter>("Monster/Monster Character") ??
                                    FindComponent<BattleCharacterPresenter>("Monster Character");
        heroUiAnimator = heroCharacterPresenter != null ? heroCharacterPresenter.GetComponent<UiSpriteSheetAnimator>() : null;
        monsterUiAnimator = monsterCharacterPresenter != null ? monsterCharacterPresenter.GetComponent<UiSpriteSheetAnimator>() : null;
        heroCharacterRect = heroCharacterPresenter != null ? heroCharacterPresenter.GetComponent<RectTransform>() : null;
        monsterCharacterRect = monsterCharacterPresenter != null ? monsterCharacterPresenter.GetComponent<RectTransform>() : null;
        heroHealParticleSystem = FindComponent<ParticleSystem>("Main Camera/Hero Heal Hearts") ??
                                 FindComponent<ParticleSystem>("Hero Heal Hearts");
        monsterHealParticleSystem = FindComponent<ParticleSystem>("Main Camera/Monster Heal Hearts") ??
                                    FindComponent<ParticleSystem>("Monster Heal Hearts");
        endPanelRoot = FindGameObject("Canvas/End Panel");
        defeatPanelRoot = FindGameObject("Canvas/Defeat Panel");
        monsterDropsRoot = FindComponent<RectTransform>("Canvas/Monster Drops");
        monsterDropTemplate = FindComponent<RectTransform>("Canvas/Monster Drops/Object");
        battleLogPanelRoot = FindGameObject("Canvas/Battle Log Panel");
        pauseMenuPanelRoot = FindGameObject("Canvas/Pause Menu Panel");
        battleLogTemplateText = FindComponent<TMP_Text>("Canvas/Battle Log Panel/Battle Log Text");
        pauseMenuHoverText = FindComponent<TMP_Text>("Canvas/Pause Menu Panel/Buttons Hover Text");
        battleLogScrollbar = FindComponent<Scrollbar>("Canvas/Battle Log Panel/Scroll Bar");
        battleLogOpenButton = FindComponent<Button>("Canvas/Battle Log Button");
        battleLogCloseButton = FindComponent<Button>("Canvas/Battle Log Panel/Close Button");
        pauseMenuResumeButton = FindComponent<Button>("Canvas/Pause Menu Panel/Buttons/Resume Button");
        pauseMenuExitBattleButton = FindComponent<Button>("Canvas/Pause Menu Panel/Buttons/Exit Battle Button");
        pauseMenuExitToMainMenuButton = FindComponent<Button>("Canvas/Pause Menu Panel/Buttons/Exit To Main Menu Button");
        usingBattleSceneUi = FindGameObject("Canvas/Moves Bar/Moves/Move 1") != null ||
                             endPanelRoot != null ||
                             defeatPanelRoot != null;

        moveButtons.Clear();
        moveButtonLabels.Clear();
        moveButtonIcons.Clear();
        moveButtonHasMoveStates.Clear();
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
                moveButtonHasMoveStates.Add(false);
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
        moveStatsIcon = FindComponent<Image>("Canvas/Current Move Stats/Move/Move Icon");
        currentMoveStatsRoot = FindGameObject("Canvas/Current Move Stats");
        moveStatsTitleText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Title");
        moveStatsTypeText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Type");
        moveStatsDescriptionText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Description");
        moveStatsAttackText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Attack/Attack Text");
        moveStatsHealText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Heal/Heal Text");
        heroAttackEffectText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Hero Effects Stat/Attack/Attack Text");
        heroDefenseEffectText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Hero Effects Stat/Defense/Defense Text");
        heroMagicEffectText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Hero Effects Stat/Magic/Magic Text");
        monsterAttackEffectText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Monster Effects Stat/Attack/Attack Text");
        monsterDefenseEffectText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Monster Effects Stat/Defense/Defense Text");
        monsterMagicEffectText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Monster Effects Stat/Magic/Magic Text");
        moveStatsEffectsAmountText = FindComponent<TMP_Text>("Canvas/Current Move Stats/Effects/Effects Amount");
        ConfigureCurrentMoveStatsAnimation();

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
        defeatMapButton = FindComponent<Button>("Canvas/Defeat Panel/Exit Button") ??
                          FindComponent<Button>("Canvas/Defeat Panel/Map Button");

        continueArrowButton = ConfigureImageButton("Canvas/Continue Arrow Button", OnContinueArrowPressed);
        backArrowButton = ConfigureImageButton("Canvas/Back Arrow Button", OnBackArrowPressed);
        ConfigureClickableName(heroNameText, isHeroLabel: true);
        ConfigureClickableName(monsterNameText, isHeroLabel: false);

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

        SetDefeatMapButtonLabel(RunSession.IsEndlessMode ? "Exit" : "Map");

        ConfigurePauseMenuButtons();
        ConfigureBattleLogButtons();

        SetEndPanelVisible(false);
        SetDefeatPanelVisible(false);
        SetContinueArrowVisible(false);
        SetBackArrowVisible(false);
        ClearMonsterDrops();
        SetBattleLogVisible(false);
        SetPauseMenuVisible(false);

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
            moveButtonHasMoveStates.Add(false);
            moveHoverSelectorRoots.Add(CreateMoveHoverSelector(moveRoot.transform));
            ConfigureMoveHoverListener(moveRoot, capturedIndex);
        }

        UpdateMoveHoverSelectors();
    }

    private void PrepareHealParticleSystems()
    {
        EnsureParticleRendererMaterialInstance(
            heroHealParticleSystem,
            ref heroParticleDefaultTexture,
            ref heroParticleDefaultColor);
        EnsureParticleRendererMaterialInstance(
            monsterHealParticleSystem,
            ref monsterParticleDefaultTexture,
            ref monsterParticleDefaultColor);

        if (heroHealParticleSystem != null)
        {
            heroHealParticleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (monsterHealParticleSystem != null)
        {
            monsterHealParticleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void PlayHealParticleEffect(bool targetIsHero)
    {
        PlayParticleEffect(
            targetIsHero,
            defaultTexture: targetIsHero ? heroParticleDefaultTexture : monsterParticleDefaultTexture,
            startColor: targetIsHero ? heroParticleDefaultColor : monsterParticleDefaultColor);
    }

    private void PlayModifierParticleEffect(bool targetIsHero, StatModifier modifier, Color particleColor)
    {
        if (modifier == null || modifier.value == 0)
        {
            return;
        }

        var iconSprite = ResolveStatParticleSprite(modifier.stat);
        if (iconSprite == null)
        {
            return;
        }

        PlayParticleEffect(targetIsHero, iconSprite.texture, particleColor);
    }

    private void PlayParticleEffect(
        bool targetIsHero,
        Texture defaultTexture = null,
        Color? particleColor = null,
        ParticleSystem.MinMaxGradient? startColor = null)
    {
        var particleSystem = targetIsHero ? heroHealParticleSystem : monsterHealParticleSystem;
        if (particleSystem == null)
        {
            return;
        }

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.mainTexture = defaultTexture;
        }

        var main = particleSystem.main;
        main.startColor = startColor ?? new ParticleSystem.MinMaxGradient(particleColor ?? Color.white);

        particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Play(withChildren: true);
    }

    private static void EnsureParticleRendererMaterialInstance(
        ParticleSystem particleSystem,
        ref Texture defaultTexture,
        ref ParticleSystem.MinMaxGradient defaultColor)
    {
        if (particleSystem == null)
        {
            return;
        }

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return;
        }

        renderer.material = new Material(renderer.sharedMaterial);
        defaultTexture = renderer.material.mainTexture;
        defaultColor = particleSystem.main.startColor;
    }

    private Sprite ResolveStatParticleSprite(string statName)
    {
        if (string.Equals(statName, "magic", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectMagicIcon?.sprite ?? monsterEffectMagicIcon?.sprite;
        }

        if (string.Equals(statName, "defense", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectDefenseIcon?.sprite ?? monsterEffectDefenseIcon?.sprite;
        }

        if (string.Equals(statName, "attack", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectAttackIcon?.sprite ?? monsterEffectAttackIcon?.sprite;
        }

        return null;
    }

    private void PlayMoveStartEffects(bool actorIsHero, Move move)
    {
        if (move?.effect == "heal")
        {
            PlayHealParticleEffect(actorIsHero);
            return;
        }

        if ((move?.effect == "stat_modifier" || move?.effect == "damage_and_stat_modifier") &&
            move.statModifier != null &&
            move.statModifier.value != 0)
        {
            var targetIsHero = move.target == "self" ? actorIsHero : !actorIsHero;
            var particleColor = ResolveModifierParticleColor(actorIsHero, move, move.statModifier);
            PlayModifierParticleEffect(targetIsHero, move.statModifier, particleColor);
        }
    }

    private Color ResolveModifierParticleColor(bool actorIsHero, Move move, StatModifier modifier)
    {
        if (modifier == null)
        {
            return Color.white;
        }

        var targetIsHero = move.target == "self" ? actorIsHero : !actorIsHero;
        if (targetIsHero == actorIsHero && modifier.value > 0)
        {
            return buffEffectColor;
        }

        if (targetIsHero != actorIsHero && modifier.value < 0)
        {
            return debuffEffectColor;
        }

        return modifier.value > 0 ? buffEffectColor : debuffEffectColor;
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
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Move, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Attack, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Hurt, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Defend, kind);
        SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Death, kind);
    }

    private float PlayActorAnimation(bool actorIsHero, Move move, out float recoveryDelay)
    {
        var actorPresenter = actorIsHero ? heroCharacterPresenter : monsterCharacterPresenter;
        var actorState = ResolveAnimationStateForMove(move);
        recoveryDelay = 0f;

        if (actorPresenter == null)
        {
            return 0f;
        }

        if (IsAttackingMove(move))
        {
            var targetPresenter = actorIsHero ? monsterCharacterPresenter : heroCharacterPresenter;
            var timing = actorPresenter.PlayAttackLunge(targetPresenter);
            recoveryDelay = Mathf.Max(0f, timing.TotalDuration - timing.ImpactDelay);
            return timing.ImpactDelay;
        }

        actorPresenter.PlayTemporaryState(actorState, returnState: BattleAnimationState.Idle);
        recoveryDelay = actorPresenter.GetStateDuration(actorState);
        return 0f;
    }

    private static bool ShouldResolveMoveImmediately(Move move)
    {
        // Healing should respect the same turn pacing as other moves so the
        // next action does not begin before the move animation has time to read.
        return false;
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

    private static bool IsAttackingMove(Move move)
    {
        return move?.effect is "damage" or "damage_and_stat_modifier" or "drain";
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

        if (usingBattleSceneUi)
        {
            InitializeBattleLogUi();
            return;
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

    private void InitializeBattleLogUi()
    {
        if (battleLogPanelRoot == null || battleLogTemplateText == null || battleLogScrollRect != null)
        {
            return;
        }

        var templateRect = battleLogTemplateText.rectTransform;
        var backgroundRect = FindComponent<RectTransform>("Canvas/Battle Log Panel/Background");
        var templateWorldCorners = new Vector3[4];
        templateRect.GetWorldCorners(templateWorldCorners);
        var viewportObject = new GameObject("Battle Log Viewport", typeof(RectTransform), typeof(RectMask2D));
        var viewport = viewportObject.GetComponent<RectTransform>();
        if (backgroundRect != null)
        {
            viewportObject.transform.SetParent(backgroundRect, false);

            var topLeft = (Vector2)backgroundRect.InverseTransformPoint(templateWorldCorners[1]);
            var bottomRight = (Vector2)backgroundRect.InverseTransformPoint(templateWorldCorners[3]);
            var viewportCenter = (topLeft + bottomRight) * 0.5f;
            var viewportSize = new Vector2(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);

            viewport.anchorMin = new Vector2(0.5f, 0.5f);
            viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = viewportCenter;
            viewport.sizeDelta = viewportSize;
        }
        else
        {
            viewportObject.transform.SetParent(battleLogPanelRoot.transform, false);
            viewport.anchorMin = templateRect.anchorMin;
            viewport.anchorMax = templateRect.anchorMax;
            viewport.pivot = templateRect.pivot;
            viewport.anchoredPosition = templateRect.anchoredPosition;
            viewport.sizeDelta = templateRect.sizeDelta;
            viewport.SetSiblingIndex(templateRect.GetSiblingIndex());
        }

        battleLogTemplateText.text = string.Empty;
        battleLogTemplateText.enabled = false;
        templateRect.SetParent(viewport, false);
        templateRect.anchorMin = new Vector2(0f, 1f);
        templateRect.anchorMax = new Vector2(0f, 1f);
        templateRect.pivot = new Vector2(0f, 1f);
        templateRect.anchoredPosition = Vector2.zero;
        templateRect.sizeDelta = Vector2.zero;

        var contentObject = new GameObject("Battle Log Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport, false);
        battleLogContentRoot = contentObject.GetComponent<RectTransform>();
        battleLogContentRoot.anchorMin = new Vector2(0f, 1f);
        battleLogContentRoot.anchorMax = new Vector2(1f, 1f);
        battleLogContentRoot.pivot = new Vector2(0.5f, 1f);
        battleLogContentRoot.anchoredPosition = Vector2.zero;
        battleLogContentRoot.sizeDelta = new Vector2(0f, 0f);

        var layoutGroup = contentObject.GetComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 14f;
        layoutGroup.padding = new RectOffset(18, 18, 18, 18);
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;

        var sizeFitter = contentObject.GetComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        battleLogScrollRect = battleLogPanelRoot.GetComponent<ScrollRect>();
        if (battleLogScrollRect == null)
        {
            battleLogScrollRect = battleLogPanelRoot.AddComponent<ScrollRect>();
        }

        battleLogScrollRect.viewport = viewport;
        battleLogScrollRect.content = battleLogContentRoot;
        battleLogScrollRect.horizontal = false;
        battleLogScrollRect.vertical = true;
        battleLogScrollRect.movementType = ScrollRect.MovementType.Clamped;
        battleLogScrollRect.scrollSensitivity = 40f;
        battleLogScrollRect.inertia = true;
        battleLogScrollRect.verticalNormalizedPosition = 1f;

        var trackImage = FindComponent<Image>("Canvas/Battle Log Panel/Scroll Bar/Scroll Back");
        if (trackImage != null)
        {
            battleLogScrollbarTrackRect = trackImage.rectTransform;
        }

        var handleRect = FindComponent<RectTransform>("Canvas/Battle Log Panel/Scroll Bar/Scroll");
        var handleImage = FindComponent<Image>("Canvas/Battle Log Panel/Scroll Bar/Scroll");
        if (handleRect != null)
        {
            battleLogScrollbarHandleRect = handleRect;
            battleLogScrollbarHandleHeight = Mathf.Max(handleRect.sizeDelta.y, handleRect.rect.height);
        }

        if (battleLogScrollbar == null)
        {
            var scrollbarObject = FindGameObject("Canvas/Battle Log Panel/Scroll Bar");
            if (scrollbarObject != null)
            {
                battleLogScrollbar = scrollbarObject.GetComponent<Scrollbar>();
                if (battleLogScrollbar == null)
                {
                    battleLogScrollbar = scrollbarObject.AddComponent<Scrollbar>();
                }

                if (trackImage != null)
                {
                    battleLogScrollbar.targetGraphic = handleImage != null ? handleImage : trackImage;
                }

                if (handleRect != null)
                {
                    var slidingArea = EnsureBattleLogScrollbarSlidingArea();
                    if (slidingArea != null && handleRect.parent != slidingArea)
                    {
                        handleRect.SetParent(slidingArea, false);
                    }

                    ConfigureBattleLogScrollbarHandleLayout(handleRect);
                    battleLogScrollbarHandleRect = handleRect;
                    battleLogScrollbar.handleRect = handleRect;
                }

                battleLogScrollbar.direction = Scrollbar.Direction.BottomToTop;
                battleLogScrollbar.numberOfSteps = 0;
                battleLogScrollbar.value = 1f;
            }
        }

        var ensuredSlidingArea = EnsureBattleLogScrollbarSlidingArea();
        if (battleLogScrollbarHandleRect != null && ensuredSlidingArea != null && battleLogScrollbarHandleRect.parent != ensuredSlidingArea)
        {
            battleLogScrollbarHandleRect.SetParent(ensuredSlidingArea, false);
        }

        if (battleLogScrollbar != null)
        {
            battleLogScrollbar.targetGraphic = handleImage != null ? handleImage : trackImage;
            battleLogScrollbar.handleRect = battleLogScrollbarHandleRect;
        }

        if (battleLogScrollbarHandleRect != null)
        {
            ConfigureBattleLogScrollbarHandleLayout(battleLogScrollbarHandleRect);
        }

        if (battleLogScrollbar != null)
        {
            battleLogScrollbar.onValueChanged.RemoveListener(OnBattleLogScrollbarValueChanged);
            battleLogScrollbar.onValueChanged.AddListener(OnBattleLogScrollbarValueChanged);
        }

        battleLogScrollRect.onValueChanged.RemoveListener(OnBattleLogScrollRectValueChanged);
        battleLogScrollRect.onValueChanged.AddListener(OnBattleLogScrollRectValueChanged);
        SyncBattleLogScrollbarHandle();
    }

    private void ConfigureBattleLogButtons()
    {
        if (battleLogOpenButton == null)
        {
            var openButtonObject = FindGameObject("Canvas/Battle Log Button");
            if (openButtonObject != null)
            {
                battleLogOpenButton = openButtonObject.GetComponent<Button>();
                if (battleLogOpenButton == null)
                {
                    battleLogOpenButton = openButtonObject.AddComponent<Button>();
                }

                var image = openButtonObject.GetComponent<Image>();
                if (image != null)
                {
                    battleLogOpenButton.targetGraphic = image;
                }
            }
        }

        if (battleLogOpenButton != null)
        {
            battleLogOpenButton.onClick.RemoveAllListeners();
            battleLogOpenButton.onClick.AddListener(OnBattleLogOpenButtonPressed);
        }

        if (battleLogCloseButton == null)
        {
            var closeButtonObject = FindGameObject("Canvas/Battle Log Panel/Close Button");
            if (closeButtonObject != null)
            {
                battleLogCloseButton = closeButtonObject.GetComponent<Button>();
                if (battleLogCloseButton == null)
                {
                    battleLogCloseButton = closeButtonObject.AddComponent<Button>();
                }

                var image = closeButtonObject.GetComponent<Image>();
                if (image != null)
                {
                    battleLogCloseButton.targetGraphic = image;
                }
            }
        }

        if (battleLogCloseButton != null)
        {
            battleLogCloseButton.onClick.RemoveAllListeners();
            battleLogCloseButton.onClick.AddListener(OnBattleLogCloseButtonPressed);
        }
    }

    private void ClearBattleLog()
    {
        if (battleLogContentRoot == null)
        {
            return;
        }

        for (var index = battleLogContentRoot.childCount - 1; index >= 0; index--)
        {
            var child = battleLogContentRoot.GetChild(index);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        SyncBattleLogScrollbarHandle();
    }

    private void AddBattleLogMove(BattleLogDescriptor descriptor)
    {
        if (descriptor == null || battleLogContentRoot == null || !usingBattleSceneUi)
        {
            return;
        }

        var entry = CreateBattleLogEntryContainer();
        var row = CreateBattleLogRow(entry.transform, "Entry");

        if (descriptor.ShowActorIcon)
        {
            CreateBattleLogIcon(row.transform, ResolveBattleLogCharacterSprite(descriptor.ActorIsHero), descriptor.ActorIsHero ? "Hero" : "Monster");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.PrefixText))
        {
            CreateBattleLogTextSegment(row.transform, descriptor.PrefixText);
        }

        if (!string.IsNullOrWhiteSpace(descriptor.MoveName))
        {
            CreateBattleLogTextSegment(row.transform, descriptor.MoveName);
        }

        if (descriptor.MoveSprite != null)
        {
            CreateBattleLogIcon(row.transform, descriptor.MoveSprite, descriptor.MoveName ?? "Move");
        }

        if (descriptor.ShowTargetIcon)
        {
            CreateBattleLogTextSegment(row.transform, "and");
            CreateBattleLogIcon(row.transform, ResolveBattleLogCharacterSprite(descriptor.TargetIsHero), descriptor.TargetIsHero ? "Hero" : "Monster");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.SuffixText))
        {
            CreateBattleLogTextSegment(row.transform, descriptor.SuffixText, true);
        }

        if (!string.IsNullOrWhiteSpace(descriptor.SecondaryText) || descriptor.SecondaryDetailSprite != null || descriptor.ShowSecondaryTargetIcon)
        {
            var secondaryRow = CreateBattleLogRow(entry.transform, "Detail");

            if (descriptor.ShowSecondaryTargetIcon)
            {
                CreateBattleLogIcon(
                    secondaryRow.transform,
                    ResolveBattleLogCharacterSprite(descriptor.SecondaryTargetIsHero),
                    descriptor.SecondaryTargetIsHero ? "Hero" : "Monster");
            }

            if (descriptor.SecondaryDetailSprite != null)
            {
                CreateBattleLogIcon(secondaryRow.transform, descriptor.SecondaryDetailSprite, "Stat");
            }

            if (!string.IsNullOrWhiteSpace(descriptor.SecondaryText))
            {
                CreateBattleLogTextSegment(secondaryRow.transform, descriptor.SecondaryText, true);
            }
        }

        ScrollBattleLogToLatest();
    }

    private GameObject CreateBattleLogEntryContainer()
    {
        var entry = new GameObject("Battle Log Item", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        entry.transform.SetParent(battleLogContentRoot, false);
        entry.transform.SetAsFirstSibling();

        var entryRect = entry.GetComponent<RectTransform>();
        entryRect.anchorMin = new Vector2(0f, 1f);
        entryRect.anchorMax = new Vector2(1f, 1f);
        entryRect.pivot = new Vector2(0.5f, 1f);
        entryRect.sizeDelta = Vector2.zero;

        var layout = entry.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = entry.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var element = entry.GetComponent<LayoutElement>();
        element.flexibleWidth = 1f;

        return entry;
    }

    private GameObject CreateBattleLogRow(Transform parent, string suffix)
    {
        var row = new GameObject($"Battle Log {suffix}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = Vector2.zero;

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        var fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var element = row.GetComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minHeight = 42f;

        return row;
    }

    private Image CreateBattleLogIcon(Transform parent, Sprite sprite, string iconName)
    {
        if (sprite == null)
        {
            return null;
        }

        var iconObject = new GameObject($"{iconName} Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(parent, false);

        var image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        var layout = iconObject.GetComponent<LayoutElement>();
        var iconSize = string.Equals(iconName, "Stat", StringComparison.OrdinalIgnoreCase) ? 32f : 42f;
        layout.preferredWidth = iconSize;
        layout.preferredHeight = iconSize;
        layout.minWidth = iconSize;
        layout.minHeight = iconSize;

        return image;
    }

    private TMP_Text CreateBattleLogTextSegment(Transform parent, string text, bool flexibleWidth = false)
    {
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        var segment = textObject.GetComponent<TextMeshProUGUI>();
        segment.text = text;
        segment.font = battleLogTemplateText.font;
        segment.fontSharedMaterial = battleLogTemplateText.fontSharedMaterial;
        segment.color = battleLogTemplateText.color;
        segment.fontSize = 28f;
        segment.enableAutoSizing = false;
        segment.alignment = TextAlignmentOptions.MidlineLeft;
        segment.textWrappingMode = flexibleWidth ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        segment.overflowMode = TextOverflowModes.Overflow;
        segment.raycastTarget = false;

        var layout = textObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = flexibleWidth ? 1f : 0f;
        layout.minWidth = 0f;

        return segment;
    }

    private void ScrollBattleLogToLatest()
    {
        if (battleLogScrollRect == null || battleLogContentRoot == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(battleLogContentRoot);
        battleLogScrollRect.verticalNormalizedPosition = 1f;
        SyncBattleLogScrollbarHandle();
    }

    private void OnBattleLogScrollbarValueChanged(float value)
    {
        if (suppressBattleLogScrollbarCallbacks || battleLogScrollRect == null)
        {
            return;
        }

        battleLogScrollRect.verticalNormalizedPosition = Mathf.Clamp01(value);
        SyncBattleLogScrollbarHandle();
    }

    private void OnBattleLogScrollRectValueChanged(Vector2 normalizedPosition)
    {
        SyncBattleLogScrollbarHandle(normalizedPosition.y);
    }

    private void SyncBattleLogScrollbarHandle()
    {
        SyncBattleLogScrollbarHandle(battleLogScrollRect != null ? battleLogScrollRect.verticalNormalizedPosition : 1f);
    }

    private void SyncBattleLogScrollbarHandle(float normalizedValue)
    {
        if (battleLogScrollbar == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(battleLogContentRoot);

        var viewportHeight = battleLogScrollRect != null && battleLogScrollRect.viewport != null
            ? battleLogScrollRect.viewport.rect.height
            : 0f;
        var contentHeight = battleLogContentRoot != null ? battleLogContentRoot.rect.height : 0f;
        var needsScroll = contentHeight > viewportHeight + 1f;

        battleLogScrollbar.gameObject.SetActive(needsScroll);
        if (battleLogScrollbarTrackRect != null)
        {
            battleLogScrollbarTrackRect.gameObject.SetActive(needsScroll);
        }

        if (!needsScroll)
        {
            normalizedValue = 1f;
        }

        suppressBattleLogScrollbarCallbacks = true;
        var trackHeight = battleLogScrollbarTrackRect != null ? battleLogScrollbarTrackRect.rect.height : 0f;
        var handleHeight = GetBattleLogScrollbarHandleHeight();
        battleLogScrollbar.size = trackHeight > 0f
            ? Mathf.Clamp01(handleHeight / trackHeight)
            : 1f;
        battleLogScrollbar.SetValueWithoutNotify(Mathf.Clamp01(normalizedValue));
        suppressBattleLogScrollbarCallbacks = false;

        if (battleLogScrollbarHandleRect == null)
        {
            return;
        }

        ConfigureBattleLogScrollbarHandleLayout(battleLogScrollbarHandleRect);

        var availableTravel = Mathf.Max(0f, trackHeight - handleHeight);
        var anchoredY = -(1f - Mathf.Clamp01(normalizedValue)) * availableTravel;
        battleLogScrollbarHandleRect.anchoredPosition = new Vector2(0f, anchoredY);
    }

    private void OnBattleLogOpenButtonPressed()
    {
        SetBattleLogVisible(true);
    }

    private void OnBattleLogCloseButtonPressed()
    {
        SetBattleLogVisible(false);
    }

    private void SetBattleLogVisible(bool visible)
    {
        battleLogVisible = visible;

        if (battleLogPanelRoot != null)
        {
            battleLogPanelRoot.SetActive(visible);
            if (visible)
            {
                battleLogPanelRoot.transform.SetAsLastSibling();
            }
        }

        if (battleLogCloseButton != null)
        {
            battleLogCloseButton.gameObject.SetActive(visible);
            if (visible)
            {
                battleLogCloseButton.transform.SetAsLastSibling();
            }
        }

        if (battleLogOpenButton != null)
        {
            battleLogOpenButton.gameObject.SetActive(!visible);
            if (!visible)
            {
                battleLogOpenButton.transform.SetAsLastSibling();
            }
        }

        if (visible)
        {
            ScrollBattleLogToLatest();
        }
    }

    private Sprite ResolveBattleLogCharacterSprite(bool isHero)
    {
        var spriteKey = isHero ? ResolveHeroSpriteKey() : ResolveMonsterSpriteKey();
        var spriteKind = isHero ? CharacterSpriteKind.Hero : CharacterSpriteKind.Monster;
        var frames = SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Idle, spriteKind);
        return frames.FirstOrDefault(sprite => sprite != null);
    }

    private Sprite ResolveBattleLogStatSprite(string statName)
    {
        if (string.Equals(statName, "magic", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectMagicIcon?.sprite ?? monsterEffectMagicIcon?.sprite;
        }

        if (string.Equals(statName, "defense", StringComparison.OrdinalIgnoreCase))
        {
            return heroEffectDefenseIcon?.sprite ?? monsterEffectDefenseIcon?.sprite;
        }

        return heroEffectAttackIcon?.sprite ?? monsterEffectAttackIcon?.sprite;
    }

    private static string BuildBattleLogDamageSummary(Move move, int damage, bool continueSentence = false)
    {
        var damageKind = string.Equals(move?.type, "magic", StringComparison.OrdinalIgnoreCase)
            ? "magic damage"
            : "damage";
        return continueSentence
            ? $"dealt {damage} {damageKind},"
            : $"dealt {damage} {damageKind}.";
    }

    private static string BuildModifierBattleLogSummary(StatModifier modifier)
    {
        if (modifier == null)
        {
            return "effect applied.";
        }

        var statName = string.IsNullOrWhiteSpace(modifier.stat) ? "stat" : modifier.stat;
        var label = statName.ToLowerInvariant();
        var signedAmount = modifier.value >= 0 ? $"+{modifier.value}" : modifier.value.ToString();
        var turns = Mathf.Max(1, modifier.durationTurns);
        var turnLabel = turns == 1 ? "turn" : "turns";
        return $"{label} {signedAmount} for {turns} {turnLabel}.";
    }

    private RectTransform EnsureBattleLogScrollbarSlidingArea()
    {
        if (battleLogScrollbarTrackRect == null)
        {
            return null;
        }

        if (battleLogScrollbarSlidingAreaRect != null)
        {
            return battleLogScrollbarSlidingAreaRect;
        }

        Transform existingChild = null;
        for (var index = 0; index < battleLogScrollbarTrackRect.childCount; index++)
        {
            var child = battleLogScrollbarTrackRect.GetChild(index);
            if (child.name == "Sliding Area")
            {
                existingChild = child;
                break;
            }
        }

        if (existingChild == null)
        {
            var slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
            slidingAreaObject.transform.SetParent(battleLogScrollbarTrackRect, false);
            existingChild = slidingAreaObject.transform;
        }

        battleLogScrollbarSlidingAreaRect = existingChild as RectTransform;
        if (battleLogScrollbarSlidingAreaRect == null)
        {
            return null;
        }

        battleLogScrollbarSlidingAreaRect.anchorMin = Vector2.zero;
        battleLogScrollbarSlidingAreaRect.anchorMax = Vector2.one;
        battleLogScrollbarSlidingAreaRect.pivot = new Vector2(0.5f, 0.5f);
        battleLogScrollbarSlidingAreaRect.offsetMin = Vector2.zero;
        battleLogScrollbarSlidingAreaRect.offsetMax = Vector2.zero;

        return battleLogScrollbarSlidingAreaRect;
    }

    private void ConfigureBattleLogScrollbarHandleLayout(RectTransform handleRect)
    {
        if (handleRect == null)
        {
            return;
        }

        var handleHeight = GetBattleLogScrollbarHandleHeight();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.offsetMin = new Vector2(0f, -handleHeight);
        handleRect.offsetMax = new Vector2(0f, 0f);
    }

    private float GetBattleLogScrollbarHandleHeight()
    {
        if (battleLogScrollbarHandleHeight <= 0f && battleLogScrollbarHandleRect != null)
        {
            battleLogScrollbarHandleHeight = Mathf.Max(battleLogScrollbarHandleRect.sizeDelta.y, battleLogScrollbarHandleRect.rect.height);
        }

        return Mathf.Max(1f, battleLogScrollbarHandleHeight);
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
