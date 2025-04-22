using Fusion;
using TMPro;
using UnityEngine;

public class NPlayerInfo : NetworkBehaviour
{
    [Networked]
    public NetworkString<_32> PlayerName { get; set; }
    public TextMeshPro playerNameText;

    public override void Spawned()
    {
        playerNameText.text = PlayerName.ToString();
    }

    private void LateUpdate()
    {
        playerNameText.transform.rotation = Camera.main.transform.rotation;
    }
}
