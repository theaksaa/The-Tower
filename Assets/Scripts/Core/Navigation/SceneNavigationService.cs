public static class SceneNavigationService
{
    public static bool IsLoading => SceneLoader.IsLoading;
    public static bool HasPendingScene => SceneLoader.HasPendingScene;
    public static string PendingSceneName => SceneLoader.PendingSceneName;

    public static void Load(string sceneName)
    {
        SceneLoader.LoadScene(sceneName);
    }

    public static void CompleteLoad()
    {
        SceneLoader.CompleteLoad();
    }

    public static void CancelLoad()
    {
        SceneLoader.CancelLoad();
    }
}
