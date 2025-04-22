using Fusion;
using UnityEngine;

public class NCharacterSpawner : NetworkBehaviour
{
    public NetworkPrefabRef characterPrefab;
    public CameraController cameraController;

    public override void Spawned()
    {
        NetworkObject characterObject = Runner.Spawn(characterPrefab);
        
        cameraController.target = characterObject.transform;
    }

    private void OnBeforeSpawnCharacter(NetworkRunner runner, NetworkObject networkObject)
    {
        networkObject.GetBehaviour<NPlayerInfo>().PlayerName = PlayerPrefs.GetString("username", "hehe");
    }
}
