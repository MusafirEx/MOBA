using UnityEngine;

public class FusionConnectionUI : MonoBehaviour
{
    public FusionLauncher launcher;

    public void OnClickStart()
    {
        if (launcher != null) launcher.StartGame();
    }

    public void OnClickQuitSession()
    {
        if (launcher != null) launcher.ShutdownGame();
    }
}
