using TMPro;
using UnityEngine;

public class GameJoltLoginUI : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField tokenInput;
    public TMP_Text statusText;
    public GameJoltManager manager;

    public void OnClickLogin()
    {
        if (manager == null) manager = GameJoltManager.Instance;
        if (manager == null) return;

        manager.SetCredentials(usernameInput != null ? usernameInput.text : "", tokenInput != null ? tokenInput.text : "");
        SetStatus("Logging in...");
        manager.Auth((ok, message) => SetStatus(ok ? "Game Jolt login OK" : "Game Jolt login failed: " + message));
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
        Debug.Log(text);
    }
}
