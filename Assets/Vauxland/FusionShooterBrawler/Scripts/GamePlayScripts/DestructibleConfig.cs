/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class DestructibleConfig : NetworkBehaviour
    {
        [Header("Destructible Set Up")]
        [SerializeField] private int maxHP = 10; // the max Hp of the destructible
        [SerializeField] private int explodeTime; // time it takes to explode after being hit if no more damage is dealt to it
        [SerializeField] private float explosionRadius = 5.0f; // the explosion radius that will damage anything in the radius
        [SerializeField] private int explosionDamage = 100; // the amount of damage to be done when it explodes
        [SerializeField] private LayerMask hitLayers;  // the layers that can be affected by the explosion
        [SerializeField] private GameObject explosionEffect; // the visual effect to play if its an explosive
        [SerializeField] private GameObject destroyEffect; // the visual effect to play if its a destructible
        [SerializeField] private GameObject visualObject; // the model of the destructible
        [SerializeField] private bool isExplosive = false; // set is explosive default

        [Networked] public int CurrentHP { get; set; } // current Hp of the destructible thats networked
        [Networked] private NetworkBool WasHit { get; set; } // bool to check if its been hit by a projectile
        [Networked] private NetworkBool IsAlive { get; set; } // bool to check if the destructible is still active and not destroyed
        [Networked] private TickTimer DespawnTimer { get; set; } // network timer to despawn the destructible

        private ChangeDetector _changeDetector;
        private HitboxRoot hitboxRoot; // hitbox root of the hitbox located on a child object of the destructible
        private bool hasExploded = false; 

        public override void Spawned()
        {
            CurrentHP = maxHP; // set our current Hp to max on spawn
            IsAlive = true;
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            hitboxRoot = GetComponent<HitboxRoot>();

            if (explosionEffect != null)
            {
                explosionEffect.SetActive(false);
            }
            if (destroyEffect != null)
            {
                destroyEffect.SetActive(false);
            }
        }

        public override void Render()
        {
            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this, out var prev, out var current))
            {
                switch (change)
                {
                    case nameof(CurrentHP):
                        var hpReader = GetPropertyReader<int>(nameof(CurrentHP));
                        var (oldHp, newHp) = hpReader.Read(prev, current);
                        AffectHp(oldHp, newHp);
                        break;
                    case nameof(IsAlive):
                        var statusReader = GetPropertyReader<NetworkBool>(nameof(IsAlive));
                        var (oldStatus, newStatus) = statusReader.Read(prev, current);
                        AffectDestructible(oldStatus, newStatus);
                        break;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (CurrentHP <= 0)
            {
                IsAlive = false;

                if (isExplosive && !hasExploded)
                {
                    IsAlive = false;
                }
            }
            else if (WasHit && isExplosive && !hasExploded && DespawnTimer.Expired(Runner))
            {
                IsAlive = false;
            }
            if (hasExploded && DespawnTimer.ExpiredOrNotRunning(Runner))
            {
                Runner.Despawn(Object);
            }
        }

        // if the destructible was hit apply damage
        public void HitDestructible(int damage)
        {
            if (Object == null) return;
            if (Object.HasStateAuthority == false) return;
            if (!IsAlive) return;

            Debug.Log($"Destructible hit with {damage} damage.");

            int damageAmount = damage;

            if (damageAmount > 0)
            {
                int newHp = CurrentHP - damageAmount;
                CurrentHP = Mathf.Max(newHp, 0);
            }

            WasHit = true;
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, explodeTime);
        }

        // explode the destructible
        private void Explode()
        {
            hasExploded = true;
            hitboxRoot.HitboxRootActive = false;

            // apply explosion damage in radius
            List<LagCompensatedHit> hits = new List<LagCompensatedHit>();
            int hitCount = Runner.LagCompensation.OverlapSphere(transform.position, explosionRadius, Object.InputAuthority, hits, hitLayers, HitOptions.None);

            for (int i = 0; i < hitCount; i++)
            {
                PlayerStatsManager statsManager = hits[i].Hitbox.transform.root.GetComponent<PlayerStatsManager>();
                DestructibleConfig destructible = hits[i].Hitbox.Root.GetComponent<DestructibleConfig>();

                if (statsManager != null)
                {
                    statsManager.ApplyOtherDamage(explosionDamage);
                }

                if (destructible != null && destructible != this)
                {
                    destructible.HitDestructible(explosionDamage);
                }
            }

            if (visualObject != null)
            {
                visualObject.SetActive(false);
            }

            // play the explosion effect
            if (explosionEffect != null)
            {
                explosionEffect.SetActive(true);
                var explodeEffect = explosionEffect.GetComponent<ParticleSystem>();
                explodeEffect.Play();
                DespawnTimer = TickTimer.CreateFromSeconds(Runner, 1);
            }

            // set the current Hp to 0 because it has exploded
            CurrentHP = 0;
        }

        // destroy the obstacle after its exploded
        private void DestroyObstacle()
        {
            hasExploded = true;
            hitboxRoot.HitboxRootActive = false; // set hitbox inactive so it can't receive more damage
            if (visualObject != null)
            {
                visualObject.SetActive(false);
            }

            if (destroyEffect != null)
            {
                destroyEffect.SetActive(true);
                var destroyedEffect = destroyEffect.GetComponent<ParticleSystem>();
                destroyedEffect.Play();
                DespawnTimer = TickTimer.CreateFromSeconds(Runner, 1);
            }
        }

        // called in render so the destructible explosion is synced across all clients
        private void AffectDestructible(bool oldStatus, bool newStatus)
        {
            // if is an explodable 
            if (newStatus != oldStatus && !hasExploded && isExplosive)
            {
                Explode();
            }

            // if is just a regular destructible
            if (newStatus != oldStatus && !isExplosive)
            {
                DestroyObstacle();
            }
        }

        private void AffectHp(int oldHp, int newHp)
        {
            if (newHp != oldHp)
            {
                // do something here if you would like, like animate the object being hit or play effects, sound, etc.
            }
        }

        // just shows the bounds of the explosion radius to help you set your radius
        private void OnDrawGizmos()
        {
            if (isExplosive)
                Gizmos.DrawWireSphere(transform.position + (Vector3.up * 1f), explosionRadius);
        }
    }
}
