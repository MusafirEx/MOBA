/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using System.Collections;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public enum StatusEffectType
    {
        AddOverTime,
        ReduceOverTime,
        TemporaryBoost
    }

    [CreateAssetMenu(fileName = "StatEffectConfig", menuName = "FusionBrawler/StatEffectConfig")]
    public class StatEffectConfig : ScriptableObject
    {
        public StatusEffectType effectType; // the effect to use on the stat
        public StatType affectedStat;// the stat to effect
        public int effectValue; // the amount to effect the stat
        public float duration; // the duration of the stat change
        public float tickInterval; // how often the effect applies
        public bool revertAfter = true; // whether to revert the boost after duration
        public bool applyFullValuePerTick = false; // apply the whole value of the effect every tick
        public bool canStack = true; // whether the effect can stack
        public bool stackDuration = false; // whether to stack the duration if the effect can't stack

        private Coroutine effectCoroutine;

        // apply the effect to the stat in the player stats manager
        public void ApplyEffect(PlayerStatsManager playerStatsManager)
        {
            if (canStack)
            {
                playerStatsManager.StartCoroutine(ApplyEffectCoroutine(playerStatsManager));
            }
            else if (stackDuration)
            {
                if (effectCoroutine != null)
                {
                    playerStatsManager.StopCoroutine(effectCoroutine);
                }
                effectCoroutine = playerStatsManager.StartCoroutine(ApplyEffectCoroutine(playerStatsManager));
            }
            else
            {
                if (effectCoroutine == null)
                {
                    effectCoroutine = playerStatsManager.StartCoroutine(ApplyEffectCoroutine(playerStatsManager));
                }
            }
        }

        // start the effect coroutine to apply the stat effect based on duration
        private IEnumerator ApplyEffectCoroutine(PlayerStatsManager playerStatsManager)
        {
            float elapsed = 0f;
            int ticks = Mathf.CeilToInt(duration / tickInterval);
            int valuePerTick = applyFullValuePerTick ? effectValue : effectValue / ticks;

            switch (effectType)
            {
                case StatusEffectType.AddOverTime:
                    while (elapsed < duration)
                    {
                        playerStatsManager.ModifyStat(affectedStat, valuePerTick);
                        yield return new WaitForSeconds(tickInterval);
                        elapsed += tickInterval;
                    }
                    break;

                case StatusEffectType.ReduceOverTime:
                    while (elapsed < duration)
                    {
                        if (affectedStat != StatType.Hp)
                        {
                            playerStatsManager.ModifyStat(affectedStat, -valuePerTick);
                        }
                        else
                        {
                            playerStatsManager.ApplyOtherDamage(valuePerTick);
                        }
                        yield return new WaitForSeconds(tickInterval);
                        elapsed += tickInterval;
                    }
                    break;

                case StatusEffectType.TemporaryBoost:
                    playerStatsManager.TemporaryBoostStat(affectedStat, effectValue, duration, revertAfter, canStack, stackDuration);
                    yield break;
            }

            effectCoroutine = null;

        }
    }

}
