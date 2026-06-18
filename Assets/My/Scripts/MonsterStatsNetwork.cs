using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MonsterStatsNetwork : NetworkBehaviour
{
    public float maxHP = 1000f;

    [Networked] public int TeamId { get; set; }
    [Networked] public float HP { get; set; }
    [Networked] public NetworkBool IsDead { get; set; }
    [Networked] public TickTimer RespawnTimer { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority && HP <= 0f)
            HP = maxHP;
    }

    public void InitOnServer(int teamId)
    {
        if (!Object.HasStateAuthority) return;
        TeamId = teamId;
        HP = maxHP;
        IsDead = false;
    }

    public void ApplyDamage(float amount, int attackerTeamId)
    {
        if (!Object.HasStateAuthority || IsDead) return;
        if (attackerTeamId == TeamId) return; // no friendly fire for prototype

        HP = Mathf.Max(0f, HP - amount);
        if (HP <= 0f)
            Die();
    }

    private void Die()
    {
        IsDead = true;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, 5f);
        RPC_PlayDeathFX();
    }

    public void RespawnAt(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;
        transform.SetPositionAndRotation(position, rotation);
        HP = maxHP;
        IsDead = false;
        RespawnTimer = TickTimer.None;
        RPC_PlayRespawnFX();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDeathFX()
    {
        Debug.Log($"{name} died");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayRespawnFX()
    {
        Debug.Log($"{name} respawned");
    }
}
