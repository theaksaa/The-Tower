using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartGameSceneController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

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

    private Button backButton;
    private Button newGameButton;
    private Button continueButton;
    private Button storyModeButton;
    private Button endlessModeButton;

    private VisiblePanel activePanel;
    private ModePreview selectedMode = ModePreview.Story;

    private enum VisiblePanel
    {
        Game,
        NewGame,
        Continue
    }

    private enum ModePreview
    {
        Story,
        Endless
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
        private StartGameSceneController owner;
        private ModePreview mode;

        public void Initialize(StartGameSceneController controller, ModePreview previewMode)
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
        ShowPanel(VisiblePanel.Game);
        HideModePreview();
    }

    private void BindButtons()
    {
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

        backButton = FindComponent<Button>("Back Button");
        newGameButton = FindComponent<Button>("Game Panel/Buttons/New Game Button");
        continueButton = FindComponent<Button>("Game Panel/Buttons/Continue Button");
        storyModeButton = FindComponent<Button>("New Game Panel/Buttons/Story Mode Button");
        endlessModeButton = FindComponent<Button>("New Game Panel/Buttons/Endless Mode Button");
    }

    private void HandleBackButtonPressed()
    {
        switch (activePanel)
        {
            case VisiblePanel.Continue:
                ShowPanel(VisiblePanel.Game);
                break;
            case VisiblePanel.NewGame:
                ShowPanel(VisiblePanel.Game);
                break;
            default:
                SceneManager.LoadScene(mainMenuSceneName);
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
        ShowPanel(VisiblePanel.Continue);
    }

    private void SelectStoryMode()
    {
        selectedMode = ModePreview.Story;
    }

    private void SelectEndlessMode()
    {
        selectedMode = ModePreview.Endless;
    }

    private void ShowPanel(VisiblePanel panel)
    {
        activePanel = panel;

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
}
