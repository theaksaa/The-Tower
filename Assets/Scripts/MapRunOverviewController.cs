using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TheTower;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapRunOverviewController : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    [Header("Navigation")]
    [SerializeField] private string battleSceneName = "BattleScene2";

    [Header("Modal")]
    [SerializeField] private float modalAnimationDuration = 0.24f;
    [SerializeField] private float learnedCardWidth = 440f;
    [SerializeField] private float learnedCardHeight = 132f;

    private Canvas canvas;
    private Font font;
    private Text titleText;
    private Text statusText;
    private Text heroStatsText;
    private Image learnedRewardPanel;
    private Text learnedRewardText;
    private Button equipLearnedMoveButton;
    private Button keepLoadoutButton;
    private RectTransform encounterContent;
    private RectTransform equippedBar;
    private RectTransform modalBackdrop;
    private RectTransform modalSheet;
    private RectTransform modalEquippedRow;
    private RectTransform modalLearnedContent;
    private RectTransform dragLayer;
    private Button toggleModalButton;
    private Button newRunButton;
    private Button modalCloseButton;
    private Image backdropImage;
    private Coroutine modalAnimation;
    private Coroutine pendingDropRefresh;
    private bool modalOpen;

    private readonly List<Button> encounterButtons = new();
    private readonly List<Text> encounterButtonLabels = new();
    private readonly List<Text> equippedButtonLabels = new();

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildLayout();
        SetModalVisible(false, immediate: true);
    }

    private void Start()
    {
        StartCoroutine(EnsureRunSession());
    }

    public bool TryApplyMoveDrop(string moveId, bool sourceWasEquipped, int sourceEquippedIndex, int targetEquippedIndex)
    {
        if (!RunSession.HasActiveRun)
        {
            return false;
        }

        var hero = RunSession.Hero;
        if (targetEquippedIndex < 0 || targetEquippedIndex >= hero.EquippedMoves.Count)
        {
            return false;
        }

        if (sourceWasEquipped)
        {
            if (sourceEquippedIndex < 0 || sourceEquippedIndex >= hero.EquippedMoves.Count)
            {
                return false;
            }

            if (sourceEquippedIndex == targetEquippedIndex)
            {
                return true;
            }

            var sourceMove = hero.EquippedMoves[sourceEquippedIndex];
            hero.EquippedMoves[sourceEquippedIndex] = hero.EquippedMoves[targetEquippedIndex];
            hero.EquippedMoves[targetEquippedIndex] = sourceMove;
        }
        else
        {
            hero.EquippedMoves[targetEquippedIndex] = moveId;
        }

        var moveName = RunSession.GetMove(hero.EquippedMoves[targetEquippedIndex])?.name ?? hero.EquippedMoves[targetEquippedIndex];
        RunSession.SetStatus($"{moveName} is now equipped in slot {targetEquippedIndex + 1}.");
        if (!sourceWasEquipped && moveId == RunSession.PendingLearnedMoveId)
        {
            RunSession.ClearPendingLearnedMove();
        }
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
        if (modalCloseButton != null)
        {
            modalCloseButton.interactable = !isDragging;
        }
    }

    private IEnumerator RefreshAfterDrop()
    {
        yield return null;
        RefreshUi();
        pendingDropRefresh = null;
    }

    private IEnumerator EnsureRunSession()
    {
        if (!RunSession.HasActiveRun || RunSession.IsRunComplete() || RunSession.IsDefeated)
        {
            yield return StartCoroutine(StartNewRunRoutine());
            yield break;
        }

        RefreshUi();
    }

    private IEnumerator StartNewRunRoutine()
    {
        SetStatus("Loading run overview...");
        yield return RunDataService.LoadRunConfig(baseUrl, useLocalFallbackIfApiFails, (config, usingFallback) =>
        {
            if (config != null)
            {
                RunSession.InitializeNewRun(config, usingFallback);
            }
        });

        if (!RunSession.HasActiveRun)
        {
            SetStatus("Unable to load run config.");
            yield break;
        }

        RefreshUi();
    }

    private void RefreshUi()
    {
        if (!RunSession.HasActiveRun)
        {
            return;
        }

        titleText.text = RunSession.IsRunComplete()
            ? "Run Complete"
            : "Map / Run Overview";

        var hero = RunSession.Hero;
        var heroStats = RunSession.GetHeroBaseStats();
        heroStatsText.text =
            $"Hero Lv.{hero.Level}   XP {hero.Xp}\n" +
            $"HP {hero.CurrentHp}/{heroStats.health}   ATK {heroStats.attack}   DEF {heroStats.defense}   MAG {heroStats.magic}";

        statusText.text = RunSession.IsRunComplete()
            ? "Every encounter has been cleared. Start a new run whenever you're ready."
            : RunSession.StatusMessage;

        RefreshEncounterButtons();
        RefreshEquippedBar();
        RefreshModalContent();
        RefreshPendingLearnedMovePrompt();
    }

    private void RefreshEncounterButtons()
    {
        var encounters = RunSession.CurrentRunConfig.encounters;

        while (encounterButtons.Count < encounters.Count)
        {
            var row = CreatePanel($"EncounterRow{encounterButtons.Count}", encounterContent, new Color(0.93f, 0.91f, 0.84f, 0.95f));
            var layoutElement = row.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 120f;
            layoutElement.minHeight = 120f;

            var button = row.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.98f, 0.96f, 0.9f, 1f);
            colors.highlightedColor = new Color(1f, 0.98f, 0.86f, 1f);
            colors.pressedColor = new Color(0.9f, 0.78f, 0.44f, 1f);
            colors.disabledColor = new Color(0.67f, 0.67f, 0.67f, 0.8f);
            button.colors = colors;

            var label = CreateText("Label", row.transform, 26, TextAnchor.MiddleLeft, Color.black);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(30f, 18f);
            labelRect.offsetMax = new Vector2(-30f, -18f);

            encounterButtons.Add(button);
            encounterButtonLabels.Add(label);
        }

        for (var index = 0; index < encounterButtons.Count; index++)
        {
            var active = index < encounters.Count;
            encounterButtons[index].gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            var encounter = encounters[index];
            var isComplete = RunSession.IsEncounterCompleted(index);
            var canEnter = RunSession.CanEnterEncounter(index);
            var stateLabel = isComplete
                ? "Cleared"
                : canEnter
                    ? "Click to enter battle"
                    : "Unavailable";

            encounterButtonLabels[index].text =
                $"{index + 1}. {encounter.name}\n" +
                $"{encounter.description}\n" +
                $"Reward: {encounter.xpReward} XP   Status: {stateLabel}";

            encounterButtons[index].interactable = canEnter;
            encounterButtons[index].onClick.RemoveAllListeners();
            var capturedIndex = index;
            encounterButtons[index].onClick.AddListener(() => EnterEncounter(capturedIndex));
        }
    }

    private void RefreshEquippedBar()
    {
        var hero = RunSession.Hero;

        while (equippedButtonLabels.Count < 4)
        {
            var slot = CreatePanel($"EquippedBarSlot{equippedButtonLabels.Count}", equippedBar, new Color(0.18f, 0.2f, 0.27f, 0.96f));
            slot.rectTransform.sizeDelta = new Vector2(0f, 92f);
            var layoutElement = slot.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 0f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 92f;

            var button = slot.gameObject.AddComponent<Button>();
            button.interactable = false;
            var label = CreateText("Text", slot.transform, 22, TextAnchor.MiddleCenter, Color.white);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16f, 12f);
            label.rectTransform.offsetMax = new Vector2(-16f, -12f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            equippedButtonLabels.Add(label);
        }

        for (var index = 0; index < equippedButtonLabels.Count; index++)
        {
            equippedButtonLabels[index].text = index < hero.EquippedMoves.Count
                ? RunSession.GetMove(hero.EquippedMoves[index])?.name ?? hero.EquippedMoves[index]
                : "-";
        }

        toggleModalButton.GetComponentInChildren<Text>().text = modalOpen ? "v" : "^";
    }

    private void RefreshModalContent()
    {
        if (!RunSession.HasActiveRun)
        {
            return;
        }

        ClearChildren(modalEquippedRow);
        ClearChildren(modalLearnedContent);

        var hero = RunSession.Hero;
        var equippedIds = hero.EquippedMoves.ToList();
        for (var index = 0; index < equippedIds.Count; index++)
        {
            CreateEquippedDropSlot(index, equippedIds[index]);
        }

        var learnedIds = hero.KnownMoves
            .Where(moveId => !equippedIds.Contains(moveId))
            .OrderBy(moveId => RunSession.GetMove(moveId)?.name ?? moveId)
            .ToList();

        if (learnedIds.Count == 0)
        {
            var emptyText = CreateText("EmptyLearned", modalLearnedContent, 22, TextAnchor.MiddleCenter, new Color(0.25f, 0.25f, 0.25f));
            emptyText.text = "No extra learned moves yet.";
            emptyText.rectTransform.sizeDelta = new Vector2(0f, 70f);
            emptyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            return;
        }

        foreach (var moveId in learnedIds)
        {
            CreateLearnedMoveCard(moveId);
        }
    }

    private void RefreshPendingLearnedMovePrompt()
    {
        if (learnedRewardPanel == null)
        {
            return;
        }

        var hasPendingMove = RunSession.HasPendingLearnedMove;
        learnedRewardPanel.gameObject.SetActive(hasPendingMove);

        if (!hasPendingMove)
        {
            return;
        }

        var pendingMove = RunSession.GetMove(RunSession.PendingLearnedMoveId);
        var moveName = pendingMove?.name ?? RunSession.PendingLearnedMoveId;
        learnedRewardText.text =
            $"New move learned: {moveName}\n" +
            "Equip it now from the move manager, or keep it in reserve.";
    }

    private void EnterEncounter(int encounterIndex)
    {
        if (!RunSession.CanEnterEncounter(encounterIndex))
        {
            return;
        }

        RunSession.SelectEncounter(encounterIndex);
        SceneManager.LoadScene(battleSceneName);
    }

    private void ToggleModal()
    {
        SetModalVisible(!modalOpen, immediate: false);
    }

    private void OpenPendingMoveManagement()
    {
        SetModalVisible(true, immediate: false);
        if (!RunSession.HasPendingLearnedMove)
        {
            return;
        }

        var pendingMove = RunSession.GetMove(RunSession.PendingLearnedMoveId);
        var moveName = pendingMove?.name ?? RunSession.PendingLearnedMoveId;
        RunSession.SetStatus($"Drag {moveName} into an equipped slot, or keep your current loadout.");
        RefreshUi();
    }

    private void KeepCurrentLoadout()
    {
        if (!RunSession.HasPendingLearnedMove)
        {
            return;
        }

        var pendingMove = RunSession.GetMove(RunSession.PendingLearnedMoveId);
        var moveName = pendingMove?.name ?? RunSession.PendingLearnedMoveId;
        RunSession.ClearPendingLearnedMove();
        RunSession.SetStatus($"{moveName} was learned and kept in reserve.");
        RefreshUi();
    }

    private void StartNewRun()
    {
        StartCoroutine(StartNewRunRoutine());
    }

    private void SetModalVisible(bool visible, bool immediate)
    {
        modalOpen = visible;
        toggleModalButton.GetComponentInChildren<Text>().text = modalOpen ? "v" : "^";
        toggleModalButton.interactable = true;

        if (modalAnimation != null)
        {
            StopCoroutine(modalAnimation);
        }

        if (immediate)
        {
            ApplyModalState(visible ? 1f : 0f);
            return;
        }

        modalAnimation = StartCoroutine(AnimateModal(visible));
    }

    private IEnumerator AnimateModal(bool visible)
    {
        modalBackdrop.gameObject.SetActive(true);
        var start = backdropImage.color.a;
        var end = visible ? 0.45f : 0f;
        var startY = modalSheet.anchoredPosition.y;
        var endY = visible ? 0f : -modalSheet.rect.height - 24f;
        var elapsed = 0f;

        while (elapsed < modalAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / modalAnimationDuration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            backdropImage.color = new Color(0.04f, 0.05f, 0.08f, Mathf.Lerp(start, end, eased));
            modalSheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, eased));
            yield return null;
        }

        ApplyModalState(visible ? 1f : 0f);
        if (!visible)
        {
            modalBackdrop.gameObject.SetActive(false);
        }

        modalAnimation = null;
    }

    private void ApplyModalState(float openness)
    {
        modalBackdrop.gameObject.SetActive(openness > 0f);
        backdropImage.color = new Color(0.04f, 0.05f, 0.08f, 0.45f * openness);
        modalSheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(-modalSheet.rect.height - 24f, 0f, openness));
    }

    private void BuildLayout()
    {
        var root = GetComponent<RectTransform>();
        if (root == null)
        {
            root = gameObject.AddComponent<RectTransform>();
        }

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var background = CreatePanel("Background", transform, new Color(0.92f, 0.86f, 0.74f, 1f));
        Stretch(background.rectTransform);

        var topStrip = CreatePanel("TopStrip", transform, new Color(0.17f, 0.18f, 0.24f, 0.98f));
        SetAnchors(topStrip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -170f), new Vector2(0f, 0f));

        titleText = CreateText("TitleText", topStrip.transform, 38, TextAnchor.UpperLeft, Color.white);
        titleText.text = "Map / Run Overview";
        titleText.fontStyle = FontStyle.Bold;
        SetAnchors(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(32f, 20f), new Vector2(-12f, -16f));

        statusText = CreateText("StatusText", topStrip.transform, 22, TextAnchor.UpperLeft, new Color(0.96f, 0.9f, 0.74f));
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchors(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(32f, 64f), new Vector2(-24f, -18f));

        heroStatsText = CreateText("HeroStatsText", topStrip.transform, 22, TextAnchor.MiddleRight, Color.white);
        heroStatsText.alignment = TextAnchor.MiddleRight;
        SetAnchors(heroStatsText.rectTransform, new Vector2(0.58f, 0f), new Vector2(0.88f, 1f), new Vector2(0f, 20f), new Vector2(-18f, -18f));

        newRunButton = CreateTextButton("NewRunButton", topStrip.transform, "New Run", new Color(0.82f, 0.53f, 0.22f), Color.white);
        SetAnchors(newRunButton.GetComponent<RectTransform>(), new Vector2(0.89f, 0.22f), new Vector2(0.98f, 0.78f), Vector2.zero, Vector2.zero);
        newRunButton.onClick.AddListener(StartNewRun);

        learnedRewardPanel = CreatePanel("LearnedRewardPanel", transform, new Color(0.85f, 0.73f, 0.42f, 0.98f));
        SetAnchors(learnedRewardPanel.rectTransform, new Vector2(0.24f, 0.82f), new Vector2(0.76f, 0.93f), Vector2.zero, Vector2.zero);

        learnedRewardText = CreateText("LearnedRewardText", learnedRewardPanel.transform, 20, TextAnchor.UpperLeft, new Color(0.16f, 0.14f, 0.1f));
        learnedRewardText.horizontalOverflow = HorizontalWrapMode.Wrap;
        learnedRewardText.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchors(learnedRewardText.rectTransform, new Vector2(0f, 0f), new Vector2(0.64f, 1f), new Vector2(18f, 14f), new Vector2(-10f, -14f));

        equipLearnedMoveButton = CreateTextButton("EquipLearnedButton", learnedRewardPanel.transform, "Equip Move", new Color(0.18f, 0.2f, 0.27f), Color.white);
        SetAnchors(equipLearnedMoveButton.GetComponent<RectTransform>(), new Vector2(0.68f, 0.54f), new Vector2(0.97f, 0.9f), Vector2.zero, Vector2.zero);
        equipLearnedMoveButton.onClick.AddListener(OpenPendingMoveManagement);

        keepLoadoutButton = CreateTextButton("KeepLoadoutButton", learnedRewardPanel.transform, "Keep Current", new Color(0.45f, 0.32f, 0.14f), Color.white);
        SetAnchors(keepLoadoutButton.GetComponent<RectTransform>(), new Vector2(0.68f, 0.12f), new Vector2(0.97f, 0.48f), Vector2.zero, Vector2.zero);
        keepLoadoutButton.onClick.AddListener(KeepCurrentLoadout);

        var encounterPanel = CreatePanel("EncounterPanel", transform, new Color(1f, 0.98f, 0.94f, 0.92f));
        SetAnchors(encounterPanel.rectTransform, new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

        var encounterHeader = CreateText("EncounterHeader", encounterPanel.transform, 28, TextAnchor.MiddleLeft, new Color(0.15f, 0.15f, 0.15f));
        encounterHeader.text = "Upcoming encounters";
        encounterHeader.fontStyle = FontStyle.Bold;
        SetAnchors(encounterHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -62f), new Vector2(-24f, -16f));

        var scrollObject = new GameObject("EncounterScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scrollObject.transform.SetParent(encounterPanel.transform, false);
        var scrollRect = scrollObject.GetComponent<RectTransform>();
        SetAnchors(scrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -78f));
        scrollObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        scrollObject.GetComponent<Mask>().showMaskGraphic = false;

        var viewport = scrollObject.GetComponent<ScrollRect>();
        viewport.horizontal = false;
        viewport.movementType = ScrollRect.MovementType.Clamped;

        var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(scrollObject.transform, false);
        encounterContent = contentObject.GetComponent<RectTransform>();
        encounterContent.anchorMin = new Vector2(0f, 1f);
        encounterContent.anchorMax = new Vector2(1f, 1f);
        encounterContent.pivot = new Vector2(0.5f, 1f);
        encounterContent.offsetMin = new Vector2(0f, 0f);
        encounterContent.offsetMax = new Vector2(0f, 0f);

        var encounterLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        encounterLayout.spacing = 16f;
        encounterLayout.padding = new RectOffset(0, 0, 0, 16);
        encounterLayout.childControlWidth = true;
        encounterLayout.childForceExpandWidth = true;
        encounterLayout.childControlHeight = false;
        encounterLayout.childForceExpandHeight = false;

        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        viewport.content = encounterContent;

        var bottomPanel = CreatePanel("BottomPanel", transform, new Color(0.12f, 0.13f, 0.18f, 0.98f));
        SetAnchors(bottomPanel.rectTransform, new Vector2(0.22f, 0.03f), new Vector2(0.78f, 0.17f), Vector2.zero, Vector2.zero);

        toggleModalButton = CreateTextButton("ToggleModalButton", bottomPanel.transform, "^", new Color(0.82f, 0.53f, 0.22f), Color.white);
        SetAnchors(toggleModalButton.GetComponent<RectTransform>(), new Vector2(0.46f, 0.75f), new Vector2(0.54f, 1.15f), Vector2.zero, Vector2.zero);
        toggleModalButton.onClick.AddListener(ToggleModal);

        var equippedRoot = new GameObject("EquippedMoves", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        equippedRoot.transform.SetParent(bottomPanel.transform, false);
        equippedBar = equippedRoot.GetComponent<RectTransform>();
        SetAnchors(equippedBar, new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.72f), Vector2.zero, Vector2.zero);

        var equippedLayout = equippedRoot.GetComponent<HorizontalLayoutGroup>();
        equippedLayout.spacing = 14f;
        equippedLayout.padding = new RectOffset(12, 12, 12, 12);
        equippedLayout.childControlWidth = true;
        equippedLayout.childForceExpandWidth = true;
        equippedLayout.childControlHeight = true;
        equippedLayout.childForceExpandHeight = true;

        modalBackdrop = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        modalBackdrop.SetParent(transform, false);
        Stretch(modalBackdrop);
        backdropImage = modalBackdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.04f, 0.05f, 0.08f, 0f);
        modalCloseButton = modalBackdrop.GetComponent<Button>();
        modalCloseButton.transition = Selectable.Transition.None;
        modalCloseButton.onClick.AddListener(() => SetModalVisible(false, immediate: false));

        modalSheet = CreatePanel("ModalSheet", modalBackdrop, new Color(0.96f, 0.93f, 0.86f, 1f)).rectTransform;
        SetAnchors(modalSheet, new Vector2(0.08f, 0f), new Vector2(0.92f, 0.58f), new Vector2(0f, -24f), new Vector2(0f, 0f));

        var modalTitle = CreateText("ModalTitle", modalSheet, 28, TextAnchor.UpperLeft, new Color(0.15f, 0.15f, 0.15f));
        modalTitle.text = "Move Management";
        modalTitle.fontStyle = FontStyle.Bold;
        SetAnchors(modalTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -54f), new Vector2(-26f, -12f));

        var modalSubtitle = CreateText("ModalSubtitle", modalSheet, 20, TextAnchor.UpperLeft, new Color(0.32f, 0.28f, 0.2f));
        modalSubtitle.text = "Drag learned moves into an equipped slot, or drag equipped moves to reorder them.";
        modalSubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        modalSubtitle.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchors(modalSubtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -92f), new Vector2(-26f, -50f));

        var equippedModalLabel = CreateText("EquippedModalLabel", modalSheet, 22, TextAnchor.UpperLeft, new Color(0.15f, 0.15f, 0.15f));
        equippedModalLabel.text = "Equipped";
        equippedModalLabel.fontStyle = FontStyle.Bold;
        SetAnchors(equippedModalLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -162f), new Vector2(-26f, -126f));

        var equippedModalRowObject = new GameObject("ModalEquippedRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        equippedModalRowObject.transform.SetParent(modalSheet, false);
        modalEquippedRow = equippedModalRowObject.GetComponent<RectTransform>();
        SetAnchors(modalEquippedRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -304f), new Vector2(-26f, -176f));
        var modalEquippedLayout = equippedModalRowObject.GetComponent<HorizontalLayoutGroup>();
        modalEquippedLayout.spacing = 14f;
        modalEquippedLayout.childControlWidth = true;
        modalEquippedLayout.childForceExpandWidth = true;
        modalEquippedLayout.childControlHeight = true;
        modalEquippedLayout.childForceExpandHeight = true;

        var learnedLabel = CreateText("LearnedLabel", modalSheet, 22, TextAnchor.UpperLeft, new Color(0.15f, 0.15f, 0.15f));
        learnedLabel.text = "Learned";
        learnedLabel.fontStyle = FontStyle.Bold;
        SetAnchors(learnedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -354f), new Vector2(-26f, -318f));

        var learnedScrollObject = new GameObject("LearnedScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        learnedScrollObject.transform.SetParent(modalSheet, false);
        var learnedScrollRect = learnedScrollObject.GetComponent<RectTransform>();
        SetAnchors(learnedScrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(26f, 22f), new Vector2(-26f, -370f));
        learnedScrollObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        learnedScrollObject.GetComponent<Mask>().showMaskGraphic = false;

        var learnedScroll = learnedScrollObject.GetComponent<ScrollRect>();
        learnedScroll.horizontal = false;
        learnedScroll.movementType = ScrollRect.MovementType.Clamped;

        var learnedContentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        learnedContentObject.transform.SetParent(learnedScrollObject.transform, false);
        modalLearnedContent = learnedContentObject.GetComponent<RectTransform>();
        modalLearnedContent.anchorMin = new Vector2(0f, 1f);
        modalLearnedContent.anchorMax = new Vector2(1f, 1f);
        modalLearnedContent.pivot = new Vector2(0.5f, 1f);

        var learnedLayout = learnedContentObject.GetComponent<VerticalLayoutGroup>();
        learnedLayout.spacing = 12f;
        learnedLayout.childControlWidth = true;
        learnedLayout.childForceExpandWidth = true;
        learnedLayout.childControlHeight = false;
        learnedLayout.childForceExpandHeight = false;
        learnedLayout.padding = new RectOffset(8, 8, 0, 18);

        var learnedFitter = learnedContentObject.GetComponent<ContentSizeFitter>();
        learnedFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        learnedScroll.content = modalLearnedContent;

        dragLayer = new GameObject("DragLayer", typeof(RectTransform)).GetComponent<RectTransform>();
        dragLayer.SetParent(modalBackdrop, false);
        Stretch(dragLayer);
        dragLayer.SetAsLastSibling();
    }

    private void CreateEquippedDropSlot(int slotIndex, string moveId)
    {
        var slot = CreatePanel($"EquippedSlot{slotIndex}", modalEquippedRow, new Color(0.88f, 0.83f, 0.72f, 1f));
        var slotLayout = slot.gameObject.AddComponent<LayoutElement>();
        slotLayout.preferredHeight = 110f;
        slotLayout.flexibleWidth = 1f;

        var dropZone = slot.gameObject.AddComponent<MoveDropZone>();
        dropZone.Initialize(this, slotIndex);

        var slotLabel = CreateText("SlotLabel", slot.transform, 16, TextAnchor.UpperLeft, new Color(0.28f, 0.22f, 0.14f));
        slotLabel.text = $"Slot {slotIndex + 1}";
        SetAnchors(slotLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -26f), new Vector2(-12f, 0f));

        CreateMoveCard(slot.transform, moveId, slotIndex, sourceWasEquipped: true);
    }

    private void CreateLearnedMoveCard(string moveId)
    {
        var container = new GameObject($"Learned_{moveId}", typeof(RectTransform));
        container.transform.SetParent(modalLearnedContent, false);
        var containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.pivot = new Vector2(0.5f, 1f);
        containerRect.sizeDelta = new Vector2(0f, learnedCardHeight + 12f);
        var layout = container.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = learnedCardHeight + 12f;
        layout.minHeight = learnedCardHeight + 12f;
        CreateMoveCard(container.transform, moveId, -1, sourceWasEquipped: false);
    }

    private void CreateMoveCard(Transform parent, string moveId, int sourceEquippedIndex, bool sourceWasEquipped)
    {
        var move = RunSession.GetMove(moveId);
        var card = CreatePanel($"Card_{moveId}", parent, new Color(0.18f, 0.2f, 0.27f, 1f));
        if (sourceWasEquipped)
        {
            SetAnchors(card.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -28f));
        }
        else
        {
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(0f, -6f);
            cardRect.sizeDelta = new Vector2(learnedCardWidth, learnedCardHeight);
        }

        var title = CreateText("MoveName", card.transform, 21, TextAnchor.UpperLeft, Color.white);
        title.text = move?.name ?? moveId;
        title.fontStyle = FontStyle.Bold;
        title.horizontalOverflow = HorizontalWrapMode.Wrap;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        ConfigureTopBand(title.rectTransform, 12f, 16f, 34f);

        var body = CreateText("MoveBody", card.transform, 16, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.95f));
        body.text = move == null
            ? "Unknown move"
            : $"{move.type}  |  {move.effect}\n{move.description}";
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        body.fontSize = sourceWasEquipped ? 16 : 15;
        ConfigureFillBelow(body.rectTransform, 52f, 16f, 12f);

        var canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        var dragItem = card.gameObject.AddComponent<MoveSlotDragItem>();
        dragItem.Initialize(this, moveId, sourceEquippedIndex, sourceWasEquipped, dragLayer, canvas);
    }

    private static void ClearChildren(RectTransform parent)
    {
        for (var index = parent.childCount - 1; index >= 0; index--)
        {
            Destroy(parent.GetChild(index).gameObject);
        }
    }

    private Image CreatePanel(string name, Transform parent, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Button CreateTextButton(string name, Transform parent, string label, Color backgroundColor, Color textColor)
    {
        var image = CreatePanel(name, parent, backgroundColor);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.08f;
        colors.pressedColor = backgroundColor * 0.88f;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        button.colors = colors;

        var text = CreateText("Text", image.transform, 22, TextAnchor.MiddleCenter, textColor);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    private Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void ConfigureTopBand(RectTransform rectTransform, float topInset, float sideInset, float height)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -topInset);
        rectTransform.offsetMin = new Vector2(sideInset, -topInset - height);
        rectTransform.offsetMax = new Vector2(-sideInset, -topInset);
    }

    private static void ConfigureFillBelow(RectTransform rectTransform, float topInset, float sideInset, float bottomInset)
    {
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.offsetMin = new Vector2(sideInset, bottomInset);
        rectTransform.offsetMax = new Vector2(-sideInset, -topInset);
    }
}
