using System;
using UnityEngine;

[Serializable]
public class PlayerProgressData
{
    public int playerLevel = 1;
    public int xp = 0;
    public int coins = 0;
    public int totalMatches = 0;
    public int totalWins = 0;
    public string selectedMonsterId = "mecha_raptor";
    public string selectedMainWeaponId = "basic_cannon";
    public string selectedSubWeaponId = "basic_missile";

    public void AddReward(int xpAmount, int coinAmount, bool win)
    {
        totalMatches++;
        if (win) totalWins++;
        xp += Mathf.Max(0, xpAmount);
        coins += Mathf.Max(0, coinAmount);

        while (xp >= playerLevel * 1000)
        {
            xp -= playerLevel * 1000;
            playerLevel++;
        }
    }
}
