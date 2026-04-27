using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string battleSceneName = "HeroSelectScene";
    [SerializeField] private string statusMessage = "Settings are not implemented yet.";
    [SerializeField] private float buttonHeight = 88f;

    private Button startGameButton;
    private Button settingsButton;
    private Button exitButton;
    private Text statusText;

    private void Awake()
    {
        ConfigureMenuLayout();
        AutoBindScene();
        BindButtons();
        SetStatus(string.Empty);
    }

    public void StartGame()
    {
        SceneManager.LoadScene(battleSceneName);
    }

    public void OpenSettings()
    {
        SetStatus(statusMessage);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void AutoBindScene()
    {
        startGameButton = FindButton("StartGameButton");
        settingsButton = FindButton("SettingsButton");
        exitButton = FindButton("ExitButton");
        statusText = FindText("StatusText");
    }

    private void BindButtons()
    {
        BindButton(startGameButton, StartGame);
        BindButton(settingsButton, OpenSettings);
        BindButton(exitButton, ExitGame);
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private Button FindButton(string objectName)
    {
        var target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private Text FindText(string objectName)
    {
        var target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void ConfigureMenuLayout()
    {
        var panelObject = GameObject.Find("ButtonPanel");
        if (panelObject == null)
        {
            return;
        }

        var layout = panelObject.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
        }

        ConfigureButtonRect("StartGameButton");
        ConfigureButtonRect("SettingsButton");
        ConfigureButtonRect("ExitButton");
    }

    private void ConfigureButtonRect(string objectName)
    {
        var target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        var rect = target.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
            rect.sizeDelta = new Vector2(0f, buttonHeight);
        }

        var layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = buttonHeight;
        layoutElement.preferredHeight = buttonHeight;
        layoutElement.flexibleWidth = 1f;
    }
}
