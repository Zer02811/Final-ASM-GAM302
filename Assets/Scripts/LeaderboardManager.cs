using Fusion;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject leaderboardEntryPrefab; // Kéo Prefab bạn vừa tạo vào đây
    public Transform entryListContainer;   // Kéo GameObject "EntryListContainer" vào đây
    public float updateInterval = 1.0f;

    // Lưu trữ các UI entry đang hiển thị để cập nhật/xóa
    // Key: PlayerRef (hoặc NetworkId nếu PlayerRef không ổn định)
    private Dictionary<PlayerRef, LeaderboardEntryUI> _uiEntries = new Dictionary<PlayerRef, LeaderboardEntryUI>();
    private float _timer;

    // Biến để tránh lỗi nếu PlayerRef không hợp lệ ngay lập tức
    private List<PlayerRef> _playersToRemove = new List<PlayerRef>();


    private void OnEnable()
    {
         // Khởi tạo lại danh sách UI khi bật Leaderboard
         ClearUIEntries();
         _timer = 0f; // Cập nhật ngay lần đầu
         UpdateLeaderboard(); // Chạy cập nhật ngay khi enable
    }

     private void OnDisable()
     {
          // Xóa UI khi tắt Leaderboard
          ClearUIEntries();
     }


    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            UpdateLeaderboard();
            _timer = updateInterval;
        }
    }

    void UpdateLeaderboard()
    {
        if (leaderboardEntryPrefab == null || entryListContainer == null)
        {
            Debug.LogError("Leaderboard UI references not set!");
            return;
        }

        // --- Lấy danh sách người chơi từ danh sách tĩnh ---
        List<NPlayerInfo> activePlayers = new List<NPlayerInfo>(NPlayerInfo.AllPlayerInfos);
// --- DEBUG ---
        Debug.Log("--- Leaderboard Update Check ---");
         foreach (var pInfo in activePlayers) {
            if (pInfo != null && pInfo.Object != null) 
            {
                 Debug.Log($"Player: {pInfo.PlayerName}, Score (read by LM): {pInfo.Score}, ObjID: {pInfo.Object.Id}");
            }
         }
        // --- END DEBUG ---
        activePlayers.RemoveAll(p => p == null || p.Object == null || !p.Object.IsValid);

        // --- Sắp xếp người chơi ---
        var sortedPlayers = activePlayers.OrderByDescending(p => p.Score)
                                         .ThenBy(p => p.PlayerName.ToString())
                                         .ToList();

        // --- Cập nhật UI ---
        HashSet<PlayerRef> currentValidPlayerRefs = new HashSet<PlayerRef>();

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            NPlayerInfo playerInfo = sortedPlayers[i];

            // Cố gắng lấy PlayerRef ổn định (ưu tiên StateAuthority nếu có)
            PlayerRef playerRef = playerInfo.Object.StateAuthority;
            if (!playerRef.IsRealPlayer) {
                playerRef = playerInfo.Object.InputAuthority; // Dự phòng InputAuthority
            }

            // Nếu vẫn không lấy được PlayerRef hợp lệ, bỏ qua entry này
            if (!playerRef.IsRealPlayer) {
                 // Debug.LogWarning($"Could not get valid PlayerRef for Player {playerInfo.PlayerName}");
                 continue;
             }


            currentValidPlayerRefs.Add(playerRef); // Đánh dấu PlayerRef này hợp lệ và đang online

            LeaderboardEntryUI entryUI;
            if (_uiEntries.TryGetValue(playerRef, out entryUI))
            {
                // Cập nhật entry đã tồn tại
                if (entryUI != null) // Kiểm tra null phòng trường hợp entry bị hủy ngoài ý muốn
                {
                     entryUI.UpdateInfo(i + 1, playerInfo.PlayerName.ToString(), playerInfo.Score);
                } else {
                    // Nếu entryUI bị null, tạo lại nó
                     _uiEntries.Remove(playerRef); // Xóa key cũ
                     CreateUIEntry(playerRef, i, playerInfo); // Gọi hàm tạo mới
                }
            }
            else
            {
                // Tạo entry mới
                CreateUIEntry(playerRef, i, playerInfo);
            }

             // Đảm bảo thứ tự hiển thị đúng trong Vertical Layout Group
             if (_uiEntries.TryGetValue(playerRef, out entryUI) && entryUI != null) {
                 entryUI.transform.SetSiblingIndex(i);
             }
        }

        // --- Xóa các UI entry của người chơi đã rời đi ---
        _playersToRemove.Clear();
        foreach (var kvp in _uiEntries)
        {
            // Nếu PlayerRef không còn trong danh sách hợp lệ hiện tại VÀ entry UI vẫn tồn tại
            if (!currentValidPlayerRefs.Contains(kvp.Key) && kvp.Value != null)
            {
                _playersToRemove.Add(kvp.Key);
                Destroy(kvp.Value.gameObject);
                 // Debug.Log($"Removing UI entry for PlayerRef {kvp.Key}");
            }
            // Cũng xóa nếu entry UI bị null vì lý do nào đó
            else if (kvp.Value == null) {
                 _playersToRemove.Add(kvp.Key);
                 // Debug.Log($"Removing null UI entry reference for PlayerRef {kvp.Key}");
            }
        }

        foreach (PlayerRef playerRef in _playersToRemove)
        {
            _uiEntries.Remove(playerRef);
        }
    }

    // Hàm trợ giúp tạo UI Entry
    void CreateUIEntry(PlayerRef playerRef, int index, NPlayerInfo playerInfo)
    {
         GameObject newEntryGO = Instantiate(leaderboardEntryPrefab, entryListContainer);
         LeaderboardEntryUI entryUI = newEntryGO.GetComponent<LeaderboardEntryUI>();
         if (entryUI != null)
         {
             entryUI.UpdateInfo(index + 1, playerInfo.PlayerName.ToString(), playerInfo.Score);
             _uiEntries.Add(playerRef, entryUI);
              // Debug.Log($"Added UI entry for {playerInfo.PlayerName}");
         }
         else
         {
             Debug.LogError("LeaderboardEntryPrefab is missing LeaderboardEntryUI script!");
             Destroy(newEntryGO);
         }
    }


     void ClearUIEntries() {
          // Debug.Log("Clearing UI Entries");
          foreach(var entry in _uiEntries.Values) {
               if(entry != null) Destroy(entry.gameObject);
          }
          _uiEntries.Clear();
     }
}