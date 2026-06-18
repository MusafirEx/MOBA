using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MonsterControllerFusion : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float dashSpeed = 16f;
    public float turnSpeed = 240f;
    public float acceleration = 20f;
    public float gravity = -25f;

    [Header("References")]
    public CharacterController characterController;
    public Transform visualRoot;
    public MonsterWeaponManager weaponManager;

    [Networked] public Vector3 NetworkVelocity { get; set; }
    [Networked] public NetworkBool IsDashing { get; set; }

    private MonsterStatsNetwork _stats;
    private Vector3 _verticalVelocity;

    public override void Spawned()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (weaponManager == null) weaponManager = GetComponent<MonsterWeaponManager>();
        _stats = GetComponent<MonsterStatsNetwork>();

        if (HasInputAuthority)
        {
            var cam = FindFirstObjectByType<MonsterCameraController>();
            if (cam != null) cam.SetTarget(transform);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_stats != null && _stats.IsDead) return;
        if (!GetInput(out MonsterInputData input)) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 desired = (forward * input.Move.y + right * input.Move.x);
        desired = Vector3.ClampMagnitude(desired, 1f);

        IsDashing = input.IsDown(MonsterButtons.Dash);
        float speed = IsDashing ? dashSpeed : moveSpeed;
        Vector3 targetVelocity = desired * speed;
        NetworkVelocity = Vector3.MoveTowards(NetworkVelocity, targetVelocity, acceleration * Runner.DeltaTime);

        RotateTowardAim(input.AimWorldPoint);

        if (characterController != null)
        {
            if (characterController.isGrounded && _verticalVelocity.y < 0f)
                _verticalVelocity.y = -2f;

            _verticalVelocity.y += gravity * Runner.DeltaTime;
            Vector3 motion = (NetworkVelocity + _verticalVelocity) * Runner.DeltaTime;
            characterController.Move(motion);
        }
        else
        {
            transform.position += NetworkVelocity * Runner.DeltaTime;
        }

        if (weaponManager != null)
            weaponManager.ProcessInput(input);
    }

    private void RotateTowardAim(Vector3 aimPoint)
    {
        Vector3 toAim = aimPoint - transform.position;
        toAim.y = 0f;
        if (toAim.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(toAim.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Runner.DeltaTime);
    }
}
