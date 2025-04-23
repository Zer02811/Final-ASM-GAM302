
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI; 

public class NPlayerInfo : NetworkBehaviour
{
    [Header("UI Elements")]
    public TextMeshPro playerNameText;
    public Slider healthSlider; 
    public GameObject playerModel; 
    [Header("Gameplay Data")]
    [Networked] public int Score { get; set; }

    private Dictionary<NetworkId, Tick> _lastProcessedAwardTick = new Dictionary<NetworkId, Tick>();
    [Networked, OnChangedRender(nameof(PlayerNameChanged))]
    public NetworkString<_32> PlayerName { get; set; }
    public static List<NPlayerInfo> AllPlayerInfos = new List<NPlayerInfo>();


    private void Awake()
    {
        if (playerNameText == null) Debug.LogWarning("PlayerNameText not assigned.", this);
        if (healthSlider == null) Debug.LogWarning("HealthSlider not assigned.", this);
        if (playerModel == null) Debug.LogWarning("PlayerModel not assigned.", this);
    }

    private void PlayerNameChanged()
    {
        if (playerNameText != null)
        {
            playerNameText.text = PlayerName.ToString();
        }
    }
    public void UpdateHealthUI(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }
     public void SetDeadState(bool isDead)
     {
         if (playerModel != null)
         {
             playerModel.SetActive(!isDead);
         }
          if (healthSlider != null) {
              healthSlider.gameObject.SetActive(!isDead); 
          }
           if (playerNameText != null) {
               playerNameText.gameObject.SetActive(!isDead);
           }
     }

    public override void Spawned()
    {
        if (!AllPlayerInfos.Contains(this))
        {
            AllPlayerInfos.Add(this);
        }
        
         PlayerNameChanged();
         
         
         if (Runner != null && Runner.TryGetBehaviour(out NCharacterController characterController))
         {
             UpdateHealthUI(characterController.CurrentHealth, characterController.maxHealth);
             SetDeadState(characterController.IsDead);
         } else {
              UpdateHealthUI(100f, 100f); 
              SetDeadState(false);
         }
         if (!AllPlayerInfos.Contains(this)) AllPlayerInfos.Add(this);
         if (Object.HasStateAuthority)
         {
             Score = 0;
         }

         if (playerNameText != null) PlayerNameChanged();
         if (healthSlider != null) UpdateHealthUI(GetComponent<NCharacterController>()?.CurrentHealth ?? 100f, GetComponent<NCharacterController>()?.maxHealth ?? 100f);
         SetDeadState(GetComponent<NCharacterController>()?.IsDead ?? false); 
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        AllPlayerInfos.Remove(this);
    }

    private void LateUpdate()
    {
        if (playerModel != null && playerModel.activeSelf && Camera.main != null)
        {
            Transform uiContainer = healthSlider != null ? healthSlider.transform.parent : (playerNameText != null ? playerNameText.transform.parent : null);
            if (uiContainer != null)
            {
                 uiContainer.rotation = Camera.main.transform.rotation;
            }
            else 
            {
                if(playerNameText != null) playerNameText.transform.rotation = Camera.main.transform.rotation;
                if(healthSlider != null) healthSlider.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
     public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (player == Runner.LocalPlayer) continue;

                if (Runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
                {
                    if (playerObject.TryGetBehaviour<NPlayerInfo>(out var otherPlayerInfo))
                    {

                        // Lấy tick cuối cùng đã xử lý, dùng default(Tick) nếu chưa có
                        Tick lastProcessedTick = _lastProcessedAwardTick.GetValueOrDefault(playerObject.Id, default(Tick));

                        // Kiểm tra mình là người nhận, TickAwarded hợp lệ (>0), và TickAwarded mới hơn tick đã xử lý
                        
                    }
                }
            }
        }
    }

    // Hàm này được gọi bởi NCharacterController của đối tượng NÀY khi nó bị bắn trúng
    
    
}