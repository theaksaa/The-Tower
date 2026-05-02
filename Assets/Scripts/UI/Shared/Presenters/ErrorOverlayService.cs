using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ErrorOverlayService
{
    private const string DefaultErrorMessage = "An unexpected error occurred.";

    private static bool isInitialized;
    private static string pendingMessage;
    private static string pendingSceneName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        Application.logMessageReceived += HandleLogMessageReceived;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        isInitialized = true;
    }

    public static void ShowError(string message)
    {
        Initialize();

        pendingMessage = NormalizeMessage(message);
        pendingSceneName = null;

        foreach (var overlay in GetActiveSceneOverlays())
        {
            overlay.Show(pendingMessage);
        }
    }

    public static void QueueError(string message)
    {
        QueueError(message, null);
    }

    public static void QueueError(string message, string sceneName)
    {
        Initialize();
        pendingMessage = NormalizeMessage(message);
        pendingSceneName = string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
    }

    public static void HideAll()
    {
        pendingMessage = null;
        pendingSceneName = null;

        foreach (var overlay in GetActiveSceneOverlays())
        {
            overlay.Hide();
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrWhiteSpace(pendingMessage))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(pendingSceneName) &&
            !string.Equals(scene.name, pendingSceneName, System.StringComparison.Ordinal))
        {
            return;
        }

        ShowError(pendingMessage);
    }

    private static void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Assert && type != LogType.Exception)
        {
            return;
        }

        ShowError(condition);
    }

    private static string NormalizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? DefaultErrorMessage
            : message.Trim();
    }

    private static List<ErrorOverlayView> GetActiveSceneOverlays()
    {
        var activeScene = SceneManager.GetActiveScene();
        var overlays = Object.FindObjectsByType<ErrorOverlayView>(FindObjectsInactive.Include);
        var results = new List<ErrorOverlayView>(overlays.Length);

        for (var index = 0; index < overlays.Length; index++)
        {
            var overlay = overlays[index];
            if (overlay == null || overlay.gameObject.scene != activeScene)
            {
                continue;
            }

            results.Add(overlay);
        }

        return results;
    }
}
