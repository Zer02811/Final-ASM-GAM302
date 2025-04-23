// NCharacterController.cs
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider))] // Cần Collider để raycast bắn và kiểm tra đất
public class NCharacterController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravityValue = -19.62f; // Mạnh hơn một chút so với mặc định (-9.81 * 2)
    public LayerMask groundLayer; // Layer của mặt đất để kiểm tra

    [Header("Combat")]
    public float maxHealth = 100f;
    public float shootDistance = 100f;
    public float shootDamage = 10f;
    public Transform shootOrigin; // Điểm bắt đầu của raycast bắn (ví dụ: đầu nòng súng)
    public float respawnDelay = 10f; // Thời gian chờ hồi sinh (giây)

    [Networked, OnChangedRender(nameof(HealthChangedRenderer))] // Gọi hàm HealthChangedRenderer khi thay đổi
    public float CurrentHealth { get; set; }
    [Networked, OnChangedRender(nameof(DeadStateChangedRenderer))] // Gọi hàm DeadStateChangedRenderer khi thay đổi
    public NetworkBool IsDead { get; set; } // Cờ trạng thái chết
    [Networked] private NetworkBool IsGrounded { get; set; } // Trạng thái tiếp đất (đồng bộ để tối ưu)
    [Networked] private TickTimer ShootCooldown { get; set; } // Bộ đếm thời gian hồi chiêu bắn
    [Networked] private TickTimer RespawnTimer { get; set; } // Bộ đếm thời gian hồi sinh

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
            RespawnTimer = TickTimer.None; // Không có timer hồi sinh ban đầu
            ShootCooldown = TickTimer.None; // Sẵn sàng bắn
        }
        // Cập nhật UI máu ban đầu (tất cả client cần làm điều này khi object spawn)
         if (_playerInfo != null)
         {
             _playerInfo.UpdateHealthUI(CurrentHealth, maxHealth);
             // Cập nhật trạng thái chết ban đầu (ẩn/hiện model?)
              _playerInfo.SetDeadState(IsDead);
         }

         // Nếu là người chơi cục bộ, đảm bảo camera theo dõi đúng đối tượng
         // (Đã xử lý trong PlayerInputHandler, nhưng kiểm tra lại không thừa)
         // if (Object.HasInputAuthority) {
         //     CameraController cam = FindObjectOfType<CameraController>();
         //     if(cam != null) cam.target = this.transform;
         // }
    }

    public override void FixedUpdateNetwork()
    {
         // Luôn kiểm tra đất và áp dụng trọng lực (cả trên client và server/host)
         CheckGrounded();
         ApplyGravity();

        // Lấy input chỉ khi có Input Authority
        if (GetInput(out NetworkInputData data))
        {
            // --- XỬ LÝ KHI CÒN SỐNG ---
            if (!IsDead)
            {
                // Di chuyển
                Vector3 moveDirection = data.direction.normalized;
                Move(moveDirection);

                // Bắn
                if (data.shoot && ShootCooldown.ExpiredOrNotRunning(Runner))
                {
                    Shoot();
                    ShootCooldown = TickTimer.CreateFromSeconds(Runner, 0.2f); // Hồi chiêu 0.2 giây
                }
            }

            // --- CẬP NHẬT ANIMATOR (chỉ client có Input Authority mới gửi input) ---
             // Client có Input Authority tính toán tốc độ dựa trên input
            float currentSpeed = new Vector3(data.direction.x, 0, data.direction.z).magnitude * moveSpeed;
            _animator.SetFloat("speed", currentSpeed);
            _animator.SetBool("IsGrounded", IsGrounded); // Cập nhật trạng thái tiếp đất cho Animator
        }
        else
        {
             // --- XỬ LÝ TRÊN PROXY (CLIENT KHÔNG CÓ INPUT AUTHORITY) ---
             // Proxies không xử lý input, chỉ nhận trạng thái từ NetworkTransform và [Networked] properties.
             // Animator có thể dựa vào velocity của NetworkTransform hoặc các [Networked] bools khác nếu cần.
             // Ví dụ đơn giản: nếu không có input, coi như tốc độ là 0 (NetworkTransform sẽ nội suy vị trí)
             _animator.SetFloat("speed", 0f); // Hoặc dùng cách khác phức tạp hơn
             _animator.SetBool("IsGrounded", IsGrounded); // Animator vẫn cần biết trạng thái tiếp đất
        }


        // --- XỬ LÝ HỒI SINH (Chỉ State Authority mới chạy logic này) ---
        if (Object.HasStateAuthority && IsDead)
        {
            if (RespawnTimer.Expired(Runner))
            {
                Respawn();
                RespawnTimer = TickTimer.None; // Dừng timer
            }
        }

         // --- CẬP NHẬT TRẠNG THÁI CHẾT CHO ANIMATOR (Tất cả client) ---
         // Dựa vào IsDead đã được đồng bộ hóa
         _animator.SetBool("IsDead", IsDead);
    }

    private void HealthChangedRenderer()
    {
        if (_playerInfo != null)
        {
            _playerInfo.UpdateHealthUI(CurrentHealth, maxHealth);
        }
    }
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

    private void CheckGrounded()
    {
        // Raycast ngắn xuống dưới từ tâm collider để kiểm tra đất
        float rayLength = _collider.height * 0.5f + 0.1f; // Chiều dài raycast
        Vector3 rayOrigin = transform.position + Vector3.up * (_collider.height * 0.5f - 0.05f); // Bắt đầu từ gần đáy collider

        // bool grounded = Physics.Raycast(rayOrigin, Vector3.down, rayLength, groundLayer);
        // Sử dụng SphereCast để ổn định hơn trên dốc và các cạnh
        float sphereRadius = _collider.radius * 0.9f;
         bool grounded = Physics.SphereCast(rayOrigin, sphereRadius, Vector3.down, out RaycastHit hitInfo, rayLength - sphereRadius, groundLayer);


        // Chỉ State Authority mới được set giá trị [Networked] này
        if (Object.HasStateAuthority)
        {
            IsGrounded = grounded;
        }
        else {
            // Client khác có thể dự đoán trạng thái tiếp đất nếu cần thiết cho animation mượt hơn
            // Nhưng giá trị IsGrounded thực sự sẽ đến từ server/state authority
        }

        // Reset vận tốc dọc nếu tiếp đất
        if (grounded && _verticalVelocity.y < 0)
        {
            _verticalVelocity.y = -2f; // Áp một lực nhỏ xuống để giữ trên mặt đất
        }
    }

    private void ApplyGravity()
    {
         // Nếu không tiếp đất, áp dụng trọng lực
         if (!IsGrounded) // Sử dụng giá trị IsGrounded đã được cập nhật (có thể từ State Authority hoặc dự đoán)
         {
              _verticalVelocity.y += gravityValue * Runner.DeltaTime;
         }

         // Di chuyển nhân vật theo trục Y (áp dụng trọng lực/nhảy)
         // Client có State Authority sẽ di chuyển trực tiếp
         // Client chỉ có Input Authority (hoặc Proxy) nên để NetworkTransform xử lý vị trí Y cuối cùng
         // Tuy nhiên, việc mô phỏng trọng lực cục bộ giúp animation và cảm giác mượt hơn
         if (Object.HasStateAuthority || Object.HasInputAuthority) // Cả người điều khiển và chủ sở hữu đều cần mô phỏng trọng lực cục bộ
         {
            _networkTransform.transform.position += _verticalVelocity * Runner.DeltaTime;
         }
    }


    

    // ========================================
    // HÀM CHIẾN ĐẤU VÀ HỒI SINH
    // ========================================

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
                 hitCharacter.Rpc_TakeDamage(shootDamage, Object.InputAuthority);
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

    // RPC được gọi trên State Authority của đối tượng này
    // Nguồn gọi là Input Authority của người bắn (được Fusion kiểm tra)
    // [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)] // Cho phép cả Input và State Authority gọi RPC này (linh hoạt hơn)
     // Cho phép mọi nguồn gửi, chỉ định nơi chạy
   public void Rpc_TakeDamage(float damage, PlayerRef attackerRef) // Vẫn nhận attackerRef
    {
        if (IsDead || !Object.HasStateAuthority) return; // Guard conditions
        if (attackerRef.IsRealPlayer && attackerRef != Object.InputAuthority)
        {
            CurrentHealth -= damage;

            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                Die();
            }
        }
    }

    private void Die()
    {
        if (!Object.HasStateAuthority) return; // Đảm bảo

        if (IsDead) 
        {
            

            return; // Tránh gọi nhiều lần
            
        }

        IsDead = true; // Đặt cờ chết (sẽ đồng bộ)
        _verticalVelocity = Vector3.zero; // Ngừng di chuyển dọc khi chết


        // Bắt đầu timer hồi sinh
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);

        // Tắt collider hoặc đặt vào layer khác để không bị bắn nữa
        // _collider.enabled = false; // Tắt hoàn toàn
         gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // Chuyển sang layer khác

        // Cập nhật UI/Visuals (State Authority cập nhật trực tiếp, client khác qua OnChangedRender)
        if (_playerInfo != null)
        {

            _playerInfo.SetDeadState(true);
        }

    }

    private void Respawn()
    {
        // Hàm này chỉ nên được gọi trên State Authority
        if (!Object.HasStateAuthority) return;

        IsDead = false; // Hồi sinh (sẽ đồng bộ)
        CurrentHealth = maxHealth; // Hồi đầy máu

        // Tìm điểm spawn ngẫu nhiên
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        Vector3 spawnPosition = spawnPoints.Length > 0
            ? spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform.position
            : Vector3.zero;

        // Dịch chuyển nhân vật đến điểm spawn bằng NetworkTransform
        _networkTransform.Teleport(spawnPosition); // Fusion 2 dùng Teleport()

        // Bật lại collider hoặc đặt lại layer
        // _collider.enabled = true;
         gameObject.layer = LayerMask.NameToLayer("Default"); // Chuyển về layer mặc định (hoặc layer "Player")


        // Cập nhật UI/Visuals
        if (_playerInfo != null)
        {
             _playerInfo.UpdateHealthUI(CurrentHealth, maxHealth);
             _playerInfo.SetDeadState(false);
        }

    }
}