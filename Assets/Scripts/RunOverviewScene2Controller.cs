using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TheTower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RunOverviewScene2Controller : MonoBehaviour, IMoveLoadoutController
{
    private static readonly Color EmptyMoveSlotTint = new(1f, 1f, 1f, 0.28f);
    private static readonly Color LockedMonsterTint = Color.black;
    private static readonly Color AvailableMonsterTint = Color.white;
    private static readonly Color SelectedSelectorTint = Color.white;
    private static readonly Color HoverSelectorTint = new(0.72f, 0.72f, 0.72f, 1f);
    private const string MapContinueLabel = "Continue";
    private const string MapReviveLabel = "Revive monster";
    private const string PauseExitGameHoverText = "Exit the game application.";
    private const string PauseExitMainMenuHoverText = "Return to the main menu.";

    [Header("Navigation")]
    [SerializeField] private string battleSceneName = "BattleScene";
    [SerializeField] private string heroSelectSceneName = "HeroSelectScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Hero")]
    [SerializeField] private float heroIdleFramesPerSecond = 10f;

    [Header("Moves")]
    [SerializeField] private Sprite emptyMovesInventorySprite;
    [SerializeField] private Sprite selectorTopLeft;
    [SerializeField] private Sprite selectorTopRight;
    [SerializeField] private Sprite selectorBottomLeft;
    [SerializeField] private Sprite selectorBottomRight;
    [SerializeField] private float selectorCornerSize = 24f;
    [SerializeField] private Sprite pressedButtonSprite;
    [SerializeField] private Color buttonHoverTint = new(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color buttonPressedTint = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Vector2 pressedButtonTextOffset = new(0f, -6f);

    private Image heroImage;
    private UiSpriteSheetAnimator heroAnimator;
    private Button xpBarButton;
    private Image xpFillImage;
    private TMP_Text xpValueText;
    private GameObject levelPanelRoot;
    private TMP_Text levelTitleText;
    private TMP_Text levelDescText;
    private TMP_Text levelHelpText;
    private TMP_Text attackText;
    private TMP_Text defenseText;
    private TMP_Text magicText;
    private Button attackButton;
    private Button defenseButton;
    private Button magicButton;
    private Button closeButton;
    private Canvas canvas;
    private Transform canvasRoot;
    private RectTransform movesBarRoot;
    private RectTransform bottomMovesRoot;
    private RectTransform panelMovesRoot;
    private RectTransform movesInventoryRoot;
    private GameObject movesPanelRoot;
    private Button movesBarButton;
    private Button movesPanelCloseButton;
    private RectTransform dragLayer;
    private Image moveStatsIcon;
    private TMP_Text moveStatsTitleText;
    private TMP_Text moveStatsTypeText;
    private TMP_Text moveStatsDescriptionText;
    private TMP_Text moveStatsAttackText;
    private TMP_Text moveStatsHealText;
    private GameObject currentMoveStatsRoot;
    private TMP_Text heroAttackEffectText;
    private TMP_Text heroDefenseEffectText;
    private TMP_Text heroMagicEffectText;
    private TMP_Text monsterAttackEffectText;
    private TMP_Text monsterDefenseEffectText;
    private TMP_Text monsterMagicEffectText;
    private TMP_Text moveStatsEffectsAmountText;
    private GameObject mapPanelRoot;
    private TMP_Text mapPanelText;
    private Button mapButton;
    private Button mapPanelCloseButton;
    private Button mapStartButton;
    private TMP_Text mapStartButtonText;
    private GameObject pauseMenuPanelRoot;
    private TMP_Text pauseMenuHoverText;
    private Button pauseMenuResumeButton;
    private Button pauseMenuExitGameButton;
    private Button pauseMenuExitToMainMenuButton;
    private Coroutine pendingDropRefresh;
    private string hoveredMoveId;
    private int hoveredEncounterIndex = -1;
    private int selectedEncounterIndex = -1;
    private int initialBottomEquippedSlotCount;
    private int initialPanelEquippedSlotCount;
    private int initialInventorySlotCount;
    private bool pauseMenuOpen;
    private readonly List<MoveSlotView> bottomEquippedSlots = new();
    private readonly List<MoveSlotView> panelEquippedSlots = new();
    private readonly List<MoveSlotView> inventorySlots = new();
    private readonly List<MapMonsterSlotView> monsterSlots = new();

    private sealed class MoveSlotView
    {
        public RectTransform Root;
        public Image Icon;
        public Image Background;
        public GameObject SelectorRoot;
        public Sprite DefaultIconSprite;
        public Sprite DefaultBackgroundSprite;
        public string MoveId;
    }

    private sealed class MoveHoverPreviewItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RunOverviewScene2Controller owner;
        private string moveId;

        public void Initialize(RunOverviewScene2Controller controller, string currentMoveId)
        {
            owner = controller;
            moveId = currentMoveId;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.HandleMoveHoverChanged(moveId, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HandleMoveHoverChanged(moveId, false);
        }

        private void OnDisable()
        {
        }
    }

    private sealed class MonsterHoverPreviewItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RunOverviewScene2Controller owner;
        private int encounterIndex;

        public void Initialize(RunOverviewScene2Controller controller, int currentEncounterIndex)
        {
            owner = controller;
            encounterIndex = currentEncounterIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.HandleMonsterHoverChanged(encounterIndex, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HandleMonsterHoverChanged(encounterIndex, false);
        }

        private void OnDisable()
        {
        }
    }

    private sealed class PauseMenuHoverTextTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RunOverviewScene2Controller owner;
        private string hoverMessage;

        public void Initialize(RunOverviewScene2Controller controller, string message)
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

    private sealed class MapMonsterSlotView
    {
        public int EncounterIndex;
        public RectTransform Root;
        public Image Icon;
        public Button Button;
        public GameObject SelectorRoot;
        public TMP_Text Label;
        public Sprite DefaultSprite;
    }

    private sealed class PressedButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button button;
        private Image backgroundImage;
        private RectTransform labelRect;
        private TMP_Text labelText;
        private Sprite normalSprite;
        private Sprite pressedSprite;
        private Color normalColor;
        private Color disabledColor;
        private Color labelNormalColor;
        private Color hoverColor;
        private Color pressedColor;
        private Vector2 labelBasePosition;
        private Vector2 pressedLabelOffset;
        private bool isHovered;
        private bool isPressed;
        private bool wasInteractable;

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
            labelText = targetLabelRect != null ? targetLabelRect.GetComponent<TMP_Text>() : null;
            pressedSprite = targetPressedSprite;
            hoverColor = targetHoverColor;
            pressedColor = targetPressedColor;
            pressedLabelOffset = targetPressedLabelOffset;
            disabledColor = targetButton != null ? targetButton.colors.disabledColor : new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);

            if (backgroundImage != null)
            {
                normalSprite = backgroundImage.sprite;
                normalColor = backgroundImage.color;
            }

            if (labelRect != null)
            {
                labelBasePosition = labelRect.anchoredPosition;
            }

            if (labelText != null)
            {
                labelNormalColor = labelText.color;
            }

            wasInteractable = button == null || button.IsInteractable();
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

        private void LateUpdate()
        {
            if (button == null)
            {
                return;
            }

            var isInteractable = button.IsInteractable();
            if (isInteractable == wasInteractable)
            {
                return;
            }

            wasInteractable = isInteractable;
            if (!isInteractable)
            {
                isHovered = false;
                isPressed = false;
            }

            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            var isInteractable = button == null || button.IsInteractable();

            if (backgroundImage != null)
            {
                backgroundImage.sprite = isInteractable && isPressed && pressedSprite != null ? pressedSprite : normalSprite;
                backgroundImage.color = !isInteractable
                    ? disabledColor
                    : isPressed
                    ? pressedColor
                    : isHovered ? hoverColor : normalColor;
            }

            if (labelRect != null)
            {
                labelRect.anchoredPosition = labelBasePosition + (isInteractable && isPressed ? pressedLabelOffset : Vector2.zero);
            }

            if (labelText != null)
            {
                labelText.color = isInteractable ? labelNormalColor : disabledColor;
            }
        }
    }

    private void Awake()
    {
        AutoBindScene();
        ConfigureStaticUi();
        ConfigureHeroAnimation();
    }

    private void Start()
    {
        if (!RunSession.HasActiveRun || RunSession.IsDefeated)
        {
            SceneManager.LoadScene(heroSelectSceneName);
            return;
        }

        RefreshUi();
        SetLevelPanelVisible(false);
        SetMapPanelVisible(false);
        SetMovesPanelVisible(false);
        SetPauseMenuVisible(false);
    }

    private void Update()
    {
        if (pauseMenuPanelRoot == null)
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
            ResumeFromPauseMenu();
            return;
        }

        OpenPauseMenu();
    }

    private void AutoBindScene()
    {
        canvasRoot = GameObject.Find("Canvas")?.transform;
        canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
        heroImage = FindComponent<Image>("Hero");
        xpFillImage = FindComponent<Image>("XP Bar/XP Bar");
        xpValueText = FindComponent<TMP_Text>("XP Bar/Value");
        levelPanelRoot = FindChild("Level Panel")?.gameObject;
        levelTitleText = FindComponent<TMP_Text>("Level Panel/Title");
        levelDescText = FindComponent<TMP_Text>("Level Panel/Desc");
        levelHelpText = FindComponent<TMP_Text>("Level Panel/Help");
        attackText = FindComponent<TMP_Text>("Level Panel/Stats/Attack Text");
        defenseText = FindComponent<TMP_Text>("Level Panel/Stats/Defense Text");
        magicText = FindComponent<TMP_Text>("Level Panel/Stats/Magic Text");
        attackButton = FindComponent<Button>("Level Panel/Boost Buttons/Attack Button");
        defenseButton = FindComponent<Button>("Level Panel/Boost Buttons/Defense Button");
        magicButton = FindComponent<Button>("Level Panel/Boost Buttons/Magic Button");
        closeButton = FindComponent<Button>("Level Panel/Close Button");
        movesBarRoot = FindChild("Moves Bar") as RectTransform;
        bottomMovesRoot = FindChild("Moves Bar/Moves") as RectTransform;
        panelMovesRoot = FindChild("Moves Panel/Moves") as RectTransform;
        movesInventoryRoot = FindChild("Moves Panel/Moves Inventory") as RectTransform;
        movesPanelRoot = FindChild("Moves Panel")?.gameObject;
        movesPanelCloseButton = FindComponent<Button>("Moves Panel/Close Button");
        moveStatsIcon = FindComponent<Image>("Moves Panel/Current Move Stats/Move/Move Icon");
        currentMoveStatsRoot = FindChild("Moves Panel/Current Move Stats")?.gameObject;
        moveStatsTitleText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Title");
        moveStatsTypeText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Type");
        moveStatsDescriptionText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Description");
        moveStatsAttackText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Action/Attack/Attack Text");
        moveStatsHealText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Action/Heal/Heal Text");
        heroAttackEffectText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Hero Effects Stat/Attack/Attack Text");
        heroDefenseEffectText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Hero Effects Stat/Defense/Defense Text");
        heroMagicEffectText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Hero Effects Stat/Magic/Magic Text");
        monsterAttackEffectText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Monster Effects Stat/Attack/Attack Text");
        monsterDefenseEffectText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Monster Effects Stat/Defense/Defense Text");
        monsterMagicEffectText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Monster Effects Stat/Magic/Magic Text");
        moveStatsEffectsAmountText = FindComponent<TMP_Text>("Moves Panel/Current Move Stats/Effects/Effects Amount");
        mapButton = EnsureButton(FindChild("Map Button"));
        mapPanelRoot = FindChild("Map Panel")?.gameObject;
        mapPanelText = FindComponent<TMP_Text>("Map Panel/Text");
        mapPanelCloseButton = FindComponent<Button>("Map Panel/Close Button");
        mapStartButton = FindComponent<Button>("Map Panel/Start Button");
        mapStartButtonText = FindComponent<TMP_Text>("Map Panel/Start Button/Text");
        pauseMenuPanelRoot = FindChild("Pause Menu Panel")?.gameObject;
        pauseMenuHoverText = FindComponent<TMP_Text>("Pause Menu Panel/Buttons Hover Text");
        pauseMenuResumeButton = FindComponent<Button>("Pause Menu Panel/Buttons/Resume Button");
        pauseMenuExitGameButton = FindComponent<Button>("Pause Menu Panel/Buttons/Exit Game Button");
        pauseMenuExitToMainMenuButton = FindComponent<Button>("Pause Menu Panel/Buttons/Exit To Main Menu Button");

        xpBarButton = EnsureButton(FindChild("XP Bar"));
        if (xpBarButton == null)
        {
            xpBarButton = EnsureButton(FindChild("XP Bar/Backround"));
        }
    }

    private void ConfigureStaticUi()
    {
        if (xpBarButton != null)
        {
            xpBarButton.onClick.RemoveAllListeners();
            xpBarButton.onClick.AddListener(OpenLevelPanel);
        }

        ConfigureLevelButton(attackButton, "attack");
        ConfigureLevelButton(defenseButton, "defense");
        ConfigureLevelButton(magicButton, "magic");
        ConfigurePressedButtonFeedback(attackButton);
        ConfigurePressedButtonFeedback(defenseButton);
        ConfigurePressedButtonFeedback(magicButton);

        if (mapButton != null)
        {
            mapButton.onClick.RemoveAllListeners();
            mapButton.onClick.AddListener(OpenMapPanel);
        }

        if (mapPanelCloseButton != null)
        {
            mapPanelCloseButton.onClick.RemoveAllListeners();
            mapPanelCloseButton.onClick.AddListener(() => SetMapPanelVisible(false));
        }

        if (mapStartButton != null)
        {
            mapStartButton.onClick.RemoveAllListeners();
            mapStartButton.onClick.AddListener(EnterSelectedEncounter);
        }

        ConfigurePressedButtonFeedback(mapStartButton);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => SetLevelPanelVisible(false));
        }

        if (xpFillImage != null)
        {
            xpFillImage.type = Image.Type.Filled;
            xpFillImage.fillMethod = Image.FillMethod.Horizontal;
            xpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        if (movesBarRoot != null)
        {
            var movesBarImage = movesBarRoot.GetComponent<Image>();
            if (movesBarImage == null)
            {
                movesBarImage = movesBarRoot.gameObject.AddComponent<Image>();
                movesBarImage.color = new Color(1f, 1f, 1f, 0.001f);
            }

            movesBarButton = movesBarRoot.GetComponent<Button>();
            if (movesBarButton == null)
            {
                movesBarButton = movesBarRoot.gameObject.AddComponent<Button>();
            }

            movesBarButton.onClick.RemoveAllListeners();
            movesBarButton.onClick.AddListener(ToggleMovesPanel);
        }

        if (movesPanelCloseButton != null)
        {
            movesPanelCloseButton.onClick.RemoveAllListeners();
            movesPanelCloseButton.onClick.AddListener(() => SetMovesPanelVisible(false));
        }

        EnsureDragLayer();
        CacheMoveSlots(bottomMovesRoot, bottomEquippedSlots);
        CacheMoveSlots(panelMovesRoot, panelEquippedSlots);
        CacheMoveSlots(movesInventoryRoot, inventorySlots);
        CacheMonsterSlots();
        initialBottomEquippedSlotCount = bottomEquippedSlots.Count;
        initialPanelEquippedSlotCount = panelEquippedSlots.Count;
        initialInventorySlotCount = inventorySlots.Count;
        ConfigurePauseMenuButtons();
    }

    private void ConfigureHeroAnimation()
    {
        if (heroImage == null)
        {
            return;
        }

        heroImage.preserveAspect = true;
        var spriteKey = RunSession.SelectedHeroDefinition?.spriteKey;
        var idleFrames = SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Idle, CharacterSpriteKind.Hero);
        if (idleFrames == null || idleFrames.Length == 0)
        {
            return;
        }

        heroAnimator = heroImage.GetComponent<UiSpriteSheetAnimator>();
        if (heroAnimator == null)
        {
            heroAnimator = heroImage.gameObject.AddComponent<UiSpriteSheetAnimator>();
        }

        heroAnimator.Frames = idleFrames;
        heroAnimator.FramesPerSecond = heroIdleFramesPerSecond;
        heroAnimator.Loop = true;
        heroAnimator.Restart();
    }

    private void RefreshUi()
    {
        RefreshHeroAnimation();
        RefreshXpBar();
        RefreshLevelPanel();
        RefreshMovesUi();
        RefreshMapPanel();
    }

    private void RefreshHeroAnimation()
    {
        if (heroAnimator == null && heroImage != null)
        {
            ConfigureHeroAnimation();
        }
    }

    private void RefreshXpBar()
    {
        if (!RunSession.HasActiveRun || RunSession.Hero == null)
        {
            return;
        }

        var hero = RunSession.Hero;
        var nextThreshold = RunSession.GetNextLevelXpThreshold();
        var previousThreshold = hero.Level <= 1 || RunSession.CurrentRunConfig?.xpTable == null
            ? 0
            : RunSession.CurrentRunConfig.xpTable[Mathf.Clamp(hero.Level - 1, 0, RunSession.CurrentRunConfig.xpTable.Count - 1)];

        if (xpValueText != null)
        {
            xpValueText.text = nextThreshold >= 0
                ? $"LV {hero.Level}   {hero.Xp}/{nextThreshold}   {hero.Coins} coins"
                : $"LV {hero.Level}   MAX   {hero.Coins} coins";
        }

        if (xpFillImage != null)
        {
            if (nextThreshold < 0)
            {
                xpFillImage.fillAmount = 1f;
            }
            else
            {
                var span = Mathf.Max(1, nextThreshold - previousThreshold);
                var progress = hero.Xp - previousThreshold;
                xpFillImage.fillAmount = Mathf.Clamp01(progress / (float)span);
            }
        }
    }

    private void RefreshLevelPanel()
    {
        if (!RunSession.HasActiveRun || RunSession.Hero == null)
        {
            return;
        }

        var heroStats = RunSession.GetHeroBaseStats();
        var hero = RunSession.Hero;
        var availableLevelUps = RunSession.GetAvailableLevelUpCount();
        var nextThreshold = RunSession.GetNextLevelXpThreshold();

        if (levelTitleText != null)
        {
            levelTitleText.text = $"Level {hero.Level}";
        }

        if (levelDescText != null)
        {
            levelDescText.text = availableLevelUps > 0
                ? $"Choose a stat to improve. {availableLevelUps} level-up{(availableLevelUps == 1 ? string.Empty : "s")} ready."
                : nextThreshold >= 0
                    ? $"Earn {nextThreshold - hero.Xp} more XP to unlock the next level."
                    : "Maximum level reached.";
        }

        if (levelHelpText != null)
        {
            levelHelpText.text = availableLevelUps > 0
                ? "Pick Attack, Defense, or Magic."
                : "Click the XP bar any time to check progress.";
        }

        if (attackText != null)
        {
            attackText.text = heroStats.attack.ToString();
        }

        if (defenseText != null)
        {
            defenseText.text = heroStats.defense.ToString();
        }

        if (magicText != null)
        {
            magicText.text = heroStats.magic.ToString();
        }

        var canLevelUp = availableLevelUps > 0;
        if (attackButton != null)
        {
            attackButton.interactable = canLevelUp;
        }

        if (defenseButton != null)
        {
            defenseButton.interactable = canLevelUp;
        }

        if (magicButton != null)
        {
            magicButton.interactable = canLevelUp;
        }
    }

    private void ConfigureLevelButton(Button button, string stat)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => TryApplyLevelUp(stat));
    }

    private void ConfigurePressedButtonFeedback(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;

        var buttonImage = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (buttonImage != null)
        {
            button.targetGraphic = buttonImage;
        }

        var labelRect = button.GetComponentInChildren<TMP_Text>()?.rectTransform;
        var feedback = button.GetComponent<PressedButtonFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<PressedButtonFeedback>();
        }

        feedback.Initialize(
            button,
            buttonImage,
            labelRect,
            pressedButtonSprite,
            buttonHoverTint,
            buttonPressedTint,
            pressedButtonTextOffset);
    }

    private void OpenLevelPanel()
    {
        RefreshLevelPanel();
        SetLevelPanelVisible(true);
    }

    private void SetLevelPanelVisible(bool isVisible)
    {
        if (levelPanelRoot != null)
        {
            levelPanelRoot.SetActive(isVisible);
        }
    }

    private void OpenMapPanel()
    {
        RefreshMapPanel();
        SetMapPanelVisible(true);
    }

    private void SetMapPanelVisible(bool isVisible)
    {
        if (mapPanelRoot != null)
        {
            mapPanelRoot.SetActive(isVisible);
        }
    }

    private void ToggleMovesPanel()
    {
        SetMovesPanelVisible(movesPanelRoot == null || !movesPanelRoot.activeSelf);
    }

    private void SetMovesPanelVisible(bool isVisible)
    {
        if (movesPanelRoot != null)
        {
            movesPanelRoot.SetActive(isVisible);
        }
    }

    private void ConfigurePauseMenuButtons()
    {
        SetPauseMenuHoverMessage(null);

        if (pauseMenuResumeButton != null)
        {
            ConfigurePressedButtonFeedback(pauseMenuResumeButton);
            pauseMenuResumeButton.onClick.RemoveAllListeners();
            pauseMenuResumeButton.onClick.AddListener(ResumeFromPauseMenu);
            ConfigurePauseMenuHoverTextTarget(pauseMenuResumeButton, null);
        }

        if (pauseMenuExitGameButton != null)
        {
            ConfigurePressedButtonFeedback(pauseMenuExitGameButton);
            pauseMenuExitGameButton.onClick.RemoveAllListeners();
            pauseMenuExitGameButton.onClick.AddListener(ExitGameFromPauseMenu);
            ConfigurePauseMenuHoverTextTarget(pauseMenuExitGameButton, PauseExitGameHoverText);
        }

        if (pauseMenuExitToMainMenuButton != null)
        {
            ConfigurePressedButtonFeedback(pauseMenuExitToMainMenuButton);
            pauseMenuExitToMainMenuButton.onClick.RemoveAllListeners();
            pauseMenuExitToMainMenuButton.onClick.AddListener(ExitToMainMenuFromPauseMenu);
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

    private void OpenPauseMenu()
    {
        SetPauseMenuVisible(true);
    }

    private void ResumeFromPauseMenu()
    {
        SetPauseMenuVisible(false);
    }

    private void ExitGameFromPauseMenu()
    {
        RunSaveService.SaveCurrentRun();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ExitToMainMenuFromPauseMenu()
    {
        RunSaveService.SaveCurrentRun();
        SetPauseMenuVisible(false);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetPauseMenuVisible(bool isVisible)
    {
        pauseMenuOpen = isVisible && pauseMenuPanelRoot != null;

        if (pauseMenuPanelRoot != null)
        {
            pauseMenuPanelRoot.SetActive(pauseMenuOpen);
        }

        SetPauseMenuHoverMessage(null);
    }

    private void RefreshMovesUi()
    {
        if (!RunSession.HasActiveRun || RunSession.Hero == null)
        {
            return;
        }

        var hero = RunSession.Hero;
        var equippedMoveIds = hero.EquippedMoves
            .Where(moveId => !string.IsNullOrWhiteSpace(moveId))
            .ToList();
        var orderedKnownMoves = hero.KnownMoves
            .Where(moveId => !equippedMoveIds.Contains(moveId))
            .OrderBy(moveId => RunSession.GetMove(moveId)?.name ?? moveId)
            .ToList();

        EnsureSlotCount(bottomMovesRoot, bottomEquippedSlots, Mathf.Max(initialBottomEquippedSlotCount, hero.EquippedMoves.Count));
        EnsureSlotCount(panelMovesRoot, panelEquippedSlots, Mathf.Max(initialPanelEquippedSlotCount, hero.EquippedMoves.Count));
        EnsureSlotCount(movesInventoryRoot, inventorySlots, Mathf.Max(initialInventorySlotCount, orderedKnownMoves.Count));

        if (!string.IsNullOrWhiteSpace(hoveredMoveId) &&
            !equippedMoveIds.Contains(hoveredMoveId) &&
            !orderedKnownMoves.Contains(hoveredMoveId))
        {
            hoveredMoveId = null;
        }

        ConfigureEquippedSlots(bottomEquippedSlots, hero.EquippedMoves, canDrag: false);
        ConfigureEquippedSlots(panelEquippedSlots, hero.EquippedMoves, canDrag: true);
        ConfigureInventorySlots(orderedKnownMoves);
        RefreshMoveSelectors();
        RefreshMoveStats(hoveredMoveId);
    }

    private void CacheMonsterSlots()
    {
        monsterSlots.Clear();

        var monstersRoot = FindChild("Map Panel/Monsters") as RectTransform;
        if (monstersRoot == null)
        {
            return;
        }

        for (var index = 0; index < monstersRoot.childCount; index++)
        {
            var child = monstersRoot.GetChild(index) as RectTransform;
            if (child == null)
            {
                continue;
            }

            monsterSlots.Add(CreateMonsterSlotView(child, index));
        }
    }

    private MapMonsterSlotView CreateMonsterSlotView(RectTransform root, int encounterIndex)
    {
        var slot = new MapMonsterSlotView
        {
            EncounterIndex = encounterIndex,
            Root = root,
            Icon = root.GetComponent<Image>(),
            Button = EnsureButton(root),
            SelectorRoot = EnsureSelectorRoot(root, root.Find("Selector")?.gameObject),
            Label = root.Find("Level Text")?.GetComponent<TMP_Text>()
        };

        if (slot.Label == null)
        {
            slot.Label = CreateMonsterLevelLabel(root);
        }

        if (slot.Button != null)
        {
            slot.Button.transition = Selectable.Transition.None;
        }

        var hoverItem = root.GetComponent<MonsterHoverPreviewItem>();
        if (hoverItem == null)
        {
            hoverItem = root.gameObject.AddComponent<MonsterHoverPreviewItem>();
        }

        hoverItem.Initialize(this, encounterIndex);

        slot.DefaultSprite = slot.Icon != null ? slot.Icon.sprite : null;
        return slot;
    }

    private TMP_Text CreateMonsterLevelLabel(RectTransform parent)
    {
        var labelObject = new GameObject("Level Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.SetParent(parent, false);
        labelTransform.anchorMin = new Vector2(0f, 0f);
        labelTransform.anchorMax = new Vector2(1f, 0f);
        labelTransform.pivot = new Vector2(0.5f, 0f);
        labelTransform.anchoredPosition = new Vector2(0f, -34f);
        labelTransform.sizeDelta = new Vector2(0f, 32f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        label.raycastTarget = false;
        if (mapPanelText != null)
        {
            label.font = mapPanelText.font;
            label.fontSharedMaterial = mapPanelText.fontSharedMaterial;
        }

        return label;
    }

    private void RefreshMapPanel()
    {
        if (!RunSession.HasActiveRun || RunSession.CurrentRunConfig?.encounters == null)
        {
            return;
        }

        if (!RunSession.CanEnterEncounterFromMap(selectedEncounterIndex))
        {
            selectedEncounterIndex = -1;
        }

        if (!RunSession.CanEnterEncounterFromMap(hoveredEncounterIndex))
        {
            hoveredEncounterIndex = -1;
        }

        var encounters = RunSession.CurrentRunConfig.encounters;
        for (var index = 0; index < monsterSlots.Count; index++)
        {
            var slot = monsterSlots[index];
            var hasEncounter = index < encounters.Count;
            slot.Root.gameObject.SetActive(hasEncounter);
            if (!hasEncounter)
            {
                continue;
            }

            var encounter = encounters[index];
            var isUnlocked = RunSession.CanEnterEncounterFromMap(index);
            var isHovered = hoveredEncounterIndex == index;
            var isSelected = selectedEncounterIndex == index;

            if (slot.Icon != null)
            {
                slot.Icon.sprite = ResolveMonsterMapSprite(encounter, slot.DefaultSprite);
                slot.Icon.preserveAspect = true;
                slot.Icon.color = isUnlocked ? AvailableMonsterTint : LockedMonsterTint;
            }

            if (slot.Button != null)
            {
                slot.Button.interactable = isUnlocked;
                slot.Button.onClick.RemoveAllListeners();
                var capturedIndex = index;
                slot.Button.onClick.AddListener(() => SelectEncounter(capturedIndex));
            }

            SetSelectorState(slot.SelectorRoot, isSelected, isHovered && !isSelected);

            if (slot.Label != null)
            {
                slot.Label.text = $"Level {slot.EncounterIndex + 1}";
                slot.Label.color = Color.white;
            }
        }

        var hasSelection = RunSession.CanEnterEncounterFromMap(selectedEncounterIndex);
        if (mapStartButton != null)
        {
            mapStartButton.interactable = hasSelection;
        }

        if (mapStartButtonText != null)
        {
            mapStartButtonText.text = hasSelection && RunSession.IsEncounterCompleted(selectedEncounterIndex)
                ? MapReviveLabel
                : MapContinueLabel;
        }

    }

    private void SelectEncounter(int encounterIndex)
    {
        if (!RunSession.CanEnterEncounterFromMap(encounterIndex))
        {
            return;
        }

        selectedEncounterIndex = encounterIndex;
        RefreshMapPanel();
    }

    private void HandleMonsterHoverChanged(int encounterIndex, bool isHovered)
    {
        hoveredEncounterIndex = isHovered ? encounterIndex : hoveredEncounterIndex == encounterIndex ? -1 : hoveredEncounterIndex;
        RefreshMapPanel();
    }

    private void EnterSelectedEncounter()
    {
        if (!RunSession.CanEnterEncounterFromMap(selectedEncounterIndex))
        {
            return;
        }

        RunSession.SelectEncounter(selectedEncounterIndex);
        SceneManager.LoadScene(battleSceneName);
    }

    private void ConfigureEquippedSlots(IReadOnlyList<MoveSlotView> slots, IReadOnlyList<string> equippedMoves, bool canDrag)
    {
        for (var index = 0; index < slots.Count; index++)
        {
            var moveId = index < equippedMoves.Count && !string.IsNullOrWhiteSpace(equippedMoves[index]) ? equippedMoves[index] : null;
            ConfigureSlot(
                slots[index],
                moveId,
                sourceWasEquipped: true,
                sourceEquippedIndex: index,
                canDrop: canDrag,
                canDrag: canDrag,
                emptyBackgroundSprite: emptyMovesInventorySprite);
        }
    }

    private void ConfigureInventorySlots(IReadOnlyList<string> moveIds)
    {
        for (var index = 0; index < inventorySlots.Count; index++)
        {
            var slot = inventorySlots[index];
            var hasMove = index < moveIds.Count;
            slot.Root.gameObject.SetActive(true);
            if (hasMove)
            {
                ConfigureSlot(slot, moveIds[index], sourceWasEquipped: false, sourceEquippedIndex: -1, canDrop: false, canDrag: true);
            }
            else
            {
                ConfigureSlot(
                    slot,
                    moveId: null,
                    sourceWasEquipped: false,
                    sourceEquippedIndex: -1,
                    canDrop: false,
                    canDrag: false,
                    emptyBackgroundSprite: emptyMovesInventorySprite);
            }
        }
    }

    private void ConfigureSlot(
        MoveSlotView slot,
        string moveId,
        bool sourceWasEquipped,
        int sourceEquippedIndex,
        bool canDrop,
        bool canDrag,
        Sprite emptyIconSprite = null,
        bool showEmptyIcon = false,
        Sprite emptyBackgroundSprite = null)
    {
        moveId = string.IsNullOrWhiteSpace(moveId) ? null : moveId;
        slot.MoveId = moveId;
        slot.SelectorRoot = EnsureSelectorRoot(slot.Root, slot.SelectorRoot);

        if (slot.Icon != null)
        {
            var move = RunSession.GetMove(moveId);
            var resolvedSprite = moveId == null && emptyIconSprite != null
                ? emptyIconSprite
                : ResolveMoveIconSprite(move, slot.DefaultIconSprite);
            slot.Icon.sprite = resolvedSprite;
            slot.Icon.color = moveId == null
                ? (showEmptyIcon && resolvedSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f))
                : Color.white;
            slot.Icon.preserveAspect = true;
        }

        if (slot.Background != null)
        {
            var usesEmptyBackgroundSprite = moveId == null && emptyBackgroundSprite != null;
            slot.Background.sprite = usesEmptyBackgroundSprite
                ? emptyBackgroundSprite
                : slot.DefaultBackgroundSprite;
            slot.Background.color = usesEmptyBackgroundSprite
                ? EmptyMoveSlotTint
                : moveId == null
                    ? EmptyMoveSlotTint
                    : Color.white;
            slot.Background.preserveAspect = true;
        }

        var canvasGroup = slot.Root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = slot.Root.gameObject.AddComponent<CanvasGroup>();
        }

        var dragItem = slot.Root.GetComponent<MoveSlotDragItem>();
        if (moveId == null || !canDrag)
        {
            if (dragItem != null)
            {
                Destroy(dragItem);
            }
        }
        else
        {
            if (dragItem == null)
            {
                dragItem = slot.Root.gameObject.AddComponent<MoveSlotDragItem>();
            }

            dragItem.Initialize(this, moveId, sourceEquippedIndex, sourceWasEquipped, dragLayer, canvas, emptyBackgroundSprite, false);
        }

        var hoverItem = slot.Root.GetComponent<MoveHoverPreviewItem>();
        if (moveId == null)
        {
            if (hoverItem != null)
            {
                Destroy(hoverItem);
            }
        }
        else
        {
            if (hoverItem == null)
            {
                hoverItem = slot.Root.gameObject.AddComponent<MoveHoverPreviewItem>();
            }

            hoverItem.Initialize(this, moveId);
        }

        var dropZone = slot.Root.GetComponent<MoveDropZone>();
        if (canDrop)
        {
            if (dropZone == null)
            {
                dropZone = slot.Root.gameObject.AddComponent<MoveDropZone>();
            }

            dropZone.Initialize(this, sourceEquippedIndex);
        }
        else if (dropZone != null)
        {
            Destroy(dropZone);
        }

        SetSelectorState(slot.SelectorRoot, isSelected: false, isHovered: hoveredMoveId == moveId && moveId != null);
    }

    private void RefreshMoveStats(string moveId)
    {
        var move = RunSession.GetMove(moveId);

        if (currentMoveStatsRoot != null)
        {
            currentMoveStatsRoot.SetActive(move != null);
        }

        if (moveStatsIcon != null)
        {
            moveStatsIcon.sprite = ResolveMoveIconSprite(move, moveStatsIcon.sprite);
            moveStatsIcon.color = move == null ? new Color(1f, 1f, 1f, 0f) : Color.white;
            moveStatsIcon.preserveAspect = true;
        }

        if (moveStatsTitleText != null)
        {
            moveStatsTitleText.text = move?.name ?? "Move Stats";
        }

        if (moveStatsTypeText != null)
        {
            moveStatsTypeText.text = move == null
                ? "Hover a move to inspect it."
                : $"{move.type?.ToUpperInvariant() ?? "UNKNOWN"}  |  {move.target?.ToUpperInvariant() ?? "TARGET"}";
        }

        if (moveStatsDescriptionText != null)
        {
            moveStatsDescriptionText.text = move == null
                ? "Open the moves panel and hover a move to see its details."
                : BuildMoveDescription(move);
        }

        if (moveStatsAttackText != null)
        {
            moveStatsAttackText.text = move == null ? "-" : BuildAttackValue(move);
        }

        if (moveStatsHealText != null)
        {
            moveStatsHealText.text = move == null ? "-" : BuildHealValue(move);
        }

        SetEffectValue(heroAttackEffectText, move, "attack", targetIsHero: true);
        SetEffectValue(heroDefenseEffectText, move, "defense", targetIsHero: true);
        SetEffectValue(heroMagicEffectText, move, "magic", targetIsHero: true);
        SetEffectValue(monsterAttackEffectText, move, "attack", targetIsHero: false);
        SetEffectValue(monsterDefenseEffectText, move, "defense", targetIsHero: false);
        SetEffectValue(monsterMagicEffectText, move, "magic", targetIsHero: false);

        if (moveStatsEffectsAmountText != null)
        {
            moveStatsEffectsAmountText.text = move == null ? "-" : BuildEffectDurationLine(move);
        }
    }

    public void HandleMoveHoverChanged(string moveId, bool isHovered)
    {
        hoveredMoveId = isHovered ? moveId : null;
        RefreshMoveSelectors();
        RefreshMoveStats(hoveredMoveId);
    }

    public bool TryApplyMoveDrop(string moveId, bool sourceWasEquipped, int sourceEquippedIndex, int targetEquippedIndex)
    {
        if (!RunSession.HasActiveRun || RunSession.Hero == null)
        {
            return false;
        }

        var hero = RunSession.Hero;
        if (targetEquippedIndex < 0 || targetEquippedIndex >= panelEquippedSlots.Count || targetEquippedIndex >= hero.EquippedMoves.Count)
        {
            return false;
        }

        if (sourceWasEquipped)
        {
            if (sourceEquippedIndex < 0 || sourceEquippedIndex >= hero.EquippedMoves.Count)
            {
                return false;
            }

            if (sourceEquippedIndex != targetEquippedIndex)
            {
                var sourceMove = hero.EquippedMoves[sourceEquippedIndex];
                hero.EquippedMoves[sourceEquippedIndex] = hero.EquippedMoves[targetEquippedIndex];
                hero.EquippedMoves[targetEquippedIndex] = sourceMove;
            }
        }
        else
        {
            var existingEquippedIndex = hero.EquippedMoves.FindIndex(id => id == moveId);
            if (existingEquippedIndex >= 0)
            {
                if (existingEquippedIndex != targetEquippedIndex)
                {
                    hero.EquippedMoves[existingEquippedIndex] = hero.EquippedMoves[targetEquippedIndex];
                    hero.EquippedMoves[targetEquippedIndex] = moveId;
                }
            }
            else
            {
                hero.EquippedMoves[targetEquippedIndex] = moveId;
            }
        }

        hoveredMoveId = null;

        var moveName = RunSession.GetMove(hero.EquippedMoves[targetEquippedIndex])?.name ?? hero.EquippedMoves[targetEquippedIndex];
        RunSession.SetStatus($"{moveName} is now equipped in slot {targetEquippedIndex + 1}.");
        if (!sourceWasEquipped && moveId == RunSession.PendingLearnedMoveId)
        {
            RunSession.ClearPendingLearnedMove();
        }

        RunSaveService.SaveCurrentRun();
        return true;
    }

    public void QueueDropRefresh()
    {
        if (pendingDropRefresh != null)
        {
            StopCoroutine(pendingDropRefresh);
        }

        pendingDropRefresh = StartCoroutine(RefreshAfterDrop());
    }

    public void NotifyDragStateChanged(bool isDragging)
    {
        if (movesBarButton != null)
        {
            movesBarButton.interactable = !isDragging;
        }
    }

    private IEnumerator RefreshAfterDrop()
    {
        yield return null;
        RefreshUi();
        pendingDropRefresh = null;
    }

    private void TryApplyLevelUp(string stat)
    {
        if (!RunSession.TrySpendLevelUp(stat))
        {
            RefreshLevelPanel();
            return;
        }

        RefreshUi();
    }

    private void CacheMoveSlots(RectTransform root, ICollection<MoveSlotView> slots)
    {
        slots.Clear();
        if (root == null)
        {
            return;
        }

        for (var index = 0; index < root.childCount; index++)
        {
            var child = root.GetChild(index) as RectTransform;
            if (child == null)
            {
                continue;
            }

            slots.Add(CreateMoveSlotView(child));
        }
    }

    private void EnsureSlotCount(RectTransform root, List<MoveSlotView> slots, int requiredCount)
    {
        if (root == null || slots == null || slots.Count == 0)
        {
            return;
        }

        while (slots.Count < requiredCount)
        {
            var slotObject = Instantiate(slots[0].Root.gameObject, root, false);
            slotObject.name = $"{slots[0].Root.name} {slots.Count + 1}";

            var dragItem = slotObject.GetComponent<MoveSlotDragItem>();
            if (dragItem != null)
            {
                Destroy(dragItem);
            }

            var dropZone = slotObject.GetComponent<MoveDropZone>();
            if (dropZone != null)
            {
                Destroy(dropZone);
            }

            var hoverPreview = slotObject.GetComponent<MoveHoverPreviewItem>();
            if (hoverPreview != null)
            {
                Destroy(hoverPreview);
            }

            slots.Add(CreateMoveSlotView(slotObject.GetComponent<RectTransform>()));
        }
    }

    private static MoveSlotView CreateMoveSlotView(RectTransform root)
    {
        var slot = new MoveSlotView
        {
            Root = root,
            Background = root.Find("Move Background")?.GetComponent<Image>(),
            Icon = root.Find("Move Icon")?.GetComponent<Image>(),
            SelectorRoot = root.Find("Selector")?.gameObject
        };
        slot.DefaultIconSprite = slot.Icon != null ? slot.Icon.sprite : null;
        slot.DefaultBackgroundSprite = slot.Background != null ? slot.Background.sprite : null;
        return slot;
    }

    private void RefreshMoveSelectors()
    {
        RefreshMoveSelectors(bottomEquippedSlots);
        RefreshMoveSelectors(panelEquippedSlots);
        RefreshMoveSelectors(inventorySlots);
    }

    private void RefreshMoveSelectors(IEnumerable<MoveSlotView> slots)
    {
        foreach (var slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            SetSelectorState(slot.SelectorRoot, isSelected: false, isHovered: hoveredMoveId == slot.MoveId && !string.IsNullOrWhiteSpace(slot.MoveId));
        }
    }

    private void EnsureDragLayer()
    {
        if (movesPanelRoot == null || dragLayer != null)
        {
            return;
        }

        var dragLayerObject = new GameObject("Drag Layer", typeof(RectTransform));
        dragLayer = dragLayerObject.GetComponent<RectTransform>();
        dragLayer.SetParent(movesPanelRoot.transform, false);
        dragLayer.anchorMin = Vector2.zero;
        dragLayer.anchorMax = Vector2.one;
        dragLayer.offsetMin = Vector2.zero;
        dragLayer.offsetMax = Vector2.zero;
        dragLayer.SetAsLastSibling();
    }

    private static string ResolvePreviewMoveId(string preferredMoveId, IEnumerable<string> equippedMoves, IEnumerable<string> knownMoves)
    {
        if (!string.IsNullOrWhiteSpace(preferredMoveId) &&
            (equippedMoves.Contains(preferredMoveId) || knownMoves.Contains(preferredMoveId)))
        {
            return preferredMoveId;
        }

        return equippedMoves.FirstOrDefault() ?? knownMoves.FirstOrDefault();
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

        if (move.statModifier == null || !string.Equals(move.statModifier.stat, statName))
        {
            return "0";
        }

        var moveTargetsHero = string.Equals(move.target, "self", System.StringComparison.OrdinalIgnoreCase);
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

    private static Sprite ResolveMoveIconSprite(Move move, Sprite fallbackSprite)
    {
        var keyedSprite = SpriteKeyLookup.LoadMoveSprite(move?.spriteKey);
        return keyedSprite != null ? keyedSprite : fallbackSprite;
    }

    private static Sprite ResolveMonsterMapSprite(Monster monster, Sprite fallbackSprite)
    {
        var idleFrames = SpriteKeyLookup.LoadCharacterAnimationOrDefault(monster?.spriteKey, BattleAnimationState.Idle, CharacterSpriteKind.Monster);
        return idleFrames.FirstOrDefault() ?? SpriteKeyLookup.LoadMoveSprite(monster?.spriteKey) ?? fallbackSprite;
    }

    private static Button EnsureButton(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        var button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        var image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
        }

        image.raycastTarget = true;
        button.targetGraphic = image;

        return button;
    }

    private GameObject EnsureSelectorRoot(RectTransform root, GameObject existingSelectorRoot)
    {
        if (root == null ||
            selectorTopLeft == null ||
            selectorTopRight == null ||
            selectorBottomLeft == null ||
            selectorBottomRight == null)
        {
            return null;
        }

        var selectorRoot = existingSelectorRoot;
        if (selectorRoot != null && selectorRoot.transform.childCount < 4)
        {
            Destroy(selectorRoot);
            selectorRoot = null;
        }

        if (selectorRoot == null)
        {
            selectorRoot = new GameObject("Selector", typeof(RectTransform));
            var selectorRect = selectorRoot.GetComponent<RectTransform>();
            selectorRect.SetParent(root, false);
            selectorRect.anchorMin = Vector2.zero;
            selectorRect.anchorMax = Vector2.one;
            selectorRect.offsetMin = Vector2.zero;
            selectorRect.offsetMax = Vector2.zero;

            CreateSelectorCorner(selectorRoot.transform, "Top Left", selectorTopLeft, new Vector2(0f, 1f));
            CreateSelectorCorner(selectorRoot.transform, "Top Right", selectorTopRight, new Vector2(1f, 1f));
            CreateSelectorCorner(selectorRoot.transform, "Bottom Left", selectorBottomLeft, new Vector2(0f, 0f));
            CreateSelectorCorner(selectorRoot.transform, "Bottom Right", selectorBottomRight, new Vector2(1f, 0f));
        }

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
        rect.sizeDelta = new Vector2(selectorCornerSize, selectorCornerSize);

        var image = cornerObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void SetSelectorState(GameObject selectorRoot, bool isSelected, bool isHovered)
    {
        if (selectorRoot == null)
        {
            return;
        }

        var isVisible = isSelected || isHovered;
        if (selectorRoot.activeSelf != isVisible)
        {
            selectorRoot.SetActive(isVisible);
        }

        var tint = isSelected ? SelectedSelectorTint : HoverSelectorTint;
        foreach (var image in selectorRoot.GetComponentsInChildren<Image>(true))
        {
            image.color = tint;
        }
    }

    private static T FindComponent<T>(string path) where T : Component
    {
        var canvas = GameObject.Find("Canvas")?.transform;
        if (canvas == null)
        {
            return null;
        }

        var target = canvas.Find(path);
        if (target == null)
        {
            target = FindDescendant(canvas, path);
        }

        return target != null ? target.GetComponent<T>() : null;
    }

    private Transform FindChild(string path)
    {
        if (canvasRoot == null)
        {
            return null;
        }

        var target = canvasRoot.Find(path);
        return target ?? FindDescendant(canvasRoot, path);
    }

    private static Transform FindDescendant(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split('/');
        return FindDescendant(root, segments, 0);
    }

    private static Transform FindDescendant(Transform current, string[] segments, int index)
    {
        if (current == null)
        {
            return null;
        }

        if (index >= segments.Length)
        {
            return current;
        }

        for (var i = 0; i < current.childCount; i++)
        {
            var child = current.GetChild(i);
            if (!string.Equals(child.name, segments[index]))
            {
                continue;
            }

            var result = FindDescendant(child, segments, index + 1);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
