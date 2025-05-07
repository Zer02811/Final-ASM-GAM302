// NCharacterController.cs
using Fusion;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))] // Cần Collider để raycast bắn và kiểm tra đất
public class NCharacterController : NetworkBehaviour
{
    [Header("UI Effects")]
    public float lowHealthThreshold = 0.3f; // Ngưỡng máu yếu (30%)
    public float maxVignetteAlpha = 0.5f; // Độ mờ tối đa (0 -> 1)
    
    [Header("Effects")]
    public GameObject hitVFXPrefab; // Kéo HitVFXPrefab vào đây
    public AudioClip hitSound;
    public AudioClip collisionSound;
    
    private const float COLLISION_SOUND_COOLDOWN = 0.5f; // Nửa giây giữa các tiếng va chạm
    [Networked] private TickTimer CollisionSoundCooldown { get; set; }
    
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravityValue = -19.62f; // Mạnh hơn một chút so với mặc định (-9.81 * 2)
    public LayerMask groundLayer; // Layer của mặt đất để kiểm tra
    public float jumpForce = 8f; 
    private const int MAX_JUMPS = 2; 
    [Networked] private int JumpCount { get; set; }


    [Header("Combat")]
    public float maxHealth = 100f;
    public float shootDistance = 100f;
    public float shootDamage = 10f;
    public Transform shootOrigin; // Điểm bắt đầu của raycast bắn (ví dụ: đầu nòng súng)

    [Networked, OnChangedRender(nameof(HealthChangedRenderer))] // Gọi hàm HealthChangedRenderer khi thay đổi
    public float CurrentHealth { get; set; }
    [Networked, OnChangedRender(nameof(DeadStateChangedRenderer))] // Gọi hàm DeadStateChangedRenderer khi thay đổi
    public NetworkBool IsDead { get; set; } // Cờ trạng thái chết
    [Networked] private NetworkBool IsGrounded { get; set; } // Trạng thái tiếp đất (đồng bộ để tối ưu)
    [Networked] private TickTimer ShootCooldown { get; set; } // Bộ đếm thời gian hồi chiêu bắn
    // Tham chiếu Components
    private Animator _animator;
    private NetworkTransform _networkTransform;
    private CapsuleCollider _collider;
    private Vector3 _verticalVelocity; // Lưu vận tốc theo trục Y cho trọng lực và nhảy

    // Cần tham chiếu đến NPlayerInfo để cập nhật UI máu
    private NPlayerInfo _playerInfo;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _networkTransform = GetComponent<NetworkTransform>();
        _collider = GetComponent<CapsuleCollider>();
        _playerInfo = GetComponent<NPlayerInfo>(); // Lấy NPlayerInfo cùng GameObject

        if (shootOrigin == null) shootOrigin = transform; // Mặc định bắn từ tâm nếu chưa gán
    }

    public override void Spawned()
    {
        // Khởi tạo khi đối tượng được spawn
        if (Object.HasStateAuthority) // Chỉ State Authority mới khởi tạo giá trị mạng
        {
            CurrentHealth = maxHealth;
            IsDead = false;
            IsGrounded = false; // Sẽ được cập nhật trong FixedUpdateNetwork
            ShootCooldown = TickTimer.None;
            CollisionSoundCooldown = TickTimer.None;// Sẵn sàng bắn
        }

        // Cập nhật UI máu ban đầu (tất cả client cần làm điều này khi object spawn)
         if (_playerInfo != null)
         {
             _playerInfo.UpdateHealthUI(CurrentHealth, maxHealth);
             // Cập nhật trạng thái chết ban đầu (ẩn/hiện model?)
              _playerInfo.SetDeadState(IsDead);
         }

         if (Object.HasStateAuthority)
         {
             // ...
             JumpCount = 0; // Khởi tạo số lần nhảy
         }
    }

    public override void FixedUpdateNetwork()
{
    bool localIsGrounded = CheckGrounded(); // Gọi CheckGrounded MỘT LẦN và lưu kết quả cục bộ
    // ApplyGravity(); // ApplyGravity sẽ dùng this.IsGrounded (networked) theo code trên, hoặc bạn có thể sửa ApplyGravity để nhận localIsGrounded

    // Reset JumpCount khi chạm đất (DÙNG localIsGrounded)
    if (Object.HasStateAuthority && localIsGrounded && _verticalVelocity.y <= 0)
    {
         if(JumpCount != 0) JumpCount = 0;
    }

    if (GetInput(out NetworkInputData data))
    {
        if (!IsDead)
        {
            Vector3 moveDirection = data.direction.normalized;
            Move(moveDirection);

            // Xử lý nhảy đôi (DÙNG localIsGrounded để kiểm tra điều kiện nhảy)
            if (data.jump && JumpCount < MAX_JUMPS && localIsGrounded) // Chỉ nhảy lần đầu khi đang chạm đất (dùng localIsGrounded)
            {
                _verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravityValue);
                if (_animator != null) _animator.SetTrigger("Jump");
                if (Object.HasStateAuthority) JumpCount = 1; // Nhảy lần 1
                // ApplyGravity(); // Có thể gọi ApplyGravity ngay sau khi nhảy để bắt đầu rơi
            }
             else if (data.jump && JumpCount < MAX_JUMPS && !localIsGrounded) // Cho phép nhảy lần 2 khi đang trên không
             {
                 _verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravityValue);
                 if (_animator != null) _animator.SetTrigger("Jump"); // Có thể dùng trigger khác hoặc parameter khác cho nhảy đôi
                 if (Object.HasStateAuthority) JumpCount++; // Nhảy lần tiếp theo
                 // ApplyGravity();
             }


            // Bắn
            if (data.shoot && ShootCooldown.ExpiredOrNotRunning(Runner)) { Shoot(); ShootCooldown = TickTimer.CreateFromSeconds(Runner, 0.2f); }
        }

        // Cập nhật Animator (DÙNG localIsGrounded)
        float currentSpeed = new Vector3(data.direction.x, 0, data.direction.z).magnitude * moveSpeed;
         if(_animator != null) {
            _animator.SetFloat("speed", currentSpeed);
            _animator.SetBool("IsGrounded", localIsGrounded); // Cập nhật Animator bằng trạng thái cục bộ
         }
    }
    else // Proxy
    {
         if(_animator != null) {
            _animator.SetFloat("speed", 0f);
            _animator.SetBool("IsGrounded", this.IsGrounded); // Proxy dùng trạng thái mạng
         }
    }

     // XỬ LÝ HỒI SINH (Đã bị xóa theo yêu cầu trước)
     // if (Object.HasStateAuthority && IsDead) { ... }

     // Cập nhật trạng thái chết cho Animator
      if(_animator != null) _animator.SetBool("IsDead", IsDead);

      // *** QUAN TRỌNG: Gọi ApplyGravity ở cuối cùng sau khi đã xử lý input/nhảy ***
      ApplyGravity();
}

    // Bên trong NCharacterController.cs

private void HealthChangedRenderer()
{
    // Cập nhật thanh máu như cũ
    if (_playerInfo != null)
    {
        _playerInfo.UpdateHealthUI(CurrentHealth, maxHealth);
    }

    // --- XỬ LÝ HIỆU ỨNG MÁU YẾU (CHỈ CHO LOCAL PLAYER) ---
    // Kiểm tra xem có phải người chơi cục bộ không
    if (Object.HasInputAuthority)
    {
        // Kiểm tra xem UIManager và Image có tồn tại không
        if (UIManager.Instance != null && UIManager.Instance.lowHealthVignetteImage != null)
        {
            Image vignetteImage = UIManager.Instance.lowHealthVignetteImage; // Lấy tham chiếu từ Singleton

            float healthPercent = maxHealth > 0 ? CurrentHealth / maxHealth : 0;

            if (healthPercent < lowHealthThreshold) // lowHealthThreshold bạn đã khai báo trước đó
            {
                if (!vignetteImage.gameObject.activeSelf)
                {
                    vignetteImage.gameObject.SetActive(true);
                }

                float targetAlpha = Mathf.Lerp(maxVignetteAlpha, 0, healthPercent / lowHealthThreshold); // maxVignetteAlpha bạn đã khai báo
                targetAlpha = Mathf.Clamp01(targetAlpha);

                Color currentColor = vignetteImage.color;
                vignetteImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);
            }
            else
            {
                if (vignetteImage.gameObject.activeSelf)
                {
                    vignetteImage.gameObject.SetActive(false);
                    // Reset alpha về 0 khi tắt
                    Color currentColor = vignetteImage.color;
                    vignetteImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0f);
                }
            }
        }
        // Optional: Thêm else để log lỗi nếu không tìm thấy UIManager.Instance hoặc Image
        // else if (UIManager.Instance == null) { Debug.LogError("UIManager.Instance is null!"); }
        // else { Debug.LogError("UIManager's lowHealthVignetteImage is null!"); }
    }
    // --- KẾT THÚC XỬ LÝ HIỆU ỨNG ---
}

// Xóa dòng tắt vignette trong Spawned (nếu bạn đã thêm) vì UIManager Awake đã làm việc đó
// public override void Spawned() { ... }
    private void DeadStateChangedRenderer()
    {
        if (_playerInfo != null)
        {
            _playerInfo.SetDeadState(IsDead);
        }

        if(_animator != null) {
            _animator.SetBool("IsDead", IsDead);
        }
    }
    private void Move(Vector3 direction)
    {
        if (direction.magnitude > 0.1f) // Ngưỡng nhỏ để tránh drift
        {
            // Di chuyển dựa trên hướng input và tốc độ
            Vector3 horizontalMove = direction * moveSpeed * Runner.DeltaTime;
            _networkTransform.transform.position += horizontalMove;

            // Xoay nhân vật theo hướng di chuyển
            _networkTransform.transform.forward = direction;
        }
    }

    private bool CheckGrounded() // <--- Trả về bool
{
    float rayLength = _collider.height * 0.5f + 0.1f;
    Vector3 rayOrigin = transform.position + Vector3.up * (_collider.height * 0.5f - 0.05f);
    float sphereRadius = _collider.radius * 0.9f;

    // Thực hiện SphereCast
    bool isGroundedNow = Physics.SphereCast(rayOrigin, sphereRadius, Vector3.down, out RaycastHit hitInfo, rayLength - sphereRadius, groundLayer);

    // --- DEBUG VISUALIZATION ---
    // Vector3 castEnd = rayOrigin + Vector3.down * (rayLength - sphereRadius);
    // Color castColor = isGroundedNow ? Color.green : Color.red;
    // Debug.DrawLine(rayOrigin, castEnd, castColor, 0.1f);
    // if (isGroundedNow) Debug.Log($"Grounded on: {hitInfo.collider.name}", hitInfo.collider);
    // --- END DEBUG ---

    // State Authority vẫn cập nhật biến mạng
    if (Object.HasStateAuthority)
    {
        IsGrounded = isGroundedNow;
    }

    // Reset vận tốc dọc nếu vừa chạm đất (DÙNG isGroundedNow)
    if (isGroundedNow && _verticalVelocity.y < 0)
    {
        _verticalVelocity.y = -2f;
    }

    return isGroundedNow; // <--- Trả về kết quả cục bộ
}

// Sửa hàm ApplyGravity để dùng kết quả CheckGround cục bộ
private void ApplyGravity()
{
    // Đọc trạng thái IsGrounded cục bộ từ CheckGrounded (đã chạy ở đầu FixedUpdateNetwork)
    // Giả sử bạn lưu kết quả vào biến cục bộ trong FixedUpdateNetwork: bool localIsGrounded = CheckGrounded();
    // Hoặc gọi lại CheckGrounded nếu cần, nhưng hiệu năng kém hơn.
    // TỐT NHẤT: Gọi CheckGrounded() một lần ở đầu FixedUpdateNetwork và lưu kết quả.

    // *** GIẢ SỬ bạn đã gọi và lưu kết quả vào localIsGrounded ở đầu FixedUpdateNetwork ***
    // if (!localIsGrounded) // <--- Dùng kết quả cục bộ
    // {
    //     _verticalVelocity.y += gravityValue * Runner.DeltaTime;
    // }
    // *** KẾT THÚC GIẢ SỬ ***

    // ---> CÁCH ĐƠN GIẢN HƠN: Gọi lại CheckGrounded() ngay tại đây nếu không muốn thay đổi FixedUpdateNetwork nhiều
     bool isGroundedNow = this.IsGrounded; // Đọc giá trị mạng hiện tại (có thể trễ)
     // Tuy nhiên, để chính xác nhất cho vật lý cục bộ, nên dùng kết quả cast tức thời:
     // bool isGroundedNow = CheckGrounded(); // Gọi lại hoặc lấy từ biến đã lưu

     // Lấy giá trị IsGrounded từ biến Networked để quyết định trọng lực
     // (Có thể không phản ứng tức thì bằng kết quả cast cục bộ)
     if (!this.IsGrounded) // Vẫn dùng biến Networked để các client đồng bộ về trọng lực
     {
          _verticalVelocity.y += gravityValue * Runner.DeltaTime;
     }


    // Áp dụng vận tốc dọc
    if (Object.HasStateAuthority || Object.HasInputAuthority)
    {
         if (_networkTransform != null) {
             _networkTransform.transform.position += _verticalVelocity * Runner.DeltaTime;
         } else {
             transform.position += _verticalVelocity * Runner.DeltaTime;
         }
    }
}
    private void Shoot()
    {
        _animator.SetTrigger("Shoot"); // Kích hoạt animation bắn

        // Tạo raycast từ điểm bắn
        // Sử dụng LayerMask để chỉ bắn trúng đối tượng mong muốn (ví dụ: không bắn trúng Trigger, không bắn trúng layer "Ignore Raycast")
        // LayerMask shootLayerMask = LayerMask.GetMask("Default", "Player"); // Ví dụ: bắn layer Default và Player
        LayerMask shootLayerMask = ~LayerMask.GetMask("Ignore Raycast", "Trigger"); // Bắn mọi thứ trừ 2 layer này

        if (Runner.GetPhysicsScene().Raycast(shootOrigin.position, shootOrigin.forward, out var hit, shootDistance, shootLayerMask, QueryTriggerInteraction.Ignore))
        {

            // Kiểm tra xem có bắn trúng NetworkObject không
            NetworkObject hitObject = hit.collider.GetComponentInParent<NetworkObject>();
             // Kiểm tra xem có bắn trúng NCharacterController không (để lấy máu)
             NCharacterController hitCharacter = hit.collider.GetComponentInParent<NCharacterController>();

            if (hitCharacter != null && hitCharacter != this) // Nếu bắn trúng người chơi khác
            {
                // Gửi RPC đến State Authority của đối tượng bị bắn trúng để trừ máu
                 hitCharacter.Rpc_TakeDamage(shootDamage, Object.InputAuthority, hit.point, hit.normal);
            }
            else if(hitObject != null)
            {
                 // Có thể thêm logic xử lý bắn trúng các đối tượng mạng khác ở đây (thùng gỗ, mục tiêu,...)
            }
            else {
                 // Xử lý bắn trúng môi trường (tạo hiệu ứng va chạm,...)
            }
        }
        else
        {
            Debug.DrawRay(shootOrigin.position, shootOrigin.forward * shootDistance, Color.green, 1f); // Vẽ tia debug nếu bắn trượt
        }
    } 
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_TakeDamage(float damage, PlayerRef attackerRef, Vector3 hitPosition, Vector3 hitNormal) // Vẫn nhận attackerRef
    {
        if (IsDead || !Object.HasStateAuthority) return; // Guard conditions
        if (attackerRef.IsRealPlayer && attackerRef != Object.InputAuthority)
        {
            Rpc_ShowHitEffect(hitPosition, hitNormal);

            CurrentHealth -= damage;

            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                Die();
            }
            LogDamage(damage, attackerRef); // Gọi hàm log riêng cho gọn

        }
    }
    private void LogDamage(float damage, PlayerRef attackerRef) {
        // Cố gắng lấy tên người tấn công
        string attackerName = $"Player {attackerRef.PlayerId}"; // Mặc định
        if (Runner != null && Runner.TryGetPlayerObject(attackerRef, out var attackerObject) && attackerObject != null) {
            if (attackerObject.TryGetBehaviour<NPlayerInfo>(out var attackerNPlayerInfo)) {
                attackerName = attackerNPlayerInfo.PlayerName.ToString();
            }
        }
        // Log trên console của người bị bắn (vì hàm này chạy trên State Authority của người bị bắn)
        Debug.Log($"[DamageLog] {gameObject.name} took {damage:F1} damage from {attackerName}. Remaining Health: {CurrentHealth:F1}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Chỉ State Authority mới xử lý và gửi RPC để tránh trùng lặp và spam
        if (!Object.HasStateAuthority || !CollisionSoundCooldown.ExpiredOrNotRunning(Runner))
        {
            return; // Không phải authority hoặc đang cooldown thì bỏ qua
        }

        // Kiểm tra xem có va chạm với người chơi khác không
        if (collision.gameObject.TryGetComponent<NCharacterController>(out var otherPlayer))
        {
            // Va chạm với người chơi khác, lấy điểm va chạm đầu tiên
            Vector3 contactPoint = collision.GetContact(0).point;

            // Gửi RPC để tất cả client cùng phát âm thanh
            Rpc_PlayCollisionSound(contactPoint);

            // Đặt lại cooldown
            CollisionSoundCooldown = TickTimer.CreateFromSeconds(Runner, COLLISION_SOUND_COOLDOWN);
        }
    }

    //[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_PlayCollisionSound(Vector3 position)
    {
        if (collisionSound != null)
        {
            AudioSource.PlayClipAtPoint(collisionSound, position);
        }
    }
    
// --- THÊM RPC MỚI ĐỂ HIỂN THỊ HIỆU ỨNG ---
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] // Chạy trên tất cả client, gửi từ State Authority (của người bị bắn)
    private void Rpc_ShowHitEffect(Vector3 position, Vector3 normal)
    {
        // Chỉ thực thi nếu có prefab và âm thanh được gán
        if (hitVFXPrefab != null)
        {
            // Tạo hiệu ứng tại điểm va chạm, xoay theo pháp tuyến bề mặt
            Instantiate(hitVFXPrefab, position, Quaternion.LookRotation(normal));
        }
        if (hitSound != null)
        {
            // Phát âm thanh tại điểm va chạm
            AudioSource.PlayClipAtPoint(hitSound, position);
        }
    }

    private void Die()
    {
        if (!Object.HasStateAuthority) return; // Đảm bảo

        if (IsDead) return; 
        
        IsDead = true;

        

        IsDead = true; // Đặt cờ chết (sẽ đồng bộ)
        _verticalVelocity = Vector3.zero; // Ngừng di chuyển dọc khi chết


        // Bắt đầu timer hồi sinh

        // Tắt collider hoặc đặt vào layer khác để không bị bắn nữa
        // _collider.enabled = false; // Tắt hoàn toàn
         gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // Chuyển sang layer khác

        // Cập nhật UI/Visuals (State Authority cập nhật trực tiếp, client khác qua OnChangedRender)
        if (_playerInfo != null)
        {
            _playerInfo.SetDeadState(true);
        }
        if (Object.HasInputAuthority)
        {
            // Tìm CameraController và gọi EnterSpectatorMode
            CameraController cam = FindObjectOfType<CameraController>(); // Hoặc lấy tham chiếu đã lưu
            cam?.EnterSpectatorMode();
        }

    }
    
}