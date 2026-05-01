using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public sealed class EndSceneController : MonoBehaviour
{
    private const string EndSceneMusicPath = "Sounds/Background Music/Background/Ambient 10";

    [SerializeField] private string endSceneName = "EndScene";
    [SerializeField] private string runOverviewSceneName = "RunOverviewScene";
    [SerializeField] private string title = "The Curse Is Broken";

    private static readonly string[] Pages =
    {
        "At last, the tower falls silent.\nThe curse that bound it begins to fade, and the restless spirits are finally at peace.",
        "The princess is free - no longer a shadow at the peak, but no longer the same as before.\nWhat was taken by the tower cannot be fully returned.",
        "The path behind you is gone.\nThe tower that once seemed endless now crumbles into nothing.",
        "And yet... something lingers."
    };

    private static bool sceneHookRegistered;

    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private Button nextButton;
    private TMP_Text nextButtonLabel;
    private int pageIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneHookRegistered = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "EndScene")
        {
            return;
        }

        if (FindAnyObjectByType<EndSceneController>() != null)
        {
            return;
        }

        var controllerRoot = new GameObject(nameof(EndSceneController));
        controllerRoot.AddComponent<EndSceneController>();
    }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != endSceneName)
        {
            Destroy(gameObject);
            return;
        }

        BindScene();
        BindButtons();
        ShowPage(0);
        AudioManager.PlayMusic(EndSceneMusicPath, true);
    }

    private void BindScene()
    {
        titleText = FindText("Title");
        descriptionText = FindText("Description");
        nextButton = FindButton("Next Button");
        nextButtonLabel = FindText("Text", nextButton != null ? nextButton.transform : null);

        if (titleText != null)
        {
            titleText.text = title;
        }
    }

    private void BindButtons()
    {
        BindButton(nextButton, ShowNextPage);
    }

    private void ShowPage(int index)
    {
        if (Pages.Length == 0)
        {
            GoToRunOverview();
            return;
        }

        pageIndex = Mathf.Clamp(index, 0, Pages.Length - 1);

        if (descriptionText != null)
        {
            descriptionText.text = Pages[pageIndex];
        }

        if (nextButtonLabel != null)
        {
            nextButtonLabel.text = pageIndex >= Pages.Length - 1 ? "Continue" : "Next";
        }
    }

    private void ShowNextPage()
    {
        if (pageIndex >= Pages.Length - 1)
        {
            GoToRunOverview();
            return;
        }

        ShowPage(pageIndex + 1);
    }

    private void GoToRunOverview()
    {
        SceneManager.LoadScene(runOverviewSceneName);
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

    private static Button FindButton(string name, Transform parent = null)
    {
        var target = FindTransform(name, parent);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static TMP_Text FindText(string name, Transform parent = null)
    {
        var target = FindTransform(name, parent);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindTransform(string name, Transform parent = null)
    {
        if (parent != null)
        {
            return FindTransformRecursive(parent, name);
        }

        var activeScene = SceneManager.GetActiveScene();
        foreach (var rootObject in activeScene.GetRootGameObjects())
        {
            var match = FindTransformRecursive(rootObject.transform, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (var index = 0; index < root.childCount; index++)
        {
            var match = FindTransformRecursive(root.GetChild(index), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
