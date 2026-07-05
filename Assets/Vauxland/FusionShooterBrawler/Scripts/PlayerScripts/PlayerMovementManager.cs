/*
 * Modified for heavier third-person monster/mech movement.
 */
using UnityEngine;
using Fusion;

namespace Vauxland.FusionBrawler
{
    public class PlayerMovementManager : NetworkBehaviour
    {
        private PlayerInputManager _playerInput;
        protected PlayerManager _playerManager;

        public Camera targetCamera;
        public CameraFollow cameraFollow;
        public Transform _cacheTransform;
        private NetworkCharacterController _networkCharacterController;

        [Header("Monster Movement")]
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float braking = 24f;
        [SerializeField] private float turnSpeed = 7f;
        [SerializeField, Range(0.1f, 1f)] private float reverseSpeedMultiplier = 0.65f;
        [SerializeField, Range(0.1f, 1f)] private float strafeSpeedMultiplier = 0.8f;

        [Header("Boost Prototype")]
        [SerializeField] private float boostSpeedMultiplier = 1.5f;

        [Header("Rotation Behaviour")]
        [SerializeField] private bool faceMovementDirection = true;

        private Vector3 playerMovement;
        private Vector3 playerRotation;
        private bool boostHeld;

        protected Vector3? previousPosition;
        public Vector3 currentVelocity;

        public override void Spawned()
        {
            _cacheTransform = transform;
            _playerManager = GetComponent<PlayerManager>();
            _networkCharacterController = GetComponent<NetworkCharacterController>();

            if (_networkCharacterController == null)
            {
                Debug.LogError("PlayerMovementManager requires NetworkCharacterController.", this);
                return;
            }

            _networkCharacterController.rotationSpeed = 0f;

            if (Object.HasInputAuthority)
            {
                cameraFollow = FindObjectOfType<CameraFollow>();
                _playerInput = GetComponent<PlayerInputManager>();

                if (_playerInput != null)
                    _playerInput.SetInputComponents(transform, cameraFollow, _playerManager);
            }
        }

        public override void Render()
        {
            if (_cacheTransform == null)
                return;

            if (!previousPosition.HasValue)
                previousPosition = _cacheTransform.position;

            Vector3 currentMove = _cacheTransform.position - previousPosition.Value;
            float delta = Mathf.Max(0.0001f, Runner.DeltaTime);
            currentVelocity = currentMove / delta;
            previousPosition = _cacheTransform.position;
        }

        public override void FixedUpdateNetwork()
        {
            if (_playerManager == null || _playerManager._playerStats == null)
                return;

            if (!_playerManager._playerStats.AcceptPlayerInput)
                return;

            playerMovement = Vector3.zero;
            playerRotation = Vector3.zero;
            boostHeld = false;

            if (Runner.TryGetInputForPlayer<PlayerNetworkInputData>(Object.InputAuthority, out var inputData))
            {
                playerMovement = inputData.inputPlayerMove;
                playerRotation = inputData.inputPlayerDirection;
                boostHeld = inputData.networkButtons.IsSet(NetInputButtons.Boost);
            }

            UpdateMovements();
        }

        public Vector3 GetCurrentVelocity()
        {
            return currentVelocity;
        }

        protected virtual void UpdateMovements()
        {
            if (_playerManager._playerController.IsBot)
                return;

            MovePlayer(playerMovement, boostHeld);
            RotatePlayer(playerMovement);
        }

        public void BotMove(Vector3 direction)
        {
            MovePlayer(direction, false);
        }

        protected void MovePlayer(Vector3 direction, bool boosting)
        {
            if (_networkCharacterController == null)
                return;

            direction.y = 0f;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            _networkCharacterController.acceleration = acceleration;
            _networkCharacterController.braking = braking;
            _networkCharacterController.rotationSpeed = 0f;

            float baseSpeed = Mathf.Max(0f, _playerManager._playerStats.MoveSpeed);
            float directionalMultiplier = GetDirectionalSpeedMultiplier(direction);
            float boostMultiplier = boosting ? boostSpeedMultiplier : 1f;

            if (_playerManager._playerController.IsBot)
                baseSpeed = Mathf.Max(0f, baseSpeed - 1f);

            _networkCharacterController.maxSpeed = baseSpeed * directionalMultiplier * boostMultiplier;
            _networkCharacterController.Move(direction);
        }

        private float GetDirectionalSpeedMultiplier(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 0.0001f)
                return 1f;

            Vector3 localDirection = _cacheTransform.InverseTransformDirection(worldDirection.normalized);

            if (localDirection.z < -0.25f)
                return reverseSpeedMultiplier;

            if (Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.z))
                return strafeSpeedMultiplier;

            return 1f;
        }

        public void BotRotate(Vector3 direction)
        {
            RotateTowards(direction, 12f);
        }

        protected void RotatePlayer(Vector3 movementDirection)
        {
            if (!faceMovementDirection)
                return;

            // W faces camera-forward, S faces camera-backward,
            // A faces camera-left, and D faces camera-right.
            // Camera rotation itself does not rotate the player.
            RotateTowards(movementDirection, turnSpeed);
        }

        private void RotateTowards(Vector3 direction, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            float interpolation = 1f - Mathf.Exp(-speed * Runner.DeltaTime);
            _cacheTransform.rotation = Quaternion.Slerp(_cacheTransform.rotation, targetRotation, interpolation);
        }
    }
}
