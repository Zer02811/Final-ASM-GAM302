// PlayerInputHandler.cs

using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

// Thêm RequireComponent để đảm bảo NetworkRunner tồn tại
[RequireComponent(typeof(NetworkRunner))]
public class PlayerInputHandler : MonoBehaviour, INetworkRunnerCallbacks // Implement callbacks
{
    [Header("Prefabs and Camera")]
    public NetworkPrefabRef characterPrefab; // Kéo Prefab nhân vật vào đây
    public CameraController cameraController; // Kéo Camera Controller vào đây

    private NetworkRunner _runner;
    private NetworkObject _localPlayerObject; // Lưu trữ tham chiếu đến nhân vật của người chơi cục bộ

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
        {
            Debug.LogError("NetworkRunner not found on the same GameObject!");
        }
         // Tìm CameraController nếu chưa gán
         if (cameraController == null)
         {
             cameraController = FindObjectOfType<CameraController>();
             if (cameraController == null) {
                  Debug.LogError("CameraController not found in the scene!");
             }
         }
    }

    // --- INetworkRunnerCallbacks Implementation ---

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Tạo một instance mới của cấu trúc input
        var data = new NetworkInputData();

        // Lấy input từ Unity Input System (hoặc Input Manager cũ)
        data.direction.x = Input.GetAxis("Horizontal");
        data.direction.z = Input.GetAxis("Vertical");
        data.jump = Input.GetButtonDown("Jump"); // <--- THÊM LẠI (Dùng GetButtonDown)
        data.shoot = Input.GetMouseButton(0); // Nút chuột trái
        input.Set(data);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined.");
        // Chỉ spawn nhân vật nếu đây là người chơi cục bộ (Local Player)
        if (player == runner.LocalPlayer)
        {
            Debug.Log($"Local player {player} joined. Spawning character...");
            // Xác định vị trí spawn (ví dụ: điểm spawn cố định hoặc logic phức tạp hơn)
            Vector3 spawnPosition = Vector3.zero; // Thay đổi nếu cần

            // Spawn nhân vật, gán quyền điều khiển (Input Authority) cho người chơi cục bộ
            _localPlayerObject = runner.Spawn(
                characterPrefab,
                spawnPosition,
                Quaternion.identity,
                player, // Gán StateAuthority và InputAuthority cho player này
                (runner, obj) => {
                    // Callback này được gọi NGAY TRƯỚC KHI đối tượng được kích hoạt mạng
                    // Đây là nơi tốt để đặt dữ liệu khởi tạo ban đầu
                    string playerName = PlayerPrefs.GetString("username", "Player_" + player.PlayerId);
                    Debug.Log($"Setting player name on spawn: {playerName}");
                    obj.GetComponent<NPlayerInfo>().PlayerName = playerName; // Thiết lập tên
                }
            );

            // Thiết lập target cho camera sau khi đối tượng đã được spawn
            if (_localPlayerObject != null && cameraController != null)
            {
                Debug.Log("Setting camera target.");
                cameraController.target = _localPlayerObject.transform;
            }
            else if (cameraController == null)
             {
                 Debug.LogError("CameraController reference is missing in PlayerInputHandler.");
             }
             else
             {
                Debug.LogError($"Failed to spawn character for local player {player}.");
             }
        }
         else {
            Debug.Log($"Remote player {player} joined. Character will be spawned by their client.");
         }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
         Debug.Log($"Player {player} left.");
        // Xử lý khi người chơi rời đi (ví dụ: xóa nhân vật của họ)
        // Tìm NetworkObject của người chơi đã rời đi và despawn nó
        if (player == runner.LocalPlayer)
        {
             // Nếu người chơi cục bộ rời đi, reset camera
             if (cameraController != null) cameraController.target = null;
             _localPlayerObject = null; // Xóa tham chiếu cục bộ
             // Có thể thêm logic quay lại menu chính ở đây
        }

        // Tìm và xóa đối tượng của người chơi đã rời đi (nếu cần thiết)
        // Cần một cách để map PlayerRef với NetworkObject của họ
        // Ví dụ: foreach (var playerObj in FindObjectsOfType<NPlayerInfo>()) {
        //            if (playerObj.Object != null && playerObj.Object.InputAuthority == player) {
        //                if (runner.IsServer || runner.HasStateAuthority) { // Chỉ server/host hoặc state authority mới được despawn
        //                   runner.Despawn(playerObj.Object);
        //                }
        //                break;
        //            }
        // }
    }

    // --- Implement các phương thức khác của INetworkRunnerCallbacks (để trống nếu không cần) ---
    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("Connected to server."); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.LogError($"Connect failed: {reason}"); }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner) { Debug.Log("Disconnected from server."); }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { Debug.Log("Scene load done."); }
    public void OnSceneLoadStart(NetworkRunner runner) { Debug.Log("Scene load start."); }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { Debug.Log($"Runner shutdown: {shutdownReason}"); }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        // Được gọi khi client bị ngắt kết nối khỏi server/host với lý do cụ thể.
        Debug.LogWarning($"Disconnected from server. Reason: {reason}");
        // Bạn có thể thêm logic xử lý ngắt kết nối ở đây (ví dụ: quay lại menu chính)
        // if (_localPlayerObject != null && _localPlayerObject.InputAuthority == runner.LocalPlayer) {
        //    runner.Despawn(_localPlayerObject); // Cân nhắc despawn nếu cần
        // }
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene"); // Ví dụ quay về menu
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // Được gọi khi một NetworkObject đi vào Vùng Quan Tâm (Area of Interest - AOI) của một Player.
        // Callback này hữu ích cho việc tối ưu hóa, ví dụ chỉ kích hoạt một số component khi người chơi nhìn thấy đối tượng.
        // Thường được gọi trên Server/Host, và trên Client nếu 'player' là LocalPlayer.
        // Debug.Log($"Object {obj.name} entered AOI of player {player}");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        // Được gọi khi một NetworkObject đi ra khỏi Vùng Quan Tâm (Area of Interest - AOI) của một Player.
        // Thường được gọi trên Server/Host, và trên Client nếu 'player' là LocalPlayer.
        // Debug.Log($"Object {obj.name} exited AOI of player {player}");
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        // Callback này cung cấp thông tin về tiến trình gửi dữ liệu đáng tin cậy (reliable data).
        // Hữu ích khi gửi các gói dữ liệu lớn và muốn theo dõi tiến độ.
        // Debug.Log($"Reliable data progress for key {key} to player {player}: {progress * 100f}%");
    }

    // Lưu ý về chữ ký: Lỗi của bạn yêu cầu phiên bản có ReliableKey.
    // Phiên bản cũ hơn hoặc callback khác có thể chỉ có ArraySegment<byte> data.
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        // Được gọi khi nhận được dữ liệu đáng tin cậy (reliable data) được gửi với một 'key' cụ thể.
        // Hữu ích cho việc gửi dữ liệu quan trọng cần đảm bảo đến nơi.
        // Debug.Log($"Received reliable data with key {key} from player {player}. Size: {data.Count} bytes");
        // Xử lý dữ liệu nhận được ở đây...
    }
}