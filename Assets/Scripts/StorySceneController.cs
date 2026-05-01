using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public sealed class StorySceneController : MonoBehaviour
{
    private const string StorySceneMusicPath = "Sounds/Background Music/Background/Dark Ambient 3";

    [SerializeField] private string storySceneName = "StoryScene";
    [SerializeField] private string heroSelectSceneName = "HeroSelectScene";
    [SerializeField] private string storyModeTitle = "Story Mode";
    [SerializeField] private string endlessModeTitle = "Endless Mode";

    private static readonly string[] StoryModePages =
    {
        "The tower stands quiet, its peak hiding a forgotten princess. Those who enter soon learn - its rooms do not follow a single path. Some routes feel safer, others whisper of greater danger... and greater reward.",
        "The spirits within cannot rest. Even when defeated, they linger, bound to the tower's curse. Face them again, and their strength may become your own.",
        "Do not trust what you see. The tower is deeper than it appears - filled with hidden rooms, unseen paths, and truths that refuse to stay buried."
    };

    private static readonly string[] EndlessModePages =
    {
        "Beyond the known paths rises a tower with no end. Its peak is lost among the clouds, unseen and unreachable.",
        "Each room leads only to another, and another - no path truly repeats. The deeper you climb, the stronger the spirits become... yet they never cease.",
        "Some say the tower itself is alive, feeding on those who dare to ascend. There is no escape, no final room - only the climb."
    };

    private static bool sceneHookRegistered;

    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private Button nextButton;
    private Button skipButton;
    private TMP_Text nextButtonLabel;
    private int pageIndex;
    private IReadOnlyList<string> pages;

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
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "StoryScene")
        {
            return;
        }

        if (FindAnyObjectByType<StorySceneController>() != null)
        {
            return;
        }

        var controllerRoot = new GameObject(nameof(StorySceneController));
        controllerRoot.AddComponent<StorySceneController>();
    }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != storySceneName)
        {
            Destroy(gameObject);
            return;
        }

        BindScene();
        BindButtons();
        LoadPagesForPendingMode();
        ShowPage(0);
        AudioManager.PlayMusic(StorySceneMusicPath, true);
    }

    private void BindScene()
    {
        titleText = FindText("Title");
        descriptionText = FindText("Description");
        nextButton = FindButton("Next Button");
        skipButton = FindButton("Skip Button");
        nextButtonLabel = FindText("Text", nextButton != null ? nextButton.transform : null);
    }

    private void BindButtons()
    {
        BindButton(nextButton, ShowNextPage);
        BindButton(skipButton, GoToHeroSelect);
    }

    private void LoadPagesForPendingMode()
    {
        var isEndlessMode = string.Equals(RunSession.PendingMode, "Endless", System.StringComparison.OrdinalIgnoreCase);
        pages = isEndlessMode ? EndlessModePages : StoryModePages;

        if (titleText != null)
        {
            titleText.text = isEndlessMode ? endlessModeTitle : storyModeTitle;
        }
    }

    private void ShowPage(int index)
    {
        if (pages == null || pages.Count == 0)
        {
            GoToHeroSelect();
            return;
        }

        pageIndex = Mathf.Clamp(index, 0, pages.Count - 1);

        if (descriptionText != null)
        {
            descriptionText.text = pages[pageIndex];
        }

        if (nextButtonLabel != null)
        {
            nextButtonLabel.text = pageIndex >= pages.Count - 1 ? "Start" : "Next Page";
        }
    }

    private void ShowNextPage()
    {
        if (pages == null || pageIndex >= pages.Count - 1)
        {
            GoToHeroSelect();
            return;
        }

        ShowPage(pageIndex + 1);
    }

    private void GoToHeroSelect()
    {
        SceneLoader.LoadScene(heroSelectSceneName);
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
