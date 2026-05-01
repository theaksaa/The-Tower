using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    private const string MainMenuMusicPath = "Sounds/Background Music/Background/Ambient 8";

    [Header("Navigation")]
    [SerializeField] private string storySceneName = "StoryScene";
    [SerializeField] private string runOverviewSceneName = "RunOverviewScene";

    [Header("Mode Preview")]
    [SerializeField] private string storyModeTitle = "Story mode";
    [SerializeField] private string storyModeDescription = "Take on a curated tower run, grow your hero, and push through each encounter one by one.";
    [SerializeField] private string endlessModeTitle = "Endless mode";
    [SerializeField] private string endlessModeDescription = "Stay alive for as long as you can in an endless run where every fight pushes the difficulty higher.";

    [Header("Button Feedback")]
    [SerializeField] private Sprite pressedButtonSprite;
    [SerializeField] private Color buttonHoverTint = new(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color buttonPressedTint = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Vector2 pressedButtonTextOffset = new(0f, -6f);

    private GameObject gamePanel;
    private GameObject newGamePanel;
    private GameObject continuePanel;
    private GameObject currentModeRoot;
    private TMP_Text currentModeTitleText;
    private TMP_Text currentModeDescriptionText;

    private Button startGameButton;
    private Button settingsButton;
    private Button exitButton;
    private Button backButton;
    private Button newGameButton;
    private Button continueButton;
    private Button storyModeButton;
    private Button endlessModeButton;
    private SettingsPanelController settingsPanel;
    private RectTransform continueGamesRoot;
    private readonly List<ContinueRunCardView> continueRunCards = new();

    private VisiblePanel activePanel;

    private enum VisiblePanel
    {
        RootMenu,
        Game,
        NewGame,
        Continue
    }

    private enum ModePreview
    {
        Story,
        Endless
    }

    private sealed class ContinueRunCardView
    {
        public RectTransform Root;
        public Button OpenButton;
        public Button DeleteButton;
        public Image HeroImage;
        public TMP_Text ModeText;
        public TMP_Text ProgressText;
        public TMP_Text LastSaveText;
        public Sprite DefaultHeroSprite;
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
            disabledColor = targetButton != null
                ? targetButton.colors.disabledColor
                : new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);

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
                backgroundImage.sprite = isInteractable && isPressed && pressedSprite != null
                    ? pressedSprite
                    : normalSprite;
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

    private sealed class ModeHoverPreview : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private MainMenuController owner;
        private ModePreview mode;

        public void Initialize(MainMenuController controller, ModePreview previewMode)
        {
            owner = controller;
            mode = previewMode;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.PreviewMode(mode);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HideModePreview();
        }
    }

    private void Awake()
    {
        AutoBindScene();
        BindButtons();
        ConfigureModeHover(storyModeButton, ModePreview.Story);
        ConfigureModeHover(endlessModeButton, ModePreview.Endless);
        ConfigurePressedButtonFeedback();
        CacheContinueRunCards();
        RefreshContinuePanel();
        settingsPanel?.Close();
        ShowPanel(VisiblePanel.RootMenu);
        HideModePreview();
        AudioManager.PlayMusic(MainMenuMusicPath, true);
    }

    public void OpenSettings()
    {
        settingsPanel?.Toggle();
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BindButtons()
    {
        BindButton(startGameButton, OpenGamePanel);
        BindButton(settingsButton, OpenSettings);
        BindButton(exitButton, ExitGame);
        BindButton(backButton, HandleBackButtonPressed);
        BindButton(newGameButton, OpenNewGamePanel);
        BindButton(continueButton, OpenContinuePanel);
        BindButton(storyModeButton, SelectStoryMode);
        BindButton(endlessModeButton, SelectEndlessMode);
    }

    private void AutoBindScene()
    {
        gamePanel = FindObject("Game Panel");
        newGamePanel = FindObject("New Game Panel");
        continuePanel = FindObject("Continue Panel");
        currentModeRoot = FindObject("New Game Panel/Current Mode");
        currentModeTitleText = FindComponent<TMP_Text>("New Game Panel/Current Mode/Title");
        currentModeDescriptionText = FindComponent<TMP_Text>("New Game Panel/Current Mode/Description");
        continueGamesRoot = ResolveContinueGamesRoot();

        startGameButton = FindButton("Start Game Button", "StartGameButton");
        settingsButton = FindButton("Settings Button", "SettingsButton");
        exitButton = FindButton("Exit Button", "ExitButton");
        backButton = FindComponent<Button>("Back Button");
        newGameButton = FindComponent<Button>("Game Panel/Buttons/New Game Button");
        continueButton = FindComponent<Button>("Game Panel/Buttons/Continue Button");
        storyModeButton = FindComponent<Button>("New Game Panel/Buttons/Story Mode Button");
        endlessModeButton = FindComponent<Button>("New Game Panel/Buttons/Endless Mode Button");
        settingsPanel = SettingsPanelController.FindOrCreate(transform);
    }

    private RectTransform ResolveContinueGamesRoot()
    {
        var candidatePaths = new[]
        {
            "Continue Panel/Scroll View/Viewport/Content/games",
            "Continue Panel/Scroll View/Viewport/Content/Games",
            "Continue Panel/Scroll View/Viewport/Content",
            "Continue Panel/Games"
        };

        foreach (var path in candidatePaths)
        {
            var rectTransform = FindObject(path)?.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private void OpenGamePanel()
    {
        ShowPanel(VisiblePanel.Game);
    }

    private void HandleBackButtonPressed()
    {
        switch (activePanel)
        {
            case VisiblePanel.Continue:
            case VisiblePanel.NewGame:
                ShowPanel(VisiblePanel.Game);
                break;
            case VisiblePanel.Game:
                ShowPanel(VisiblePanel.RootMenu);
                break;
            default:
                ShowPanel(VisiblePanel.RootMenu);
                break;
        }
    }

    private void OpenNewGamePanel()
    {
        ShowPanel(VisiblePanel.NewGame);
        HideModePreview();
    }

    private void OpenContinuePanel()
    {
        RefreshContinuePanel();
        ShowPanel(VisiblePanel.Continue);
    }

    private void SelectStoryMode()
    {
        RunSession.ClearActiveRun();
        RunSession.SetPendingMode("Story");
        SceneLoader.LoadScene(storySceneName);
    }

    private void SelectEndlessMode()
    {
        RunSession.ClearActiveRun();
        RunSession.SetPendingMode("Endless");
        SceneLoader.LoadScene(storySceneName);
    }

    private void ShowPanel(VisiblePanel panel)
    {
        activePanel = panel;

        var showRootMenu = panel == VisiblePanel.RootMenu;
        SetActive(startGameButton, showRootMenu);
        SetActive(settingsButton, showRootMenu);
        SetActive(exitButton, showRootMenu);
        SetActive(backButton, !showRootMenu);

        if (gamePanel != null)
        {
            gamePanel.SetActive(panel == VisiblePanel.Game);
        }

        if (newGamePanel != null)
        {
            newGamePanel.SetActive(panel == VisiblePanel.NewGame);
        }

        if (continuePanel != null)
        {
            continuePanel.SetActive(panel == VisiblePanel.Continue);
        }

        if (!showRootMenu)
        {
            settingsPanel?.Close();
        }
    }

    private void ConfigureModeHover(Button button, ModePreview mode)
    {
        if (button == null)
        {
            return;
        }

        var hoverPreview = button.GetComponent<ModeHoverPreview>();
        if (hoverPreview == null)
        {
            hoverPreview = button.gameObject.AddComponent<ModeHoverPreview>();
        }

        hoverPreview.Initialize(this, mode);
    }

    private void ConfigurePressedButtonFeedback()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            button.transition = Selectable.Transition.None;

            var buttonImage = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (buttonImage != null)
            {
                button.targetGraphic = buttonImage;
            }

            var labelRect = button.GetComponentInChildren<TMP_Text>(true)?.rectTransform;
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
    }

    private void PreviewMode(ModePreview mode)
    {
        ApplyModePreview(mode);
    }

    private void ApplyModePreview(ModePreview mode)
    {
        if (currentModeRoot != null)
        {
            currentModeRoot.SetActive(true);
        }

        if (currentModeTitleText != null)
        {
            currentModeTitleText.text = mode == ModePreview.Endless ? endlessModeTitle : storyModeTitle;
        }

        if (currentModeDescriptionText != null)
        {
            currentModeDescriptionText.text = mode == ModePreview.Endless ? endlessModeDescription : storyModeDescription;
        }
    }

    private void HideModePreview()
    {
        if (currentModeRoot != null)
        {
            currentModeRoot.SetActive(false);
        }
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private T FindComponent<T>(string path) where T : Component
    {
        var target = FindObject(path);
        return target != null ? target.GetComponent<T>() : null;
    }

    private GameObject FindObject(string path)
    {
        var target = transform.Find(path);
        return target != null ? target.gameObject : null;
    }

    private static void SetActive(Component component, bool isActive)
    {
        if (component != null)
        {
            component.gameObject.SetActive(isActive);
        }
    }

    private void CacheContinueRunCards()
    {
        continueRunCards.Clear();
        if (continueGamesRoot == null)
        {
            return;
        }

        for (var index = 0; index < continueGamesRoot.childCount; index++)
        {
            if (continueGamesRoot.GetChild(index) is not RectTransform child)
            {
                continue;
            }

            continueRunCards.Add(BuildContinueRunCard(child));
        }
    }

    private ContinueRunCardView BuildContinueRunCard(RectTransform root)
    {
        var backgroundImage = root.Find("Background")?.GetComponent<Image>();
        var button = EnsureButton(root, backgroundImage);
        var deleteButton = EnsureButton(root.Find("Delete Button") as RectTransform);
        return new ContinueRunCardView
        {
            Root = root,
            OpenButton = button,
            DeleteButton = deleteButton,
            HeroImage = root.Find("Content/Hero")?.GetComponent<Image>(),
            ModeText = root.Find("Content/Mode/Mode Text")?.GetComponent<TMP_Text>(),
            ProgressText = root.Find("Content/Progress/Progress Text")?.GetComponent<TMP_Text>(),
            LastSaveText = root.Find("Content/Last Save/Last Date Text")?.GetComponent<TMP_Text>(),
            DefaultHeroSprite = root.Find("Content/Hero")?.GetComponent<Image>()?.sprite
        };
    }

    private void RefreshContinuePanel()
    {
        var saves = RunSaveService.GetAllRuns();
        EnsureContinueRunCardCount(saves.Count);

        for (var index = 0; index < continueRunCards.Count; index++)
        {
            var hasSave = index < saves.Count;
            var card = continueRunCards[index];
            card.Root.gameObject.SetActive(hasSave);
            if (!hasSave)
            {
                continue;
            }

            BindContinueRunCard(card, saves[index]);
        }

        if (continueButton != null)
        {
            continueButton.interactable = saves.Count > 0;
        }
    }

    private void EnsureContinueRunCardCount(int requiredCount)
    {
        if (continueGamesRoot == null || continueRunCards.Count == 0)
        {
            return;
        }

        while (continueRunCards.Count < requiredCount)
        {
            var clone = Instantiate(continueRunCards[0].Root.gameObject, continueGamesRoot, false);
            clone.name = $"Game {continueRunCards.Count + 1}";
            continueRunCards.Add(BuildContinueRunCard(clone.GetComponent<RectTransform>()));
        }
    }

    private void BindContinueRunCard(ContinueRunCardView card, RunSaveService.RunSaveSummary save)
    {
        if (card == null || save == null)
        {
            return;
        }

        if (card.HeroImage != null)
        {
            card.HeroImage.sprite = ResolveRunHeroSprite(save, card.DefaultHeroSprite);
            card.HeroImage.preserveAspect = true;
            card.HeroImage.color = Color.white;
        }

        if (card.ModeText != null)
        {
            var modeName = string.IsNullOrWhiteSpace(save.Mode) ? "Story" : save.Mode;
            card.ModeText.text = modeName;
        }

        if (card.ProgressText != null)
        {
            var progressPercent = save.TotalEncounterCount > 0
                ? Mathf.Clamp(Mathf.RoundToInt((save.CompletedEncounterCount / (float)save.TotalEncounterCount) * 100f), 0, 100)
                : 0;
            card.ProgressText.text = $"{progressPercent}%";
        }

        if (card.LastSaveText != null)
        {
            var localTimestamp = save.LastUpdatedUtc.Kind == DateTimeKind.Utc
                ? save.LastUpdatedUtc.ToLocalTime()
                : save.LastUpdatedUtc;
            card.LastSaveText.text = localTimestamp.ToString("g");
        }

        if (card.OpenButton != null)
        {
            card.OpenButton.onClick.RemoveAllListeners();
            card.OpenButton.onClick.AddListener(() => OpenSavedRun(save.SaveId));
        }

        if (card.DeleteButton != null)
        {
            card.DeleteButton.onClick.RemoveAllListeners();
            card.DeleteButton.onClick.AddListener(() => DeleteSavedRun(save.SaveId));
        }
    }

    private void OpenSavedRun(string saveId)
    {
        if (!RunSaveService.TryLoadRun(saveId))
        {
            RefreshContinuePanel();
            return;
        }

        SceneLoader.LoadScene(runOverviewSceneName);
    }

    private void DeleteSavedRun(string saveId)
    {
        if (!RunSaveService.DeleteRun(saveId))
        {
            return;
        }

        RefreshContinuePanel();
    }

    private static Sprite ResolveRunHeroSprite(RunSaveService.RunSaveSummary save, Sprite fallbackSprite)
    {
        var idleFrames = SpriteKeyLookup.LoadCharacterAnimationOrDefault(
            save.HeroSpriteKey,
            BattleAnimationState.Idle,
            CharacterSpriteKind.Hero);
        return idleFrames.FirstOrDefault(sprite => sprite != null) ?? fallbackSprite;
    }

    private static Button EnsureButton(RectTransform target, Image fallbackGraphic = null)
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

        var targetGraphic = fallbackGraphic != null ? fallbackGraphic : target.GetComponent<Image>();
        if (targetGraphic != null)
        {
            button.targetGraphic = targetGraphic;
        }

        return button;
    }

    private Button FindButton(params string[] objectNames)
    {
        foreach (var objectName in objectNames)
        {
            var button = FindComponent<Button>(objectName);
            if (button != null)
            {
                return button;
            }

            var target = GameObject.Find(objectName);
            if (target == null)
            {
                continue;
            }

            button = target.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }
}
