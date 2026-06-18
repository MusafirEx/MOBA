using Fusion;
using UnityEngine;

public class ProjectileWeapon : NetworkBehaviour
{
    public NetworkProjectile projectilePrefab;
    public Transform muzzle;
    public float damage = 80f;
    public float projectileSpeed = 35f;
    public float fireCooldown = 0.25f;

    [Networked] private TickTimer Cooldown { get; set; }

    private MonsterStatsNetwork _ownerStats;

    public override void Spawned()
    {
        _ownerStats = GetComponentInParent<MonsterStatsNetwork>();
    }

    public void TryFire(Vector3 aimWorldPoint)
    {
        if (!Object.HasStateAuthority) return;
        if (Cooldown.IsRunning) return;
        if (projectilePrefab == null || muzzle == null) return;

        Vector3 direction = (aimWorldPoint - muzzle.position).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = muzzle.forward;

        NetworkProjectile projectile = Runner.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation(direction));
        projectile.Init(direction, projectileSpeed, damage, _ownerStats != null ? _ownerStats.TeamId : -1);

        Cooldown = TickTimer.CreateFromSeconds(Runner, fireCooldown);
        RPC_PlayFireFX();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFireFX()
    {
        // Add muzzle flash/audio here.
    }
}
