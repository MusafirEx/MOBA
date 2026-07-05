using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    /// <summary>
    /// Local target selection. In TeamDeathMatch it rejects same-team targets.
    /// In DeathMatch it only checks self, alive state, visibility and distance.
    /// </summary>
    public class TargetLockController : MonoBehaviour
    {
        [Header("Lock Settings")]
        [SerializeField] private float lockDistance = 60f;
        [SerializeField] private float unlockDistance = 75f;
        [SerializeField] private float maximumScreenAngle = 18f;
        [SerializeField] private LayerMask visibilityLayers = ~0;
        [SerializeField] private bool requireLineOfSight = true;

        private PlayerManager playerManager;
        private CameraFollow cameraFollow;
        private PlayerNetworkController currentTarget;

        public PlayerNetworkController CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;
        public NetworkId CurrentTargetId => currentTarget != null && currentTarget.Object != null
            ? currentTarget.Object.Id
            : default;

        public void Initialize(PlayerManager manager, CameraFollow follow)
        {
            playerManager = manager;
            cameraFollow = follow;
        }

        public void ToggleLock()
        {
            if (currentTarget != null)
            {
                ClearLock();
                return;
            }

            currentTarget = FindBestTarget();
        }

        public void ClearLock()
        {
            currentTarget = null;
        }

        private void Update()
        {
            if (currentTarget == null)
                return;

            if (!IsValidTarget(currentTarget, unlockDistance))
                ClearLock();
        }

        private PlayerNetworkController FindBestTarget()
        {
            Camera camera = cameraFollow != null ? cameraFollow.CacheCamera : Camera.main;
            if (camera == null || playerManager == null)
                return null;

            PlayerNetworkController best = null;
            float bestScore = float.MaxValue;
            Vector3 cameraForward = camera.transform.forward;

            foreach (PlayerNetworkController candidate in PlayerNetworkController.AllPlayers)
            {
                if (!IsValidTarget(candidate, lockDistance))
                    continue;

                Vector3 targetPoint = GetTargetPoint(candidate);
                Vector3 toTarget = targetPoint - camera.transform.position;
                float angle = Vector3.Angle(cameraForward, toTarget);
                if (angle > maximumScreenAngle)
                    continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                float score = angle * 4f + distance * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        public bool IsValidTarget(PlayerNetworkController candidate, float allowedDistance)
        {
            if (candidate == null || playerManager == null)
                return false;

            if (candidate == playerManager._playerController)
                return false;

            PlayerStatsManager stats = candidate.GetComponent<PlayerStatsManager>();
            if (stats == null || !stats.IsAlive || !stats.PlayerIsReady)
                return false;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance > allowedDistance)
                return false;

            GameMatchManager match = playerManager._matchManager;
            if (match != null && match.matchType == MatchType.TeamDeathMatch)
            {
                if (candidate.TeamInt == playerManager._playerController.TeamInt)
                    return false;
            }

            if (requireLineOfSight && !HasLineOfSight(candidate))
                return false;

            return true;
        }

        private bool HasLineOfSight(PlayerNetworkController candidate)
        {
            Camera camera = cameraFollow != null ? cameraFollow.CacheCamera : Camera.main;
            if (camera == null)
                return true;

            Vector3 origin = camera.transform.position;
            Vector3 targetPoint = GetTargetPoint(candidate);
            Vector3 direction = targetPoint - origin;

            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                    direction.magnitude, visibilityLayers, QueryTriggerInteraction.Ignore))
                return true;

            return hit.transform.root == candidate.transform.root;
        }

        private static Vector3 GetTargetPoint(PlayerNetworkController candidate)
        {
            Collider collider = candidate.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : candidate.transform.position + Vector3.up;
        }
    }
}
