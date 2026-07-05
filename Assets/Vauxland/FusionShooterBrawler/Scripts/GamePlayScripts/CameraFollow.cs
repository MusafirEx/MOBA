/*
 * Modified for a third-person combat camera.
 * Based on VAUXLAND Fusion Shooter Brawler CameraFollow.
 */
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public enum CameraMode
    {
        TopDown,
        ThirdPerson
    }

    public class CameraFollow : MonoBehaviour
    {
        [Header("Mode")]
        public CameraMode cameraMode = CameraMode.ThirdPerson;

        [Header("References")]
        public Camera targetCamera;
        public Transform target;
        public GameObject targetFollower;

        [Header("Shared Follow")]
        public Vector3 targetOffset = new Vector3(0f, 2.5f, 0f);
        public float followSmoothTime = 0.05f;

        [Header("Top Down")]
        public float xRotation = 45f;
        public float yRotation = 45f;
        public float zoomDistance = 10f;

        [Header("Third Person Rotation")]
        public float mouseSensitivity = 3f;
        public float yaw = 0f;
        public float pitch = 15f;
        public float minPitch = -10f;
        public float maxPitch = 55f;
        public bool lockCursor = true;

        [Header("Third Person Position")]
        public float thirdPersonDistance = 8f;
        public Vector3 shoulderOffset = new Vector3(0.8f, 0f, 0f);
        public float cameraPositionSmooth = 14f;

        [Header("Camera Collision")]
        public bool useCameraCollision = true;
        public LayerMask collisionLayers = ~0;
        public float collisionRadius = 0.25f;
        public float collisionPadding = 0.15f;
        public float minimumDistance = 1f;

        [Header("Camera Shake")]
        public bool useCameraShake = true;
        public float shakeDuration = 0.1f;
        public float shakeMagnitude = 0.2f;

        private Vector3 shakeOffset;
        private float shakeTimeRemaining;
        private float noiseSeedX;
        private float noiseSeedY;
        private Vector3 followVelocity;

        public Transform CacheCameraTransform { get; private set; }
        public Camera CacheCamera => targetCamera;

        public Vector3 FlatForward
        {
            get
            {
                Vector3 forward = CacheCameraTransform != null ? CacheCameraTransform.forward : transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }

        public Vector3 FlatRight
        {
            get
            {
                Vector3 right = CacheCameraTransform != null ? CacheCameraTransform.right : transform.right;
                right.y = 0f;
                return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            }
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            if (targetCamera == null)
            {
                Debug.LogError("CameraFollow requires a Camera component or Target Camera reference.", this);
                enabled = false;
                return;
            }

            CacheCameraTransform = targetCamera.transform;

            if (targetFollower == null)
            {
                targetFollower = new GameObject("CameraTarget");
                targetFollower.transform.position = target != null ? target.position : Vector3.zero;
            }
        }

        private void Start()
        {
            if (cameraMode == CameraMode.ThirdPerson && lockCursor)
                SetCursorLocked(true);
        }

        private void OnDestroy()
        {
            if (targetFollower != null)
                Destroy(targetFollower);
        }

        private void OnDisable()
        {
            if (lockCursor)
                SetCursorLocked(false);
        }

        public void StartShake()
        {
            shakeTimeRemaining = shakeDuration;
            noiseSeedX = Random.Range(0f, 100f);
            noiseSeedY = Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (PlayerNetworkController.LocalPlayer != null)
            {
                PlayerMovementManager movement = PlayerNetworkController.LocalPlayer.GetComponent<PlayerMovementManager>();
                if (movement != null)
                    target = movement._cacheTransform;
            }

            if (target == null)
                return;

            if (cameraMode == CameraMode.TopDown)
                UpdateTopDownCamera();
            else
                UpdateThirdPersonCamera();
        }

        private void UpdateTopDownCamera()
        {
            targetFollower.transform.position = target.position;
            targetFollower.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

            UpdateCameraShake();

            Vector3 cameraPosition = targetFollower.transform.position
                                     - targetFollower.transform.forward * zoomDistance
                                     + targetOffset
                                     + shakeOffset;

            targetCamera.transform.position = cameraPosition;
            targetCamera.transform.LookAt(targetFollower.transform.position);

            if (targetCamera.orthographic)
                targetCamera.orthographicSize = zoomDistance / 2f;
        }

        private void UpdateThirdPersonCamera()
        {
            if (lockCursor)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    SetCursorLocked(false);
                else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
                    SetCursorLocked(true);
            }

            if (Cursor.lockState == CursorLockMode.Locked || !lockCursor)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Vector3 desiredPivot = target.position + targetOffset;
            targetFollower.transform.position = Vector3.SmoothDamp(
                targetFollower.transform.position,
                desiredPivot,
                ref followVelocity,
                followSmoothTime);

            Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
            targetFollower.transform.rotation = cameraRotation;

            UpdateCameraShake();

            Vector3 shoulderWorldOffset = cameraRotation * shoulderOffset;
            Vector3 pivot = targetFollower.transform.position + shoulderWorldOffset;
            Vector3 desiredDirection = -(cameraRotation * Vector3.forward);
            float desiredDistance = thirdPersonDistance;

            if (useCameraCollision)
            {
                if (Physics.SphereCast(
                        pivot,
                        collisionRadius,
                        desiredDirection,
                        out RaycastHit hit,
                        thirdPersonDistance,
                        collisionLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    desiredDistance = Mathf.Max(minimumDistance, hit.distance - collisionPadding);
                }
            }

            Vector3 desiredPosition = pivot + desiredDirection * desiredDistance + shakeOffset;
            float interpolation = 1f - Mathf.Exp(-cameraPositionSmooth * Time.deltaTime);

            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                desiredPosition,
                interpolation);

            targetCamera.transform.rotation = cameraRotation;

            if (targetCamera.orthographic)
                targetCamera.orthographic = false;
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void UpdateCameraShake()
        {
            if (shakeTimeRemaining <= 0f)
            {
                shakeOffset = Vector3.zero;
                return;
            }

            shakeTimeRemaining -= Time.deltaTime;
            float normalizedTime = 1f - Mathf.Clamp01(shakeTimeRemaining / Mathf.Max(0.0001f, shakeDuration));
            float dampingFactor = Mathf.Lerp(shakeMagnitude, 0f, normalizedTime);

            noiseSeedX += Time.deltaTime * 10f;
            noiseSeedY += Time.deltaTime * 10f;

            float shakeX = (Mathf.PerlinNoise(noiseSeedX, 0f) - 0.5f) * 2f * dampingFactor;
            float shakeY = (Mathf.PerlinNoise(0f, noiseSeedY) - 0.5f) * 2f * dampingFactor;

            shakeOffset = targetCamera.transform.right * shakeX + targetCamera.transform.up * shakeY;
        }
    }
}
