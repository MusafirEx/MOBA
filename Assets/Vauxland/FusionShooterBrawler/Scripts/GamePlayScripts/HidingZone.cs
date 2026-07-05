/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class HidingZone : MonoBehaviour
    {
        // when our player enters a hiding zone set is hiding in the network controller
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var playerManager = other.GetComponent<PlayerManager>();
                if (playerManager != null)
                {
                    playerManager._playerController.SetIsHiding(true);
                }
            }
        }

        // when the player leaves set is hiding false
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var playerManager = other.GetComponent<PlayerManager>();
                if (playerManager != null)
                {
                    playerManager._playerController.SetIsHiding(false);
                }
            }
        }
    }
}

