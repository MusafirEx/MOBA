using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkProjectile : NetworkBehaviour
{
    public float lifeTime = 4f;
    public LayerMask hitMask = ~0;

    [Networked] private Vector3 Direction { get; set; }
    [Networked] private float Speed { get; set; }
    [Networked] private float Damage { get; set; }
    [Networked] private int OwnerTeamId { get; set; }
    [Networked] private TickTimer LifeTimer { get; set; }

    public void Init(Vector3 direction, float speed, float damage, int ownerTeamId)
    {
        if (!Object.HasStateAuthority) return;
        Direction = direction.normalized;
        Speed = speed;
        Damage = damage;
        OwnerTeamId = ownerTeamId;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        Vector3 start = transform.position;
        Vector3 end = start + Direction * Speed * Runner.DeltaTime;

        if (Physics.Linecast(start, end, out RaycastHit hit, hitMask, QueryTriggerInteraction.Ignore))
        {
            var stats = hit.collider.GetComponentInParent<MonsterStatsNetwork>();
            if (stats != null)
                stats.ApplyDamage(Damage, OwnerTeamId);

            RPC_PlayImpactFX(hit.point);
            Runner.Despawn(Object);
            return;
        }

        transform.position = end;

        if (LifeTimer.Expired(Runner))
            Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayImpactFX(Vector3 point)
    {
        // Add impact VFX/audio here.
    }
}
