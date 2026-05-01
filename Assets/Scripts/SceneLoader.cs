using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public const string LoadingSceneName = "LoadingScreen";

    private static string pendingSceneName;
    private static bool isLoading;

    public static bool HasPendingScene => !string.IsNullOrWhiteSpace(pendingSceneName);
    public static string PendingSceneName => pendingSceneName;
    public static bool IsLoading => isLoading;

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneLoader was asked to load an empty scene name.");
            return;
        }

        if (string.Equals(sceneName, LoadingSceneName, System.StringComparison.Ordinal))
        {
            SceneManager.LoadScene(LoadingSceneName);
            return;
        }

        pendingSceneName = sceneName;
        isLoading = true;
        SceneManager.LoadScene(LoadingSceneName);
    }

    public static bool TryConsumePendingScene(out string sceneName)
    {
        sceneName = pendingSceneName;
        pendingSceneName = null;
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    public static void CompleteLoad()
    {
        pendingSceneName = null;
        isLoading = false;
    }

    public static void CancelLoad()
    {
        pendingSceneName = null;
        isLoading = false;
    }
}
