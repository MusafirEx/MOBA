using UnityEngine;
using UnityEngine.UI;

public class MonsterHealthUI : MonoBehaviour
{
    public MonsterStatsNetwork stats;
    public Image fill;

    private void Update()
    {
        if (stats == null || fill == null) return;
        fill.fillAmount = stats.maxHP <= 0f ? 0f : Mathf.Clamp01(stats.HP / stats.maxHP);
    }
}
