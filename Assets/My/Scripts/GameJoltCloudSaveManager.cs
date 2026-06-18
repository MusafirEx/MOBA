using UnityEngine;

public class GameJoltCloudSaveManager : MonoBehaviour
{
    public const string ProgressKey = "progress";
    public GameJoltManager manager;
    public PlayerProgressData loadedProgress = new PlayerProgressData();

    private void Awake()
    {
        if (manager == null) manager = GameJoltManager.Instance;
    }

    public void SaveProgress(PlayerProgressData progress)
    {
        if (manager == null) manager = GameJoltManager.Instance;
        if (manager == null || !manager.IsAuthenticated)
        {
            Debug.LogWarning("Game Jolt not authenticated. Progress not uploaded.");
            return;
        }

        string json = JsonUtility.ToJson(progress);
        manager.SetData(ProgressKey, json, (ok, msg) =>
        {
            Debug.Log(ok ? "Progress saved to Game Jolt" : "Save failed: " + msg);
        });
    }

    public void LoadProgress(System.Action<PlayerProgressData> onLoaded)
    {
        if (manager == null) manager = GameJoltManager.Instance;
        if (manager == null || !manager.IsAuthenticated)
        {
            onLoaded?.Invoke(loadedProgress);
            return;
        }

        manager.GetData(ProgressKey, (ok, msg) =>
        {
            if (ok)
            {
                // Minimal parser: Game Jolt wraps data in JSON. For prototype, extract between "data":"..." later with robust parser if needed.
                Debug.Log("Raw Game Jolt progress response: " + msg);
            }
            onLoaded?.Invoke(loadedProgress);
        });
    }
}
