using TMPro;
using UnityEngine;

public class LeaderboardEntryUI : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text scoreText;

    public void UpdateInfo(int rank, string playerName, int score)
    {
        if (rankText != null) rankText.text = $"#{rank}";
        if (nameText != null) nameText.text = playerName;
        if (scoreText != null) scoreText.text = $"{score} PTS"; // Hoặc chỉ score
    }
}