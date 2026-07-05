using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class WeaponModelComponent : MonoBehaviour
    {
        [Header("Effects / Launch")]
        public ParticleSystem muzzleEffect;
        public Transform projectileLauncher;

        [Header("Optional Aim Pivots")]
        public Transform yawPivot;
        public Transform pitchPivot;

        private Quaternion initialYawRotation;
        private Quaternion initialPitchRotation;
        private bool initialized;

        private void Awake()
        {
            CacheInitialRotations();
        }

        public void CacheInitialRotations()
        {
            if (yawPivot != null)
                initialYawRotation = yawPivot.localRotation;
            if (pitchPivot != null)
                initialPitchRotation = pitchPivot.localRotation;
            initialized = true;
        }

        public void AimAt(Vector3 worldPoint, WeaponConfig config, float deltaTime)
        {
            if (config == null || config.aimMode == WeaponAimMode.Fixed ||
                config.aimMode == WeaponAimMode.CameraRaycast ||
                config.aimMode == WeaponAimMode.LockOnHoming)
                return;

            if (!initialized)
                CacheInitialRotations();

            if (config.aimMode == WeaponAimMode.YawAndPitch && yawPivot != null)
                RotateYaw(worldPoint, config, deltaTime);

            if ((config.aimMode == WeaponAimMode.PitchOnly || config.aimMode == WeaponAimMode.YawAndPitch) && pitchPivot != null)
                RotatePitch(worldPoint, config, deltaTime);
        }

        private void RotateYaw(Vector3 worldPoint, WeaponConfig config, float deltaTime)
        {
            Transform parent = yawPivot.parent;
            Vector3 localDirection = parent != null
                ? parent.InverseTransformDirection(worldPoint - yawPivot.position)
                : worldPoint - yawPivot.position;

            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, config.minimumYaw, config.maximumYaw);
            Quaternion target = initialYawRotation * Quaternion.Euler(0f, yaw, 0f);
            yawPivot.localRotation = Quaternion.RotateTowards(yawPivot.localRotation, target, config.yawSpeed * deltaTime);
        }

        private void RotatePitch(Vector3 worldPoint, WeaponConfig config, float deltaTime)
        {
            Transform parent = pitchPivot.parent;
            Vector3 localDirection = parent != null
                ? parent.InverseTransformDirection(worldPoint - pitchPivot.position)
                : worldPoint - pitchPivot.position;

            float horizontal = new Vector2(localDirection.x, localDirection.z).magnitude;
            float pitch = -Mathf.Atan2(localDirection.y, horizontal) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, config.minimumPitch, config.maximumPitch);
            Quaternion target = initialPitchRotation * Quaternion.Euler(pitch, 0f, 0f);
            pitchPivot.localRotation = Quaternion.RotateTowards(pitchPivot.localRotation, target, config.pitchSpeed * deltaTime);
        }

        public Vector3 GetLaunchPosition(float offset)
        {
            Transform launcher = projectileLauncher != null ? projectileLauncher : transform;
            return launcher.position + launcher.forward * offset;
        }

        public Vector3 GetMuzzleForward()
        {
            Transform launcher = projectileLauncher != null ? projectileLauncher : transform;
            return launcher.forward;
        }
    }
}
