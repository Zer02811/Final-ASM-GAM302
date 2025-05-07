using Fusion; // Cần cho PlayerRef
using System.Collections.Generic; // Cần cho List
using System.Linq; // Cần cho LINQ
using TMPro; // Cần cho TextMeshPro
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target; // Target mặc định (người chơi cục bộ)
    public float lerpSpeed = 5f; // Tốc độ lerp (thay vì 0.1f để dùng DeltaTime)

    [Header("Spectator Mode")]
    public TMP_Text spectatorNameText; // Kéo TextMeshPro hiển thị tên vào đây

    private bool _isSpectating = false;
    private PlayerRef _currentSpectateTargetPlayerRef = PlayerRef.None; // Lưu PlayerRef của người đang xem
    private Transform _currentSpectateTargetTransform = null; // Lưu Transform để theo dõi

    private List<NPlayerInfo> _potentialTargets = new List<NPlayerInfo>(); // Cache danh sách target
    private int _spectatorIndex = -1; // Index trong danh sách _potentialTargets

    private void LateUpdate()
    {
        Transform currentTarget = _isSpectating ? _currentSpectateTargetTransform : target;

        if (currentTarget == null)
        {
             // Nếu đang spectate mà không có target, thử tìm target mới
             if (_isSpectating) FindNextSpectatorTarget(1); // Thử tìm người tiếp theo
             return; // Không có ai để follow
        }

        // Di chuyển camera mượt mà theo target
        transform.position = Vector3.Lerp(transform.position, currentTarget.position, lerpSpeed * Time.deltaTime); // Dùng Time.deltaTime
        // Có thể thêm logic xoay camera nhìn vào target ở đây nếu muốn
        // transform.LookAt(currentTarget);
    }

    private void Update()
    {
        // Chỉ xử lý input khi đang ở chế độ spectator
        if (_isSpectating)
        {
            if (Input.GetKeyDown(KeyCode.E)) // Next player
            {
                FindNextSpectatorTarget(1);
            }
            else if (Input.GetKeyDown(KeyCode.Q)) // Previous player
            {
                FindNextSpectatorTarget(-1);
            }
        }
    }

    // Được gọi từ NCharacterController khi người chơi cục bộ chết
    public void EnterSpectatorMode()
    {
        Debug.Log("Entering Spectator Mode");
        _isSpectating = true;
        target = null; // Xóa target người chơi cục bộ
        _currentSpectateTargetPlayerRef = PlayerRef.None; // Reset target đang xem
        _currentSpectateTargetTransform = null;
        FindNextSpectatorTarget(1); // Tìm người đầu tiên để xem
        if (spectatorNameText != null) spectatorNameText.gameObject.SetActive(true); // Hiện UI tên
    }

    // Bên trong CameraController.cs

private void FindNextSpectatorTarget(int direction)
{
    // Lấy NetworkRunner instance (cần using Fusion;)
    NetworkRunner runner = NetworkRunner.Instances.FirstOrDefault(); // Lấy runner đầu tiên đang hoạt động
    if (runner == null)
    {
         Debug.LogWarning("FindNextSpectatorTarget: No active NetworkRunner found.");
         _potentialTargets.Clear(); // Xóa danh sách cũ nếu không có runner
    }

    // Lấy PlayerRef của người chơi cục bộ
    PlayerRef localPlayerRef = runner != null ? runner.LocalPlayer : PlayerRef.None;

    // Lọc danh sách người chơi:
    // - Còn tồn tại (p != null, p.Object != null, p.Object.IsValid)
    // - Không phải người chơi cục bộ (dùng StateAuthority để so sánh ổn định hơn InputAuthority)
    // - Còn sống (!controller.IsDead)
    _potentialTargets = NPlayerInfo.AllPlayerInfos
        .Where(p => p != null &&
                    p.Object != null &&
                    p.Object.IsValid &&
                    p.Object.StateAuthority != localPlayerRef && // Quan trọng: Không xem chính mình
                    p.TryGetComponent<NCharacterController>(out var controller) && !controller.IsDead) // Lấy component và kiểm tra IsDead
        .ToList();

    if (_potentialTargets.Count == 0)
    {
        // Log gốc của bạn xuất hiện ở đây
        Debug.Log("No living players to spectate.");
        _currentSpectateTargetPlayerRef = PlayerRef.None;
        _currentSpectateTargetTransform = null;
        if (spectatorNameText != null) spectatorNameText.text = "No targets available";
        return;
    }

    // Tìm index hiện tại (dùng StateAuthority để ổn định hơn)
    _spectatorIndex = _potentialTargets.FindIndex(p => p.Object.StateAuthority == _currentSpectateTargetPlayerRef);
    // Nếu không tìm thấy target hiện tại trong danh sách mới (ví dụ target vừa chết), bắt đầu từ đầu
     if (_spectatorIndex == -1) _spectatorIndex = (direction > 0 ? -1 : 0); // Để phép tính tiếp theo chọn đúng


    // Tính index mới, xử lý vòng lặp
    _spectatorIndex += direction;
    if (_spectatorIndex >= _potentialTargets.Count) _spectatorIndex = 0;
    else if (_spectatorIndex < 0) _spectatorIndex = _potentialTargets.Count - 1;

    // Lấy thông tin target mới
    NPlayerInfo newTargetInfo = _potentialTargets[_spectatorIndex];
    // Cập nhật cả PlayerRef và Transform
    _currentSpectateTargetPlayerRef = newTargetInfo.Object.StateAuthority; // Dùng StateAuthority làm key tham chiếu
    _currentSpectateTargetTransform = newTargetInfo.transform;

    Debug.Log($"Spectating Player: {newTargetInfo.PlayerName} (Ref: {_currentSpectateTargetPlayerRef})");

    // Cập nhật UI tên
    if (spectatorNameText != null)
    {
        spectatorNameText.text = $"Spectating: {newTargetInfo.PlayerName}";
    }
}
}