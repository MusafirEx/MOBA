using Fusion;
using UnityEngine;

public class RespawnManagerNetwork : NetworkBehaviour
{
    public Transform[] teamASpawnPoints;
    public Transform[] teamBSpawnPoints;

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        MonsterStatsNetwork[] allStats = FindObjectsByType<MonsterStatsNetwork>(FindObjectsSortMode.None);
        foreach (var stats in allStats)
        {
            if (stats != null && stats.IsDead && stats.RespawnTimer.Expired(Runner))
            {
                Transform spawn = GetSpawn(stats.TeamId);
                Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
                Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;
                stats.RespawnAt(pos, rot);
            }
        }
    }

    private Transform GetSpawn(int teamId)
    {
        Transform[] list = teamId == 0 ? teamASpawnPoints : teamBSpawnPoints;
        if (list == null || list.Length == 0) return null;
        return list[Random.Range(0, list.Length)];
    }
}
