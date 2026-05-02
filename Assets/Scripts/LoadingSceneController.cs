using System.Collections;
using TMPro;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class LoadingSceneController : MonoBehaviour
{
    private const string DefaultServerBaseUrl = "http://127.0.0.1:3000";
    private const string ServerConfigFileName = "server_config.json";
    [SerializeField] private string loadingSceneName = SceneLoader.LoadingSceneName;
    [SerializeField] private string loadingLabelObjectName = "Title Text";
    [SerializeField] private string loadingMessage = "Loading...";
    [SerializeField] private string missingTargetMessage = "Nothing to load.";
    [SerializeField] private float minimumVisibleDuration = 0.1f;
    [SerializeField] private string heroSelectSceneName = "HeroSelectScene";
    [SerializeField] private string defaultBaseUrl = "http://localhost:3000";
    [SerializeField] private bool useLocalFallbackIfApiFails = true;

    private static bool sceneHookRegistered;
    private TMP_Text loadingText;
    private bool startedLoading;

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
        if (!scene.IsValid() || !scene.isLoaded || scene.name != SceneLoader.LoadingSceneName)
        {
            return;
        }

        if (FindAnyObjectByType<LoadingSceneController>() != null)
        {
            return;
        }

        var controllerRoot = new GameObject(nameof(LoadingSceneController));
        controllerRoot.AddComponent<LoadingSceneController>();
    }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != loadingSceneName)
        {
            Destroy(gameObject);
            return;
        }

        defaultBaseUrl = ResolveBaseUrl();
        loadingText = FindLoadingText();
        SetLoadingText(loadingMessage);
    }

    private void Start()
    {
        if (!startedLoading)
        {
            startedLoading = true;
            StartCoroutine(LoadPendingSceneRoutine());
        }
    }

    private IEnumerator LoadPendingSceneRoutine()
    {
        yield return null;

        if (minimumVisibleDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(minimumVisibleDuration);
        }

        if (!SceneLoader.TryConsumePendingScene(out var targetSceneName))
        {
            SetLoadingText(missingTargetMessage);
            SceneLoader.CancelLoad();
            yield break;
        }

        if (string.Equals(targetSceneName, heroSelectSceneName, System.StringComparison.Ordinal))
        {
            yield return RunDataService.LoadRunConfig(defaultBaseUrl, useLocalFallbackIfApiFails, (config, usingFallback) =>
            {
                if (config != null)
                {
                    RunDataService.CacheRunConfig(config, usingFallback);
                }
            });
        }

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        SceneLoader.CompleteLoad();
    }

    private TMP_Text FindLoadingText()
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (var rootObject in activeScene.GetRootGameObjects())
        {
            var match = FindTransformRecursive(rootObject.transform, loadingLabelObjectName);
            if (match == null)
            {
                continue;
            }

            var text = match.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private string ResolveBaseUrl()
    {
        var configPath = Path.Combine(Application.persistentDataPath, ServerConfigFileName);
        if (File.Exists(configPath))
        {
            var existingJson = File.ReadAllText(configPath);
            var existingConfig = JsonUtility.FromJson<ServerUrlConfig>(existingJson);
            var normalizedExistingUrl = NormalizeServerBaseUrl(existingConfig);
            if (!string.IsNullOrWhiteSpace(normalizedExistingUrl))
            {
                return normalizedExistingUrl;
            }
        }

        var fallbackConfig = new ServerUrlConfig
        {
            serverUrl = DefaultServerBaseUrl
        };

        File.WriteAllText(configPath, JsonUtility.ToJson(fallbackConfig, true));
        return DefaultServerBaseUrl;
    }

    [System.Serializable]
    private sealed class ServerUrlConfig
    {
        public string serverUrl;
    }

    private string NormalizeServerBaseUrl(ServerUrlConfig config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.serverUrl))
        {
            return null;
        }

        var trimmedValue = config.serverUrl.Trim().TrimEnd('/');
        if (trimmedValue.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
        {
            return trimmedValue;
        }

        return $"http://{trimmedValue}:3000";
    }

    private void SetLoadingText(string value)
    {
        if (loadingText != null)
        {
            loadingText.text = value;
        }
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
