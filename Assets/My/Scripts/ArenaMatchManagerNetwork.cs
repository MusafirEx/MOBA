using Fusion;
using UnityEngine;

public enum MatchState
{
    Waiting,
    Playing,
    Ended
}

public class ArenaMatchManagerNetwork : NetworkBehaviour
{
    public static ArenaMatchManagerNetwork Instance { get; private set; }

    public float matchLengthSeconds = 300f;
    public float targetScore = 100f;

    [Networked] public MatchState State { get; set; }
    [Networked] public TickTimer MatchTimer { get; set; }
    [Networked] public float Team0Score { get; set; }
    [Networked] public float Team1Score { get; set; }
    [Networked] public int WinningTeamId { get; set; }

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            StartMatch();
    }

    public void StartMatch()
    {
        if (!Object.HasStateAuthority) return;
        State = MatchState.Playing;
        MatchTimer = TickTimer.CreateFromSeconds(Runner, matchLengthSeconds);
        Team0Score = 0f;
        Team1Score = 0f;
        WinningTeamId = -1;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || State != MatchState.Playing) return;

        if (Team0Score >= targetScore) EndMatch(0);
        else if (Team1Score >= targetScore) EndMatch(1);
        else if (MatchTimer.Expired(Runner))
        {
            if (Team0Score > Team1Score) EndMatch(0);
            else if (Team1Score > Team0Score) EndMatch(1);
            else EndMatch(-1);
        }
    }

    public void AddTeamScore(int teamId, float amount)
    {
        if (!Object.HasStateAuthority || State != MatchState.Playing) return;
        if (teamId == 0) Team0Score += amount;
        if (teamId == 1) Team1Score += amount;
    }

    private void EndMatch(int winnerTeamId)
    {
        State = MatchState.Ended;
        WinningTeamId = winnerTeamId;
        RPC_ShowMatchResult(winnerTeamId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowMatchResult(int winnerTeamId)
    {
        Debug.Log(winnerTeamId < 0 ? "Draw" : $"Team {winnerTeamId} wins");
        RewardManager reward = FindFirstObjectByType<RewardManager>();
        if (reward != null) reward.ApplyPrototypeReward(winnerTeamId);
    }
}
