using UnityEngine;

namespace Vauxland.FusionBrawler
{
    /// <summary>
    /// Local-only camera aiming. Calculates the world point under the centre crosshair.
    /// Attach this to the player prefab.
    /// </summary>
    public class ThirdPersonAimController : MonoBehaviour
    {
        [Header("Aim Ray")]
        [SerializeField] private LayerMask aimLayers = ~0;
        [SerializeField] private float maximumAimDistance = 250f;
        [SerializeField] private float minimumAimDistance = 5f;
        [SerializeField] private bool ignoreTriggerColliders = true;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRay;

        private CameraFollow cameraFollow;
        private Transform ownerRoot;

        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        public void Initialize(CameraFollow follow, Transform owner)
        {
            cameraFollow = follow;
            ownerRoot = owner;
            RefreshAim();
        }

        private void LateUpdate()
        {
            RefreshAim();
        }

        public void RefreshAim()
        {
            Camera camera = cameraFollow != null ? cameraFollow.CacheCamera : Camera.main;
            if (camera == null)
                return;

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            QueryTriggerInteraction triggerMode = ignoreTriggerColliders
                ? QueryTriggerInteraction.Ignore
                : QueryTriggerInteraction.Collide;

            Vector3 fallbackPoint = ray.origin + ray.direction * maximumAimDistance;
            AimPoint = fallbackPoint;

            RaycastHit[] hits = Physics.RaycastAll(ray, maximumAimDistance, aimLayers, triggerMode);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit hit in hits)
                {
                    if (ownerRoot != null && hit.transform.root == ownerRoot)
                        continue;

                    AimPoint = hit.point;
                    break;
                }
            }

            Vector3 origin = ownerRoot != null ? ownerRoot.position : ray.origin;
            Vector3 direction = AimPoint - origin;
            if (direction.magnitude < minimumAimDistance)
                AimPoint = origin + ray.direction * minimumAimDistance;

            AimDirection = (AimPoint - origin).normalized;

            if (drawDebugRay)
                Debug.DrawLine(ray.origin, AimPoint, Color.cyan);
        }
    }
}
