using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public PlayerProgressData progress = new PlayerProgressData();
    public GameJoltCloudSaveManager cloudSave;

    public void ApplyPrototypeReward(int winnerTeamId)
    {
        // Prototype: every match gives base reward. Later compare local player's team.
        bool win = winnerTeamId >= 0;
        int xp = win ? 300 : 150;
        int coins = win ? 100 : 50;

        progress.AddReward(xp, coins, win);
        Debug.Log($"Reward: +{xp} XP, +{coins} coins");

        if (cloudSave != null)
            cloudSave.SaveProgress(progress);
    }
}
