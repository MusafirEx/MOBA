using UnityEngine;

[CreateAssetMenu(menuName = "Monster Arena/Game Jolt Config")]
public class GameJoltConfig : ScriptableObject
{
    public string gameId;
    [Tooltip("Game Jolt private key is required by the classic Game API. For stronger security, later proxy requests through your own backend.")]
    public string privateKey;
    public string apiBaseUrl = "https://api.gamejolt.com/api/game/v1_2";
}
