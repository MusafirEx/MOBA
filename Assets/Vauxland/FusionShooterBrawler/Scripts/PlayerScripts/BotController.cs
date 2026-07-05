/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace Vauxland.FusionBrawler
{
    public class BotController : NetworkBehaviour
    {
        private Transform currentTarget; // bots current selected target
        private PlayerStatsManager targetStatsManager; // the bots target's player stat manager script

        private PlayerManager _playerManager; // the bots player manager script
        private PlayerMovementManager _movementHandler; // the bots movement handler script
        private ProjectileController _projectileController; // legacy fallback
        private MountedWeaponCombatController _mountedWeaponCombat;
        private MonsterWeaponLoadout _weaponLoadout;

        [Header("Combat")]
        public float shootingRange = 15f;
        public float preferredCombatDistance = 10f;
        public bool fireTopWeapon = true;
        public bool fireLeftWeapon = true;
        public bool fireRightWeapon = true;
        [Range(0f, 1f)] public float sideWeaponFireChance = 0.75f;
        public float aimHeightOffset = 1f;
        public float fireDecisionInterval = 0.35f;

        private float nextFireDecisionTime;
        private bool currentFireTop;
        private bool currentFireLeft;
        private bool currentFireRight;

        public float targetSearchRadius = 20f;      // radius within which the bot will search for targets
        public float targetDuration = 10f;          // how long the bot will keep the same target before switching
        private float targetTimer;
        public float directionChangeIntervalMin = 1f; // minimum time to change direction
        public float directionChangeIntervalMax = 3f; // maximum time to change direction
        public float navMeshPathRadius = 5f;        // radius for random NavMesh destination when strafing or moving randomly
        [SerializeField]
        private float avoidanceRadius = 2f; // radius to avoid other players
        [SerializeField]
        private LayerMask playerLayerMask; // layer mask to identify players

        private NavMeshPath navMeshPath;
        private int currentPathIndex;
        private float nextDirectionChangeTime;

        private Vector3 smoothedDirection = Vector3.zero;

        private void Start()
        {
            navMeshPath = new NavMeshPath();
            _playerManager = GetComponent<PlayerManager>();
            _movementHandler = GetComponent<PlayerMovementManager>();
            _projectileController = GetComponent<ProjectileController>();
            _mountedWeaponCombat = GetComponent<MountedWeaponCombatController>();
            _weaponLoadout = GetComponent<MonsterWeaponLoadout>();

            SetNextDirectionChangeTime();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _playerManager._matchManager.GameIsOver) return;

            if (!_playerManager._playerStats.IsAlive) return;

            if (_weaponLoadout != null &&
                !_weaponLoadout.IsReady &&
                _playerManager._playerStats.playerCharacter != null)
            {
                _weaponLoadout.EnsureBotLoadout(
                    _playerManager._playerStats.playerCharacter);
            }

            // if current target is dead, reset target and search for a new one
            if (currentTarget != null && !targetStatsManager.IsAlive && targetStatsManager.PlayerIsReady)
            {
                _playerManager._playerStats.IsShooting = false;
                currentTarget = null;
                targetTimer = 0f;  // reset target timer when target is killed
                SetTarget();
            }

            // if no target, search for one or move randomly
            if (currentTarget == null)
            {
                SetTarget();
                if (currentTarget == null)
                {
                    MoveRandomly();
                }
            }

            // if the bot has a valid target and the match is not over, engage the target
            if (currentTarget != null)
            {
                // update the target timer
                targetTimer += Runner.DeltaTime;

                // check if the target timer has exceeded the allowed duration
                if (targetTimer >= targetDuration)
                {
                    // time to switch targets
                    currentTarget = null;
                    targetTimer = 0f;  // Reset the timer
                    SetTarget();        // Look for a new target
                }

                // get direction and distance to target
                Vector3 direction = GetDirectionToTarget(out float distanceToTarget);

                // handle shooting logic and strafing movement
                ShootAndStrafeAtTarget(direction, distanceToTarget);
            }
        }

        // gets the bot a target
        void SetTarget()
        {
            currentTarget = null;
            targetStatsManager = null;

            // find all objects tagged as Player
            GameObject[] potentialTargets = GameObject.FindGameObjectsWithTag("Player");

            float closestDistance = Mathf.Infinity;
            Transform closestTarget = null;
            var teamDeathmatch = _playerManager._matchManager.matchType == MatchType.TeamDeathMatch;

            foreach (var potentialTarget in potentialTargets)
            {
                var targetStats = potentialTarget.GetComponent<PlayerStatsManager>();
                var targetTeam = potentialTarget.GetComponent<PlayerNetworkController>().TeamInt;
                // it skips itself
                if (potentialTarget.transform == transform)
                    continue;

                if (!targetStats.IsAlive)
                    continue;

                if (teamDeathmatch && targetTeam == _playerManager._playerController.TeamInt)
                    continue;

                // calculate distance to the potential target
                float distance = Vector3.Distance(transform.position, potentialTarget.transform.position);

                // only choose targets within the search radius and find the closest one
                if (distance < targetSearchRadius && distance < closestDistance)
                {
                    closestTarget = potentialTarget.transform;
                    closestDistance = distance;
                }
            }

            // set the closest target as its target if found within the search radius
            if (closestTarget != null)
            {
                currentTarget = closestTarget;
                targetStatsManager = currentTarget.GetComponent<PlayerStatsManager>();
                targetTimer = 0f; // reset target duration timer
            }
            else
            {
                // no target found within range
                currentTarget = null;
                targetStatsManager = null;
            }
        }

        // calculate the direction to the target
        public Vector3 GetDirectionToTarget(out float distanceToTarget)
        {
            distanceToTarget = 0;

            if (currentTarget == null)
                return Vector3.zero;

            distanceToTarget = (currentTarget.position - transform.position).magnitude;

            return (currentTarget.position - transform.position).normalized;
        }

        // moves the bot randomly around the navmesh
        private void MoveRandomly()
        {
            // handle random movement using NavMesh pathfinding
            if (Runner.DeltaTime >= nextDirectionChangeTime || currentPathIndex >= navMeshPath.corners.Length)
            {
                // calculate a random NavMesh destination within a radius
                Vector3 randomDirection = Random.insideUnitSphere * navMeshPathRadius;
                randomDirection += transform.position;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, navMeshPathRadius, NavMesh.AllAreas))
                {
                    NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, navMeshPath);
                    currentPathIndex = 0;
                }

                // set next direction change time
                SetNextDirectionChangeTime();
            }

            // follow the current NavMesh path
            if (navMeshPath.corners.Length > 1 && currentPathIndex < navMeshPath.corners.Length)
            {
                Vector3 nextCorner = navMeshPath.corners[currentPathIndex];
                Vector3 directionToCorner = (nextCorner - transform.position).normalized;

                //_movementHandler.BotMove(directionToCorner * 0.5f);

                // adjust direction to avoid other players better
                Vector3 adjustedDirection = AdjustDirectionToAvoidOthers(directionToCorner);
                _movementHandler.BotMove(adjustedDirection * 0.5f);

                // if the bot is not shooting, rotate it in the direction it is moving
                if (!_playerManager._playerStats.IsShooting)
                {
                    _movementHandler.BotRotate(adjustedDirection);//(directionToCorner);
                }

                // if close enough to the next corner, move to the next path index
                if (Vector3.Distance(transform.position, nextCorner) < 0.5f)
                {
                    currentPathIndex++;
                }
            }
        }

        // moves the bot like a real player where is strafes while shooting because our controls set up make our player move like this when shooting
        private void ShootAndStrafeAtTarget(Vector3 directionToTarget, float distanceToTarget)
        {
            if (targetStatsManager != null && targetStatsManager.IsAlive && currentTarget != null)
            {
                // check if within shooting range
                if (distanceToTarget <= shootingRange)
                {
                    // look at the target
                    // if the bot is not shooting, rotate it in the direction it is moving
                    if (_playerManager._playerStats.IsShooting)
                        _movementHandler.BotRotate(directionToTarget);


                    Vector3 aimPoint =
                        currentTarget.position + Vector3.up * aimHeightOffset;
                    Vector3 aimDirection =
                        (aimPoint - transform.position).normalized;

                    NetworkObject targetObject =
                        currentTarget.GetComponent<NetworkObject>();
                    NetworkId targetId =
                        targetObject != null ? targetObject.Id : default;

                    UpdateFireDecision();

                    if (_mountedWeaponCombat != null)
                    {
                        _mountedWeaponCombat.BotAimAndFire(
                            aimPoint,
                            aimDirection,
                            targetId,
                            currentFireTop,
                            currentFireLeft,
                            currentFireRight);
                    }
                    else if (_projectileController != null)
                    {
                        // Legacy fallback for prefabs not yet upgraded to Phase 3C.
                        PlayerNetworkInputData inputData =
                            new PlayerNetworkInputData
                            {
                                inputPlayerMove = Vector3.zero,
                                inputPlayerDirection = directionToTarget,
                                aimDirection = aimDirection,
                                aimPoint = aimPoint,
                                lockedTargetId = targetId,
                                networkButtons = default
                            };

                        inputData.networkButtons.Set(
                            NetInputButtons.Shoot, true);
                        _projectileController.BotShoot(inputData);
                    }

                    // handle strafing and random movement while shooting
                    HandleStrafingWhileShooting(directionToTarget);
                }
                else
                {
                    // move toward the target
                    MoveTowardTarget(directionToTarget);
                }

                if (distanceToTarget < preferredCombatDistance * 0.65f)
                {
                    _movementHandler.BotMove(-directionToTarget * 0.5f);
                    _movementHandler.BotRotate(directionToTarget);
                }

            }
        }

        // we make the bot feel more player like instead of just going towards it target, in other words make it better
        private void MoveTowardTarget(Vector3 directionToTarget)
        {
            if (Time.time >= nextDirectionChangeTime || currentPathIndex >= navMeshPath.corners.Length)
            {
                // here we decide how much to bias toward the bots target
                float biasFactor = 0.7f; // 0 means random movement, 1 means directly toward target

                // random offset within half of navMeshPathRadius
                Vector3 randomOffset = Random.insideUnitSphere * (navMeshPathRadius * 0.5f);

                // random distance toward the target within navMeshPathRadius
                float randomDistance = Random.Range(0.5f * navMeshPathRadius, navMeshPathRadius);

                Vector3 biasedDirection = (directionToTarget * randomDistance * biasFactor) + (randomOffset * (1 - biasFactor));

                Vector3 desiredPosition = transform.position + biasedDirection;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(desiredPosition, out hit, navMeshPathRadius, NavMesh.AllAreas))
                {
                    NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, navMeshPath);
                    currentPathIndex = 0;
                }

                // set next direction change time
                SetNextDirectionChangeTime();
            }

            // follow the current NavMesh path
            if (navMeshPath.corners.Length > 1 && currentPathIndex < navMeshPath.corners.Length)
            {
                Vector3 nextCorner = navMeshPath.corners[currentPathIndex];
                Vector3 directionToCorner = (nextCorner - transform.position).normalized;

                //_movementHandler.BotMove(directionToCorner * 0.5f);
                // adjust direction to avoid other players better
                Vector3 adjustedDirection = AdjustDirectionToAvoidOthers(directionToCorner);
                _movementHandler.BotMove(adjustedDirection * 0.5f);

                // rotate toward movement direction
                _movementHandler.BotRotate(adjustedDirection);//(directionToCorner);

                // if close enough to the next corner, move to the next path index
                if (Vector3.Distance(transform.position, nextCorner) < 0.5f)
                {
                    currentPathIndex++;
                }
            }
        }

        // here we want the bot to strafe when shooting just like players movement changes when they are shooting
        private void HandleStrafingWhileShooting(Vector3 directionToTarget)
        {
            if (Time.time >= nextDirectionChangeTime || currentPathIndex >= navMeshPath.corners.Length)
            {
                // calculate a random NavMesh destination within a small radius
                Vector3 randomDirection = Random.insideUnitSphere * navMeshPathRadius;
                randomDirection += transform.position;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, navMeshPathRadius, NavMesh.AllAreas))
                {
                    NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, navMeshPath);
                    currentPathIndex = 0;
                }

                // set next direction change time // which changes the direction of the bot
                SetNextDirectionChangeTime();
            }

            // follow the current NavMesh path
            if (navMeshPath.corners.Length > 1 && currentPathIndex < navMeshPath.corners.Length)
            {
                Vector3 nextCorner = navMeshPath.corners[currentPathIndex];
                Vector3 directionToCorner = (nextCorner - transform.position).normalized;

                //_movementHandler.BotMove(directionToCorner * 0.5f);
                // adjust direction to avoid other players better
                Vector3 adjustedDirection = AdjustDirectionToAvoidOthers(directionToCorner);
                _movementHandler.BotMove(adjustedDirection * 0.5f);


                // if the bot is not shooting, rotate it in the direction it is moving// because that's how our players move
                if (!_playerManager._playerStats.IsShooting)
                {
                    _movementHandler.BotRotate(adjustedDirection);//(directionToCorner);
                }

                // if close enough to the next corner, move to the next path index
                if (Vector3.Distance(transform.position, nextCorner) < 0.5f)
                {
                    currentPathIndex++;
                }
            }
        }


        private void UpdateFireDecision()
        {
            if (Time.time < nextFireDecisionTime)
                return;

            nextFireDecisionTime =
                Time.time + Mathf.Max(0.05f, fireDecisionInterval);

            currentFireTop =
                fireTopWeapon &&
                (_mountedWeaponCombat == null ||
                 _mountedWeaponCombat.CanBotFireMount(
                     WeaponMountType.Top));

            currentFireLeft =
                fireLeftWeapon &&
                Random.value <= sideWeaponFireChance &&
                (_mountedWeaponCombat == null ||
                 _mountedWeaponCombat.CanBotFireMount(
                     WeaponMountType.Left));

            currentFireRight =
                fireRightWeapon &&
                Random.value <= sideWeaponFireChance &&
                (_mountedWeaponCombat == null ||
                 _mountedWeaponCombat.CanBotFireMount(
                     WeaponMountType.Right));
        }

        private void SetNextDirectionChangeTime()
        {
            // set a random time interval for the bot's next direction change
            nextDirectionChangeTime = Time.time + Random.Range(directionChangeIntervalMin, directionChangeIntervalMax);
        }

        // method to adjust the bot's movement direction to avoid other players, keeps them more realistic
        private Vector3 AdjustDirectionToAvoidOthers(Vector3 originalDirection)
        {
            Vector3 adjustedDirection = originalDirection.normalized;

            // get nearby colliders within avoidance radius
            Collider[] colliders = Physics.OverlapSphere(transform.position, avoidanceRadius, playerLayerMask);

            Vector3 repulsion = Vector3.zero;

            foreach (var collider in colliders)
            {
                // skip self
                if (collider.transform == transform)
                    continue;

                // ignore dead players
                var otherPlayerStats = collider.GetComponent<PlayerStatsManager>();
                if (otherPlayerStats != null && !otherPlayerStats.IsAlive)
                    continue;

                Vector3 toOther = collider.transform.position - transform.position;
                float distance = toOther.magnitude;

                if (distance > 0.01f)
                {
                    Vector3 repulsionDir = -toOther.normalized / distance;
                    repulsion += repulsionDir;
                }
            }

            if (repulsion != Vector3.zero)
            {
                // adjust original direction with avoidance
                float avoidanceStrength = 0.3f; // you can change this value between 0 and 1
                Vector3 avoidanceDirection = (originalDirection + repulsion.normalized).normalized;
                adjustedDirection = Vector3.Lerp(originalDirection, avoidanceDirection, avoidanceStrength).normalized;
            }

            // smooth the adjusted direction to prevent jittering
            float smoothingFactor = 0.2f; // you can change this value between 0 and 1
            smoothedDirection = Vector3.Slerp(smoothedDirection, adjustedDirection, smoothingFactor).normalized;

            return smoothedDirection;
        }
    }
}

