using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour // Hoặc tên script khác bạn dùng
{
    public static UIManager Instance { get; private set; }
    [Header("HUD Elements")]
    // Tham chiếu đến Image viền đỏ trong Scene
    public Image lowHealthVignetteImage;
    public GameObject leaderboardCanvas; // Kéo LeaderboardCanvas vào đây
private void Awake()
    {
        // --- Singleton Setup ---
        // Đảm bảo chỉ có một instance UIManager tồn tại
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Một instance khác của UIManager đã tồn tại. Hủy bỏ instance này.");
            Destroy(gameObject); // Hủy bỏ GameObject này nếu đã có Instance khác
            return;
        }
        Instance = this; // Gán instance này làm instance tĩnh
        // DontDestroyOnLoad(gameObject); // Bỏ comment dòng này nếu UIManager cần tồn tại qua các Scene
        // -----------------------

        // Đảm bảo viền đỏ bị tắt ban đầu khi game bắt đầu
        if (lowHealthVignetteImage != null)
        {
            lowHealthVignetteImage.gameObject.SetActive(false);
            // Đặt alpha về 0 phòng trường hợp nó không bị tắt hoàn toàn
            Color c = lowHealthVignetteImage.color;
            c.a = 0;
            lowHealthVignetteImage.color = c;
        }
        else
        {
             Debug.LogWarning("Low Health Vignette Image chưa được gán cho UIManager trong Inspector!");
        }
    }
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