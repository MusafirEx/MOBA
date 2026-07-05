/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System.Collections.Generic;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public enum ZoneEffectType
    {
        ReduceOverTime,
        AddOverTime,
        StatBoost
    }

    public class StatZoneEffect : NetworkBehaviour
    {
        [Header("Zone Effect Settings")]
        [SerializeField] private ZoneEffectType effectType; // the zone effect type
        [SerializeField] private StatType affectedStat = StatType.Hp; // the stat the zone will effect
        [SerializeField] private int effectValue = 10; // how much it will effect the stat
        [SerializeField] private float effectInterval = 1.0f; // how long it will effect the stat
        [SerializeField] private bool revertAfterExit = true; // only for StatBoost

        private List<PlayerStatsManager> playersInZone = new List<PlayerStatsManager>();
        [Networked] private TickTimer effectTimer { get; set; }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            // only apply periodic effects for ReduceOverTime and AddOverTime
            if (effectType == ZoneEffectType.ReduceOverTime || effectType == ZoneEffectType.AddOverTime)
            {
                if (effectTimer.ExpiredOrNotRunning(Runner))
                {
                    ApplyEffectToPlayers();
                    effectTimer = TickTimer.CreateFromSeconds(Runner, effectInterval);
                }
            }
        }

        private void ApplyEffectToPlayers()
        {
            foreach (var player in playersInZone)
            {
                if (player != null && player.IsAlive)
                {
                    switch (effectType)
                    {
                        case ZoneEffectType.ReduceOverTime:
                            // apply damage using ApplyOtherDamage to affect body armor and Hp to call set health method
                            player.ApplyOtherDamage(effectValue);
                            break;

                        case ZoneEffectType.AddOverTime:
                            // heal the player by modifying the stat directly
                            player.ModifyStat(affectedStat, effectValue);
                            break;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

            var hitboxRoot = other.GetComponentInParent<HitboxRoot>();
            if (hitboxRoot != null)
            {
                var playerStats = hitboxRoot.GetComponent<PlayerStatsManager>();
                if (playerStats != null && !playersInZone.Contains(playerStats))
                {
                    playersInZone.Add(playerStats);

                    if (effectType == ZoneEffectType.StatBoost)
                    {
                        // apply stat boost when the player enters the zone
                        playerStats.ModifyStat(affectedStat, effectValue);
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

            var hitboxRoot = other.GetComponentInParent<HitboxRoot>();
            if (hitboxRoot != null)
            {
                var playerStats = hitboxRoot.GetComponent<PlayerStatsManager>();
                if (playerStats != null && playersInZone.Contains(playerStats))
                {
                    playersInZone.Remove(playerStats);

                    if (effectType == ZoneEffectType.StatBoost && revertAfterExit)
                    {
                        // revert stat boost when the player exits the zone
                        playerStats.ModifyStat(affectedStat, -effectValue);
                    }
                }
            }
        }
    }
}


