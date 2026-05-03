using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TheTower;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeroSelectController : MonoBehaviour
{
    private const string HeroSelectMusicPath = AudioPaths.HeroSelectMusic;
    [Header("API")]
    [SerializeField] private string baseUrl = ServerConfigService.DefaultServerBaseUrl;
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Navigation")]
    [SerializeField] private string overviewSceneName = GameScenes.RunOverview;

    [Header("Button Feedback")]
    [SerializeField] private Sprite pressedButtonSprite;
    [SerializeField] private Color buttonHoverTint = new(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color buttonPressedTint = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Vector2 pressedButtonTextOffset = new(0f, -6f);

    private Transform heroSelectorRoot;
    private TMP_Text heroNameText;
    private TMP_Text heroDescriptionText;
    private Button selectButton;
    private TMP_Text selectButtonText;

    private readonly List<HeroCardView> heroCardViews = new();
    private readonly List<TMP_Text> statValueTexts = new();
    private readonly List<TMP_Text> moveTexts = new();
    private readonly List<Image> moveIconImages = new();
    private readonly List<Sprite> templatePortraitSprites = new();
    private readonly List<Sprite> templateMoveIconSprites = new();

    private RunConfig loadedRunConfig;
    private bool usingFallbackData;
    private IReadOnlyList<HeroDefinition> availableHeroes;
    private int selectedHeroIndex = -1;

    private sealed class HeroCardView
    {
        public GameObject Root;
        public Image SelectorImage;
        public Image PortraitImage;
        public Button Button;
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
                backgroundImage.color = isInteractable
                    ? isPressed
                        ? pressedColor
                        : isHovered ? hoverColor : normalColor
                    : normalColor;
            }

            if (labelRect != null)
            {
                labelRect.anchoredPosition = labelBasePosition + (isInteractable && isPressed ? pressedLabelOffset : Vector2.zero);
            }

            if (labelText != null)
            {
                labelText.color = labelNormalColor;
            }
        }
    }

    private void Awake()
    {
        baseUrl = ServerConfigService.ResolveBaseUrl();
        AutoBindScene();
        ConfigureStaticUi();
        AudioManager.PlayMusic(HeroSelectMusicPath, true);
    }

    private void Start()
    {
        StartCoroutine(LoadHeroesRoutine());
    }

    private void AutoBindScene()
    {
        var references = HeroSelectReferences.Create();
        heroSelectorRoot = references.HeroSelectorRoot;
        heroNameText = references.HeroNameText;
        heroDescriptionText = references.HeroDescriptionText;
        selectButton = references.SelectButton;
        selectButtonText = references.SelectButtonText;

        CacheHeroCardTemplates();
        statValueTexts.Clear();
        statValueTexts.AddRange(references.StatValueTexts);
        moveTexts.Clear();
        moveTexts.AddRange(references.MoveTexts);
        moveIconImages.Clear();
        moveIconImages.AddRange(references.MoveIconImages);
        templateMoveIconSprites.Clear();
        templateMoveIconSprites.AddRange(references.MoveIconImages.Select(icon => icon != null ? icon.sprite : null));
    }

    private void ConfigureStaticUi()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(SelectCurrentHeroAndStartRun);
            selectButton.interactable = true;
            ConfigurePressedButtonFeedback(selectButton);
        }

        if (heroNameText != null)
        {
            heroNameText.text = "Loading heroes...";
        }

        if (heroDescriptionText != null)
        {
            heroDescriptionText.text = "Fetching the current run configuration.";
        }

        SetMoveTexts("...", "...", "...", "...");
        SetStatTexts("-", "-", "-");
        UpdateSelectButtonLabel();
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

        var feedback = button.GetComponent<global::PressedButtonFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<global::PressedButtonFeedback>();
        }

        var labelRect = button.GetComponentInChildren<TMP_Text>(true)?.rectTransform;
        feedback.Initialize(
            button,
            buttonImage,
            labelRect,
            pressedButtonSprite,
            buttonHoverTint,
            buttonPressedTint,
            pressedButtonTextOffset,
            shouldApplyDisabledTint: false);
    }

    private IEnumerator LoadHeroesRoutine()
    {
        if (RunConfigService.TryConsumeCachedRunConfig(out var cachedConfig, out var cachedFallback))
        {
            loadedRunConfig = cachedConfig;
            usingFallbackData = cachedFallback;
        }
        else
        {
            yield return RunConfigService.LoadRunConfig(baseUrl, useLocalFallbackIfApiFails, (config, fallback) =>
            {
                loadedRunConfig = config;
                usingFallbackData = fallback;
            });
        }

        availableHeroes = RunSession.GetAvailableHeroes(loadedRunConfig);
        if (loadedRunConfig == null || availableHeroes.Count == 0)
        {
            if (heroNameText != null)
            {
                heroNameText.text = "No heroes available";
            }

            if (heroDescriptionText != null)
            {
                heroDescriptionText.text = "The run config could not be loaded, or it did not include any hero options.";
            }

            SetMoveTexts("-", "-", "-", "-");
            SetStatTexts("-", "-", "-");
            UpdateSelectButtonLabel();
            yield break;
        }

        RebuildHeroSelector();
        SetSelectedHero(0);
    }

    private void CacheHeroCardTemplates()
    {
        heroCardViews.Clear();
        templatePortraitSprites.Clear();

        if (heroSelectorRoot == null)
        {
            return;
        }

        for (var index = 0; index < heroSelectorRoot.childCount; index++)
        {
            var child = heroSelectorRoot.GetChild(index);
            var cardView = BuildCardView(child.gameObject);
            heroCardViews.Add(cardView);
            templatePortraitSprites.Add(cardView.PortraitImage != null ? cardView.PortraitImage.sprite : null);
        }
    }

    private void RebuildHeroSelector()
    {
        if (heroSelectorRoot == null || heroCardViews.Count == 0)
        {
            return;
        }

        while (heroCardViews.Count < availableHeroes.Count)
        {
            var clone = Instantiate(heroCardViews[0].Root, heroSelectorRoot);
            clone.name = $"Hero {heroCardViews.Count + 1}";
            heroCardViews.Add(BuildCardView(clone));
            templatePortraitSprites.Add(templatePortraitSprites.Count > 0 ? templatePortraitSprites[0] : null);
        }

        for (var index = 0; index < heroCardViews.Count; index++)
        {
            var isActive = index < availableHeroes.Count;
            var cardView = heroCardViews[index];
            cardView.Root.SetActive(isActive);
            if (!isActive)
            {
                continue;
            }

            if (cardView.PortraitImage != null)
            {
                cardView.PortraitImage.sprite = ResolvePortraitSprite(availableHeroes[index], index);
            }

            if (cardView.Button != null)
            {
                var capturedIndex = index;
                cardView.Button.onClick.RemoveAllListeners();
                cardView.Button.onClick.AddListener(() => SetSelectedHero(capturedIndex));
            }
        }
    }

    private HeroCardView BuildCardView(GameObject root)
    {
        var selector = root.transform.Find("Selector")?.GetComponent<Image>();
        var portrait = root.transform.Find("Image")?.GetComponent<Image>();
        var button = root.GetComponent<Button>();
        if (button == null)
        {
            button = root.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        button.colors = colors;

        return new HeroCardView
        {
            Root = root,
            SelectorImage = selector,
            PortraitImage = portrait,
            Button = button
        };
    }

    private Sprite ResolvePortraitSprite(HeroDefinition hero, int heroIndex)
    {
        var spriteKey = hero != null && !string.IsNullOrWhiteSpace(hero.spriteKey)
            ? hero.spriteKey
            : hero?.portraitKey;

        var keyedPortrait = SpriteKeyLookup.LoadCharacterAnimationOrDefault(spriteKey, BattleAnimationState.Idle, CharacterSpriteKind.Hero)
            .FirstOrDefault();
        if (keyedPortrait != null)
        {
            return keyedPortrait;
        }

        if (templatePortraitSprites.Count == 0)
        {
            return null;
        }

        if (heroIndex < templatePortraitSprites.Count && templatePortraitSprites[heroIndex] != null)
        {
            return templatePortraitSprites[heroIndex];
        }

        return templatePortraitSprites[heroIndex % templatePortraitSprites.Count];
    }

    private void SetSelectedHero(int index)
    {
        if (availableHeroes == null || index < 0 || index >= availableHeroes.Count)
        {
            return;
        }

        selectedHeroIndex = index;
        var hero = availableHeroes[index];

        if (heroNameText != null)
        {
            heroNameText.text = hero.name;
        }

        if (heroDescriptionText != null)
        {
            var fallbackLabel = usingFallbackData ? "\n\nOffline fallback data loaded." : string.Empty;
            heroDescriptionText.text = $"{hero.description}{fallbackLabel}";
        }

        SetStatTexts(
            $"{hero.baseStats.attack}",
            $"{hero.baseStats.defense}",
            $"{hero.baseStats.magic}");
        SetMoveTexts(hero.moves);
        UpdateSelectButtonLabel();
        RefreshHeroCardSelection();

        if (selectButton != null)
        {
            selectButton.interactable = true;
        }
    }

    private void RefreshHeroCardSelection()
    {
        for (var index = 0; index < heroCardViews.Count; index++)
        {
            var selector = heroCardViews[index].SelectorImage;
            if (selector != null)
            {
                selector.enabled = index == selectedHeroIndex;
            }
        }
    }

    private void SelectCurrentHeroAndStartRun()
    {
        if (loadedRunConfig == null || availableHeroes == null || selectedHeroIndex < 0 || selectedHeroIndex >= availableHeroes.Count)
        {
            return;
        }

        RunSession.InitializeNewRun(loadedRunConfig, usingFallbackData, availableHeroes[selectedHeroIndex], RunSession.PendingMode);
        SceneLoader.LoadScene(overviewSceneName);
    }

    private void UpdateSelectButtonLabel()
    {
        if (selectButtonText == null)
        {
            return;
        }

        selectButtonText.text = selectedHeroIndex >= 0 && availableHeroes != null && selectedHeroIndex < availableHeroes.Count
            ? $"Play as {availableHeroes[selectedHeroIndex].name}"
            : "Select Hero";
    }

    private void SetStatTexts(string attack, string defense, string magic)
    {
        if (statValueTexts.Count > 0 && statValueTexts[0] != null)
        {
            statValueTexts[0].text = attack;
        }

        if (statValueTexts.Count > 1 && statValueTexts[1] != null)
        {
            statValueTexts[1].text = defense;
        }

        if (statValueTexts.Count > 2 && statValueTexts[2] != null)
        {
            statValueTexts[2].text = magic;
        }
    }

    private void SetMoveTexts(params string[] values)
    {
        for (var index = 0; index < moveTexts.Count; index++)
        {
            if (moveTexts[index] == null)
            {
                continue;
            }

            moveTexts[index].text = index < values.Length ? values[index] : "-";
        }
    }

    private void SetMoveTexts(IReadOnlyList<string> moveIds)
    {
        var moveIdsBySlot = moveIds?
            .Take(4)
            .Concat(Enumerable.Repeat<string>(null, 4))
            .Take(4)
            .ToArray()
            ?? new string[4];

        var resolvedMoves = moveIdsBySlot
            .Select(moveId => loadedRunConfig?.moveRegistry != null && !string.IsNullOrWhiteSpace(moveId) && loadedRunConfig.moveRegistry.TryGetValue(moveId, out var move)
                ? move.name
                : string.IsNullOrWhiteSpace(moveId) ? "-" : moveId)
            .ToArray();

        SetMoveTexts(resolvedMoves);
        SetMoveIcons(moveIdsBySlot);
    }

    private void SetMoveIcons(IReadOnlyList<string> moveIds)
    {
        for (var index = 0; index < moveIconImages.Count; index++)
        {
            var image = moveIconImages[index];
            if (image == null)
            {
                continue;
            }

            Move move = null;
            if (moveIds != null && index < moveIds.Count)
            {
                var moveId = moveIds[index];
                if (!string.IsNullOrWhiteSpace(moveId) &&
                    loadedRunConfig?.moveRegistry != null &&
                    loadedRunConfig.moveRegistry.TryGetValue(moveId, out var resolvedMove))
                {
                    move = resolvedMove;
                }
            }

            image.sprite = ResolveMoveIconSprite(move, index);
        }
    }

    private Sprite ResolveMoveIconSprite(Move move, int slotIndex)
    {
        var keyedSprite = SpriteKeyLookup.LoadMoveSprite(move?.spriteKey);
        if (keyedSprite != null)
        {
            return keyedSprite;
        }

        if (slotIndex >= 0 && slotIndex < templateMoveIconSprites.Count && templateMoveIconSprites[slotIndex] != null)
        {
            return templateMoveIconSprites[slotIndex];
        }

        return templateMoveIconSprites.FirstOrDefault(sprite => sprite != null);
    }

}
