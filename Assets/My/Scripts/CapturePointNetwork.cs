using Fusion;
using UnityEngine;

public class CapturePointNetwork : NetworkBehaviour
{
    public string pointName = "A";
    public float captureSpeed = 25f;
    public float scorePerSecond = 1f;

    [Networked] public int OwnerTeamId { get; set; } // -1 neutral, 0 Team A, 1 Team B
    [Networked] public int CapturingTeamId { get; set; }
    [Networked] public float CaptureProgress { get; set; }

    private int _team0Inside;
    private int _team1Inside;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            OwnerTeamId = -1;
            CapturingTeamId = -1;
            CaptureProgress = 0f;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        int activeTeam = GetActiveCapturingTeam();
        CapturingTeamId = activeTeam;

        if (activeTeam >= 0 && activeTeam != OwnerTeamId)
        {
            CaptureProgress += captureSpeed * Runner.DeltaTime;
            if (CaptureProgress >= 100f)
            {
                OwnerTeamId = activeTeam;
                CaptureProgress = 0f;
            }
        }
        else
        {
            CaptureProgress = Mathf.Max(0f, CaptureProgress - captureSpeed * Runner.DeltaTime);
        }

        if (OwnerTeamId >= 0)
        {
            ArenaMatchManagerNetwork.Instance?.AddTeamScore(OwnerTeamId, scorePerSecond * Runner.DeltaTime);
        }
    }

    private int GetActiveCapturingTeam()
    {
        if (_team0Inside > 0 && _team1Inside == 0) return 0;
        if (_team1Inside > 0 && _team0Inside == 0) return 1;
        return -1;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        var stats = other.GetComponentInParent<MonsterStatsNetwork>();
        if (stats == null || stats.IsDead) return;
        if (stats.TeamId == 0) _team0Inside++;
        if (stats.TeamId == 1) _team1Inside++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        var stats = other.GetComponentInParent<MonsterStatsNetwork>();
        if (stats == null) return;
        if (stats.TeamId == 0) _team0Inside = Mathf.Max(0, _team0Inside - 1);
        if (stats.TeamId == 1) _team1Inside = Mathf.Max(0, _team1Inside - 1);
    }
}
