using TMPro;
using UnityEngine;

public class InGameHUD : MonoBehaviour
{
    public TMP_Text team0ScoreText;
    public TMP_Text team1ScoreText;
    public TMP_Text resultText;

    private void Update()
    {
        var match = ArenaMatchManagerNetwork.Instance;
        if (match == null) return;

        if (team0ScoreText != null) team0ScoreText.text = Mathf.FloorToInt(match.Team0Score).ToString();
        if (team1ScoreText != null) team1ScoreText.text = Mathf.FloorToInt(match.Team1Score).ToString();

        if (resultText != null)
        {
            resultText.gameObject.SetActive(match.State == MatchState.Ended);
            if (match.State == MatchState.Ended)
                resultText.text = match.WinningTeamId < 0 ? "DRAW" : $"TEAM {match.WinningTeamId} WIN";
        }
    }
}
