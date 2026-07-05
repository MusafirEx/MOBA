/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using UnityEngine;
using Fusion;

namespace Vauxland.FusionBrawler
{
    public class NetworkPickup : NetworkBehaviour
    {
        [Header("Powerup Setup")]
        public PowerupConfig powerupConfig;

        [Header("Weapon Pickup")]
        public WeaponConfig weaponPickup;

        [Header("Stat Effect")]
        public StatEffectConfig statEffect;

        private SpawnManager spawnManager;


        public override void Spawned()
        {

        }

        public void SetSpawnManager(SpawnManager manager)
        {
            spawnManager = manager;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) // if a player walks into the projectile
            {
                var playerStatsManager = other.GetComponent<PlayerStatsManager>();
                if (playerStatsManager != null && Object.HasStateAuthority)
                {
                    // A dead/respawning player can re-enter triggers when its controllers
                    // are disabled or re-enabled. Do not let that consume pickups.
                    if (!playerStatsManager.IsAlive ||
                        playerStatsManager.IsRespawning ||
                        !playerStatsManager.PlayerIsReady)
                    {
                        return;
                    }

                    if (!ApplyPowerUp(playerStatsManager))
                        return;

                    // notify the SpawnManager
                    if (spawnManager != null)
                    {
                        spawnManager.PickupPickedUp(Object);
                    }

                    Runner.Despawn(Object); // despawn only after a valid pickup was applied
                }
            }
        }

        // apply the pickup stats or weapon to our player
        private bool ApplyPowerUp(PlayerStatsManager playerStatsManager)
        {
            bool applied = false;

            if (powerupConfig != null)
            {
                foreach (var statEntry in powerupConfig.powerUpStats.stats)
                {
                    playerStatsManager.ModifyStat(statEntry.statType, statEntry.value);
                }

                applied = true;
            }

            if (statEffect != null)
            {
                statEffect.ApplyEffect(playerStatsManager);
                applied = true;
            }

            if (weaponPickup != null)
            {
                if (PlayerGameData.PlayerData == null)
                {
                    Debug.LogWarning(
                        $"Weapon pickup '{weaponPickup.name}' could not be applied because PlayerGameData is unavailable.",
                        this);
                    return applied;
                }

                int weaponId = PlayerGameData.PlayerData.GetWeaponID(weaponPickup);

                if (weaponId < 0)
                {
                    Debug.LogWarning(
                        $"Weapon pickup '{weaponPickup.weaponName}' is not registered in PlayerGameData.weapons. " +
                        "Add the exact WeaponConfig asset to the PlayerGameData weapons array.",
                        this);
                    return applied;
                }

                // Phase 3B still keeps WeaponID as the legacy/primary active weapon.
                // Three independent mounted-weapon pickups will be handled in Phase 3C.
                playerStatsManager.WeaponID = weaponId;
                applied = true;
            }

            return applied;
        }

    }
}

