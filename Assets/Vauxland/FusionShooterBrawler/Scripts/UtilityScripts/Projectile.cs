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
    public class Projectile : NetworkBehaviour
    {
        // projectile fire data synced over the network
        [Networked] private int _fireTick { get; set; }
        [Networked] private Vector3 _firePosition { get; set; }
        [Networked] private Vector3 _fireVelocity { get; set; }
        [Networked] private NetworkBool DidHit { get; set; }
        [Networked] private TickTimer ProjectileLifeTimer { get; set; }
        [Networked] private Vector3 _hitPosition { get; set; }
        [Networked] private int _bounceCount { get; set; }
        [Networked] private NetworkId _homingTargetId { get; set; }
        [Networked] private NetworkBool _isHoming { get; set; }

        private bool _isProjectileInitialized;

        [Header("ProjectileSettings")]
        [SerializeField] private float projectileSpeed; // the speed of the projectile
        [SerializeField] private float projectileLifetime; // the lifetime of the projectile
        [SerializeField] private Transform impactPoint; // the transform where a prjectile rgeisters a hit
        [SerializeField] private GameObject projectileObject; // the visual object representing the projectile
        [SerializeField] private TrailRenderer trail; // trail reneder effect of the projectile
        [SerializeField] private LayerMask hitLayers; // the layers this projectile can affect
        [SerializeField] private int maxBounceCount = 3; // the max amount of times it can bounce before the lifetime runs out

        [Header("Projectile Type")]
        public bool isExplosive;
        [SerializeField] private float explosionRadius; // the radius this projectile can damage things if its an explosive

        [Header("Stat Effect")]
        public StatEffectConfig statEffect;

        [Header("Projectile Effects")]
        public GameObject explosionEffect; // explosion visual effect
        public GameObject hitEffect; // particle system to play when projectile hits something
        public GameObject spawnEffect; // spawn effect if you want when the projectile is spawned 

        // hit info
        private readonly List<LagCompensatedHit> hits = new List<LagCompensatedHit>(10); // pre-allocate list with an initial capacity, change to what you prefer
        private LagCompensatedHit hit = new LagCompensatedHit(); // single hit Hit info

        // shooter of this projectile info
        private PlayerRef shooterPlayerRef;
        private NetworkObject shooterNetworkObject;
        private PlayerNetworkController shooterPlayerController;
        private WeaponConfig attackingWeapon;
        private ChangeDetector _changeDetector;

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            DidHit = false;
        }

        // shoots the projectile with the given data and syncs it over the network
        public void ShootProjectile(Vector3 position, Vector3 direction, PlayerRef firedByPlayerRef, NetworkObject firedByNetworkObject, WeaponConfig playerWeapon, PlayerNetworkController playerController, NetworkId homingTargetId = default)
        {
            // save fire data
            _fireTick = Runner.Tick;
            _firePosition = position;
            _fireVelocity = direction * projectileSpeed;
            _bounceCount = 0;

            if (projectileLifetime > 0f)
            {
                ProjectileLifeTimer = TickTimer.CreateFromSeconds(Runner, projectileLifetime);
            }

            shooterPlayerRef = firedByPlayerRef;
            shooterNetworkObject = firedByNetworkObject;
            attackingWeapon = playerWeapon;
            shooterPlayerController = playerController;
            _homingTargetId = homingTargetId;
            _isHoming = playerWeapon != null && playerWeapon.aimMode == WeaponAimMode.LockOnHoming && homingTargetId != default;
        }

        // reset the projectiles state
        public void ResetProjectileState()
        {
            // mark it as not initialized so it re-initializes properly on reuse
            _isProjectileInitialized = false;

            // reset the life timer
            ProjectileLifeTimer = TickTimer.None;
            // clear the bounce count
            _bounceCount = 0;
            _homingTargetId = default;
            _isHoming = false;

            // reset the TrailRenderer
            if (trail != null)
            {
                trail.Clear();
            }
        }

        public override void FixedUpdateNetwork()
        {
            // check if the projectile has reached the end of its life
            if (ProjectileLifeTimer.IsRunning && ProjectileLifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            // if its already hit something stop
            if (DidHit)
            {
                return;
            }

            UpdateHomingVelocity();

            // we calculate our projectile movement here
            var previousPosition = GetProjectilePosition(Runner.Tick - 1);
            var nextPosition = GetProjectilePosition(Runner.Tick);

            var direction = nextPosition - previousPosition;

            float distance = direction.magnitude;
            direction /= distance;

            if (Object.HasStateAuthority)
            {
                if (!isExplosive)
                {
                    bool isHit = Runner.LagCompensation.Raycast(previousPosition, direction, 0.5f, shooterPlayerRef, out hit, hitLayers, HitOptions.IncludePhysX);
                    HandleSingleHit(isHit);
                }
                else
                {
                    int hitCount = Runner.LagCompensation.OverlapSphere(previousPosition, 0.5f, shooterPlayerRef, hits, hitLayers, HitOptions.IncludePhysX);
                    HandleExplosiveHit(hitCount);
                }
            }
        }

        public override void Render()
        {
            // only initialize if networked variables are valid
            if (IsProxy && _fireVelocity.sqrMagnitude <= 0.0001f)
                return;

            // set up our projectile after its set active
            InitializeProjectile();

            foreach (var change in _changeDetector.DetectChanges(this, out var prev, out var current))
            {
                switch (change)
                {
                    case nameof(DidHit):
                        var reader = GetPropertyReader<NetworkBool>(nameof(DidHit));
                        var (wasActive, isActive) = reader.Read(prev, current);
                        HandleProjectileVisuals(wasActive, isActive);
                        break;
                }
            }

            if (DidHit)
                return;

            float renderTime = Object.IsProxy == true ? Runner.RemoteRenderTime : Runner.LocalRenderTime;
            float floatTick = renderTime / Runner.DeltaTime;

            // calculate positions
            var previousPosition = GetProjectilePosition(floatTick - 1);
            var currentPosition = GetProjectilePosition(floatTick);

            // update rotation based on movement direction
            Vector3 movementDirection = currentPosition - previousPosition;
            if (movementDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(movementDirection.normalized);
            }

            // update position
            transform.position = currentPosition;
        }

        private void UpdateHomingVelocity()
        {
            if (!_isHoming || !Object.HasStateAuthority || attackingWeapon == null)
                return;

            if (!Runner.TryFindObject(_homingTargetId, out NetworkObject target) || target == null)
            {
                _isHoming = false;
                return;
            }

            PlayerStatsManager targetStats = target.GetComponent<PlayerStatsManager>();
            if (targetStats == null || !targetStats.IsAlive)
            {
                _isHoming = false;
                return;
            }

            Vector3 currentPosition = GetProjectilePosition(Runner.Tick - 1);
            Vector3 targetPoint = target.transform.position + Vector3.up * attackingWeapon.homingTargetOffsetY;
            Vector3 desiredDirection = (targetPoint - currentPosition).normalized;
            Vector3 currentDirection = _fireVelocity.sqrMagnitude > 0.0001f
                ? _fireVelocity.normalized
                : transform.forward;

            float maxRadians = attackingWeapon.homingTurnSpeed * Mathf.Deg2Rad * Runner.DeltaTime;
            Vector3 newDirection = Vector3.RotateTowards(currentDirection, desiredDirection, maxRadians, 0f);
            float speed = _fireVelocity.magnitude;

            _firePosition = currentPosition;
            _fireTick = Runner.Tick - 1;
            _fireVelocity = newDirection.normalized * speed;
        }

        private void InitializeProjectile()
        {
            if (_isProjectileInitialized == true)
                return;

            // here we try to ensure that the networked variables have valid data
            if (IsProxy)
            {
                if (_fireVelocity.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(_fireVelocity);
                    transform.position = _firePosition;

                    _isProjectileInitialized = true;
                }
                else
                {
                    // wait until the networked variables are valid
                    return;
                }
            }
            else
            {
                _isProjectileInitialized = true;
            }

            // set our projectile visual inactive
            if (projectileObject != null)
                projectileObject.SetActive(true);

            // re-enable the trail renderer
            if (trail != null)
                trail.enabled = true;

            if (spawnEffect != null)
            {
                spawnEffect.GetComponent<ParticleSystem>().Play();
            }

            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            DidHit = false;
        }

        // get our prpjectile's current position over the network
        private Vector3 GetProjectilePosition(float currentTick)
        {
            float time = (currentTick - _fireTick) * Runner.DeltaTime;

            if (time <= 0f)
                return _firePosition;

            return _firePosition + _fireVelocity * time;
        }

        // non-explosive projectiles
        private void HandleSingleHit(bool isHit)
        {
            if (isHit)
            {
                _hitPosition = hit.Point; // set the networked hit position to projectiles impact point
                if (hit.Hitbox != null)
                {
                    // don't hit our own player and then take action on valid hit
                    if (hit.Hitbox.Root.GetBehaviour<NetworkObject>() != shooterNetworkObject)
                    {
                        // set DidHit to true because successfully hit something
                        DidHit = true;
                        PlayerManager shooterPlayer = shooterNetworkObject.GetComponent<PlayerManager>();
                        PlayerManager targetPlayer = hit.Hitbox.transform.root.GetComponent<PlayerManager>();
                        DestructibleConfig destructible = hit.Hitbox.Root.GetComponent<DestructibleConfig>();

                        if (targetPlayer != null)
                        {
                            // if players are on the same team don't apply damage
                            bool isSameTeam = (targetPlayer._playerController.TeamInt != 0 && targetPlayer._playerController.TeamInt == shooterPlayerController.TeamInt);
                            if (!isSameTeam && !shooterPlayer._matchManager.FriendlyFire)
                            {
                                if (shooterPlayerController.IsBot)
                                {
                                    targetPlayer._playerStats.ApplyBotDamage(shooterPlayer._playerStats.AttackDamage, shooterNetworkObject);
                                }
                                else
                                {
                                    targetPlayer._playerStats.ApplyDamage(Object.InputAuthority);
                                }
                                
                                if (statEffect != null)
                                {
                                    statEffect.ApplyEffect(targetPlayer._playerStats); // apply the stat effect if the prpjectile has one set
                                }
                                return;
                            }
                            else if (shooterPlayer._matchManager.FriendlyFire && targetPlayer._networkObject != shooterNetworkObject) // if friendly fire is on apply damage to teammates
                            {
                                if (shooterPlayerController.IsBot)
                                {
                                    targetPlayer._playerStats.ApplyBotDamage(shooterPlayer._playerStats.AttackDamage, shooterNetworkObject);
                                }
                                else
                                {
                                    targetPlayer._playerStats.ApplyDamage(Object.InputAuthority);
                                }

                                if (statEffect != null)
                                {
                                    statEffect.ApplyEffect(targetPlayer._playerStats);
                                }
                                return;
                            }
                        }

                        // if we hit a destructible apply the damage to it
                        if (destructible != null)
                        {
                            destructible.HitDestructible(attackingWeapon.destructibleDamage);
                            return;
                        }
                    }
                }
                else if (hit.GameObject != null) // hit an obstacle or wall
                {
                    // bounce the projectile if maxBounceCount is not set to 0
                    Vector3 reflectedVelocity = Vector3.Reflect(_fireVelocity, hit.Normal);
                    _fireVelocity = reflectedVelocity;
                    _firePosition = hit.Point;
                    _fireTick = Runner.Tick;

                    // add each bounce to the count and check if it exceeds the maximum
                    _bounceCount++;
                    if (_bounceCount >= maxBounceCount)
                    {
                        DidHit = true; // stop the projectile
                    }
                }
            }
        }

        // if our projectile is an exsplosive
        private void HandleExplosiveHit(int hitCount)
        {
            bool isValidHit = false;

            if (hitCount > 0)
            {
                isValidHit = true;
            }

            // when we launch the projectile we dont want our own player triggering it
            for (int i = 0; i < hitCount; i++)
            {
                if (hits[i].Hitbox != null)
                {
                    if (hits[i].Hitbox.Root.GetBehaviour<NetworkObject>() == shooterNetworkObject)
                    {
                        isValidHit = false;
                        break;
                    }
                }
            }

            // same as single hit setup except in the explosion radius so multiple targets can be hit
            if (isValidHit)
            {
                DidHit = true;
                hitCount = Runner.LagCompensation.OverlapSphere(impactPoint.position, explosionRadius, shooterPlayerRef, hits, hitLayers, HitOptions.None);
                _hitPosition = impactPoint.position;
                for (int i = 0; i < hitCount; i++)
                {
                    if (hits[i].Hitbox != null)
                    {
                        PlayerManager shooterPlayer = shooterNetworkObject.GetComponent<PlayerManager>();
                        PlayerManager targetPlayer = hits[i].Hitbox.Root.GetComponent<PlayerManager>();
                        DestructibleConfig destructible = hits[i].Hitbox.Root.GetComponent<DestructibleConfig>();

                        if (targetPlayer != null) // if we hit a player
                        {
                            var targetTeamInt = targetPlayer._playerController.TeamInt;

                            // if players are not on the same team do damage, however if its us we still apply damage to ourself
                            bool isSameTeam = (targetTeamInt > 0 && targetTeamInt == shooterPlayerController.TeamInt && targetPlayer._networkObject != shooterNetworkObject); 
                            if (!isSameTeam && !shooterPlayer._matchManager.FriendlyFire)
                            {
                                if (shooterPlayerController.IsBot)
                                {
                                    targetPlayer._playerStats.ApplyBotDamage(shooterPlayer._playerStats.AttackDamage, shooterNetworkObject);
                                }
                                else
                                {
                                    targetPlayer._playerStats.ApplyDamage(Object.InputAuthority);
                                }

                                if (statEffect != null)
                                {
                                    statEffect.ApplyEffect(targetPlayer._playerStats);
                                }
                            }
                            else if (shooterPlayer._matchManager.FriendlyFire && targetPlayer._networkObject != shooterNetworkObject)
                            {
                                if (shooterPlayerController.IsBot)
                                {
                                    targetPlayer._playerStats.ApplyBotDamage(shooterPlayer._playerStats.AttackDamage, shooterNetworkObject);
                                }
                                else
                                {
                                    targetPlayer._playerStats.ApplyDamage(Object.InputAuthority);
                                }

                                if (statEffect != null)
                                {
                                    statEffect.ApplyEffect(targetPlayer._playerStats);
                                }
                                return;
                            }
                        }

                        if (destructible != null) // if we hit a destructible
                        {
                            destructible.HitDestructible(attackingWeapon.destructibleDamage);
                        }
                    }
                    else if (hits[i].GameObject != null) // hit and object or wall
                    {
                        return;
                    }
                }
            }
        }

        // set our projectiles visal objects inactive after it hit something and playy particle effects
        private void HandleProjectileVisuals(bool wasActive, bool isActive)
        {
            if (isActive != wasActive)
            {
                if (hitEffect != null)
                {
                    hitEffect.GetComponent<ParticleSystem>().Play();
                }

                if (explosionEffect != null)
                {
                    explosionEffect.GetComponent<ParticleSystem>().Play();
                }

                if (projectileObject != null)
                {
                    projectileObject.SetActive(false);
                }


                if (trail != null)
                {
                    trail.enabled = false;
                    trail.Clear();
                }

                // let the particle system play before despawning projectile
                ProjectileLifeTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);

            }
        }

        // when despawning the object resets its state
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ResetProjectileState();
        }

    }
}


