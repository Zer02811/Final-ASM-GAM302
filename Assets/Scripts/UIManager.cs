using UnityEngine;

public class UIManager : MonoBehaviour // Hoặc tên script khác bạn dùng
{
    public GameObject leaderboardCanvas; // Kéo LeaderboardCanvas vào đây

    void Update()
    {
        // Kiểm tra nếu người dùng nhấn phím Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (leaderboardCanvas != null)
            {
                // Đảo ngược trạng thái active của leaderboard
                leaderboardCanvas.SetActive(!leaderboardCanvas.activeSelf);
            }
        }
    }
}