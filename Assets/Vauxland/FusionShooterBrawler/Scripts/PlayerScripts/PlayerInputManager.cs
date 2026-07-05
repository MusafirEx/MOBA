/*
 * Modified for third-person camera-relative movement, centre-screen aiming and target lock.
 */
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vauxland.FusionBrawler
{
    public class PlayerInputManager : MonoBehaviour
    {
        [HideInInspector] public CameraFollow cameraFollow;

        [Header("Third Person Input")]
        [SerializeField] private bool allowJump = true;
        [SerializeField] private KeyCode lockOnKey = KeyCode.R;
        [SerializeField] private KeyCode fireLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode fireRightKey = KeyCode.E;

        public Vector3 cameraForward;
        protected Vector3 cameraRight;

        protected Vector3 inputPlayerMove;
        protected Vector3 inputPlayerDirection;
        protected Vector3 mousePosition;
        protected Vector2 inputLook;
        protected Vector3 aimPoint;
        protected Vector3 aimDirection;

        protected bool inputPlayerShoot;
        protected bool inputPlayerJump;
        protected bool lockTogglePressed;

        protected PlayerManager _playerManager;
        public Transform _cacheTransform { get; private set; }

        private ThirdPersonAimController aimController;
        private TargetLockController targetLockController;

        [HideInInspector] public bool isSet;

        public void SetInputComponents(Transform transform, CameraFollow camera, PlayerManager playerManager)
        {
            _cacheTransform = transform;
            cameraFollow = camera;
            _playerManager = playerManager;

            aimController = GetComponent<ThirdPersonAimController>();
            if (aimController == null)
                aimController = gameObject.AddComponent<ThirdPersonAimController>();
            aimController.Initialize(cameraFollow, transform);

            targetLockController = GetComponent<TargetLockController>();
            if (targetLockController == null)
                targetLockController = gameObject.AddComponent<TargetLockController>();
            targetLockController.Initialize(playerManager, cameraFollow);
        }

        protected void Update()
        {
            UpdatePlayerInput();
        }

        protected virtual void UpdatePlayerInput()
        {
            if (!isSet || _playerManager == null || !_playerManager.Object.HasInputAuthority)
                return;

            bool isMobileInput = Application.isMobilePlatform;
#if UNITY_EDITOR
            if (PlayerGameData.PlayerData != null)
                isMobileInput = PlayerGameData.PlayerData.testMobileControls;
#endif

            UpdateCameraVectors();
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            inputPlayerMove = Vector3.zero;
            inputLook = Vector2.zero;
            lockTogglePressed = false;

            if (!isMobileInput)
            {
                float horizontal = Input.GetAxisRaw("Horizontal");
                float vertical = Input.GetAxisRaw("Vertical");

                inputPlayerMove = cameraForward * vertical + cameraRight * horizontal;
                if (inputPlayerMove.sqrMagnitude > 1f)
                    inputPlayerMove.Normalize();

                inputLook = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                inputPlayerShoot = !pointerOverUI && Input.GetButton("Fire1");
                inputPlayerJump = allowJump && Input.GetButton("Jump");

                if (!pointerOverUI && Input.GetKeyDown(lockOnKey))
                {
                    lockTogglePressed = true;
                    targetLockController?.ToggleLock();
                }
            }
            else if (_playerManager._mobileControls != null)
            {
                _playerManager._mobileControls.useMobileControls = true;
            }

            mousePosition = Input.mousePosition;

            // Unit body rotation remains based on movement, not camera aim.
            inputPlayerDirection = inputPlayerMove.sqrMagnitude > 0.0001f
                ? inputPlayerMove.normalized
                : Vector3.zero;

            UpdateAimData();
        }

        private void UpdateAimData()
        {
            if (targetLockController != null && targetLockController.HasTarget)
            {
                PlayerNetworkController target = targetLockController.CurrentTarget;
                Collider targetCollider = target.GetComponentInChildren<Collider>();
                aimPoint = targetCollider != null
                    ? targetCollider.bounds.center
                    : target.transform.position + Vector3.up;
            }
            else if (aimController != null)
            {
                aimController.RefreshAim();
                aimPoint = aimController.AimPoint;
            }
            else
            {
                aimPoint = _cacheTransform.position + cameraForward * 100f;
            }

            aimDirection = (aimPoint - _cacheTransform.position).normalized;
        }

        private void UpdateCameraVectors()
        {
            if (cameraFollow != null && cameraFollow.CacheCameraTransform != null)
            {
                cameraForward = cameraFollow.FlatForward;
                cameraRight = cameraFollow.FlatRight;
            }
            else
            {
                cameraForward = Vector3.forward;
                cameraRight = Vector3.right;
            }
        }

        public void SetMovementInput(Vector3 move)
        {
            inputPlayerMove = cameraForward * move.z + cameraRight * move.x;
            if (inputPlayerMove.sqrMagnitude > 1f)
                inputPlayerMove.Normalize();
        }

        public void SetAimInput(Vector3 aim)
        {
            if (aim.sqrMagnitude <= 0.0001f)
                return;

            aim.y = 0f;
            aimDirection = aim.normalized;
            aimPoint = _cacheTransform.position + aimDirection * 100f;
        }

        public void SetShootInput(bool shoot) => inputPlayerShoot = shoot;
        public void SetJumpInput(bool jump) => inputPlayerJump = jump;

        public void ToggleTargetLock()
        {
            lockTogglePressed = true;
            targetLockController?.ToggleLock();
        }

        public PlayerNetworkInputData SetPlayerNetworkInput()
        {
            NetworkId targetId = targetLockController != null
                ? targetLockController.CurrentTargetId
                : default;

            PlayerNetworkInputData inputData = new PlayerNetworkInputData
            {
                inputPlayerMove = inputPlayerMove,
                inputPlayerDirection = inputPlayerDirection,
                inputPlayerShoot = inputPlayerShoot,
                inputPlayerJump = inputPlayerJump,
                mousePosition = mousePosition,
                inputLook = inputLook,
                aimPoint = aimPoint,
                aimDirection = aimDirection,
                lockedTargetId = targetId
            };

            bool usingMobile = _playerManager != null &&
                               _playerManager._mobileControls != null &&
                               _playerManager._mobileControls.useMobileControls;

            bool shootPressed = usingMobile
                ? _playerManager._mobileControls.isShooting
                : Input.GetButton("Fire1");

            inputData.networkButtons.Set(NetInputButtons.Shoot, shootPressed);
            inputData.networkButtons.Set(NetInputButtons.FireLeft, !usingMobile && Input.GetKey(fireLeftKey));
            inputData.networkButtons.Set(NetInputButtons.FireRight, !usingMobile && Input.GetKey(fireRightKey));
            inputData.networkButtons.Set(NetInputButtons.Jump, inputPlayerJump);
            inputData.networkButtons.Set(NetInputButtons.Boost, Input.GetKey(KeyCode.LeftShift));
            inputData.networkButtons.Set(NetInputButtons.LockOn, lockTogglePressed);

            inputPlayerJump = false;
            lockTogglePressed = false;

            return inputData;
        }
    }
}
