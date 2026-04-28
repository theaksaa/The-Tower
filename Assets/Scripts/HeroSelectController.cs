using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TheTower;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeroSelectController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Navigation")]
    [SerializeField] private string battleSceneName = "BattleScene2";

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

    private void Awake()
    {
        AutoBindScene();
        ConfigureStaticUi();
    }

    private void Start()
    {
        StartCoroutine(LoadHeroesRoutine());
    }

    private void AutoBindScene()
    {
        heroSelectorRoot = GameObject.Find("Canvas/Hero Selection Panel/Hero Selector")?.transform;
        heroNameText = FindComponent<TMP_Text>("Canvas/Hero Selection Panel/Hero Name");
        heroDescriptionText = FindComponent<TMP_Text>("Canvas/Hero Selection Panel/Hero Description");
        selectButton = FindComponent<Button>("Canvas/Hero Selection Panel/Select Button");
        selectButtonText = selectButton != null ? selectButton.GetComponentInChildren<TMP_Text>() : null;

        CacheHeroCardTemplates();
        CacheStatTexts();
        CacheMoveTexts();
    }

    private void ConfigureStaticUi()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(SelectCurrentHeroAndStartRun);
            selectButton.interactable = false;
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

    private IEnumerator LoadHeroesRoutine()
    {
        yield return RunDataService.LoadRunConfig(baseUrl, useLocalFallbackIfApiFails, (config, fallback) =>
        {
            loadedRunConfig = config;
            usingFallbackData = fallback;
        });

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

    private void CacheStatTexts()
    {
        statValueTexts.Clear();

        foreach (var statName in new[] { "Attack Stat", "Defense Stat", "Magic Stat" })
        {
            var statRoot = GameObject.Find($"Canvas/Hero Selection Panel/Stats/{statName}");
            if (statRoot == null)
            {
                statValueTexts.Add(null);
                continue;
            }

            var text = statRoot.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(candidate => !candidate.name.Contains("Label"));
            statValueTexts.Add(text);
        }
    }

    private void CacheMoveTexts()
    {
        moveTexts.Clear();
        moveIconImages.Clear();
        templateMoveIconSprites.Clear();

        for (var index = 0; index < 4; index++)
        {
            var moveRoot = GameObject.Find($"Canvas/Hero Selection Panel/Moves/Move {index + 1}");
            var text = moveRoot != null ? moveRoot.GetComponentsInChildren<TMP_Text>(true).LastOrDefault() : null;
            var icon = moveRoot != null ? moveRoot.transform.Find("Move Icon")?.GetComponent<Image>() : null;
            moveTexts.Add(text);
            moveIconImages.Add(icon);
            templateMoveIconSprites.Add(icon != null ? icon.sprite : null);
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

        RunSession.InitializeNewRun(loadedRunConfig, usingFallbackData, availableHeroes[selectedHeroIndex]);
        SceneManager.LoadScene(battleSceneName);
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

    private static T FindComponent<T>(string path) where T : Component
    {
        var target = GameObject.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }
}
